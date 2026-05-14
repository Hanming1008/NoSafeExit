using System;
using System.Collections.Generic;
using JUTPS;
using JUTPS.InteractionSystem;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class GameplayUIRoot : MonoBehaviour
{
    private const string SmallCarryComboTestId = "debug_combo_small";
    private static readonly Color GridFrameDefaultColor = new Color(0.83f, 0.87f, 0.93f, 0.96f);
    private static readonly Color GridFrameHoverColor = new Color(0.34f, 0.88f, 0.56f, 1f);
    private static readonly Color GridFrameInvalidColor = new Color(0.92f, 0.34f, 0.34f, 0.98f);
    private static readonly Color EquipmentHoverColor = new Color(0.20f, 0.42f, 0.26f, 0.98f);
    private static readonly Color ValueTierBlueColor = new Color(0.13f, 0.26f, 0.40f, 0.98f);
    private static readonly Color ValueTierGoldColor = new Color(0.86f, 0.68f, 0.08f, 0.98f);
    private static readonly Color ValueTierRedColor = new Color(0.48f, 0.10f, 0.12f, 0.98f);

    private sealed class SlotView
    {
        public RectTransform rect;
        public Image background;
        public Image iconFrame;
        public RectTransform iconRect;
        public Image iconImage;
        public Text keyText;
        public Text itemText;
        public Text detailText;
        public Text quantityText;
    }

    private sealed class GridContainerView
    {
        public GridContainerKind kind;
        public RectTransform rect;
        public Text titleText;
        public RectTransform previewRect;
        public Image previewBackground;
        public Image previewIcon;
        public Text previewText;
        public RectTransform gridFrameRect;
        public Image gridFrameImage;
        public RectTransform gridRect;
        public GridLayoutGroup gridLayout;
        public RectTransform gridLinesRoot;
        public RectTransform placementsRoot;
        public readonly List<Image> cells = new List<Image>();
        public readonly List<Image> gridLines = new List<Image>();
        public readonly List<GridPlacementView> placementViews = new List<GridPlacementView>();
    }

    private sealed class GridPlacementView
    {
        public RectTransform rect;
        public RectTransform contentRect;
        public Image background;
        public Image iconImage;
        public Text nameText;
        public Text quantityText;
        public GridContainerKind containerKind;
        public string runtimeInstanceId;
        public ItemDefinition item;
        public int sourceSlotIndex;
        public bool sourceIsExternal;
        public int row;
        public int column;
        public bool rotated;
    }

    private sealed class GridDragState
    {
        public bool sourceIsEquipment;
        public bool sourceIsCorpseEquipment;
        public bool sourceIsPopup;
        public bool sourceIsExternal;
        public GridContainerKind sourceContainerKind;
        public EquipmentSlotType sourceEquipmentSlotType;
        public EquipmentSlotType sourceCorpseEquipmentSlotType;
        public int sourceSlotIndex;
        public string runtimeInstanceId;
        public ItemDefinition item;
        public int quantity;
        public int sourceRow;
        public int sourceColumn;
        public bool sourceRotated;
        public bool rotated;
        public int rotationQuarterTurns;
        public ItemRuntimeData runtimeData;
    }

    private readonly struct GridDropTarget
    {
        public readonly bool isEquipmentSlot;
        public readonly bool isPopup;
        public readonly GridContainerKind containerKind;
        public readonly GridContainerState actualContainer;
        public readonly GridContainerState displayContainer;
        public readonly EquipmentSlotType equipmentSlotType;
        public readonly int row;
        public readonly int column;

        public GridDropTarget(
            bool isEquipmentSlot,
            bool isPopup,
            GridContainerKind containerKind,
            GridContainerState actualContainer,
            GridContainerState displayContainer,
            EquipmentSlotType equipmentSlotType,
            int row,
            int column)
        {
            this.isEquipmentSlot = isEquipmentSlot;
            this.isPopup = isPopup;
            this.containerKind = containerKind;
            this.actualContainer = actualContainer;
            this.displayContainer = displayContainer;
            this.equipmentSlotType = equipmentSlotType;
            this.row = row;
            this.column = column;
        }
    }

    private readonly struct GridMirroredPlacement
    {
        public readonly GridContainerKind containerKind;
        public readonly GridItemPlacement placement;
        public readonly int sourceSlotIndex;

        public GridMirroredPlacement(GridContainerKind containerKind, GridItemPlacement placement, int sourceSlotIndex)
        {
            this.containerKind = containerKind;
            this.placement = placement;
            this.sourceSlotIndex = sourceSlotIndex;
        }
    }

    private readonly struct MirroredGridAnchor
    {
        public readonly string itemId;
        public readonly GridContainerKind containerKind;
        public readonly int row;
        public readonly int column;
        public readonly bool rotated;

        public MirroredGridAnchor(string itemId, GridContainerKind containerKind, int row, int column, bool rotated)
        {
            this.itemId = itemId;
            this.containerKind = containerKind;
            this.row = row;
            this.column = column;
            this.rotated = rotated;
        }
    }

    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerGridInventory gridInventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private CharacterEquipmentVisuals equipmentVisuals;
    [SerializeField] private PlayerQuickbar quickbar;
    [SerializeField] private PlayerItemUse itemUse;
    [SerializeField] private PlayerItemDrop itemDrop;
    [SerializeField] private PlayerWeaponSelection weaponSelection;
    [SerializeField] private PlayerGameplayInput gameplayInput;
    [SerializeField] private RuntimeMinimapSystem minimapSystem;
    [SerializeField] private JUCharacterController juCharacter;
    [SerializeField] private JUInteractionSystem juInteractionSystem;
    [SerializeField] private JUHealth juHealth;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private Rigidbody playerRigidbody;

    [Header("Grid Inventory Test Mode")]
    [SerializeField] private bool singleGridTestMode = true;
    [SerializeField] private string singleGridTestItemId = "armor_body_level_i";

    [Header("UI")]
    [SerializeField] private bool showMinimap = true;

    private readonly List<SlotView> backpackSlotViews = new List<SlotView>();
    private readonly List<SlotView> quickbarSlotViews = new List<SlotView>();
    private readonly Dictionary<EquipmentSlotType, SlotView> equipmentSlotViews = new Dictionary<EquipmentSlotType, SlotView>();
    private readonly Dictionary<EquipmentSlotType, SlotView> corpseEquipmentSlotViews = new Dictionary<EquipmentSlotType, SlotView>();
    private readonly Dictionary<GridContainerKind, GridContainerView> gridContainerViews = new Dictionary<GridContainerKind, GridContainerView>();
    private readonly Dictionary<GridContainerKind, GridContainerState> gridDisplayStates = new Dictionary<GridContainerKind, GridContainerState>();
    private readonly Dictionary<string, Sprite> gridFrameSpriteCache = new Dictionary<string, Sprite>();
    private readonly List<GridMirroredPlacement> mirroredGridPlacements = new List<GridMirroredPlacement>();
    private readonly Dictionary<int, MirroredGridAnchor> mirroredGridAnchors = new Dictionary<int, MirroredGridAnchor>();
    private readonly Dictionary<string, Sprite> runtimeStatusIconCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<EquipmentSlotType, Sprite> equipmentPlaceholderIconCache = new Dictionary<EquipmentSlotType, Sprite>();

    private Canvas rootCanvas;
    private Font uiFont;
    private Font hudLabelFont;
    private RectTransform runtimeRoot;
    private RectTransform inventoryPanel;
    private RectTransform fullMapPanel;
    private RectTransform minimapPanel;
    private RectTransform quickbarPanel;
    private RectTransform statusHudPanel;
    private RectTransform healthBarFillRect;
    private RectTransform equipmentHealthBarFillRect;
    private RectTransform hydrationBarFillRect;
    private RectTransform hungerBarFillRect;
    private RectTransform weightBarFillRect;
    private RectTransform weaponHudPanel;
    private RectTransform contextMenuPanel;
    private RectTransform containerPopupPanel;
    private RectTransform dropDialogPanel;
    private RectTransform splitDialogPanel;
    private RectTransform useProgressPanel;
    private RectTransform rightWorkspacePanel;
    private RectTransform corpseLootPanel;
    private Text equipmentSummaryText;
    private Text inventoryWeightText;
    private Text operatorPreviewFallbackText;
    private Text healthValueText;
    private Text equipmentHealthValueText;
    private Text hydrationValueText;

    public bool SingleGridTestMode => singleGridTestMode;
    public string SingleGridTestItemId => singleGridTestItemId;
    private Text hungerValueText;
    private Text weightValueText;
    private Text weaponHudNameText;
    private Text weaponHudModeText;
    private Text weaponHudAmmoText;
    private Text weaponHudDetailText;
    private Text minimapInfoText;
    private Text fullMapInfoText;
    private Text minimapArrowText;
    private Text fullMapArrowText;
    private Text rightWorkspaceTitleText;
    private Text rightWorkspaceHintText;
    private Text rightWorkspaceCashText;
    private Text contextPrimaryText;
    private Text contextSecondaryText;
    private Text containerPopupTitleText;
    private Text dropDialogTitleText;
    private Text dropDialogQuantityText;
    private Text splitDialogTitleText;
    private Text splitDialogMaxText;
    private Text useProgressCountdownText;
    private Text useProgressItemNameText;
    private Image healthBarFillImage;
    private Image equipmentHealthBarFillImage;
    private Image hydrationBarFillImage;
    private Image hungerBarFillImage;
    private Image weightBarFillImage;
    private RawImage operatorPreviewImage;
    private Image useProgressBackgroundImage;
    private Image useProgressRingImage;
    private Image useProgressItemIconImage;
    private Image weaponHudIconImage;
    private Image weaponHudIconFrame;
    private RawImage minimapFeedImage;
    private RawImage fullMapFeedImage;

    private Button contextPrimaryButton;
    private Button contextSecondaryButton;
    private Button contextInspectButton;
    private Button contextSplitButton;
    private Button contextDropButton;
    private Button containerPopupCloseButton;
    private Button dropMinusButton;
    private Button dropPlusButton;
    private Button dropConfirmButton;
    private Button dropCancelButton;
    private Button splitMinusButton;
    private Button splitPlusButton;
    private Button splitConfirmButton;
    private Button splitCancelButton;
    private Slider splitQuantitySlider;
    private InputField splitQuantityInput;
    private Image splitDialogItemIconImage;
    private InventoryCharacterPreview inventoryCharacterPreview;
    private ItemInspectPanel itemInspectPanel;

    private int selectedBackpackSlotIndex = -1;
    private int selectedEquipmentSlotTypeIndex = -1;
    private int selectedCorpseEquipmentSlotTypeIndex = -1;
    private GridContainerKind selectedCarryContainerKind = GridContainerKind.Pocket;
    private string selectedCarryRuntimeInstanceId = string.Empty;
    private string selectedPopupRuntimeInstanceId = string.Empty;
    private int selectedCarryRow = -1;
    private int selectedCarryColumn = -1;
    private bool selectedCarryRotated;
    private ItemDefinition selectedCarryItem;
    private int selectedPopupRow = -1;
    private int selectedPopupColumn = -1;
    private bool selectedPopupRotated;
    private ItemDefinition selectedPopupItem;
    private ItemRuntimeData openedContainerRuntimeData;
    private ItemDefinition openedContainerDefinition;
    private int dropDialogSlotIndex = -1;
    private int dropDialogQuantity = 1;
    private GridContainerState splitDialogContainer;
    private string splitDialogRuntimeInstanceId = string.Empty;
    private int splitDialogSourceRow = -1;
    private int splitDialogSourceColumn = -1;
    private bool splitDialogSourceRotated;
    private ItemDefinition splitDialogItem;
    private int splitDialogMaxQuantity = 1;
    private int splitDialogQuantity = 1;
    private bool splitDialogUpdating;
    private bool contextMenuTargetsEquipmentSlot;
    private bool contextMenuTargetsCorpseEquipmentSlot;
    private bool contextMenuTargetsCarryPlacement;
    private bool contextMenuTargetsPopupPlacement;
    private bool uiBuilt;
    private bool overlayWasBlockingMovement;
    private bool overlayCursorStateCaptured;
    private bool crosshairCursorSuppressed;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool gridDragActive;
    private const float HealthBarWidth = 284f;
    private const float NeedBarWidth = 222f;
    private const float InventoryGridCellSize = 58f;
    private const float InventoryGridLineThickness = 1f;
    private const float InventoryGridOuterBorderThickness = 3f;
    private const float InventoryPlacementInset = 2f;

    private GridContainerView containerPopupView;
    private GridContainerView externalContainerView;
    private GridContainerView corpsePocketView;
    private GridDragState activeGridDrag;
    private RectTransform gridDragPreviewRect;
    private RectTransform gridDragPreviewContentRect;
    private Image gridDragPreviewBackground;
    private Image gridDragPreviewIcon;
    private Text gridDragPreviewNameText;
    private Text gridDragPreviewQuantityText;
    private RectTransform gridDropPreviewRect;
    private RectTransform gridDropPreviewContentRect;
    private Image gridDropPreviewBackground;
    private Image gridDropPreviewIcon;
    private Text gridDropPreviewNameText;
    private Outline gridDropPreviewOutline;
    private SearchableContainer openedSearchableContainer;
    private EnemyCorpseLoot openedCorpseLoot;
    private Sprite useProgressCircleSprite;
    private Sprite useProgressRingSprite;

    void Awake()
    {
        ResolveReferences();
        BuildUi();
        RefreshAll();
        ApplyInitialVisibility();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    void Update()
    {
        if (NeedsReferenceResolution())
            ResolveReferences();

        if (!uiBuilt)
            BuildUi();

        HandleInput();
        RefreshDynamicInfo();
    }

    void OnGUI()
    {
        if (Event.current == null)
            return;

        if (inventoryPanel == null || !inventoryPanel.gameObject.activeSelf)
            return;

        if (dropDialogPanel != null && dropDialogPanel.gameObject.activeSelf)
            return;

        if (splitDialogPanel != null && splitDialogPanel.gameObject.activeSelf)
            return;

        if (Event.current.type != EventType.MouseDown || Event.current.button != 1)
            return;

        Vector2 guiPointerPosition = Event.current.mousePosition;
        if (!TryOpenBackpackContextMenuAtGui(guiPointerPosition)
            && !TryOpenEquipmentContextMenuAtGui(guiPointerPosition)
            && !TryOpenCorpseEquipmentContextMenuAtGui(guiPointerPosition))
            return;

        Event.current.Use();
    }

    public void OpenContextMenuForBackpackSlot(int slotIndex, Vector2 screenPosition)
    {
        if (inventoryPanel == null || !inventoryPanel.gameObject.activeSelf || inventory == null)
            return;

        InventorySlot slot = inventory.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
            return;

        selectedBackpackSlotIndex = slotIndex;
        selectedEquipmentSlotTypeIndex = -1;
        selectedCarryRuntimeInstanceId = string.Empty;
        selectedPopupRuntimeInstanceId = string.Empty;
        contextMenuTargetsEquipmentSlot = false;
        contextMenuTargetsCorpseEquipmentSlot = false;
        contextMenuTargetsCarryPlacement = false;
        contextMenuTargetsPopupPlacement = false;

        bool showPrimary = false;
        bool showSecondary = false;

        if (itemUse != null && itemUse.CanUse(slot.Item))
        {
            contextPrimaryText.text = "Use";
            contextSecondaryText.text = "Quickbar";
            showPrimary = true;
            showSecondary = true;
        }
        else if (slot.Item is WeaponItemDefinition || slot.Item is ArmorItemDefinition || slot.Item is ContainerItemDefinition)
        {
            contextPrimaryText.text = "Equip";
            showPrimary = true;
            if (CanOpenContainerItem(slot.Item, slot.RuntimeData))
            {
                contextSecondaryText.text = "Open";
                showSecondary = true;
            }
        }

        contextPrimaryButton.gameObject.SetActive(showPrimary);
        contextSecondaryButton.gameObject.SetActive(showSecondary);
        contextInspectButton.gameObject.SetActive(true);
        contextSplitButton.gameObject.SetActive(false);
        contextDropButton.gameObject.SetActive(true);

        PositionPanelAtScreenPoint(contextMenuPanel, screenPosition);
        contextMenuPanel.SetAsLastSibling();
        contextMenuPanel.gameObject.SetActive(true);
    }

    public void OpenContextMenuForEquipmentSlot(EquipmentSlotType slotType, Vector2 screenPosition)
    {
        if (inventoryPanel == null || !inventoryPanel.gameObject.activeSelf || equipment == null)
            return;

        InventorySlot slot = equipment.GetSlot(slotType);
        if (slot == null || slot.IsEmpty)
            return;

        selectedBackpackSlotIndex = -1;
        selectedEquipmentSlotTypeIndex = (int)slotType;
        selectedCarryRuntimeInstanceId = string.Empty;
        selectedPopupRuntimeInstanceId = string.Empty;
        contextMenuTargetsEquipmentSlot = true;
        contextMenuTargetsCorpseEquipmentSlot = false;
        contextMenuTargetsCarryPlacement = false;
        contextMenuTargetsPopupPlacement = false;

        bool showUnequip = CanUnequipEquipmentSlot(slotType);
        bool showDrop = CanDropEquipmentSlot(slotType);
        bool showOpen = CanOpenContainerItem(slot.Item, slot.RuntimeData);

        contextPrimaryText.text = "Unequip";
        contextPrimaryButton.gameObject.SetActive(showUnequip);
        contextSecondaryText.text = "Open";
        contextSecondaryButton.gameObject.SetActive(showOpen);
        contextInspectButton.gameObject.SetActive(true);
        contextSplitButton.gameObject.SetActive(false);
        contextDropButton.gameObject.SetActive(showDrop);

        PositionPanelAtScreenPoint(contextMenuPanel, screenPosition);
        contextMenuPanel.SetAsLastSibling();
        contextMenuPanel.gameObject.SetActive(true);
    }

    public void OpenContextMenuForCorpseEquipmentSlot(EquipmentSlotType slotType, Vector2 screenPosition)
    {
        if (inventoryPanel == null || !inventoryPanel.gameObject.activeSelf || openedCorpseLoot == null)
            return;

        InventorySlot slot = openedCorpseLoot.GetSlot(slotType);
        if (slot == null || slot.IsEmpty)
            return;

        selectedBackpackSlotIndex = -1;
        selectedEquipmentSlotTypeIndex = -1;
        selectedCorpseEquipmentSlotTypeIndex = (int)slotType;
        selectedCarryRuntimeInstanceId = string.Empty;
        selectedPopupRuntimeInstanceId = string.Empty;
        contextMenuTargetsEquipmentSlot = false;
        contextMenuTargetsCorpseEquipmentSlot = true;
        contextMenuTargetsCarryPlacement = false;
        contextMenuTargetsPopupPlacement = false;

        bool showPrimary = slot.Item is WeaponItemDefinition || slot.Item is ArmorItemDefinition || slot.Item is ContainerItemDefinition;
        bool showSecondary = CanOpenContainerItem(slot.Item, slot.RuntimeData);

        contextPrimaryText.text = "Equip";
        contextPrimaryButton.gameObject.SetActive(showPrimary);
        contextSecondaryText.text = "Open";
        contextSecondaryButton.gameObject.SetActive(showSecondary);
        contextInspectButton.gameObject.SetActive(true);
        contextSplitButton.gameObject.SetActive(false);
        contextDropButton.gameObject.SetActive(true);

        PositionPanelAtScreenPoint(contextMenuPanel, screenPosition);
        contextMenuPanel.SetAsLastSibling();
        contextMenuPanel.gameObject.SetActive(true);
    }

    public void OpenContextMenuForCarryPlacement(GridContainerKind containerKind, GridItemPlacement placement, Vector2 screenPosition)
    {
        if (inventoryPanel == null || !inventoryPanel.gameObject.activeSelf || placement == null || placement.IsEmpty)
            return;

        selectedBackpackSlotIndex = -1;
        selectedEquipmentSlotTypeIndex = -1;
        selectedCarryContainerKind = containerKind;
        selectedCarryRuntimeInstanceId = placement.RuntimeInstanceId;
        selectedCarryRow = placement.Row;
        selectedCarryColumn = placement.Column;
        selectedCarryRotated = placement.Rotated;
        selectedCarryItem = placement.Item;
        selectedPopupRuntimeInstanceId = string.Empty;
        selectedPopupRow = -1;
        selectedPopupColumn = -1;
        selectedPopupRotated = false;
        selectedPopupItem = null;
        contextMenuTargetsEquipmentSlot = false;
        contextMenuTargetsCorpseEquipmentSlot = false;
        contextMenuTargetsCarryPlacement = true;
        contextMenuTargetsPopupPlacement = false;

        bool showPrimary = placement.Item is WeaponItemDefinition
            || placement.Item is ArmorItemDefinition
            || placement.Item is ContainerItemDefinition
            || (itemUse != null && itemUse.CanUse(placement.Item));
        bool showSecondary = CanOpenContainerItem(placement.Item, placement.RuntimeData);
        bool showSplit = placement.Item.canStack && placement.Quantity > 1;

        contextPrimaryText.text = itemUse != null && itemUse.CanUse(placement.Item) ? "Use" : "Equip";
        contextPrimaryButton.gameObject.SetActive(showPrimary);
        contextSecondaryText.text = "Open";
        contextSecondaryButton.gameObject.SetActive(showSecondary);
        contextInspectButton.gameObject.SetActive(true);
        contextSplitButton.gameObject.SetActive(showSplit);
        contextDropButton.gameObject.SetActive(true);

        PositionPanelAtScreenPoint(contextMenuPanel, screenPosition);
        contextMenuPanel.SetAsLastSibling();
        contextMenuPanel.gameObject.SetActive(true);
    }

    public void OpenContextMenuForPopupPlacement(GridItemPlacement placement, Vector2 screenPosition)
    {
        if (inventoryPanel == null || !inventoryPanel.gameObject.activeSelf || placement == null || placement.IsEmpty)
            return;

        selectedBackpackSlotIndex = -1;
        selectedEquipmentSlotTypeIndex = -1;
        selectedCarryRuntimeInstanceId = string.Empty;
        selectedCarryRow = -1;
        selectedCarryColumn = -1;
        selectedCarryRotated = false;
        selectedCarryItem = null;
        selectedPopupRuntimeInstanceId = placement.RuntimeInstanceId;
        selectedPopupRow = placement.Row;
        selectedPopupColumn = placement.Column;
        selectedPopupRotated = placement.Rotated;
        selectedPopupItem = placement.Item;
        contextMenuTargetsEquipmentSlot = false;
        contextMenuTargetsCorpseEquipmentSlot = false;
        contextMenuTargetsCarryPlacement = false;
        contextMenuTargetsPopupPlacement = true;

        bool showPrimary = placement.Item is WeaponItemDefinition
            || placement.Item is ArmorItemDefinition
            || placement.Item is ContainerItemDefinition
            || (itemUse != null && itemUse.CanUse(placement.Item));
        bool showSecondary = CanOpenContainerItem(placement.Item, placement.RuntimeData);
        bool showSplit = placement.Item.canStack && placement.Quantity > 1;

        contextPrimaryText.text = itemUse != null && itemUse.CanUse(placement.Item) ? "Use" : "Equip";
        contextPrimaryButton.gameObject.SetActive(showPrimary);
        contextSecondaryText.text = "Open";
        contextSecondaryButton.gameObject.SetActive(showSecondary);
        contextInspectButton.gameObject.SetActive(true);
        contextSplitButton.gameObject.SetActive(showSplit);
        contextDropButton.gameObject.SetActive(true);

        PositionPanelAtScreenPoint(contextMenuPanel, screenPosition);
        contextMenuPanel.SetAsLastSibling();
        contextMenuPanel.gameObject.SetActive(true);
    }

    public void ClearQuickbarSlot(int slotIndex)
    {
        if (quickbar == null)
            return;

        quickbar.ClearSlot(slotIndex);
        RefreshQuickbarDisplay();
    }

    public bool TryUseQuickbarSlot(int slotIndex)
    {
        if (quickbar == null || itemUse == null)
            return false;

        ItemDefinition assignedItem = quickbar.GetAssignedItem(slotIndex);
        if (assignedItem == null)
            return false;

        bool usedSuccessfully = itemUse.TryUseAssignedItem(assignedItem);
        if (GetCarriedItemQuantity(assignedItem) <= 0)
            quickbar.ClearSlot(slotIndex);

        RefreshAll();
        return usedSuccessfully;
    }

    private void HandleInput()
    {
        if (gameplayInput == null)
            return;

        if (gameplayInput.IsInventoryTogglePressed())
            ToggleInventory();

        if (gameplayInput.IsMapTogglePressed())
            ToggleFullMap();

        if (gameplayInput.IsClosePressed())
            CloseTopmostPanel();

        HandleGridDragInput();
        HandlePointerFallbackInput();

        if (!IsBlockingOverlayOpen())
        {
            int quickbarIndex = gameplayInput.GetTriggeredQuickbarIndex();
            if (quickbarIndex >= 0)
                TryUseQuickbarSlot(quickbarIndex);
        }
    }

    private bool NeedsReferenceResolution()
    {
        return rootCanvas == null
            || gameplayInput == null
            || inventory == null
            || gridInventory == null
            || equipment == null
            || quickbar == null
            || itemUse == null
            || itemDrop == null
            || weaponSelection == null
            || minimapSystem == null
            || juCharacter == null
            || juInteractionSystem == null
            || juHealth == null
            || playerStats == null
            || playerRigidbody == null;
    }

    private void ToggleInventory()
    {
        bool shouldOpen = inventoryPanel != null && !inventoryPanel.gameObject.activeSelf;
        if (inventoryPanel != null)
            inventoryPanel.gameObject.SetActive(shouldOpen);

        if (!shouldOpen)
        {
            itemInspectPanel?.Hide();
            CloseExternalContainer();
        }

        if (shouldOpen && fullMapPanel != null)
        {
            fullMapPanel.gameObject.SetActive(false);
            if (minimapSystem != null)
                minimapSystem.SetFullMapActive(false);

            if (minimapPanel != null)
                minimapPanel.gameObject.SetActive(showMinimap);
        }

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        if (containerPopupPanel != null)
            containerPopupPanel.gameObject.SetActive(false);

        if (dropDialogPanel != null)
            dropDialogPanel.gameObject.SetActive(false);

        if (splitDialogPanel != null)
            splitDialogPanel.gameObject.SetActive(false);

        if (containerPopupPanel != null)
            containerPopupPanel.gameObject.SetActive(false);

        itemInspectPanel?.Hide();
        CancelGridDrag();
        ClearContextSelection();
        RefreshAll();
    }

    public void OpenInventoryWithExternalContainer(SearchableContainer container)
    {
        if (container == null)
            return;

        container.EnsureInitialized();
        openedSearchableContainer = container;
        openedCorpseLoot = null;

        if (inventoryPanel != null)
            inventoryPanel.gameObject.SetActive(true);

        if (fullMapPanel != null)
            fullMapPanel.gameObject.SetActive(false);

        if (minimapSystem != null)
            minimapSystem.SetFullMapActive(false);

        if (minimapPanel != null)
            minimapPanel.gameObject.SetActive(showMinimap);

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        if (containerPopupPanel != null)
            containerPopupPanel.gameObject.SetActive(false);

        if (dropDialogPanel != null)
            dropDialogPanel.gameObject.SetActive(false);

        itemInspectPanel?.Hide();
        CancelGridDrag();
        ClearContextSelection();
        RefreshAll();
    }

    public void OpenInventoryWithCorpse(EnemyCorpseLoot corpseLoot)
    {
        if (corpseLoot == null || !corpseLoot.IsSearchable)
            return;

        corpseLoot.EnsureInitialized();
        openedCorpseLoot = corpseLoot;
        openedSearchableContainer = null;

        if (inventoryPanel != null)
            inventoryPanel.gameObject.SetActive(true);

        if (fullMapPanel != null)
            fullMapPanel.gameObject.SetActive(false);

        if (minimapSystem != null)
            minimapSystem.SetFullMapActive(false);

        if (minimapPanel != null)
            minimapPanel.gameObject.SetActive(showMinimap);

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        if (containerPopupPanel != null)
            containerPopupPanel.gameObject.SetActive(false);

        if (dropDialogPanel != null)
            dropDialogPanel.gameObject.SetActive(false);

        itemInspectPanel?.Hide();
        CancelGridDrag();
        ClearContextSelection();
        RefreshAll();
    }

    private void CloseExternalContainer()
    {
        openedSearchableContainer = null;
        openedCorpseLoot = null;
        if (externalContainerView != null)
            ClearGridPlacementViews(externalContainerView);
        if (corpsePocketView != null)
            ClearGridPlacementViews(corpsePocketView);
    }

    private void ToggleFullMap()
    {
        bool shouldOpen = fullMapPanel != null && !fullMapPanel.gameObject.activeSelf;
        if (fullMapPanel != null)
            fullMapPanel.gameObject.SetActive(shouldOpen);

        if (minimapSystem != null)
            minimapSystem.SetFullMapActive(shouldOpen);

        if (shouldOpen && inventoryPanel != null)
        {
            inventoryPanel.gameObject.SetActive(false);
            CloseExternalContainer();
        }

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        if (dropDialogPanel != null)
            dropDialogPanel.gameObject.SetActive(false);

        if (containerPopupPanel != null)
            containerPopupPanel.gameObject.SetActive(false);

        itemInspectPanel?.Hide();
        CancelGridDrag();
        ClearContextSelection();
        if (minimapPanel != null)
            minimapPanel.gameObject.SetActive(showMinimap && !shouldOpen);

        RefreshDynamicInfo();
    }

    private void CloseTopmostPanel()
    {
        if (gridDragActive)
        {
            CancelGridDrag();
            return;
        }

        if (dropDialogPanel != null && dropDialogPanel.gameObject.activeSelf)
        {
            HideDropDialog();
            return;
        }

        if (splitDialogPanel != null && splitDialogPanel.gameObject.activeSelf)
        {
            HideSplitDialog();
            return;
        }

        if (itemInspectPanel != null && itemInspectPanel.IsOpen)
        {
            itemInspectPanel.Hide();
            return;
        }

        if (containerPopupPanel != null && containerPopupPanel.gameObject.activeSelf)
        {
            CloseContainerPopup();
            return;
        }

        if (contextMenuPanel != null && contextMenuPanel.gameObject.activeSelf)
        {
            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();
            return;
        }

        if (inventoryPanel != null && inventoryPanel.gameObject.activeSelf)
        {
            inventoryPanel.gameObject.SetActive(false);
            itemInspectPanel?.Hide();
            CloseExternalContainer();
            return;
        }

        if (fullMapPanel != null && fullMapPanel.gameObject.activeSelf)
        {
            fullMapPanel.gameObject.SetActive(false);
            if (minimapSystem != null)
                minimapSystem.SetFullMapActive(false);

            if (minimapPanel != null)
                minimapPanel.gameObject.SetActive(showMinimap);
        }
    }

    private void OnContextPrimaryAction()
    {
        if (contextMenuTargetsEquipmentSlot)
        {
            EquipmentSlotType slotType = (EquipmentSlotType)selectedEquipmentSlotTypeIndex;
            bool unequipped = equipment != null
                && selectedEquipmentSlotTypeIndex >= 0
                && TryHandleEquipmentUnequip(slotType);

            CloseContainerPopup();
            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();

            if (unequipped)
            {
                equipmentVisuals?.ForceRefreshNow();
                RefreshAll();
            }

            return;
        }

        if (contextMenuTargetsCorpseEquipmentSlot)
        {
            bool corpseChanged = selectedCorpseEquipmentSlotTypeIndex >= 0
                && TryHandleCorpseEquipmentEquip((EquipmentSlotType)selectedCorpseEquipmentSlotTypeIndex);

            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();

            if (corpseChanged)
            {
                equipmentVisuals?.ForceRefreshNow();
                RefreshAll();
            }

            return;
        }

        if (contextMenuTargetsCarryPlacement)
        {
            GridContainerState container = GetActualContainerState(selectedCarryContainerKind);
            GridItemPlacement placement = FindSelectedPlacement(
                container,
                selectedCarryRuntimeInstanceId,
                selectedCarryRow,
                selectedCarryColumn,
                selectedCarryRotated,
                selectedCarryItem);
            bool startedUse = placement != null
                && itemUse != null
                && itemUse.CanUse(placement.Item)
                && itemUse.TryUseGridPlacement(container, placement);
            bool carryChanged = startedUse || TryHandleCarryPlacementEquip();

            CloseContainerPopup();
            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();

            if (carryChanged)
            {
                if (startedUse)
                    CloseInventoryForItemUse();

                equipmentVisuals?.ForceRefreshNow();
                RefreshAll();
            }

            return;
        }

        if (contextMenuTargetsPopupPlacement)
        {
            GridContainerState container = openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null;
            GridItemPlacement placement = FindSelectedPlacement(
                container,
                selectedPopupRuntimeInstanceId,
                selectedPopupRow,
                selectedPopupColumn,
                selectedPopupRotated,
                selectedPopupItem);
            bool startedUse = placement != null
                && itemUse != null
                && itemUse.CanUse(placement.Item)
                && itemUse.TryUseGridPlacement(container, placement);
            bool popupChanged = startedUse || TryHandlePopupPlacementEquip();

            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();

            if (popupChanged)
            {
                if (startedUse)
                    CloseInventoryForItemUse();

                equipmentVisuals?.ForceRefreshNow();
                RefreshAll();
            }

            return;
        }

        InventorySlot slot = inventory != null ? inventory.GetSlot(selectedBackpackSlotIndex) : null;
        if (slot == null || slot.IsEmpty)
            return;

        ItemDefinition selectedItem = slot.Item;
        ItemRuntimeData selectedRuntimeData = slot.RuntimeData;
        bool changed = false;

        if (itemUse != null && itemUse.CanUse(slot.Item))
        {
            changed = itemUse != null && itemUse.TryUseBackpackSlot(selectedBackpackSlotIndex);
        }
        else if (slot.Item is WeaponItemDefinition)
        {
            changed = equipment != null && equipment.TryEquipWeaponFromInventory(selectedBackpackSlotIndex);
        }
        else if (slot.Item is ContainerItemDefinition container)
        {
            changed = equipment != null && equipment.TryEquipFromInventory(
                selectedBackpackSlotIndex,
                GetContainerEquipmentSlot(container.containerKind));
        }
        else if (slot.Item is ArmorItemDefinition armor)
        {
            changed = equipment != null && equipment.TryEquipFromInventory(
                selectedBackpackSlotIndex,
                GetArmorEquipmentSlot(armor.armorSlot));
        }

        CloseContainerPopup();
        contextMenuPanel.gameObject.SetActive(false);
        ClearContextSelection();

        if (changed)
        {
            if (itemUse != null && itemUse.CanUse(selectedItem))
                CloseInventoryForItemUse();

            CloseContainerPopupIfEquippedItem(selectedItem, selectedRuntimeData);
            equipmentVisuals?.ForceRefreshNow();
            RefreshAll();
        }
    }

    private void OnContextSecondaryAction()
    {
        if (contextMenuTargetsEquipmentSlot)
        {
            InventorySlot equipmentSlot = equipment != null && selectedEquipmentSlotTypeIndex >= 0
                ? equipment.GetSlot((EquipmentSlotType)selectedEquipmentSlotTypeIndex)
                : null;
            if (equipmentSlot != null && !equipmentSlot.IsEmpty && CanOpenContainerItem(equipmentSlot.Item, equipmentSlot.RuntimeData))
            {
                OpenContainerPopup(equipmentSlot.Item, equipmentSlot.RuntimeData, GetAdjacentPanelScreenPoint(contextMenuPanel));
                contextMenuPanel.gameObject.SetActive(false);
                ClearContextSelection();
            }

            return;
        }

        if (contextMenuTargetsCorpseEquipmentSlot)
        {
            InventorySlot corpseSlot = openedCorpseLoot != null && selectedCorpseEquipmentSlotTypeIndex >= 0
                ? openedCorpseLoot.GetSlot((EquipmentSlotType)selectedCorpseEquipmentSlotTypeIndex)
                : null;
            if (corpseSlot != null && !corpseSlot.IsEmpty && CanOpenContainerItem(corpseSlot.Item, corpseSlot.RuntimeData))
            {
                OpenContainerPopup(corpseSlot.Item, corpseSlot.RuntimeData, GetAdjacentPanelScreenPoint(contextMenuPanel));
                contextMenuPanel.gameObject.SetActive(false);
                ClearContextSelection();
            }

            return;
        }

        if (contextMenuTargetsCarryPlacement)
        {
            GridContainerState container = GetActualContainerState(selectedCarryContainerKind);
            GridItemPlacement placement = FindSelectedPlacement(
                container,
                selectedCarryRuntimeInstanceId,
                selectedCarryRow,
                selectedCarryColumn,
                selectedCarryRotated,
                selectedCarryItem);
            if (placement != null && CanOpenContainerItem(placement.Item, placement.RuntimeData))
            {
                OpenContainerPopup(placement.Item, placement.RuntimeData, GetAdjacentPanelScreenPoint(contextMenuPanel));
                contextMenuPanel.gameObject.SetActive(false);
                ClearContextSelection();
            }

            return;
        }

        if (contextMenuTargetsPopupPlacement)
        {
            GridContainerState container = openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null;
            GridItemPlacement placement = FindSelectedPlacement(
                container,
                selectedPopupRuntimeInstanceId,
                selectedPopupRow,
                selectedPopupColumn,
                selectedPopupRotated,
                selectedPopupItem);
            if (placement != null && CanOpenContainerItem(placement.Item, placement.RuntimeData))
            {
                OpenContainerPopup(placement.Item, placement.RuntimeData, GetAdjacentPanelScreenPoint(contextMenuPanel));
                contextMenuPanel.gameObject.SetActive(false);
                ClearContextSelection();
            }

            return;
        }

        InventorySlot slot = inventory != null ? inventory.GetSlot(selectedBackpackSlotIndex) : null;
        if (slot == null || slot.IsEmpty)
            return;

        if (CanOpenContainerItem(slot.Item, slot.RuntimeData))
        {
            OpenContainerPopup(slot.Item, slot.RuntimeData, GetAdjacentPanelScreenPoint(contextMenuPanel));
            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();
            return;
        }

        if (quickbar != null && quickbar.TryAssignItemToFirstAvailableSlot(slot.Item, out _))
            RefreshQuickbarDisplay();

        contextMenuPanel.gameObject.SetActive(false);
        ClearContextSelection();
    }

    private void OnContextInspectAction()
    {
        if (itemInspectPanel == null || !TryGetContextItem(out ItemDefinition item, out int quantity, out ItemRuntimeData runtimeData))
            return;

        itemInspectPanel.Show(item, quantity, runtimeData);

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        ClearContextSelection();
    }

    private void OnContextSplitAction()
    {
        if (contextMenuTargetsCarryPlacement)
        {
            GridContainerState container = GetActualContainerState(selectedCarryContainerKind);
            GridItemPlacement placement = FindSelectedPlacement(
                container,
                selectedCarryRuntimeInstanceId,
                selectedCarryRow,
                selectedCarryColumn,
                selectedCarryRotated,
                selectedCarryItem);

            if (ShowSplitDialog(container, placement))
            {
                contextMenuPanel.gameObject.SetActive(false);
                ClearContextSelection();
            }

            return;
        }

        if (contextMenuTargetsPopupPlacement)
        {
            GridContainerState container = openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null;
            GridItemPlacement placement = FindSelectedPlacement(
                container,
                selectedPopupRuntimeInstanceId,
                selectedPopupRow,
                selectedPopupColumn,
                selectedPopupRotated,
                selectedPopupItem);

            if (ShowSplitDialog(container, placement))
            {
                contextMenuPanel.gameObject.SetActive(false);
                ClearContextSelection();
            }
        }
    }

    private bool TryGetContextItem(out ItemDefinition item, out int quantity, out ItemRuntimeData runtimeData)
    {
        item = null;
        quantity = 1;
        runtimeData = null;

        if (contextMenuTargetsEquipmentSlot)
        {
            InventorySlot slot = equipment != null && selectedEquipmentSlotTypeIndex >= 0
                ? equipment.GetSlot((EquipmentSlotType)selectedEquipmentSlotTypeIndex)
                : null;
            if (slot == null || slot.IsEmpty)
                return false;

            item = slot.Item;
            quantity = slot.Quantity;
            runtimeData = slot.RuntimeData;
            return item != null;
        }

        if (contextMenuTargetsCorpseEquipmentSlot)
        {
            InventorySlot slot = openedCorpseLoot != null && selectedCorpseEquipmentSlotTypeIndex >= 0
                ? openedCorpseLoot.GetSlot((EquipmentSlotType)selectedCorpseEquipmentSlotTypeIndex)
                : null;
            if (slot == null || slot.IsEmpty)
                return false;

            item = slot.Item;
            quantity = slot.Quantity;
            runtimeData = slot.RuntimeData;
            return item != null;
        }

        if (contextMenuTargetsCarryPlacement)
        {
            GridContainerState container = GetActualContainerState(selectedCarryContainerKind);
            GridItemPlacement placement = FindSelectedPlacement(
                container,
                selectedCarryRuntimeInstanceId,
                selectedCarryRow,
                selectedCarryColumn,
                selectedCarryRotated,
                selectedCarryItem);
            if (placement == null || placement.IsEmpty)
                return false;

            item = placement.Item;
            quantity = placement.Quantity;
            runtimeData = placement.RuntimeData;
            return item != null;
        }

        if (contextMenuTargetsPopupPlacement)
        {
            GridContainerState container = openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null;
            GridItemPlacement placement = FindSelectedPlacement(
                container,
                selectedPopupRuntimeInstanceId,
                selectedPopupRow,
                selectedPopupColumn,
                selectedPopupRotated,
                selectedPopupItem);
            if (placement == null || placement.IsEmpty)
                return false;

            item = placement.Item;
            quantity = placement.Quantity;
            runtimeData = placement.RuntimeData;
            return item != null;
        }

        InventorySlot backpackSlot = inventory != null ? inventory.GetSlot(selectedBackpackSlotIndex) : null;
        if (backpackSlot == null || backpackSlot.IsEmpty)
            return false;

        item = backpackSlot.Item;
        quantity = backpackSlot.Quantity;
        runtimeData = backpackSlot.RuntimeData;
        return item != null;
    }

    private void OnContextDropAction()
    {
        if (contextMenuTargetsEquipmentSlot)
        {
            EquipmentSlotType slotType = (EquipmentSlotType)selectedEquipmentSlotTypeIndex;
            InventorySlot equipmentDropSlot = equipment != null && selectedEquipmentSlotTypeIndex >= 0
                ? equipment.GetSlot(slotType)
                : null;
            ItemRuntimeData equipmentDroppedRuntimeData = equipmentDropSlot != null ? equipmentDropSlot.RuntimeData : null;
            bool dropped = itemDrop != null
                && selectedEquipmentSlotTypeIndex >= 0
                && CanDropEquipmentSlot(slotType)
                && itemDrop.TryDropFromEquipmentSlot(slotType);

            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();

            if (dropped)
            {
                CloseContainerPopupIfDroppedItem(equipmentDroppedRuntimeData);
                equipmentVisuals?.ForceRefreshNow();
                RefreshAll();
            }

            return;
        }

        if (contextMenuTargetsCorpseEquipmentSlot)
        {
            bool dropped = selectedCorpseEquipmentSlotTypeIndex >= 0
                && TryDropCorpseEquipmentSlot((EquipmentSlotType)selectedCorpseEquipmentSlotTypeIndex);

            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();

            if (dropped)
            {
                equipmentVisuals?.ForceRefreshNow();
                RefreshAll();
            }

            return;
        }

        if (contextMenuTargetsCarryPlacement)
        {
            GridContainerState selectedContainer = GetActualContainerState(selectedCarryContainerKind);
            GridItemPlacement selectedPlacement = FindSelectedPlacement(
                selectedContainer,
                selectedCarryRuntimeInstanceId,
                selectedCarryRow,
                selectedCarryColumn,
                selectedCarryRotated,
                selectedCarryItem);
            bool dropped = TryDropCarryPlacement();

            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();

            if (dropped)
            {
                CloseContainerPopupIfDroppedItem(selectedPlacement != null ? selectedPlacement.RuntimeData : null);
                equipmentVisuals?.ForceRefreshNow();
                RefreshAll();
            }

            return;
        }

        if (contextMenuTargetsPopupPlacement)
        {
            GridContainerState popupContainer = openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null;
            GridItemPlacement popupPlacement = FindSelectedPlacement(
                popupContainer,
                selectedPopupRuntimeInstanceId,
                selectedPopupRow,
                selectedPopupColumn,
                selectedPopupRotated,
                selectedPopupItem);
            bool dropped = TryDropPopupPlacement();

            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();

            if (dropped)
            {
                CloseContainerPopupIfDroppedItem(popupPlacement != null ? popupPlacement.RuntimeData : null);
                equipmentVisuals?.ForceRefreshNow();
                RefreshAll();
            }

            return;
        }

        InventorySlot slot = inventory != null ? inventory.GetSlot(selectedBackpackSlotIndex) : null;
        if (slot == null || slot.IsEmpty || itemDrop == null)
            return;

        CloseContainerPopup();
        contextMenuPanel.gameObject.SetActive(false);

        if (slot.Item.canStack && slot.Quantity > 1)
        {
            ShowDropDialog(selectedBackpackSlotIndex, slot.Item.displayName, slot.Quantity);
            return;
        }

        ItemRuntimeData droppedRuntimeData = slot.RuntimeData;
        itemDrop.TryDropFromInventorySlot(selectedBackpackSlotIndex, 1);
        CloseContainerPopupIfDroppedItem(droppedRuntimeData);
        ClearContextSelection();
        RefreshAll();
    }

    private void ShowDropDialog(int slotIndex, string itemName, int maxQuantity)
    {
        dropDialogSlotIndex = slotIndex;
        dropDialogQuantity = Mathf.Clamp(1, 1, maxQuantity);
        dropDialogTitleText.text = "Drop " + itemName;
        dropDialogPanel.gameObject.SetActive(true);
        UpdateDropDialogQuantity(maxQuantity);
    }

    private void HideDropDialog()
    {
        if (dropDialogPanel != null)
            dropDialogPanel.gameObject.SetActive(false);

        dropDialogSlotIndex = -1;
        dropDialogQuantity = 1;
        selectedBackpackSlotIndex = -1;
    }

    private bool ShowSplitDialog(GridContainerState container, GridItemPlacement placement)
    {
        if (splitDialogPanel == null || container == null || placement == null || placement.IsEmpty)
            return false;

        if (placement.Item == null || !placement.Item.canStack || placement.Quantity <= 1)
            return false;

        splitDialogContainer = container;
        splitDialogRuntimeInstanceId = placement.RuntimeInstanceId;
        splitDialogSourceRow = placement.Row;
        splitDialogSourceColumn = placement.Column;
        splitDialogSourceRotated = placement.Rotated;
        splitDialogItem = placement.Item;
        splitDialogMaxQuantity = Mathf.Max(1, placement.Quantity - 1);
        splitDialogQuantity = 1;

        if (splitDialogTitleText != null)
            splitDialogTitleText.text = "Split " + placement.Item.displayName;

        if (splitDialogItemIconImage != null)
        {
            splitDialogItemIconImage.sprite = placement.Item.GetGridInventorySpriteOrFallback();
            splitDialogItemIconImage.enabled = splitDialogItemIconImage.sprite != null;
        }

        splitDialogPanel.SetAsLastSibling();
        splitDialogPanel.gameObject.SetActive(true);
        UpdateSplitDialogQuantity(1);
        return true;
    }

    private void HideSplitDialog()
    {
        if (splitDialogPanel != null)
            splitDialogPanel.gameObject.SetActive(false);

        splitDialogContainer = null;
        splitDialogRuntimeInstanceId = string.Empty;
        splitDialogSourceRow = -1;
        splitDialogSourceColumn = -1;
        splitDialogSourceRotated = false;
        splitDialogItem = null;
        splitDialogMaxQuantity = 1;
        splitDialogQuantity = 1;
        splitDialogUpdating = false;
    }

    private void CloseInventoryForItemUse()
    {
        HideDropDialog();
        HideSplitDialog();

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        if (containerPopupPanel != null)
            containerPopupPanel.gameObject.SetActive(false);

        if (inventoryPanel != null && inventoryPanel.gameObject.activeSelf)
        {
            inventoryPanel.gameObject.SetActive(false);
            CloseExternalContainer();
        }
    }

    private void OpenContainerPopup(ItemDefinition item, ItemRuntimeData runtimeData, Vector2 screenPoint)
    {
        if (containerPopupPanel == null || containerPopupView == null || runtimeData == null || runtimeData.StoredContainerState == null)
            return;

        openedContainerDefinition = item;
        openedContainerRuntimeData = runtimeData;
        RefreshContainerPopup();
        PositionPanelAtScreenPoint(containerPopupPanel, screenPoint);
        containerPopupPanel.gameObject.SetActive(true);
    }

    private bool TryHandleCorpseEquipmentEquip(EquipmentSlotType sourceSlotType)
    {
        if (openedCorpseLoot == null || equipment == null)
            return false;

        InventorySlot sourceSlot = openedCorpseLoot.GetSlot(sourceSlotType);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item == null)
            return false;

        if (!TryGetPreferredEquipmentSlot(sourceSlot.Item, out EquipmentSlotType targetSlotType))
            return false;

        InventorySlot targetSlot = equipment.GetSlot(targetSlotType);
        if (targetSlot == null || !equipment.CanEquip(targetSlotType, sourceSlot.Item))
            return false;

        ItemDefinition item = sourceSlot.Item;
        int quantity = sourceSlot.Quantity;
        ItemRuntimeData runtimeData = sourceSlot.GetRuntimeDataForTransfer(quantity);
        ItemDefinition previousItem = targetSlot.Item;
        int previousQuantity = targetSlot.Quantity;
        ItemRuntimeData previousRuntimeData = targetSlot.GetRuntimeDataForTransfer(previousQuantity);

        sourceSlot.Clear();

        if (previousItem != null && previousQuantity > 0)
        {
            if (openedCorpseLoot.PocketContainer == null
                || !openedCorpseLoot.PocketContainer.TryPlaceNewItem(previousItem, previousQuantity, previousRuntimeData, out _))
            {
                sourceSlot.TrySet(item, quantity, runtimeData);
                return false;
            }
        }

        targetSlot.Clear();
        if (targetSlot.TrySet(item, quantity, runtimeData))
        {
            CloseContainerPopupIfEquippedItem(item, runtimeData);
            return true;
        }

        targetSlot.TrySet(previousItem, previousQuantity, previousRuntimeData);
        sourceSlot.TrySet(item, quantity, runtimeData);
        return false;
    }

    private bool TryDropCorpseEquipmentSlot(EquipmentSlotType slotType)
    {
        if (openedCorpseLoot == null)
            return false;

        InventorySlot sourceSlot = openedCorpseLoot.GetSlot(slotType);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item == null)
            return false;

        ItemDefinition item = sourceSlot.Item;
        int quantity = sourceSlot.Quantity;
        ItemRuntimeData runtimeData = sourceSlot.GetRuntimeDataForTransfer(quantity);
        sourceSlot.Clear();

        WorldItemPickup droppedPickup = itemDrop != null
            ? itemDrop.SpawnWorldPickup(item, quantity, runtimeData)
            : WorldItemPickup.Spawn(
                item,
                quantity,
                runtimeData,
                transform.TransformPoint(new Vector3(0f, 0.75f, 1.1f)),
                transform.rotation);

        if (droppedPickup != null)
        {
            CloseContainerPopupIfDroppedItem(runtimeData);
            return true;
        }

        sourceSlot.TrySet(item, quantity, runtimeData);
        return false;
    }

    private bool TryGetPreferredEquipmentSlot(ItemDefinition item, out EquipmentSlotType slotType)
    {
        slotType = EquipmentSlotType.PrimaryWeapon;

        if (item is WeaponItemDefinition weapon)
        {
            slotType = weapon.weaponCategory == WeaponCategory.Pistol
                ? EquipmentSlotType.SecondaryWeapon
                : EquipmentSlotType.PrimaryWeapon;
            return true;
        }

        if (item is ArmorItemDefinition armor)
        {
            slotType = GetArmorEquipmentSlot(armor.armorSlot);
            return true;
        }

        if (item is ContainerItemDefinition containerItem)
        {
            slotType = GetContainerEquipmentSlot(containerItem.containerKind);
            return true;
        }

        return false;
    }

    private void CloseContainerPopup()
    {
        if (containerPopupPanel != null)
            containerPopupPanel.gameObject.SetActive(false);

        openedContainerDefinition = null;
        openedContainerRuntimeData = null;

        if (containerPopupView != null)
            ClearGridPlacementViews(containerPopupView);
    }

    private void CloseContainerPopupIfDroppedItem(ItemRuntimeData droppedRuntimeData)
    {
        if (openedContainerRuntimeData == null || droppedRuntimeData == null)
            return;

        if (ReferenceEquals(openedContainerRuntimeData, droppedRuntimeData))
            CloseContainerPopup();
    }

    private void CloseContainerPopupIfEquippedItem(ItemDefinition equippedItem, ItemRuntimeData equippedRuntimeData)
    {
        if (openedContainerRuntimeData == null || equippedRuntimeData == null || equippedItem == null)
            return;

        if (!CanOpenContainerItem(equippedItem, equippedRuntimeData))
            return;

        if (ReferenceEquals(openedContainerRuntimeData, equippedRuntimeData))
            CloseContainerPopup();
    }

    private void RefreshContainerPopup()
    {
        if (containerPopupPanel == null || containerPopupView == null)
            return;

        if (openedContainerRuntimeData == null || openedContainerRuntimeData.StoredContainerState == null)
        {
            CloseContainerPopup();
            return;
        }

        GridContainerState containerState = openedContainerRuntimeData.StoredContainerState;
        containerPopupView.kind = containerState.ContainerKind;
        int rows = Mathf.Max(1, containerState.RowCount);
        int columns = Mathf.Max(1, containerState.ColumnCount);
        float gridWidth = columns * InventoryGridCellSize;
        float gridHeight = rows * InventoryGridCellSize;

        if (containerPopupTitleText != null)
            containerPopupTitleText.text = openedContainerDefinition != null ? openedContainerDefinition.displayName : "Container";

        containerPopupPanel.sizeDelta = new Vector2(
            Mathf.Max(220f, gridWidth + 24f),
            Mathf.Max(118f, gridHeight + 54f));

        containerPopupView.gridFrameRect.sizeDelta = new Vector2(
            gridWidth + (InventoryGridOuterBorderThickness * 2f),
            gridHeight + (InventoryGridOuterBorderThickness * 2f));
        containerPopupView.gridFrameImage.sprite = GetOrCreateGridFrameSprite(rows, columns);
        containerPopupView.gridRect.sizeDelta = new Vector2(gridWidth, gridHeight);
        containerPopupView.gridLayout.constraintCount = columns;
        HideGridCellVisuals(containerPopupView);
        containerPopupView.placementsRoot.SetAsLastSibling();

        ClearGridPlacementViews(containerPopupView);
        IReadOnlyList<GridItemPlacement> placements = containerState.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            GridPlacementView placementView = CreateGridPlacementView(containerState.ContainerKind, containerPopupView.placementsRoot, placement, -1);
            containerPopupView.placementViews.Add(placementView);
        }
    }

    private void OnDropMinus()
    {
        InventorySlot slot = inventory != null ? inventory.GetSlot(dropDialogSlotIndex) : null;
        if (slot == null || slot.IsEmpty)
            return;

        dropDialogQuantity = Mathf.Max(1, dropDialogQuantity - 1);
        UpdateDropDialogQuantity(slot.Quantity);
    }

    private void OnDropPlus()
    {
        InventorySlot slot = inventory != null ? inventory.GetSlot(dropDialogSlotIndex) : null;
        if (slot == null || slot.IsEmpty)
            return;

        dropDialogQuantity = Mathf.Min(slot.Quantity, dropDialogQuantity + 1);
        UpdateDropDialogQuantity(slot.Quantity);
    }

    private void OnDropConfirm()
    {
        if (itemDrop != null && dropDialogSlotIndex >= 0)
            itemDrop.TryDropFromInventorySlot(dropDialogSlotIndex, dropDialogQuantity);

        HideDropDialog();
        RefreshAll();
    }

    private void OnDropCancel()
    {
        HideDropDialog();
    }

    private void UpdateDropDialogQuantity(int maxQuantity)
    {
        dropDialogQuantityText.text = dropDialogQuantity + " / " + maxQuantity;
    }

    private void OnSplitMinus()
    {
        UpdateSplitDialogQuantity(splitDialogQuantity - 1);
    }

    private void OnSplitPlus()
    {
        UpdateSplitDialogQuantity(splitDialogQuantity + 1);
    }

    private void OnSplitSliderChanged(float value)
    {
        if (splitDialogUpdating)
            return;

        UpdateSplitDialogQuantity(Mathf.RoundToInt(value));
    }

    private void OnSplitInputEndEdit(string value)
    {
        if (splitDialogUpdating)
            return;

        if (!int.TryParse(value, out int parsed))
            parsed = splitDialogQuantity;

        UpdateSplitDialogQuantity(parsed);
    }

    private void OnSplitConfirm()
    {
        GridItemPlacement placement = FindSelectedPlacement(
            splitDialogContainer,
            splitDialogRuntimeInstanceId,
            splitDialogSourceRow,
            splitDialogSourceColumn,
            splitDialogSourceRotated,
            splitDialogItem);

        if (placement == null || placement.IsEmpty || placement.Item == null || !placement.Item.canStack || placement.Quantity <= 1)
        {
            HideSplitDialog();
            RefreshAll();
            return;
        }

        int quantityToSplit = Mathf.Clamp(splitDialogQuantity, 1, placement.Quantity - 1);
        int removedQuantity = placement.Remove(quantityToSplit);
        if (removedQuantity <= 0)
        {
            HideSplitDialog();
            RefreshAll();
            return;
        }

        bool placed = splitDialogContainer != null
            && splitDialogContainer.TryPlaceNewItemNear(
                placement.Item,
                removedQuantity,
                placement.Row,
                placement.Column + placement.ColumnSpan,
                null,
                out _,
                placement.Rotated);

        if (!placed)
            placement.Add(placement.Item, removedQuantity);

        HideSplitDialog();
        RefreshAll();
    }

    private void OnSplitCancel()
    {
        HideSplitDialog();
    }

    private void UpdateSplitDialogQuantity(int requestedQuantity)
    {
        splitDialogQuantity = Mathf.Clamp(requestedQuantity, 1, Mathf.Max(1, splitDialogMaxQuantity));
        splitDialogUpdating = true;

        if (splitQuantitySlider != null)
        {
            splitQuantitySlider.minValue = 1f;
            splitQuantitySlider.maxValue = Mathf.Max(1, splitDialogMaxQuantity);
            splitQuantitySlider.value = splitDialogQuantity;
        }

        if (splitQuantityInput != null)
            splitQuantityInput.text = splitDialogQuantity.ToString();

        if (splitDialogMaxText != null)
            splitDialogMaxText.text = "1 / " + splitDialogMaxQuantity;

        splitDialogUpdating = false;
    }

    private bool TryHandleCarryPlacementEquip()
    {
        if (equipment == null || gridInventory == null)
            return false;

        GridContainerState container = GetActualContainerState(selectedCarryContainerKind);
        GridItemPlacement placement = FindSelectedPlacement(
            container,
            selectedCarryRuntimeInstanceId,
            selectedCarryRow,
            selectedCarryColumn,
            selectedCarryRotated,
            selectedCarryItem);
        if (placement == null || placement.IsEmpty)
            return false;

        EquipmentSlotType slotType;
        if (placement.Item is WeaponItemDefinition weapon)
            slotType = weapon.weaponCategory == WeaponCategory.Pistol ? EquipmentSlotType.SecondaryWeapon : EquipmentSlotType.PrimaryWeapon;
        else if (placement.Item is ArmorItemDefinition armor)
            slotType = GetArmorEquipmentSlot(armor.armorSlot);
        else if (placement.Item is ContainerItemDefinition containerItem)
            slotType = GetContainerEquipmentSlot(containerItem.containerKind);
        else
            return false;

        InventorySlot equipmentSlot = equipment.GetSlot(slotType);
        if (equipmentSlot == null)
            return false;

        ItemDefinition item = placement.Item;
        int quantity = placement.Quantity;
        int row = placement.Row;
        int column = placement.Column;
        bool rotated = placement.Rotated;
        ItemRuntimeData runtimeData = placement.RuntimeData;

        if (!container.TryRemovePlacement(placement))
            return false;

        ItemDefinition previousItem = equipmentSlot.Item;
        int previousQuantity = equipmentSlot.Quantity;
        ItemRuntimeData previousRuntime = equipmentSlot.GetRuntimeDataForTransfer(previousQuantity);

        if (previousItem != null && previousQuantity > 0)
        {
            if (previousRuntime != null
                && previousRuntime.StoredContainerState != null
                && ReferenceEquals(previousRuntime.StoredContainerState, container))
            {
                container.TryPlaceItemAt(item, quantity, row, column, runtimeData, out _, rotated);
                return false;
            }

            if (!container.TryPlaceNewItem(previousItem, previousQuantity, previousRuntime, out _))
            {
                container.TryPlaceItemAt(item, quantity, row, column, runtimeData, out _, rotated);
                return false;
            }
        }

        equipmentSlot.Clear();
        if (!equipmentSlot.TrySet(item, quantity, runtimeData))
        {
            equipmentSlot.TrySet(previousItem, previousQuantity, previousRuntime);
            container.TryPlaceItemAt(item, quantity, row, column, runtimeData, out _, rotated);
            return false;
        }

        CloseContainerPopupIfEquippedItem(item, runtimeData);
        return true;
    }

    private bool TryDropCarryPlacement()
    {
        if (gridInventory == null
            && selectedCarryContainerKind != GridContainerKind.External
            && selectedCarryContainerKind != GridContainerKind.CorpsePocket)
            return false;

        GridContainerState container = GetActualContainerState(selectedCarryContainerKind);
        GridItemPlacement placement = FindSelectedPlacement(
            container,
            selectedCarryRuntimeInstanceId,
            selectedCarryRow,
            selectedCarryColumn,
            selectedCarryRotated,
            selectedCarryItem);
        if (container == null || placement == null || placement.IsEmpty)
            return false;

        ItemDefinition item = placement.Item;
        int quantity = placement.Quantity;
        int row = placement.Row;
        int column = placement.Column;
        bool rotated = placement.Rotated;
        ItemRuntimeData runtimeData = placement.RuntimeData;

        if (!container.TryRemovePlacement(placement))
            return false;

        WorldItemPickup droppedPickup = itemDrop != null
            ? itemDrop.SpawnWorldPickup(item, quantity, runtimeData)
            : WorldItemPickup.Spawn(
                item,
                quantity,
                runtimeData,
                transform.TransformPoint(new Vector3(0f, 0.75f, 1.1f)),
                transform.rotation);

        if (droppedPickup != null)
            return true;

        container.TryPlaceItemAt(item, quantity, row, column, runtimeData, out _, rotated);
        return false;
    }

    private bool TryHandlePopupPlacementEquip()
    {
        if (equipment == null || openedContainerRuntimeData == null || openedContainerRuntimeData.StoredContainerState == null)
            return false;

        GridContainerState container = openedContainerRuntimeData.StoredContainerState;
        GridItemPlacement placement = FindSelectedPlacement(
            container,
            selectedPopupRuntimeInstanceId,
            selectedPopupRow,
            selectedPopupColumn,
            selectedPopupRotated,
            selectedPopupItem);
        if (placement == null || placement.IsEmpty)
            return false;

        EquipmentSlotType slotType;
        if (placement.Item is WeaponItemDefinition weapon)
            slotType = weapon.weaponCategory == WeaponCategory.Pistol ? EquipmentSlotType.SecondaryWeapon : EquipmentSlotType.PrimaryWeapon;
        else if (placement.Item is ArmorItemDefinition armor)
            slotType = GetArmorEquipmentSlot(armor.armorSlot);
        else if (placement.Item is ContainerItemDefinition containerItem)
            slotType = GetContainerEquipmentSlot(containerItem.containerKind);
        else
            return false;

        InventorySlot equipmentSlot = equipment.GetSlot(slotType);
        if (equipmentSlot == null)
            return false;

        ItemDefinition item = placement.Item;
        int quantity = placement.Quantity;
        int row = placement.Row;
        int column = placement.Column;
        bool rotated = placement.Rotated;
        ItemRuntimeData runtimeData = placement.RuntimeData;

        if (!container.TryRemovePlacement(placement))
            return false;

        ItemDefinition previousItem = equipmentSlot.Item;
        int previousQuantity = equipmentSlot.Quantity;
        ItemRuntimeData previousRuntime = equipmentSlot.GetRuntimeDataForTransfer(previousQuantity);

        if (previousItem != null && previousQuantity > 0)
        {
            if (previousRuntime != null
                && previousRuntime.StoredContainerState != null
                && ReferenceEquals(previousRuntime.StoredContainerState, container))
            {
                container.TryPlaceItemAt(item, quantity, row, column, runtimeData, out _, rotated);
                return false;
            }

            if (!container.TryPlaceNewItem(previousItem, previousQuantity, previousRuntime, out _))
            {
                container.TryPlaceItemAt(item, quantity, row, column, runtimeData, out _, rotated);
                return false;
            }
        }

        equipmentSlot.Clear();
        if (!equipmentSlot.TrySet(item, quantity, runtimeData))
        {
            equipmentSlot.TrySet(previousItem, previousQuantity, previousRuntime);
            container.TryPlaceItemAt(item, quantity, row, column, runtimeData, out _, rotated);
            return false;
        }

        CloseContainerPopupIfEquippedItem(item, runtimeData);
        return true;
    }

    private bool TryDropPopupPlacement()
    {
        GridContainerState container = openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null;
        GridItemPlacement placement = FindSelectedPlacement(
            container,
            selectedPopupRuntimeInstanceId,
            selectedPopupRow,
            selectedPopupColumn,
            selectedPopupRotated,
            selectedPopupItem);
        if (container == null || placement == null || placement.IsEmpty)
            return false;

        ItemDefinition item = placement.Item;
        int quantity = placement.Quantity;
        int row = placement.Row;
        int column = placement.Column;
        bool rotated = placement.Rotated;
        ItemRuntimeData runtimeData = placement.RuntimeData;

        if (!container.TryRemovePlacement(placement))
            return false;

        WorldItemPickup droppedPickup = itemDrop != null
            ? itemDrop.SpawnWorldPickup(item, quantity, runtimeData)
            : WorldItemPickup.Spawn(
                item,
                quantity,
                runtimeData,
                transform.TransformPoint(new Vector3(0f, 0.75f, 1.1f)),
                transform.rotation);

        if (droppedPickup != null)
            return true;

        container.TryPlaceItemAt(item, quantity, row, column, runtimeData, out _, rotated);
        return false;
    }

    private static GridItemPlacement FindRuntimePlacement(GridContainerState container, string runtimeInstanceId)
    {
        if (container == null || string.IsNullOrWhiteSpace(runtimeInstanceId))
            return null;

        IReadOnlyList<GridItemPlacement> placements = container.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            if (placement.RuntimeInstanceId == runtimeInstanceId)
                return placement;
        }

        return null;
    }

    private static GridItemPlacement FindSelectedPlacement(
        GridContainerState container,
        string runtimeInstanceId,
        int row,
        int column,
        bool rotated,
        ItemDefinition item)
    {
        if (container == null)
            return null;

        if (!string.IsNullOrWhiteSpace(runtimeInstanceId))
            return FindRuntimePlacement(container, runtimeInstanceId);

        if (item == null)
            return null;

        IReadOnlyList<GridItemPlacement> placements = container.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            if (placement.Item == item &&
                placement.Row == row &&
                placement.Column == column &&
                placement.Rotated == rotated)
            {
                return placement;
            }
        }

        return null;
    }

    private bool TryHandleEquipmentUnequip(EquipmentSlotType slotType)
    {
        if (equipment == null || !CanUnequipEquipmentSlot(slotType))
            return false;

        switch (slotType)
        {
            case EquipmentSlotType.PrimaryWeapon:
            case EquipmentSlotType.SecondaryWeapon:
                return equipment.TryUnequipToContainer(
                    slotType,
                    gridInventory != null && gridInventory.HasRigContainer ? gridInventory.RigContainer : null,
                    gridInventory != null && gridInventory.HasBackpackContainer ? gridInventory.BackpackContainer : null,
                    gridInventory != null ? gridInventory.PocketContainer : null);

            case EquipmentSlotType.HeadArmor:
                return equipment.TryUnequipToContainer(
                    slotType,
                    gridInventory != null && gridInventory.HasRigContainer ? gridInventory.RigContainer : null,
                    gridInventory != null && gridInventory.HasBackpackContainer ? gridInventory.BackpackContainer : null);

            case EquipmentSlotType.ChestArmor:
                return equipment.TryUnequipToContainer(
                    slotType,
                    gridInventory != null && gridInventory.HasBackpackContainer ? gridInventory.BackpackContainer : null);

            default:
                return equipment.TryUnequipToInventory(slotType);
        }
    }

    private static bool CanUnequipEquipmentSlot(EquipmentSlotType slotType)
    {
        return slotType != EquipmentSlotType.Backpack;
    }

    private static bool CanDragEquipmentSlot(EquipmentSlotType slotType)
    {
        return CanUnequipEquipmentSlot(slotType) || slotType == EquipmentSlotType.Backpack;
    }

    private static bool CanDropEquipmentSlot(EquipmentSlotType slotType)
    {
        return slotType == EquipmentSlotType.PrimaryWeapon
            || slotType == EquipmentSlotType.SecondaryWeapon
            || slotType == EquipmentSlotType.HeadArmor
            || slotType == EquipmentSlotType.ChestArmor
            || slotType == EquipmentSlotType.Backpack;
    }

    private void RefreshAll()
    {
        RefreshBackpackDisplay();
        RefreshExternalContainerDisplay();
        RefreshContainerPopup();
        RefreshQuickbarDisplay();
        RefreshEquipmentSlotsDisplay();
        RefreshEquipmentSummary();
        RefreshStatusHud();
        RefreshWeaponHud();
    }

    private void RefreshBackpackDisplay()
    {
        equipmentVisuals?.ForceRefreshNow();
        BuildGridDisplayStates();
        RefreshGridContainerDisplays();
    }

    private void RefreshExternalContainerDisplay()
    {
        if (externalContainerView == null || rightWorkspaceTitleText == null || rightWorkspaceHintText == null)
            return;

        if (openedCorpseLoot != null)
        {
            RefreshCorpseLootDisplay();
            return;
        }

        if (corpseLootPanel != null)
            corpseLootPanel.gameObject.SetActive(false);

        if (openedSearchableContainer == null)
        {
            ApplyRightWorkspaceMode(false);
            rightWorkspaceTitleText.text = "Reserved";
            rightWorkspaceHintText.text = "Future loot container / shelter stash area.";
            SetRightWorkspaceCashVisible(false);
            externalContainerView.rect.gameObject.SetActive(false);
            ClearGridPlacementViews(externalContainerView);
            return;
        }

        openedSearchableContainer.EnsureInitialized();
        GridContainerState containerState = openedSearchableContainer.ContainerState;
        if (containerState == null)
        {
            ApplyRightWorkspaceMode(false);
            rightWorkspaceTitleText.text = "Reserved";
            rightWorkspaceHintText.text = "Future loot container / shelter stash area.";
            SetRightWorkspaceCashVisible(false);
            externalContainerView.rect.gameObject.SetActive(false);
            ClearGridPlacementViews(externalContainerView);
            return;
        }

        bool isShelterStash = IsShelterStash(openedSearchableContainer);
        ApplyRightWorkspaceMode(isShelterStash);
        rightWorkspaceTitleText.text = openedSearchableContainer.DisplayName;
        rightWorkspaceHintText.text = isShelterStash
            ? "Drag items between stash and carry containers."
            : "Drag items into your carry containers.";
        SetRightWorkspaceCashVisible(isShelterStash);
        if (isShelterStash && rightWorkspaceCashText != null)
            rightWorkspaceCashText.text = FormatStashCash(CalculateCurrencyValue(containerState));

        int rows = Mathf.Max(1, containerState.RowCount);
        int columns = Mathf.Max(1, containerState.ColumnCount);
        float gridWidth = columns * InventoryGridCellSize;
        float gridHeight = rows * InventoryGridCellSize;

        externalContainerView.kind = GridContainerKind.External;
        externalContainerView.rect.gameObject.SetActive(true);
        externalContainerView.rect.sizeDelta = new Vector2(Mathf.Max(260f, gridWidth + 24f), Mathf.Max(180f, gridHeight + 40f));
        if (isShelterStash)
        {
            externalContainerView.rect.offsetMin = new Vector2(20f, 20f);
            externalContainerView.rect.offsetMax = new Vector2(-20f, -112f);
        }
        externalContainerView.gridFrameRect.sizeDelta = new Vector2(
            gridWidth + (InventoryGridOuterBorderThickness * 2f),
            gridHeight + (InventoryGridOuterBorderThickness * 2f));
        ApplyExternalContainerFrameLayout(isShelterStash, openedSearchableContainer);
        externalContainerView.gridFrameImage.sprite = GetOrCreateGridFrameSprite(rows, columns);
        externalContainerView.gridRect.sizeDelta = new Vector2(gridWidth, gridHeight);
        externalContainerView.gridLayout.constraintCount = columns;
        HideGridCellVisuals(externalContainerView);
        externalContainerView.placementsRoot.SetAsLastSibling();

        ClearGridPlacementViews(externalContainerView);
        IReadOnlyList<GridItemPlacement> placements = containerState.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            GridPlacementView placementView = CreateGridPlacementView(GridContainerKind.External, externalContainerView.placementsRoot, placement, -1);
            externalContainerView.placementViews.Add(placementView);
        }
    }

    private void RefreshCorpseLootDisplay()
    {
        if (openedCorpseLoot == null || corpseLootPanel == null || corpsePocketView == null)
            return;

        openedCorpseLoot.EnsureInitialized();
        ApplyRightWorkspaceMode(false);
        rightWorkspaceTitleText.text = openedCorpseLoot.EnemyTypeDisplayName;
        rightWorkspaceHintText.text = "Search corpse equipment and pockets.";
        SetRightWorkspaceCashVisible(false);

        if (externalContainerView != null)
        {
            externalContainerView.rect.gameObject.SetActive(false);
            ClearGridPlacementViews(externalContainerView);
        }

        corpseLootPanel.gameObject.SetActive(true);
        RefreshCorpseEquipmentSlotsDisplay();
        RefreshCorpsePocketDisplay();
    }

    private void RefreshCorpseEquipmentSlotsDisplay()
    {
        foreach (KeyValuePair<EquipmentSlotType, SlotView> pair in corpseEquipmentSlotViews)
        {
            SlotView slotView = pair.Value;
            InventorySlot slot = openedCorpseLoot != null ? openedCorpseLoot.GetSlot(pair.Key) : null;
            bool useInlineName = UseInlineEquipmentName(pair.Key);
            Sprite placeholderIcon = GetEquipmentPlaceholderIcon(pair.Key);

            if (slot == null || slot.IsEmpty)
            {
                slotView.keyText.text = placeholderIcon != null ? string.Empty : (useInlineName ? string.Empty : GetEquipmentSlotLabel(pair.Key));
                slotView.itemText.text = placeholderIcon != null ? string.Empty : (useInlineName ? string.Empty : GetEquipmentSlotPlaceholder(pair.Key));
                slotView.quantityText.text = string.Empty;
                slotView.background.color = GetEquipmentSlotEmptyColor(pair.Key);
                slotView.itemText.color = new Color(0.70f, 0.74f, 0.79f, 0.72f);
                if (slotView.detailText != null)
                    slotView.detailText.text = string.Empty;
                if (placeholderIcon != null)
                    ApplyEquipmentSlotPlaceholderPresentation(slotView, pair.Key);
                else
                    ApplyEquipmentSlotIconPresentation(slotView, pair.Key, null);
                SetSlotIconPreserveAspect(slotView, false);
                SetSlotIconTint(slotView, new Color(1f, 1f, 1f, 0.12f));
                SetSlotIcon(slotView, placeholderIcon);
                continue;
            }

            slotView.keyText.text = useInlineName ? string.Empty : GetEquipmentSlotLabel(pair.Key);
            slotView.itemText.text = Shorten(slot.Item.displayName, pair.Key == EquipmentSlotType.PrimaryWeapon ? 20 : 16);
            slotView.quantityText.text = slot.Quantity > 1 ? slot.Quantity.ToString() : string.Empty;
            slotView.background.color = GetEquipmentSlotFilledColor(pair.Key, slot.Item);
            slotView.itemText.color = Color.white;
            if (slotView.detailText != null)
                slotView.detailText.text = string.Empty;
            ApplyEquipmentSlotIconPresentation(slotView, pair.Key, slot.Item);
            SetSlotIconPreserveAspect(slotView, true);
            SetSlotIconTint(slotView, Color.white);
            SetSlotIcon(slotView, GetEquipmentDisplayIcon(slot.Item));
        }
    }

    private void RefreshCorpsePocketDisplay()
    {
        GridContainerState pocket = openedCorpseLoot != null ? openedCorpseLoot.PocketContainer : null;
        if (pocket == null)
        {
            ClearGridPlacementViews(corpsePocketView);
            return;
        }

        int rows = Mathf.Max(1, pocket.RowCount);
        int columns = Mathf.Max(1, pocket.ColumnCount);
        float gridWidth = columns * InventoryGridCellSize;
        float gridHeight = rows * InventoryGridCellSize;

        corpsePocketView.kind = GridContainerKind.CorpsePocket;
        corpsePocketView.gridFrameRect.sizeDelta = new Vector2(
            gridWidth + (InventoryGridOuterBorderThickness * 2f),
            gridHeight + (InventoryGridOuterBorderThickness * 2f));
        corpsePocketView.gridFrameImage.sprite = GetOrCreateGridFrameSprite(rows, columns);
        corpsePocketView.gridRect.sizeDelta = new Vector2(gridWidth, gridHeight);
        corpsePocketView.gridLayout.constraintCount = columns;
        HideGridCellVisuals(corpsePocketView);
        corpsePocketView.placementsRoot.SetAsLastSibling();

        ClearGridPlacementViews(corpsePocketView);
        IReadOnlyList<GridItemPlacement> placements = pocket.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            GridPlacementView placementView = CreateGridPlacementView(GridContainerKind.CorpsePocket, corpsePocketView.placementsRoot, placement, -1);
            corpsePocketView.placementViews.Add(placementView);
        }
    }

    private void ApplyRightWorkspaceMode(bool isShelterStash)
    {
        if (rightWorkspacePanel == null)
            return;

        rightWorkspacePanel.anchorMin = new Vector2(isShelterStash ? 0.62f : 0.637f, 0f);
        rightWorkspacePanel.anchorMax = new Vector2(1f, 1f);
        rightWorkspacePanel.offsetMin = new Vector2(6f, 24f);
        rightWorkspacePanel.offsetMax = new Vector2(-24f, -88f);
    }

    private void ApplyExternalContainerFrameLayout(bool isShelterStash, SearchableContainer container)
    {
        if (externalContainerView?.gridFrameRect == null)
            return;

        if (isShelterStash)
        {
            externalContainerView.gridFrameRect.anchorMin = new Vector2(0f, 1f);
            externalContainerView.gridFrameRect.anchorMax = new Vector2(0f, 1f);
            externalContainerView.gridFrameRect.pivot = new Vector2(0f, 1f);
            externalContainerView.gridFrameRect.anchoredPosition = new Vector2(20f, -76f);
            return;
        }

        externalContainerView.gridFrameRect.anchorMin = new Vector2(0.5f, 0.42f);
        externalContainerView.gridFrameRect.anchorMax = new Vector2(0.5f, 0.42f);
        externalContainerView.gridFrameRect.pivot = new Vector2(0.5f, 0.5f);
        externalContainerView.gridFrameRect.anchoredPosition = GetExternalContainerFrameOffset(container);
    }

    private Vector2 GetExternalContainerFrameOffset(SearchableContainer container)
    {
        if (container == null)
            return new Vector2(-220f, 0f);

        string displayName = container.DisplayName ?? string.Empty;
        if (displayName.Equals("Emergency Drop Crate", StringComparison.OrdinalIgnoreCase))
            return new Vector2(-120f, 0f);

        if (displayName.Equals("Large Supply Crate", StringComparison.OrdinalIgnoreCase))
            return new Vector2(-260f, -96f);

        if (IsShelterStash(container))
            return new Vector2(0f, -32f);

        return new Vector2(-220f, 0f);
    }

    private bool IsShelterStash(SearchableContainer container)
    {
        return container != null && container.GetComponent("ShelterStashStation") != null;
    }

    private void SetRightWorkspaceCashVisible(bool visible)
    {
        if (rightWorkspaceCashText != null)
            rightWorkspaceCashText.gameObject.SetActive(visible);
    }

    private float CalculateCurrencyValue(GridContainerState containerState)
    {
        if (containerState == null)
            return 0f;

        float totalValue = 0f;
        IReadOnlyList<GridItemPlacement> placements = containerState.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            if (placement.Item != null && placement.Item.Type == ItemType.Currency)
                totalValue += placement.Item.GetTotalMoneyValue(placement.Quantity);

            GridContainerState nestedContainer = placement.RuntimeData != null
                ? placement.RuntimeData.StoredContainerState
                : null;
            if (nestedContainer != null)
                totalValue += CalculateCurrencyValue(nestedContainer);
        }

        return totalValue;
    }

    private static string FormatStashCash(float value)
    {
        if (value >= 1000f)
            return "$ " + (value / 1000f).ToString("0.#") + "k";

        return "$ " + Mathf.RoundToInt(value).ToString();
    }

    private void RefreshQuickbarDisplay()
    {
        for (int i = 0; i < quickbarSlotViews.Count; i++)
        {
            SlotView slotView = quickbarSlotViews[i];
            ItemDefinition assignedItem = quickbar != null ? quickbar.GetAssignedItem(i) : null;
            slotView.keyText.text = string.Empty;

            if (assignedItem == null)
            {
                slotView.itemText.text = string.Empty;
                slotView.quantityText.text = string.Empty;
                slotView.background.color = new Color(0.12f, 0.13f, 0.17f, 0.94f);
                if (slotView.detailText != null)
                    slotView.detailText.text = string.Empty;
                SetSlotIcon(slotView, null);
                continue;
            }

            int availableQuantity = GetCarriedItemQuantity(assignedItem);
            slotView.itemText.text = Shorten(assignedItem.displayName, 12);
            slotView.quantityText.text = availableQuantity > 0 ? availableQuantity.ToString() : "0";
            slotView.background.color = availableQuantity > 0
                ? new Color(0.20f, 0.36f, 0.24f, 0.96f)
                : new Color(0.36f, 0.18f, 0.18f, 0.96f);
            if (slotView.detailText != null)
                slotView.detailText.text = string.Empty;
            SetSlotIcon(slotView, assignedItem.icon);
        }
    }

    private void RefreshEquipmentSlotsDisplay()
    {
        if (equipment == null)
            return;

        foreach (KeyValuePair<EquipmentSlotType, SlotView> pair in equipmentSlotViews)
        {
            InventorySlot slot = equipment.GetSlot(pair.Key);
            SlotView slotView = pair.Value;
            bool useInlineName = UseInlineEquipmentName(pair.Key);
            Sprite placeholderIcon = GetEquipmentPlaceholderIcon(pair.Key);

            if (slot == null || slot.IsEmpty)
            {
                slotView.keyText.text = placeholderIcon != null ? string.Empty : (useInlineName ? string.Empty : GetEquipmentSlotLabel(pair.Key));
                slotView.itemText.text = placeholderIcon != null ? string.Empty : (useInlineName ? string.Empty : GetEquipmentSlotPlaceholder(pair.Key));
                slotView.quantityText.text = string.Empty;
                slotView.background.color = GetEquipmentSlotEmptyColor(pair.Key);
                slotView.itemText.color = new Color(0.70f, 0.74f, 0.79f, 0.92f);
                if (slotView.detailText != null)
                    slotView.detailText.text = string.Empty;
                if (placeholderIcon != null)
                    ApplyEquipmentSlotPlaceholderPresentation(slotView, pair.Key);
                else
                    ApplyEquipmentSlotIconPresentation(slotView, pair.Key, null);
                SetSlotIconPreserveAspect(slotView, false);
                SetSlotIconTint(slotView, new Color(1f, 1f, 1f, 0.14f));
                SetSlotIcon(slotView, placeholderIcon);
                continue;
            }

            slotView.keyText.text = useInlineName ? string.Empty : GetEquipmentSlotLabel(pair.Key);
            slotView.itemText.text = Shorten(slot.Item.displayName, pair.Key == EquipmentSlotType.PrimaryWeapon ? 20 : 16);
            slotView.quantityText.text = slot.Quantity > 1 ? slot.Quantity.ToString() : string.Empty;
            slotView.background.color = GetEquipmentSlotFilledColor(pair.Key, slot.Item);
            slotView.itemText.color = Color.white;
            if (slotView.detailText != null)
                slotView.detailText.text = string.Empty;
            ApplyEquipmentSlotIconPresentation(slotView, pair.Key, slot.Item);
            SetSlotIconPreserveAspect(slotView, true);
            SetSlotIconTint(slotView, Color.white);
            SetSlotIcon(slotView, GetEquipmentDisplayIcon(slot.Item));
        }
    }

    private void RefreshEquipmentSummary()
    {
        if (equipmentSummaryText == null || equipment == null)
            return;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("Operator Loadout");
        builder.AppendLine("Slots update in real time.");
        builder.AppendLine();

        WeaponItemDefinition currentDefinition = weaponSelection != null ? weaponSelection.GetCurrentWeaponDefinition() : null;
        JUTPS.WeaponSystem.Weapon currentWeapon = weaponSelection != null ? weaponSelection.GetCurrentWeaponComponent() : null;
        if (currentDefinition == null || currentWeapon == null)
        {
            builder.AppendLine("Current: Unarmed");
        }
        else
        {
            builder.AppendLine("Current: " + currentDefinition.displayName);
            builder.AppendLine(FormatFireMode(currentDefinition.fireMode) + " | " + FormatWeaponCategory(currentDefinition.weaponCategory));
            builder.AppendLine("Ammo: " + currentWeapon.BulletsAmounts + " / " + currentWeapon.BulletsPerMagazine);
            builder.AppendLine("DMG " + currentDefinition.baseDamage.ToString("0") + " | RPM " + currentDefinition.roundsPerMinute);
        }

        equipmentSummaryText.text = builder.ToString();

        if (inventoryWeightText != null)
        {
            float totalWeight = GetCurrentCarryWeight();
            inventoryWeightText.text = "Carry Weight  " + totalWeight.ToString("0.0") + " kg";
        }
    }

    private float GetCurrentCarryWeight()
    {
        float totalWeight = 0f;

        if (inventory != null)
            totalWeight += inventory.TotalWeight;

        if (equipment != null)
        {
            for (int i = 0; i < equipment.EquippedSlots.Count; i++)
            {
                InventorySlot slot = equipment.EquippedSlots[i].slot;
                if (slot == null)
                    continue;

                if (!slot.IsEmpty && slot.Item != null)
                    totalWeight += slot.Item.weight * slot.Quantity;
            }
        }

        if (gridInventory != null)
            totalWeight += gridInventory.TotalWeight;

        return totalWeight;
    }

    private void RefreshWeaponHud()
    {
        if (weaponHudNameText == null || weaponHudModeText == null || weaponHudAmmoText == null || weaponHudDetailText == null)
            return;

        WeaponItemDefinition currentDefinition = weaponSelection != null ? weaponSelection.GetCurrentWeaponDefinition() : null;
        JUTPS.WeaponSystem.Weapon currentWeapon = weaponSelection != null ? weaponSelection.GetCurrentWeaponComponent() : null;

        if (currentDefinition == null || currentWeapon == null)
        {
            weaponHudNameText.text = "Unarmed";
            weaponHudModeText.text = "NO ACTIVE WEAPON";
            weaponHudAmmoText.text = "-- / --";
            weaponHudDetailText.text = "Equip a weapon in slot 1 or 2";

            if (weaponHudIconImage != null)
            {
                weaponHudIconImage.sprite = null;
                weaponHudIconImage.enabled = false;
            }

            if (weaponHudIconFrame != null)
                weaponHudIconFrame.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);

            return;
        }

        int currentMagazine = Mathf.Clamp(currentWeapon.BulletsAmounts, 0, currentWeapon.BulletsPerMagazine);
        int reserveAmmo = 0;
        string ammoLabel = "No Ammo";

        if (currentDefinition.usesAmmo && currentDefinition.compatibleAmmo != null)
        {
            ammoLabel = currentDefinition.compatibleAmmo.displayName;
            reserveAmmo = weaponSelection != null
                ? weaponSelection.GetReserveAmmoFor(currentDefinition)
                : GetCarriedItemQuantity(currentDefinition.compatibleAmmo);
        }

        weaponHudNameText.text = currentDefinition.displayName;
        weaponHudModeText.text = FormatFireMode(currentDefinition.fireMode) + "  |  " + FormatWeaponCategory(currentDefinition.weaponCategory);
        weaponHudAmmoText.text = currentMagazine + " / " + reserveAmmo;
        weaponHudDetailText.text = "MAG " + currentWeapon.BulletsPerMagazine + "  |  " + ammoLabel + "  |  DMG " + currentDefinition.baseDamage.ToString("0");

        Sprite icon = currentDefinition.icon != null ? currentDefinition.icon : currentWeapon.ItemIcon;
        if (weaponHudIconImage != null)
        {
            weaponHudIconImage.sprite = icon;
            weaponHudIconImage.enabled = icon != null;
        }

        if (weaponHudIconFrame != null)
            weaponHudIconFrame.color = GetWeaponHudAccentColor(currentDefinition.weaponCategory);
    }

    private void RefreshUseProgressUi()
    {
        if (useProgressPanel == null)
            return;

        bool isUsing = itemUse != null && itemUse.IsUsing;
        if (useProgressPanel.gameObject.activeSelf != isUsing)
            useProgressPanel.gameObject.SetActive(isUsing);

        if (!isUsing)
            return;

        useProgressPanel.SetAsLastSibling();

        ItemDefinition activeItem = itemUse.ActiveItem;
        if (useProgressRingImage != null)
            useProgressRingImage.fillAmount = itemUse.UseRemainingNormalized;

        if (useProgressCountdownText != null)
            useProgressCountdownText.text = Mathf.CeilToInt(itemUse.UseRemainingSeconds).ToString();

        if (useProgressItemIconImage != null)
        {
            Sprite icon = itemUse.ActiveUseIcon;
            useProgressItemIconImage.sprite = icon;
            useProgressItemIconImage.enabled = icon != null;
            useProgressItemIconImage.rectTransform.sizeDelta = GetUseProgressIconSize(activeItem);
        }

        if (useProgressItemNameText != null)
            useProgressItemNameText.text = itemUse.ActiveUseDisplayName;
    }

    private Vector2 GetUseProgressIconSize(ItemDefinition item)
    {
        if (item != null && item.itemId == "consumable_food")
            return new Vector2(76f, 76f);

        return new Vector2(104f, 104f);
    }

    private int GetCarriedItemQuantity(ItemDefinition item)
    {
        if (item == null)
            return 0;

        int totalQuantity = 0;
        if (gridInventory != null)
            totalQuantity += gridInventory.GetQuantity(item);

        if (inventory != null)
            totalQuantity += inventory.GetQuantity(item);

        return totalQuantity;
    }

    private void RefreshStatusHud()
    {
        if (healthBarFillImage == null || healthValueText == null)
            return;

        float currentHealth = 0f;
        float maxHealth = 0f;

        if (juHealth != null)
        {
            currentHealth = juHealth.Health;
            maxHealth = juHealth.MaxHealth;
        }
        else if (playerStats != null)
        {
            currentHealth = playerStats.currentHealth;
            maxHealth = playerStats.maxHealth;
        }

        float normalizedHealth = maxHealth > 0.01f
            ? Mathf.Clamp01(currentHealth / maxHealth)
            : 0f;

        if (healthBarFillRect != null)
        {
            Vector2 size = healthBarFillRect.sizeDelta;
            size.x = HealthBarWidth * normalizedHealth;
            healthBarFillRect.sizeDelta = size;
        }

        healthBarFillImage.color = Color.Lerp(
            new Color(0.72f, 0.16f, 0.16f, 0.98f),
            new Color(0.23f, 0.78f, 0.33f, 0.98f),
            normalizedHealth);

        healthValueText.text = Mathf.CeilToInt(currentHealth) + " / " + Mathf.CeilToInt(maxHealth);
    }

    private void RefreshEquipmentNeedBars()
    {
        float currentHealth = 100f;
        float maxHealth = 100f;
        if (juHealth != null)
        {
            currentHealth = juHealth.Health;
            maxHealth = juHealth.MaxHealth;
        }
        else if (playerStats != null)
        {
            currentHealth = playerStats.currentHealth;
            maxHealth = playerStats.maxHealth;
        }

        float hydration = playerStats != null ? playerStats.currentHydration : 100f;
        float hydrationMax = playerStats != null ? playerStats.maxHydration : 100f;
        float hunger = playerStats != null ? playerStats.currentHunger : 100f;
        float hungerMax = playerStats != null ? playerStats.maxHunger : 100f;
        float currentWeight = GetCurrentCarryWeight();
        float weightMax = 50f;

        float healthNormalized = maxHealth > 0.01f
            ? Mathf.Clamp01(currentHealth / maxHealth)
            : 0f;
        float hydrationNormalized = hydrationMax > 0.01f
            ? Mathf.Clamp01(hydration / hydrationMax)
            : 0f;
        float hungerNormalized = hungerMax > 0.01f
            ? Mathf.Clamp01(hunger / hungerMax)
            : 0f;
        float weightNormalized = weightMax > 0.01f
            ? Mathf.Clamp01(currentWeight / weightMax)
            : 0f;

        if (equipmentHealthBarFillRect != null)
        {
            Vector2 size = equipmentHealthBarFillRect.sizeDelta;
            size.x = NeedBarWidth * healthNormalized;
            equipmentHealthBarFillRect.sizeDelta = size;
        }

        if (hydrationBarFillRect != null)
        {
            Vector2 size = hydrationBarFillRect.sizeDelta;
            size.x = NeedBarWidth * hydrationNormalized;
            hydrationBarFillRect.sizeDelta = size;
        }

        if (hungerBarFillRect != null)
        {
            Vector2 size = hungerBarFillRect.sizeDelta;
            size.x = NeedBarWidth * hungerNormalized;
            hungerBarFillRect.sizeDelta = size;
        }

        if (weightBarFillRect != null)
        {
            Vector2 size = weightBarFillRect.sizeDelta;
            size.x = NeedBarWidth * weightNormalized;
            weightBarFillRect.sizeDelta = size;
        }

        if (equipmentHealthValueText != null)
            equipmentHealthValueText.text = Mathf.CeilToInt(currentHealth) + "/" + Mathf.CeilToInt(maxHealth);

        if (hydrationValueText != null)
            hydrationValueText.text = hydration.ToString("0.0") + "/" + hydrationMax.ToString("0");

        if (hungerValueText != null)
            hungerValueText.text = hunger.ToString("0.0") + "/" + hungerMax.ToString("0");

        if (weightValueText != null)
            weightValueText.text = currentWeight.ToString("0.0") + "/" + weightMax.ToString("0");
    }

    private void RefreshDynamicInfo()
    {
        bool inventoryOpen = inventoryPanel != null && inventoryPanel.gameObject.activeSelf;
        bool mapOpen = fullMapPanel != null && fullMapPanel.gameObject.activeSelf;
        bool useInProgress = itemUse != null && itemUse.IsUsing;
        bool isBlockingOverlayOpen = inventoryOpen || mapOpen;
        bool shouldBlockMovement = isBlockingOverlayOpen || useInProgress;

        if (juCharacter != null)
        {
            if (shouldBlockMovement && !overlayWasBlockingMovement)
                juCharacter.DisableLocomotion();
            else if (!shouldBlockMovement && overlayWasBlockingMovement)
                juCharacter.enableMove();
        }

        if (juInteractionSystem != null)
            juInteractionSystem.BlockInteractions = shouldBlockMovement;

        if (playerRigidbody != null && shouldBlockMovement)
        {
            Vector3 velocity = playerRigidbody.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            playerRigidbody.linearVelocity = velocity;
        }

        UpdateOverlayCursorState(isBlockingOverlayOpen);
        UpdateGameplayHudVisibility(inventoryOpen, mapOpen);
        UpdateInventoryCursorVisual(inventoryOpen);

        float playerYaw = gameplayInput != null ? gameplayInput.transform.eulerAngles.y : 0f;

        if (minimapSystem != null && minimapSystem.HasValidFeed)
        {
            if (minimapFeedImage != null)
                minimapFeedImage.texture = minimapSystem.MapTexture;

            if (fullMapFeedImage != null)
                fullMapFeedImage.texture = minimapSystem.MapTexture;

            playerYaw = minimapSystem.TargetYaw;
        }

        if (minimapInfoText != null)
        {
            minimapInfoText.text = minimapSystem != null && minimapSystem.HasValidFeed
                ? string.Empty
                : "Loading map...";
        }

        if (fullMapInfoText != null)
        {
            fullMapInfoText.text = minimapSystem != null && minimapSystem.HasValidFeed
                ? string.Empty
                : "Loading map...";
        }

        if (minimapArrowText != null)
            minimapArrowText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -playerYaw);

        if (fullMapArrowText != null)
            fullMapArrowText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -playerYaw);

        RefreshStatusHud();
        RefreshEquipmentNeedBars();
        RefreshWeaponHud();
        RefreshUseProgressUi();
        if (inventoryCharacterPreview != null)
            inventoryCharacterPreview.SetPreviewActive(inventoryPanel != null && inventoryPanel.gameObject.activeSelf);
        overlayWasBlockingMovement = shouldBlockMovement;
    }

    private void InitializeOperatorPreview()
    {
        if (gameplayInput == null || operatorPreviewImage == null)
            return;

        if (inventoryCharacterPreview == null)
            inventoryCharacterPreview = GetComponent<InventoryCharacterPreview>();

        if (inventoryCharacterPreview == null)
            inventoryCharacterPreview = gameObject.AddComponent<InventoryCharacterPreview>();

        inventoryCharacterPreview.Configure(gameplayInput.transform, operatorPreviewImage, operatorPreviewFallbackText);
        inventoryCharacterPreview.SetPreviewActive(inventoryPanel != null && inventoryPanel.gameObject.activeSelf);
    }

    private bool IsBlockingOverlayOpen()
    {
        bool inventoryOpen = inventoryPanel != null && inventoryPanel.gameObject.activeSelf;
        bool mapOpen = fullMapPanel != null && fullMapPanel.gameObject.activeSelf;
        return inventoryOpen || mapOpen;
    }

    public bool IsGameplayOverlayOpen => IsBlockingOverlayOpen();

    private void UpdateGameplayHudVisibility(bool inventoryOpen, bool mapOpen)
    {
        bool showGameplayHud = !inventoryOpen && !mapOpen;

        if (quickbarPanel != null && quickbarPanel.gameObject.activeSelf != showGameplayHud)
            quickbarPanel.gameObject.SetActive(showGameplayHud);

        if (statusHudPanel != null && statusHudPanel.gameObject.activeSelf != showGameplayHud)
            statusHudPanel.gameObject.SetActive(showGameplayHud);

        if (weaponHudPanel != null && weaponHudPanel.gameObject.activeSelf != showGameplayHud)
            weaponHudPanel.gameObject.SetActive(showGameplayHud);

        bool showMinimapPanel = showGameplayHud && showMinimap;
        if (minimapPanel != null && minimapPanel.gameObject.activeSelf != showMinimapPanel)
            minimapPanel.gameObject.SetActive(showMinimapPanel);
    }

    private void UpdateInventoryCursorVisual(bool inventoryOpen)
    {
        if (inventoryOpen)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            crosshairCursorSuppressed = true;
            return;
        }

        if (!crosshairCursorSuppressed)
            return;

        if (CrosshairCursor.Instance != null)
            CrosshairCursor.Instance.ApplyCursor();

        crosshairCursorSuppressed = false;
    }

    private void ApplyInitialVisibility()
    {
        if (inventoryPanel != null)
            inventoryPanel.gameObject.SetActive(false);

        if (fullMapPanel != null)
            fullMapPanel.gameObject.SetActive(false);

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        if (containerPopupPanel != null)
            containerPopupPanel.gameObject.SetActive(false);

        if (dropDialogPanel != null)
            dropDialogPanel.gameObject.SetActive(false);

        if (splitDialogPanel != null)
            splitDialogPanel.gameObject.SetActive(false);

        if (useProgressPanel != null)
            useProgressPanel.gameObject.SetActive(false);

        itemInspectPanel?.Hide();

        if (minimapPanel != null)
            minimapPanel.gameObject.SetActive(showMinimap);
    }

    private void BuildUi()
    {
        if (uiBuilt)
            return;

        rootCanvas = GetComponent<Canvas>();
        if (rootCanvas == null)
            return;

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (hudLabelFont == null)
            hudLabelFont = Resources.Load<Font>("Russo_One");
        runtimeRoot = CreateRect("GameplayRuntimeUI", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        BuildInventoryPanel();
        BuildQuickbarPanel();
        BuildStatusHudPanel();
        BuildWeaponHudPanel();
        BuildMinimapPanel();
        BuildFullMapPanel();
        BuildContextMenu();
        BuildContainerPopup();
        BuildDropDialog();
        BuildSplitDialog();
        BuildUseProgressPanel();
        itemInspectPanel = ItemInspectPanel.Create(runtimeRoot, rootCanvas, uiFont);

        uiBuilt = true;
        InitializeOperatorPreview();
    }

    private void BuildInventoryPanel()
    {
        inventoryPanel = CreatePanel(
            "InventoryPanel",
            runtimeRoot,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Color(0.05f, 0.07f, 0.10f, 0.97f));
        inventoryPanel.offsetMin = new Vector2(18f, 18f);
        inventoryPanel.offsetMax = new Vector2(-18f, -18f);

        CreateText("Title", inventoryPanel, "Inventory", 30, TextAnchor.UpperLeft, new Vector2(28f, -18f), new Vector2(260f, 40f), FontStyle.Bold);
        CreateText("Hint", inventoryPanel, "B close  |  Right click items for actions", 16, TextAnchor.UpperLeft, new Vector2(28f, -54f), new Vector2(420f, 26f), FontStyle.Normal);

        RectTransform leftWorkspace = CreateRect(
            "LeftWorkspace",
            inventoryPanel,
            new Vector2(0f, 0f),
            new Vector2(0.635f, 1f),
            new Vector2(0f, 0f),
            Vector2.zero);
        leftWorkspace.offsetMin = new Vector2(24f, 24f);
        leftWorkspace.offsetMax = new Vector2(-6f, -88f);

        RectTransform rightWorkspace = CreatePanel(
            "RightWorkspace",
            inventoryPanel,
            new Vector2(0.637f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            Vector2.zero,
            new Color(0.08f, 0.10f, 0.13f, 0.52f));
        rightWorkspacePanel = rightWorkspace;
        rightWorkspace.offsetMin = new Vector2(6f, 24f);
        rightWorkspace.offsetMax = new Vector2(-24f, -88f);
        rightWorkspaceTitleText = CreateText("RightWorkspaceTitle", rightWorkspace, "Reserved", 22, TextAnchor.UpperLeft, new Vector2(20f, -16f), new Vector2(280f, 30f), FontStyle.Bold);
        rightWorkspaceHintText = CreateText(
            "RightWorkspaceHint",
            rightWorkspace,
            "Future loot container / shelter stash area.",
            18,
            TextAnchor.UpperLeft,
            new Vector2(20f, -52f),
            new Vector2(340f, 54f),
            FontStyle.Normal);
        rightWorkspaceHintText.color = new Color(0.72f, 0.77f, 0.84f, 0.74f);
        rightWorkspaceCashText = CreateText(
            "RightWorkspaceCash",
            rightWorkspace,
            "$ 0",
            22,
            TextAnchor.MiddleRight,
            new Vector2(-20f, -18f),
            new Vector2(160f, 34f),
            FontStyle.Bold);
        RectTransform cashRect = rightWorkspaceCashText.rectTransform;
        cashRect.anchorMin = new Vector2(1f, 1f);
        cashRect.anchorMax = new Vector2(1f, 1f);
        cashRect.pivot = new Vector2(1f, 1f);
        rightWorkspaceCashText.color = new Color(0.96f, 0.96f, 0.92f, 1f);
        rightWorkspaceCashText.horizontalOverflow = HorizontalWrapMode.Overflow;
        rightWorkspaceCashText.gameObject.SetActive(false);
        BuildExternalContainerView(rightWorkspace);
        BuildCorpseLootView(rightWorkspace);

        RectTransform equipmentPanel = CreatePanel(
            "EquipmentPanel",
            leftWorkspace,
            new Vector2(0f, 0f),
            new Vector2(0.54f, 1f),
            new Vector2(0f, 0f),
            Vector2.zero,
            new Color(0.08f, 0.10f, 0.13f, 0.96f));
        equipmentPanel.offsetMin = Vector2.zero;
        equipmentPanel.offsetMax = new Vector2(-4f, 0f);
        CreateText("EquipmentTitle", equipmentPanel, "Equipment", 22, TextAnchor.UpperLeft, new Vector2(18f, -14f), new Vector2(180f, 30f), FontStyle.Bold);

        RectTransform operatorStage = CreateRect(
            "OperatorStage",
            equipmentPanel,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero);
        operatorStage.offsetMin = new Vector2(204f, 236f);
        operatorStage.offsetMax = new Vector2(-16f, 72f);

        RectTransform operatorPreviewSurface = CreateRect(
            "OperatorPreviewSurface",
            operatorStage,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero);
        operatorPreviewSurface.offsetMin = Vector2.zero;
        operatorPreviewSurface.offsetMax = Vector2.zero;
        operatorPreviewImage = operatorPreviewSurface.gameObject.AddComponent<RawImage>();
        operatorPreviewImage.color = new Color(1f, 1f, 1f, 0f);
        AspectRatioFitter operatorPreviewAspect = operatorPreviewSurface.gameObject.AddComponent<AspectRatioFitter>();
        operatorPreviewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        operatorPreviewAspect.aspectRatio = 0.9f;
        operatorPreviewFallbackText = CreateText(
            "OperatorPlaceholder",
            operatorStage,
            "PLAYER PREVIEW",
            24,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            new Vector2(220f, 36f),
            FontStyle.Bold);

        RectTransform backpackPanel = CreatePanel(
            "BackpackPanel",
            leftWorkspace,
            new Vector2(0.545f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            Vector2.zero,
            new Color(0.08f, 0.10f, 0.13f, 0.96f));
        backpackPanel.offsetMin = new Vector2(4f, 0f);
        backpackPanel.offsetMax = Vector2.zero;
        CreateText("BackpackTitle", backpackPanel, "Carry", 22, TextAnchor.UpperLeft, new Vector2(18f, -14f), new Vector2(180f, 30f), FontStyle.Bold);

        gridContainerViews.Clear();
        backpackSlotViews.Clear();
        CreateGridContainerSection(backpackPanel, GridContainerKind.Rig, "Rig");
        CreateGridContainerSection(backpackPanel, GridContainerKind.Backpack, "Backpack");
        CreateGridContainerSection(backpackPanel, GridContainerKind.Pocket, "Pocket");

        equipmentSlotViews.Clear();
        CreateEquipmentSlot(equipmentPanel, EquipmentSlotType.HeadArmor, new Vector2(18f, -82f), new Vector2(126f, 126f));
        CreateEquipmentSlot(equipmentPanel, EquipmentSlotType.ChestArmor, new Vector2(18f, -226f), new Vector2(126f, 126f));
        CreateEquipmentSlot(equipmentPanel, EquipmentSlotType.Backpack, new Vector2(160f, -82f), new Vector2(126f, 126f));
        CreateEquipmentSlot(equipmentPanel, EquipmentSlotType.SecondaryWeapon, new Vector2(18f, -388f), new Vector2(198f, 108f));
        CreateEquipmentSlot(equipmentPanel, EquipmentSlotType.PrimaryWeapon, new Vector2(18f, -514f), new Vector2(386f, 126f));
        BuildEquipmentNeedBarsV2(equipmentPanel);
    }

    private void BuildExternalContainerView(RectTransform parent)
    {
        RectTransform sectionRect = CreateRect(
            "ExternalContainerSection",
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            Vector2.zero);
        sectionRect.offsetMin = new Vector2(20f, 20f);
        sectionRect.offsetMax = new Vector2(-20f, -112f);

        externalContainerView = new GridContainerView
        {
            kind = GridContainerKind.External,
            rect = sectionRect,
            titleText = rightWorkspaceTitleText
        };

        externalContainerView.gridFrameRect = CreateRect(
            "GridFrame",
            sectionRect,
            new Vector2(0.5f, 0.42f),
            new Vector2(0.5f, 0.42f),
            new Vector2(0.5f, 0.5f),
            new Vector2(232f, 232f));
        externalContainerView.gridFrameRect.anchoredPosition = new Vector2(-220f, 0f);
        externalContainerView.gridFrameImage = externalContainerView.gridFrameRect.gameObject.AddComponent<Image>();
        externalContainerView.gridFrameImage.type = Image.Type.Simple;
        externalContainerView.gridFrameImage.color = GridFrameDefaultColor;

        externalContainerView.gridRect = CreateRect(
            "Grid",
            externalContainerView.gridFrameRect,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(220f, 220f));
        externalContainerView.gridRect.anchoredPosition = new Vector2(InventoryGridOuterBorderThickness, -InventoryGridOuterBorderThickness);

        externalContainerView.gridLayout = externalContainerView.gridRect.gameObject.AddComponent<GridLayoutGroup>();
        externalContainerView.gridLayout.cellSize = new Vector2(InventoryGridCellSize, InventoryGridCellSize);
        externalContainerView.gridLayout.spacing = Vector2.zero;
        externalContainerView.gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        externalContainerView.gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        externalContainerView.gridLayout.childAlignment = TextAnchor.UpperLeft;
        externalContainerView.gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        externalContainerView.gridLayout.constraintCount = 4;
        externalContainerView.gridLayout.padding = new RectOffset(0, 0, 0, 0);

        externalContainerView.gridLinesRoot = CreateRect(
            "GridLines",
            externalContainerView.gridRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero);
        StretchToParent(externalContainerView.gridLinesRoot, Vector2.zero, Vector2.zero);
        LayoutElement linesLayout = externalContainerView.gridLinesRoot.gameObject.AddComponent<LayoutElement>();
        linesLayout.ignoreLayout = true;

        externalContainerView.placementsRoot = CreateRect(
            "Placements",
            externalContainerView.gridRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero);
        StretchToParent(externalContainerView.placementsRoot, Vector2.zero, Vector2.zero);
        LayoutElement placementsLayout = externalContainerView.placementsRoot.gameObject.AddComponent<LayoutElement>();
        placementsLayout.ignoreLayout = true;
        externalContainerView.placementsRoot.SetAsLastSibling();

        externalContainerView.rect.gameObject.SetActive(false);
    }

    private void BuildCorpseLootView(RectTransform parent)
    {
        corpseLootPanel = CreateRect(
            "CorpseLootSection",
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            Vector2.zero);
        corpseLootPanel.offsetMin = new Vector2(20f, 20f);
        corpseLootPanel.offsetMax = new Vector2(-20f, -92f);

        corpseEquipmentSlotViews.Clear();
        CreateCorpseEquipmentSlot(corpseLootPanel, EquipmentSlotType.HeadArmor, new Vector2(0f, -76f), new Vector2(126f, 126f));
        CreateCorpseEquipmentSlot(corpseLootPanel, EquipmentSlotType.ChestArmor, new Vector2(0f, -220f), new Vector2(126f, 126f));
        CreateCorpseEquipmentSlot(corpseLootPanel, EquipmentSlotType.Backpack, new Vector2(142f, -76f), new Vector2(126f, 126f));
        CreateCorpseEquipmentSlot(corpseLootPanel, EquipmentSlotType.SecondaryWeapon, new Vector2(0f, -374f), new Vector2(190f, 106f));
        CreateCorpseEquipmentSlot(corpseLootPanel, EquipmentSlotType.PrimaryWeapon, new Vector2(0f, -498f), new Vector2(344f, 116f));

        CreateText(
            "CorpsePocketTitle",
            corpseLootPanel,
            "POCKET",
            15,
            TextAnchor.UpperLeft,
            new Vector2(0f, -638f),
            new Vector2(120f, 22f),
            FontStyle.Bold);

        corpsePocketView = new GridContainerView
        {
            kind = GridContainerKind.CorpsePocket
        };

        corpsePocketView.gridFrameRect = CreateRect(
            "CorpsePocketGridFrame",
            corpseLootPanel,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2((4f * InventoryGridCellSize) + (InventoryGridOuterBorderThickness * 2f), InventoryGridCellSize + (InventoryGridOuterBorderThickness * 2f)));
        corpsePocketView.gridFrameRect.anchoredPosition = new Vector2(0f, -672f);
        corpsePocketView.gridFrameImage = corpsePocketView.gridFrameRect.gameObject.AddComponent<Image>();
        corpsePocketView.gridFrameImage.type = Image.Type.Simple;
        corpsePocketView.gridFrameImage.color = GridFrameDefaultColor;
        corpsePocketView.gridFrameImage.sprite = GetOrCreateGridFrameSprite(1, 4);

        corpsePocketView.gridRect = CreateRect(
            "Grid",
            corpsePocketView.gridFrameRect,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(4f * InventoryGridCellSize, InventoryGridCellSize));
        corpsePocketView.gridRect.anchoredPosition = new Vector2(InventoryGridOuterBorderThickness, -InventoryGridOuterBorderThickness);

        corpsePocketView.gridLayout = corpsePocketView.gridRect.gameObject.AddComponent<GridLayoutGroup>();
        corpsePocketView.gridLayout.cellSize = new Vector2(InventoryGridCellSize, InventoryGridCellSize);
        corpsePocketView.gridLayout.spacing = Vector2.zero;
        corpsePocketView.gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        corpsePocketView.gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        corpsePocketView.gridLayout.childAlignment = TextAnchor.UpperLeft;
        corpsePocketView.gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        corpsePocketView.gridLayout.constraintCount = 4;
        corpsePocketView.gridLayout.padding = new RectOffset(0, 0, 0, 0);

        corpsePocketView.gridLinesRoot = CreateRect(
            "GridLines",
            corpsePocketView.gridRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero);
        StretchToParent(corpsePocketView.gridLinesRoot, Vector2.zero, Vector2.zero);
        LayoutElement linesLayout = corpsePocketView.gridLinesRoot.gameObject.AddComponent<LayoutElement>();
        linesLayout.ignoreLayout = true;

        corpsePocketView.placementsRoot = CreateRect(
            "Placements",
            corpsePocketView.gridRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero);
        StretchToParent(corpsePocketView.placementsRoot, Vector2.zero, Vector2.zero);
        LayoutElement placementsLayout = corpsePocketView.placementsRoot.gameObject.AddComponent<LayoutElement>();
        placementsLayout.ignoreLayout = true;
        corpsePocketView.placementsRoot.SetAsLastSibling();

        corpseLootPanel.gameObject.SetActive(false);
    }

    private void BuildQuickbarPanel()
    {
        quickbarPanel = CreateRect("QuickbarPanel", runtimeRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(680f, 118f));
        quickbarPanel.anchoredPosition = new Vector2(0f, 22f);

        HorizontalLayoutGroup layout = quickbarPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        quickbarSlotViews.Clear();
        for (int i = 0; i < 6; i++)
        {
            RectTransform slotRect = CreateSlotRect("QuickbarSlot_" + i, quickbarPanel, new Vector2(102f, 102f));
            SlotView slotView = CreateSlotView(
                slotRect,
                gameplayInput != null ? gameplayInput.GetQuickbarLabel(i) : (i + 3).ToString());
            InventorySlotWidget widget = slotRect.gameObject.AddComponent<InventorySlotWidget>();
            widget.Configure(this, i, InventorySlotWidgetMode.Quickbar);
            quickbarSlotViews.Add(slotView);
        }
    }

    private GridContainerView CreateGridContainerSection(RectTransform parent, GridContainerKind kind, string title)
    {
        RectTransform sectionRect = CreateRect(
            kind + "GridSection",
            parent,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(-36f, 120f));
        sectionRect.anchoredPosition = new Vector2(18f, -58f);

        GridContainerView view = new GridContainerView
        {
            kind = kind,
            rect = sectionRect,
            titleText = CreateText("Title", sectionRect, title.ToUpperInvariant(), 18, TextAnchor.UpperLeft, Vector2.zero, new Vector2(180f, 24f), FontStyle.Bold)
        };

        view.previewRect = CreateRect(
            "ContainerPreview",
            sectionRect,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(96f, 96f));
        view.previewRect.anchoredPosition = new Vector2(0f, -32f);
        view.previewBackground = view.previewRect.gameObject.AddComponent<Image>();
        view.previewBackground.color = new Color(0.13f, 0.15f, 0.19f, 0.94f);

        RectTransform previewIconRect = CreateRect(
            "PreviewIcon",
            view.previewRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(78f, 78f));
        view.previewIcon = previewIconRect.gameObject.AddComponent<Image>();
        view.previewIcon.color = Color.white;
        view.previewIcon.preserveAspect = true;
        view.previewIcon.enabled = false;

        view.previewText = CreateText(
            "PreviewText",
            view.previewRect,
            title.ToUpperInvariant(),
            13,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            new Vector2(78f, 40f),
            FontStyle.Bold);
        view.previewText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        view.previewText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        view.previewText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        view.previewText.color = new Color(0.78f, 0.82f, 0.88f, 0.9f);

        view.gridFrameRect = CreateRect(
            "GridFrame",
            sectionRect,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(228f, 228f));
        view.gridFrameRect.anchoredPosition = new Vector2(112f, -32f);
        view.gridFrameImage = view.gridFrameRect.gameObject.AddComponent<Image>();
        view.gridFrameImage.color = GridFrameDefaultColor;

        view.gridRect = CreateRect(
            "Grid",
            view.gridFrameRect,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(224f, 224f));
        view.gridRect.anchoredPosition = new Vector2(InventoryGridOuterBorderThickness, -InventoryGridOuterBorderThickness);
        Image gridBackground = view.gridRect.gameObject.AddComponent<Image>();
        gridBackground.color = new Color(0f, 0f, 0f, 0f);
        view.gridRect.gameObject.AddComponent<RectMask2D>();

        view.gridLayout = view.gridRect.gameObject.AddComponent<GridLayoutGroup>();
        view.gridLayout.cellSize = new Vector2(InventoryGridCellSize, InventoryGridCellSize);
        view.gridLayout.spacing = Vector2.zero;
        view.gridLayout.padding = new RectOffset(0, 0, 0, 0);
        view.gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        view.gridLayout.constraintCount = 1;
        view.gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        view.gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        view.gridLayout.childAlignment = TextAnchor.UpperLeft;

        view.gridLinesRoot = CreateRect(
            "GridLines",
            view.gridRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0f, 1f),
            Vector2.zero);
        StretchToParent(view.gridLinesRoot, Vector2.zero, Vector2.zero);
        LayoutElement lineLayout = view.gridLinesRoot.gameObject.AddComponent<LayoutElement>();
        lineLayout.ignoreLayout = true;

        view.placementsRoot = CreateRect(
            "Placements",
            view.gridRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0f, 1f),
            Vector2.zero);
        StretchToParent(view.placementsRoot, Vector2.zero, Vector2.zero);
        LayoutElement placementsLayout = view.placementsRoot.gameObject.AddComponent<LayoutElement>();
        placementsLayout.ignoreLayout = true;
        view.placementsRoot.SetAsLastSibling();

        gridContainerViews[kind] = view;
        return view;
    }

    private void BuildGridDisplayStates()
    {
        if (gridInventory == null)
            return;

        gridInventory.EnsureContainers();
        mirroredGridPlacements.Clear();

        GridContainerState rigDisplay = PrepareDisplayContainerState(
            GridContainerKind.Rig,
            gridInventory.HasRigContainer,
            gridInventory.RigContainer);
        GridContainerState backpackDisplay = PrepareDisplayContainerState(
            GridContainerKind.Backpack,
            gridInventory.HasBackpackContainer,
            gridInventory.BackpackContainer);
        GridContainerState pocketDisplay = PrepareDisplayContainerState(
            GridContainerKind.Pocket,
            true,
            gridInventory.PocketContainer);

        if (inventory == null)
        {
            PopulateDisplayPlacementsFromState(GridContainerKind.Rig, rigDisplay);
            PopulateDisplayPlacementsFromState(GridContainerKind.Backpack, backpackDisplay);
            PopulateDisplayPlacementsFromState(GridContainerKind.Pocket, pocketDisplay);
            return;
        }

        PruneStaleMirroredAnchors();

        bool rigHasActualContents = gridInventory.HasRigContainer && gridInventory.RigContainer != null && gridInventory.RigContainer.HasAnyPlacement();
        bool backpackHasActualContents = gridInventory.HasBackpackContainer && gridInventory.BackpackContainer != null && gridInventory.BackpackContainer.HasAnyPlacement();
        bool pocketHasActualContents = gridInventory.PocketContainer != null && gridInventory.PocketContainer.HasAnyPlacement();

        PopulateDisplayPlacementsFromState(GridContainerKind.Rig, rigDisplay);
        PopulateDisplayPlacementsFromState(GridContainerKind.Backpack, backpackDisplay);
        PopulateDisplayPlacementsFromState(GridContainerKind.Pocket, pocketDisplay);

        GridContainerState rigMirrorTarget = rigHasActualContents ? null : rigDisplay;
        GridContainerState backpackMirrorTarget = backpackHasActualContents ? null : backpackDisplay;
        GridContainerState pocketMirrorTarget = pocketHasActualContents ? null : pocketDisplay;

        if (singleGridTestMode)
        {
            MirrorMatchingSlots(IsSingleGridTestTarget, rigMirrorTarget, backpackMirrorTarget, pocketMirrorTarget);
            return;
        }

        MirrorMatchingSlots(_ => true, rigMirrorTarget, backpackMirrorTarget, pocketMirrorTarget);
    }

    private bool IsSingleGridTestTarget(InventorySlot sourceSlot)
    {
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item == null)
            return false;

        string itemId = sourceSlot.Item.itemId;
        if (singleGridTestItemId == SmallCarryComboTestId)
        {
            return
                itemId == "debug_weapon_p226" ||
                itemId == "weapon_glock" ||
                itemId == "debug_ammo_556" ||
                itemId == "ammo_556x45mm" ||
                itemId == "debug_medkit";
        }

        return !string.IsNullOrWhiteSpace(singleGridTestItemId) && itemId == singleGridTestItemId;
    }

    private void MirrorMatchingSlots(
        System.Predicate<InventorySlot> predicate,
        GridContainerState rigTarget,
        GridContainerState backpackTarget,
        GridContainerState pocketTarget)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            bool anchoredPass = pass == 0;
            for (int slotIndex = 0; slotIndex < inventory.SlotCount; slotIndex++)
            {
                InventorySlot sourceSlot = inventory.GetSlot(slotIndex);
                if (sourceSlot == null || sourceSlot.IsEmpty || !predicate(sourceSlot))
                    continue;

                bool hasMatchingAnchor = HasMatchingAnchor(sourceSlot, slotIndex);
                if (anchoredPass != hasMatchingAnchor)
                    continue;

                MirrorSingleSlot(sourceSlot, slotIndex, rigTarget, backpackTarget, pocketTarget);
            }
        }
    }

    private bool HasMatchingAnchor(InventorySlot sourceSlot, int sourceSlotIndex)
    {
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item == null)
            return false;

        return mirroredGridAnchors.TryGetValue(sourceSlotIndex, out MirroredGridAnchor anchor) &&
               anchor.itemId == sourceSlot.Item.itemId;
    }

    private bool MirrorSingleSlot(
        InventorySlot sourceSlot,
        int sourceSlotIndex,
        GridContainerState rigTarget,
        GridContainerState backpackTarget,
        GridContainerState pocketTarget)
    {
        if (TryMirrorInventorySlotToContainer(rigTarget, sourceSlot, sourceSlotIndex))
            return true;

        if (TryMirrorInventorySlotToContainer(backpackTarget, sourceSlot, sourceSlotIndex))
            return true;

        return TryMirrorInventorySlotToContainer(pocketTarget, sourceSlot, sourceSlotIndex);
    }

    private bool TryMirrorInventorySlotToContainer(GridContainerState container, InventorySlot sourceSlot, int sourceSlotIndex)
    {
        if (container == null || sourceSlot == null || sourceSlot.IsEmpty)
            return false;

        GridItemPlacement placement = null;
        string itemId = sourceSlot.Item != null ? sourceSlot.Item.itemId : string.Empty;

        if (mirroredGridAnchors.TryGetValue(sourceSlotIndex, out MirroredGridAnchor anchor) &&
            anchor.itemId == itemId &&
            anchor.containerKind == container.ContainerKind)
        {
            container.TryPlaceItemAt(
                sourceSlot.Item,
                sourceSlot.Quantity,
                anchor.row,
                anchor.column,
                out placement,
                anchor.rotated);
        }

        if (placement == null && !container.TryPlaceNewItem(sourceSlot.Item, sourceSlot.Quantity, out placement))
            return false;

        mirroredGridPlacements.Add(new GridMirroredPlacement(container.ContainerKind, placement, sourceSlotIndex));
        mirroredGridAnchors[sourceSlotIndex] = new MirroredGridAnchor(
            itemId,
            container.ContainerKind,
            placement.Row,
            placement.Column,
            placement.Rotated);
        return true;
    }

    private void RefreshGridContainerDisplays()
    {
        if (gridInventory == null || gridContainerViews.Count == 0)
            return;

        float currentY = -58f;
        currentY = LayoutAndRefreshGridContainer(
            GridContainerKind.Rig,
            currentY,
            gridInventory.HasRigContainer,
            gridInventory.EquippedRig,
            GetDisplayContainerState(GridContainerKind.Rig, gridInventory.RigContainer));
        currentY = LayoutAndRefreshGridContainer(
            GridContainerKind.Backpack,
            currentY,
            gridInventory.HasBackpackContainer,
            gridInventory.EquippedBackpack,
            GetDisplayContainerState(GridContainerKind.Backpack, gridInventory.BackpackContainer));
        LayoutAndRefreshGridContainer(
            GridContainerKind.Pocket,
            currentY,
            true,
            null,
            GetDisplayContainerState(GridContainerKind.Pocket, gridInventory.PocketContainer));
    }

    private float LayoutAndRefreshGridContainer(
        GridContainerKind kind,
        float currentY,
        bool visible,
        ItemDefinition containerDefinition,
        GridContainerState containerState)
    {
        if (!gridContainerViews.TryGetValue(kind, out GridContainerView view) || view == null)
            return currentY;

        view.rect.gameObject.SetActive(visible);
        if (!visible || containerState == null)
        {
            ClearGridPlacementViews(view);
            return currentY;
        }

        int rows = Mathf.Max(1, containerState.RowCount);
        int columns = Mathf.Max(1, containerState.ColumnCount);
        float gridWidth = columns * InventoryGridCellSize;
        float gridHeight = rows * InventoryGridCellSize;
        float previewSize = kind == GridContainerKind.Pocket ? 72f : 96f;
        bool showPreviewSlot = kind != GridContainerKind.Pocket;
        float leftInset = showPreviewSlot ? previewSize + 18f : 0f;
        float sectionHeight = Mathf.Max(gridHeight, previewSize) + 38f;
        RectTransform parentRect = view.rect.parent as RectTransform;
        float sectionWidth = parentRect != null
            ? Mathf.Max(parentRect.rect.width - 36f, leftInset + gridWidth + 12f)
            : Mathf.Max(364f, leftInset + gridWidth + 12f);

        view.rect.anchoredPosition = new Vector2(18f, currentY);
        view.rect.sizeDelta = new Vector2(sectionWidth, sectionHeight);
        view.titleText.text = kind.ToString().ToUpperInvariant();

        view.previewRect.gameObject.SetActive(showPreviewSlot);
        if (showPreviewSlot)
        {
            view.previewRect.sizeDelta = new Vector2(previewSize, previewSize);
            view.previewRect.anchoredPosition = new Vector2(0f, -32f);
            ItemDefinition previewDefinition = GetContainerPreviewDefinition(kind, containerDefinition);
            Sprite previewSprite = previewDefinition != null ? previewDefinition.GetGridInventorySpriteOrFallback() : null;
            view.previewIcon.sprite = previewSprite;
            view.previewIcon.enabled = previewSprite != null;
            view.previewText.text = previewSprite == null
                ? kind.ToString().ToUpperInvariant()
                : string.Empty;
        }

        view.gridFrameRect.sizeDelta = new Vector2(
            gridWidth + (InventoryGridOuterBorderThickness * 2f),
            gridHeight + (InventoryGridOuterBorderThickness * 2f));
        view.gridFrameRect.anchoredPosition = new Vector2(leftInset, -32f);
        view.gridFrameImage.sprite = GetOrCreateGridFrameSprite(rows, columns);
        view.gridRect.sizeDelta = new Vector2(gridWidth, gridHeight);
        view.gridRect.anchoredPosition = new Vector2(InventoryGridOuterBorderThickness, -InventoryGridOuterBorderThickness);
        view.gridLayout.constraintCount = columns;
        HideGridCellVisuals(view);
        view.placementsRoot.SetAsLastSibling();
        RefreshGridPlacements(view, containerState);

        return currentY - sectionHeight - 12f;
    }

    private ItemDefinition GetContainerPreviewDefinition(GridContainerKind kind, ItemDefinition containerDefinition)
    {
        if (equipment != null)
        {
            switch (kind)
            {
                case GridContainerKind.Rig:
                    {
                        InventorySlot chestSlot = equipment.GetSlot(EquipmentSlotType.ChestArmor);
                        if (chestSlot != null && !chestSlot.IsEmpty)
                            return chestSlot.Item;

                        break;
                    }
                case GridContainerKind.Backpack:
                    {
                        InventorySlot backpackSlot = equipment.GetSlot(EquipmentSlotType.Backpack);
                        if (backpackSlot != null && !backpackSlot.IsEmpty)
                            return backpackSlot.Item;

                        break;
                    }
            }
        }

        return containerDefinition;
    }

    private void HideGridCellVisuals(GridContainerView view)
    {
        for (int i = 0; i < view.cells.Count; i++)
            view.cells[i].gameObject.SetActive(false);

        for (int i = 0; i < view.gridLines.Count; i++)
            view.gridLines[i].gameObject.SetActive(false);
    }

    private Sprite GetOrCreateGridFrameSprite(int rows, int columns)
    {
        string key = rows + "x" + columns;
        if (gridFrameSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        int gridWidth = Mathf.RoundToInt(columns * InventoryGridCellSize);
        int gridHeight = Mathf.RoundToInt(rows * InventoryGridCellSize);
        int width = gridWidth + (Mathf.RoundToInt(InventoryGridOuterBorderThickness) * 2);
        int height = gridHeight + (Mathf.RoundToInt(InventoryGridOuterBorderThickness) * 2);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color outerBorder = new Color(0.67f, 0.72f, 0.78f, 0.96f);
        Color cellFill = new Color(0.14f, 0.16f, 0.19f, 0.98f);
        Color innerLine = new Color(0.19f, 0.22f, 0.27f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, outerBorder);
        }

        int inset = Mathf.RoundToInt(InventoryGridOuterBorderThickness);
        for (int y = inset; y < height - inset; y++)
        {
            for (int x = inset; x < width - inset; x++)
                texture.SetPixel(x, y, cellFill);
        }

        for (int column = 1; column < columns; column++)
        {
            int x = inset + Mathf.RoundToInt(column * InventoryGridCellSize);
            for (int y = inset; y < height - inset; y++)
                texture.SetPixel(x, y, innerLine);
        }

        for (int row = 1; row < rows; row++)
        {
            int y = inset + Mathf.RoundToInt(row * InventoryGridCellSize);
            for (int x = inset; x < width - inset; x++)
                texture.SetPixel(x, y, innerLine);
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "GridFrame_" + key;
        gridFrameSpriteCache[key] = sprite;
        return sprite;
    }

    private void RefreshGridPlacements(GridContainerView view, GridContainerState containerState)
    {
        ClearGridPlacementViews(view);

        for (int i = 0; i < mirroredGridPlacements.Count; i++)
        {
            GridMirroredPlacement mirroredPlacement = mirroredGridPlacements[i];
            if (mirroredPlacement.containerKind != view.kind || mirroredPlacement.placement == null || mirroredPlacement.placement.IsEmpty)
                continue;

            GridPlacementView placementView = CreateGridPlacementView(view.kind, view.placementsRoot, mirroredPlacement.placement, mirroredPlacement.sourceSlotIndex);
            view.placementViews.Add(placementView);
        }
    }

    private static void ClearGridPlacementViews(GridContainerView view)
    {
        if (view == null)
            return;

        for (int i = 0; i < view.placementViews.Count; i++)
        {
            if (view.placementViews[i]?.rect != null)
                Destroy(view.placementViews[i].rect.gameObject);
        }

        view.placementViews.Clear();
    }

    private void PruneStaleMirroredAnchors()
    {
        if (inventory == null)
            return;

        HashSet<int> activeSlotIndices = new HashSet<int>();
        for (int slotIndex = 0; slotIndex < inventory.SlotCount; slotIndex++)
        {
            InventorySlot sourceSlot = inventory.GetSlot(slotIndex);
            if (sourceSlot != null && !sourceSlot.IsEmpty)
                activeSlotIndices.Add(slotIndex);
        }

        List<int> staleAnchors = null;
        foreach (KeyValuePair<int, MirroredGridAnchor> pair in mirroredGridAnchors)
        {
            if (activeSlotIndices.Contains(pair.Key))
                continue;

            staleAnchors ??= new List<int>();
            staleAnchors.Add(pair.Key);
        }

        if (staleAnchors == null)
            return;

        for (int i = 0; i < staleAnchors.Count; i++)
            mirroredGridAnchors.Remove(staleAnchors[i]);
    }

    private GridContainerState PrepareDisplayContainerState(
        GridContainerKind kind,
        bool visible,
        GridContainerState sourceState)
    {
        GridContainerState displayState = GetDisplayContainerState(kind, sourceState);
        if (sourceState == null)
        {
            displayState.Configure(kind, 1, 1);
            displayState.Clear();
            gridDisplayStates[kind] = displayState;
            return displayState;
        }

        if (visible && sourceState.HasAnyPlacement())
        {
            GridContainerState clone = sourceState.DeepClone();
            clone.Configure(kind, sourceState.RowCount, sourceState.ColumnCount);
            gridDisplayStates[kind] = clone;
            return clone;
        }

        displayState.Configure(kind, sourceState.RowCount, sourceState.ColumnCount);
        displayState.Clear();
        gridDisplayStates[kind] = displayState;
        return displayState;
    }

    private GridContainerState GetDisplayContainerState(GridContainerKind kind, GridContainerState fallbackSource)
    {
        if (gridDisplayStates.TryGetValue(kind, out GridContainerState existing) && existing != null)
            return existing;

        GridContainerState displayState = new GridContainerState();
        if (fallbackSource != null)
            displayState.Configure(kind, fallbackSource.RowCount, fallbackSource.ColumnCount);
        else
            displayState.Configure(kind, 1, 1);

        gridDisplayStates[kind] = displayState;
        return displayState;
    }

    private void PopulateDisplayPlacementsFromState(GridContainerKind kind, GridContainerState containerState)
    {
        if (containerState == null)
            return;

        IReadOnlyList<GridItemPlacement> placements = containerState.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            mirroredGridPlacements.Add(new GridMirroredPlacement(kind, placement, -1));
        }
    }

    private GridPlacementView CreateGridPlacementView(GridContainerKind containerKind, RectTransform parent, GridItemPlacement placement, int sourceSlotIndex)
    {
        float width = (placement.ColumnSpan * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        float height = (placement.RowSpan * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        float baseWidth = (Mathf.Max(1, placement.Item.inventoryColumns) * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        float baseHeight = (Mathf.Max(1, placement.Item.inventoryRows) * InventoryGridCellSize) - (InventoryPlacementInset * 2f);

        RectTransform rect = CreateRect(
            "Placement_" + placement.Item.name,
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(width, height));
        rect.anchoredPosition = new Vector2(
            (placement.Column * InventoryGridCellSize) + InventoryPlacementInset,
            -((placement.Row * InventoryGridCellSize) + InventoryPlacementInset));

        GridPlacementView view = new GridPlacementView
        {
            rect = rect,
            containerKind = containerKind,
            runtimeInstanceId = placement.RuntimeInstanceId,
            item = placement.Item,
            sourceSlotIndex = sourceSlotIndex,
            sourceIsExternal = containerKind == GridContainerKind.External,
            row = placement.Row,
            column = placement.Column,
            rotated = placement.Rotated
        };

        view.background = rect.gameObject.AddComponent<Image>();
        view.background.color = GetInventorySlotColor(placement.Item);
        Outline placementOutline = rect.gameObject.AddComponent<Outline>();
        placementOutline.effectColor = new Color(0.56f, 0.62f, 0.70f, 0.96f);
        placementOutline.effectDistance = new Vector2(1f, -1f);

        view.contentRect = CreateRect(
            "Content",
            rect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(baseWidth, baseHeight));
        view.contentRect.anchoredPosition = Vector2.zero;
        view.contentRect.localEulerAngles = new Vector3(0f, 0f, GetClockwiseRotationDegrees(placement.Rotated ? 1 : 0));

        RectTransform iconRect = CreateRect(
            "Icon",
            view.contentRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(baseWidth - 8f, baseHeight - 8f));
        iconRect.anchoredPosition = new Vector2(0f, -2f);
        view.iconImage = iconRect.gameObject.AddComponent<Image>();
        view.iconImage.sprite = placement.Item.GetGridInventorySpriteOrFallback();
        view.iconImage.color = Color.white;
        view.iconImage.preserveAspect = true;
        view.iconImage.enabled = view.iconImage.sprite != null;
        iconRect.localScale = placement.Item != null && placement.Item.ShouldFlipGridDisplaySprite()
            ? new Vector3(-1f, 1f, 1f)
            : Vector3.one;

        view.nameText = CreateText(
            "Name",
            view.contentRect,
            Shorten(placement.Item.displayName, 14),
            12,
            TextAnchor.UpperLeft,
            new Vector2(4f, -4f),
            new Vector2(baseWidth - 8f, 16f),
            FontStyle.Bold);
        view.nameText.color = new Color(0.94f, 0.96f, 0.98f, 0.96f);
        Shadow nameShadow = view.nameText.gameObject.AddComponent<Shadow>();
        nameShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        nameShadow.effectDistance = new Vector2(1f, -1f);

        if (view.iconImage.sprite == null)
        {
            view.nameText.alignment = TextAnchor.MiddleCenter;
            view.nameText.rectTransform.anchorMin = Vector2.zero;
            view.nameText.rectTransform.anchorMax = Vector2.one;
            view.nameText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            view.nameText.rectTransform.offsetMin = new Vector2(4f, 4f);
            view.nameText.rectTransform.offsetMax = new Vector2(-4f, -4f);
        }

        view.quantityText = CreateText(
            "Quantity",
            view.contentRect,
            placement.Quantity > 1 ? placement.Quantity.ToString() : string.Empty,
            13,
            TextAnchor.LowerRight,
            new Vector2(-4f, 2f),
            new Vector2(46f, 16f),
            FontStyle.Bold);
        view.quantityText.rectTransform.anchorMin = new Vector2(1f, 0f);
        view.quantityText.rectTransform.anchorMax = new Vector2(1f, 0f);
        view.quantityText.rectTransform.pivot = new Vector2(1f, 0f);
        view.quantityText.color = new Color(0.90f, 0.95f, 0.98f, 1f);
        Shadow quantityShadow = view.quantityText.gameObject.AddComponent<Shadow>();
        quantityShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        quantityShadow.effectDistance = new Vector2(1f, -1f);

        return view;
    }

    private static int NormalizeQuarterTurns(int quarterTurns)
    {
        int normalized = quarterTurns % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }

    private static float GetClockwiseRotationDegrees(int quarterTurns)
    {
        return -90f * NormalizeQuarterTurns(quarterTurns);
    }

    private void BuildStatusHudPanel()
    {
        statusHudPanel = CreatePanel(
            "StatusHudPanel",
            runtimeRoot,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(320f, 92f),
            new Color(0.05f, 0.07f, 0.10f, 0.94f));
        statusHudPanel.anchoredPosition = new Vector2(22f, 22f);

        CreateText(
            "StatusLabel",
            statusHudPanel,
            "HEALTH",
            16,
            TextAnchor.UpperLeft,
            new Vector2(18f, -12f),
            new Vector2(120f, 22f),
            FontStyle.Bold);

        healthValueText = CreateText(
            "HealthValue",
            statusHudPanel,
            "100 / 100",
            18,
            TextAnchor.UpperRight,
            new Vector2(-18f, -10f),
            new Vector2(120f, 24f),
            FontStyle.Bold);
        healthValueText.rectTransform.anchorMin = new Vector2(1f, 1f);
        healthValueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        healthValueText.rectTransform.pivot = new Vector2(1f, 1f);

        RectTransform healthBarBackgroundRect = CreateRect(
            "HealthBarBackground",
            statusHudPanel,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(284f, 24f));
        healthBarBackgroundRect.anchoredPosition = new Vector2(18f, 16f);
        Image healthBarBackground = healthBarBackgroundRect.gameObject.AddComponent<Image>();
        healthBarBackground.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

        healthBarFillRect = CreateRect(
            "HealthBarFill",
            healthBarBackgroundRect,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(HealthBarWidth, 0f));
        healthBarFillRect.anchoredPosition = Vector2.zero;

        healthBarFillImage = healthBarFillRect.gameObject.AddComponent<Image>();
        healthBarFillImage.color = new Color(0.23f, 0.78f, 0.33f, 0.98f);
    }

    private void BuildEquipmentNeedBars(RectTransform equipmentPanel)
    {
        RectTransform hydrationRow = CreateRect(
            "HydrationRow",
            equipmentPanel,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(386f, 44f));
        hydrationRow.anchoredPosition = new Vector2(18f, -654f);

        Text hydrationLabelText = CreateText(
            "HydrationLabel",
            hydrationRow,
            "水分",
            16,
            TextAnchor.UpperLeft,
            new Vector2(0f, 0f),
            new Vector2(80f, 20f),
            FontStyle.Bold);
        hydrationLabelText.color = new Color(0.46f, 0.78f, 0.98f, 1f);

        hydrationValueText = CreateText(
            "HydrationValue",
            hydrationRow,
            "100/100",
            16,
            TextAnchor.UpperRight,
            new Vector2(0f, 0f),
            new Vector2(96f, 20f),
            FontStyle.Bold);
        hydrationValueText.color = new Color(0.46f, 0.78f, 0.98f, 1f);
        hydrationValueText.rectTransform.anchorMin = new Vector2(1f, 1f);
        hydrationValueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        hydrationValueText.rectTransform.pivot = new Vector2(1f, 1f);

        RectTransform hydrationBarBackgroundRect = CreateRect(
            "HydrationBarBackground",
            hydrationRow,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(386f, 16f));
        hydrationBarBackgroundRect.anchoredPosition = new Vector2(0f, 0f);
        Image hydrationBarBackground = hydrationBarBackgroundRect.gameObject.AddComponent<Image>();
        hydrationBarBackground.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

        hydrationBarFillRect = CreateRect(
            "HydrationBarFill",
            hydrationBarBackgroundRect,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(NeedBarWidth, 0f));
        hydrationBarFillRect.anchoredPosition = Vector2.zero;
        hydrationBarFillImage = hydrationBarFillRect.gameObject.AddComponent<Image>();
        hydrationBarFillImage.color = new Color(0.20f, 0.66f, 0.96f, 0.98f);

        RectTransform hungerRow = CreateRect(
            "HungerRow",
            equipmentPanel,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(386f, 44f));
        hungerRow.anchoredPosition = new Vector2(18f, -710f);

        Text hungerLabelText = CreateText(
            "HungerLabel",
            hungerRow,
            "饥饿",
            16,
            TextAnchor.UpperLeft,
            new Vector2(0f, 0f),
            new Vector2(80f, 20f),
            FontStyle.Bold);
        hungerLabelText.color = new Color(0.98f, 0.64f, 0.20f, 1f);

        hungerValueText = CreateText(
            "HungerValue",
            hungerRow,
            "100/100",
            16,
            TextAnchor.UpperRight,
            new Vector2(0f, 0f),
            new Vector2(96f, 20f),
            FontStyle.Bold);
        hungerValueText.color = new Color(0.98f, 0.64f, 0.20f, 1f);
        hungerValueText.rectTransform.anchorMin = new Vector2(1f, 1f);
        hungerValueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        hungerValueText.rectTransform.pivot = new Vector2(1f, 1f);

        RectTransform hungerBarBackgroundRect = CreateRect(
            "HungerBarBackground",
            hungerRow,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(386f, 16f));
        hungerBarBackgroundRect.anchoredPosition = new Vector2(0f, 0f);
        Image hungerBarBackground = hungerBarBackgroundRect.gameObject.AddComponent<Image>();
        hungerBarBackground.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

        hungerBarFillRect = CreateRect(
            "HungerBarFill",
            hungerBarBackgroundRect,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(NeedBarWidth, 0f));
        hungerBarFillRect.anchoredPosition = Vector2.zero;
        hungerBarFillImage = hungerBarFillRect.gameObject.AddComponent<Image>();
        hungerBarFillImage.color = new Color(0.92f, 0.52f, 0.16f, 0.98f);
    }

    private void BuildEquipmentNeedBarsV2(RectTransform equipmentPanel)
    {
        const float leftColumnX = 18f;
        const float rightColumnX = 296f;
        const float topRowY = -724f;
        const float bottomRowY = -774f;
        Vector2 compactRowSize = new Vector2(258f, 38f);

        RectTransform healthRow = CreateRect(
            "EquipmentHealthRowV2",
            equipmentPanel,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            compactRowSize);
        healthRow.anchoredPosition = new Vector2(leftColumnX, topRowY);
        CreateStatusBarIcon(healthRow, "HealthIcon", "UI/Status/Health");

        Text healthLabelText = CreateText(
            "EquipmentHealthLabelV2",
            healthRow,
            "HEALTH",
            14,
            TextAnchor.UpperLeft,
            new Vector2(36f, 0f),
            new Vector2(74f, 18f),
            FontStyle.Bold);
        healthLabelText.color = new Color(0.98f, 0.34f, 0.34f, 1f);

        equipmentHealthValueText = CreateText(
            "EquipmentHealthValueV2",
            healthRow,
            "100/100",
            14,
            TextAnchor.UpperRight,
            new Vector2(0f, 0f),
            new Vector2(56f, 18f),
            FontStyle.Bold);
        equipmentHealthValueText.color = new Color(0.98f, 0.34f, 0.34f, 1f);
        equipmentHealthValueText.rectTransform.anchorMin = new Vector2(1f, 1f);
        equipmentHealthValueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        equipmentHealthValueText.rectTransform.pivot = new Vector2(1f, 1f);
        ApplyHudLabelFont(healthLabelText, equipmentHealthValueText);

        RectTransform equipmentHealthBarBackgroundRect = CreateRect(
            "EquipmentHealthBarBackgroundV2",
            healthRow,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(NeedBarWidth, 12f));
        equipmentHealthBarBackgroundRect.anchoredPosition = new Vector2(36f, 0f);
        Image equipmentHealthBarBackground = equipmentHealthBarBackgroundRect.gameObject.AddComponent<Image>();
        equipmentHealthBarBackground.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

        equipmentHealthBarFillRect = CreateRect(
            "EquipmentHealthBarFillV2",
            equipmentHealthBarBackgroundRect,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(NeedBarWidth, 0f));
        equipmentHealthBarFillRect.anchoredPosition = Vector2.zero;
        equipmentHealthBarFillImage = equipmentHealthBarFillRect.gameObject.AddComponent<Image>();
        equipmentHealthBarFillImage.color = new Color(0.88f, 0.22f, 0.22f, 0.98f);
        CreateOpenBarFrame(equipmentHealthBarBackgroundRect, new Color(0.93f, 0.75f, 0.19f, 1f), 2f);

        RectTransform hydrationRow = CreateRect(
            "HydrationRowV2",
            equipmentPanel,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            compactRowSize);
        hydrationRow.anchoredPosition = new Vector2(leftColumnX, bottomRowY);
        CreateStatusBarIcon(hydrationRow, "HydrationIcon", "UI/Status/Hydration");

        Text hydrationLabelText = CreateText(
            "HydrationLabelV2",
            hydrationRow,
            "WATER",
            14,
            TextAnchor.UpperLeft,
            new Vector2(36f, 0f),
            new Vector2(74f, 18f),
            FontStyle.Bold);
        hydrationLabelText.color = new Color(0.46f, 0.78f, 0.98f, 1f);

        Text hydrationDrainText = CreateText(
            "HydrationDrainRateV2",
            hydrationRow,
            "-1/20s",
            11,
            TextAnchor.UpperLeft,
            new Vector2(94f, -1f),
            new Vector2(70f, 16f),
            FontStyle.Normal);
        hydrationDrainText.color = new Color(0.46f, 0.78f, 0.98f, 0.82f);

        hydrationValueText = CreateText(
            "HydrationValueV2",
            hydrationRow,
            "100/100",
            14,
            TextAnchor.UpperRight,
            new Vector2(0f, 0f),
            new Vector2(56f, 18f),
            FontStyle.Bold);
        hydrationValueText.color = new Color(0.46f, 0.78f, 0.98f, 1f);
        hydrationValueText.rectTransform.anchorMin = new Vector2(1f, 1f);
        hydrationValueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        hydrationValueText.rectTransform.pivot = new Vector2(1f, 1f);
        ApplyHudLabelFont(hydrationLabelText, hydrationValueText);

        RectTransform hydrationBarBackgroundRect = CreateRect(
            "HydrationBarBackgroundV2",
            hydrationRow,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(NeedBarWidth, 12f));
        hydrationBarBackgroundRect.anchoredPosition = new Vector2(36f, 0f);
        Image hydrationBarBackground = hydrationBarBackgroundRect.gameObject.AddComponent<Image>();
        hydrationBarBackground.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

        hydrationBarFillRect = CreateRect(
            "HydrationBarFillV2",
            hydrationBarBackgroundRect,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(NeedBarWidth, 0f));
        hydrationBarFillRect.anchoredPosition = Vector2.zero;
        hydrationBarFillImage = hydrationBarFillRect.gameObject.AddComponent<Image>();
        hydrationBarFillImage.color = new Color(0.20f, 0.66f, 0.96f, 0.98f);
        CreateOpenBarFrame(hydrationBarBackgroundRect, new Color(0.93f, 0.75f, 0.19f, 1f), 2f);

        RectTransform hungerRow = CreateRect(
            "HungerRowV2",
            equipmentPanel,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            compactRowSize);
        hungerRow.anchoredPosition = new Vector2(rightColumnX, bottomRowY);
        CreateStatusBarIcon(hungerRow, "HungerIcon", "UI/Status/Hunger");

        Text hungerLabelText = CreateText(
            "HungerLabelV2",
            hungerRow,
            "HUNGER",
            14,
            TextAnchor.UpperLeft,
            new Vector2(36f, 0f),
            new Vector2(74f, 18f),
            FontStyle.Bold);
        hungerLabelText.color = new Color(0.98f, 0.64f, 0.20f, 1f);

        Text hungerDrainText = CreateText(
            "HungerDrainRateV2",
            hungerRow,
            "-0.5/20s",
            11,
            TextAnchor.UpperLeft,
            new Vector2(98f, -1f),
            new Vector2(82f, 16f),
            FontStyle.Normal);
        hungerDrainText.color = new Color(0.98f, 0.64f, 0.20f, 0.82f);

        hungerValueText = CreateText(
            "HungerValueV2",
            hungerRow,
            "100/100",
            14,
            TextAnchor.UpperRight,
            new Vector2(0f, 0f),
            new Vector2(56f, 18f),
            FontStyle.Bold);
        hungerValueText.color = new Color(0.98f, 0.64f, 0.20f, 1f);
        hungerValueText.rectTransform.anchorMin = new Vector2(1f, 1f);
        hungerValueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        hungerValueText.rectTransform.pivot = new Vector2(1f, 1f);
        ApplyHudLabelFont(hungerLabelText, hungerValueText);

        RectTransform hungerBarBackgroundRect = CreateRect(
            "HungerBarBackgroundV2",
            hungerRow,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(NeedBarWidth, 12f));
        hungerBarBackgroundRect.anchoredPosition = new Vector2(36f, 0f);
        Image hungerBarBackground = hungerBarBackgroundRect.gameObject.AddComponent<Image>();
        hungerBarBackground.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

        hungerBarFillRect = CreateRect(
            "HungerBarFillV2",
            hungerBarBackgroundRect,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(NeedBarWidth, 0f));
        hungerBarFillRect.anchoredPosition = Vector2.zero;
        hungerBarFillImage = hungerBarFillRect.gameObject.AddComponent<Image>();
        hungerBarFillImage.color = new Color(0.92f, 0.52f, 0.16f, 0.98f);
        CreateOpenBarFrame(hungerBarBackgroundRect, new Color(0.93f, 0.75f, 0.19f, 1f), 2f);

        RectTransform weightRow = CreateRect(
            "WeightRowV2",
            equipmentPanel,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            compactRowSize);
        weightRow.anchoredPosition = new Vector2(rightColumnX, topRowY);
        CreateStatusBarIcon(weightRow, "WeightIcon", "UI/Status/Weight");

        Text weightLabelText = CreateText(
            "WeightLabelV2",
            weightRow,
            "WEIGHT",
            14,
            TextAnchor.UpperLeft,
            new Vector2(36f, 0f),
            new Vector2(74f, 18f),
            FontStyle.Bold);
        weightLabelText.color = new Color(0.34f, 0.90f, 0.34f, 1f);

        weightValueText = CreateText(
            "WeightValueV2",
            weightRow,
            "0.0/50",
            14,
            TextAnchor.UpperRight,
            new Vector2(0f, 0f),
            new Vector2(56f, 18f),
            FontStyle.Bold);
        weightValueText.color = new Color(0.34f, 0.90f, 0.34f, 1f);
        weightValueText.rectTransform.anchorMin = new Vector2(1f, 1f);
        weightValueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        weightValueText.rectTransform.pivot = new Vector2(1f, 1f);
        ApplyHudLabelFont(weightLabelText, weightValueText);

        RectTransform weightBarBackgroundRect = CreateRect(
            "WeightBarBackgroundV2",
            weightRow,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(NeedBarWidth, 12f));
        weightBarBackgroundRect.anchoredPosition = new Vector2(36f, 0f);
        Image weightBarBackground = weightBarBackgroundRect.gameObject.AddComponent<Image>();
        weightBarBackground.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

        weightBarFillRect = CreateRect(
            "WeightBarFillV2",
            weightBarBackgroundRect,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(NeedBarWidth, 0f));
        weightBarFillRect.anchoredPosition = Vector2.zero;
        weightBarFillImage = weightBarFillRect.gameObject.AddComponent<Image>();
        weightBarFillImage.color = new Color(0.24f, 0.82f, 0.24f, 0.98f);
        CreateOpenBarFrame(weightBarBackgroundRect, new Color(0.93f, 0.75f, 0.19f, 1f), 2f);
    }

    private void BuildWeaponHudPanel()
    {
        weaponHudPanel = CreatePanel(
            "WeaponHudPanel",
            runtimeRoot,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(388f, 136f),
            new Color(0.05f, 0.07f, 0.10f, 0.94f));
        weaponHudPanel.anchoredPosition = new Vector2(-22f, 22f);

        RectTransform iconFrameRect = CreateRect(
            "IconFrame",
            weaponHudPanel,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(104f, 104f));
        iconFrameRect.anchoredPosition = new Vector2(18f, 0f);
        weaponHudIconFrame = iconFrameRect.gameObject.AddComponent<Image>();
        weaponHudIconFrame.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);

        RectTransform iconRect = CreateRect(
            "WeaponIcon",
            iconFrameRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(92f, 92f));
        weaponHudIconImage = iconRect.gameObject.AddComponent<Image>();
        weaponHudIconImage.color = Color.white;
        weaponHudIconImage.preserveAspect = true;
        weaponHudIconImage.enabled = false;

        weaponHudNameText = CreateText(
            "WeaponName",
            weaponHudPanel,
            "Unarmed",
            24,
            TextAnchor.UpperLeft,
            new Vector2(136f, -16f),
            new Vector2(220f, 30f),
            FontStyle.Bold);

        weaponHudModeText = CreateText(
            "WeaponMode",
            weaponHudPanel,
            "NO ACTIVE WEAPON",
            15,
            TextAnchor.UpperLeft,
            new Vector2(136f, -46f),
            new Vector2(214f, 22f),
            FontStyle.Bold);
        weaponHudModeText.color = new Color(0.70f, 0.83f, 0.74f, 1f);

        weaponHudAmmoText = CreateText(
            "WeaponAmmo",
            weaponHudPanel,
            "-- / --",
            30,
            TextAnchor.UpperRight,
            new Vector2(-18f, -20f),
            new Vector2(132f, 40f),
            FontStyle.Bold);
        weaponHudAmmoText.rectTransform.anchorMin = new Vector2(1f, 1f);
        weaponHudAmmoText.rectTransform.anchorMax = new Vector2(1f, 1f);
        weaponHudAmmoText.rectTransform.pivot = new Vector2(1f, 1f);

        weaponHudDetailText = CreateText(
            "WeaponDetail",
            weaponHudPanel,
            "Equip a weapon in slot 1 or 2",
            14,
            TextAnchor.LowerLeft,
            new Vector2(136f, 12f),
            new Vector2(220f, 24f),
            FontStyle.Normal);
        weaponHudDetailText.rectTransform.anchorMin = new Vector2(0f, 0f);
        weaponHudDetailText.rectTransform.anchorMax = new Vector2(0f, 0f);
        weaponHudDetailText.rectTransform.pivot = new Vector2(0f, 0f);
    }

    private void BuildMinimapPanel()
    {
        minimapPanel = CreatePanel(
            "MinimapPanel",
            runtimeRoot,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(240f, 220f),
            new Color(0.07f, 0.10f, 0.12f, 0.92f));
        minimapPanel.anchoredPosition = new Vector2(-20f, -20f);

        CreateText("Title", minimapPanel, "MINIMAP", 18, TextAnchor.UpperLeft, new Vector2(16f, -14f), new Vector2(140f, 26f), FontStyle.Bold);

        RectTransform mapSurface = CreateRect("Preview", minimapPanel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        mapSurface.offsetMin = new Vector2(14f, 14f);
        mapSurface.offsetMax = new Vector2(-14f, -46f);
        minimapFeedImage = mapSurface.gameObject.AddComponent<RawImage>();
        minimapFeedImage.color = Color.white;

        minimapArrowText = CreateText("Arrow", mapSurface, "▲", 28, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(32f, 32f), FontStyle.Bold);
        minimapArrowText.color = new Color(1f, 0.33f, 0.33f, 0.95f);
        minimapArrowText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        minimapArrowText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        minimapArrowText.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        minimapInfoText = CreateText("Info", mapSurface, "Loading map...", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, FontStyle.Normal);
        StretchToParent(minimapInfoText.rectTransform, new Vector2(12f, 12f), new Vector2(-12f, -12f));
    }

    private void BuildFullMapPanel()
    {
        fullMapPanel = CreatePanel(
            "FullMapPanel",
            runtimeRoot,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Color(0.03f, 0.05f, 0.07f, 0.96f));

        CreateText("Title", fullMapPanel, "Map", 30, TextAnchor.UpperLeft, new Vector2(36f, -28f), new Vector2(300f, 40f), FontStyle.Bold);
        CreateText("Hint", fullMapPanel, "M or Esc to close", 18, TextAnchor.UpperLeft, new Vector2(36f, -68f), new Vector2(260f, 26f), FontStyle.Normal);

        RectTransform previewSurface = CreateRect(
            "PreviewSurface",
            fullMapPanel,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(980f, 560f));

        fullMapFeedImage = previewSurface.gameObject.AddComponent<RawImage>();
        fullMapFeedImage.color = Color.white;

        fullMapArrowText = CreateText("Arrow", previewSurface, "▲", 42, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(48f, 48f), FontStyle.Bold);
        fullMapArrowText.color = new Color(1f, 0.33f, 0.33f, 0.95f);
        fullMapArrowText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        fullMapArrowText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        fullMapArrowText.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        fullMapInfoText = CreateText("Info", previewSurface, "Loading map...", 22, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(880f, 340f), FontStyle.Normal);
    }

    private void BuildContextMenu()
    {
        contextMenuPanel = CreatePanel(
            "ContextMenuPanel",
            runtimeRoot,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(154f, 166f),
            new Color(0.08f, 0.10f, 0.13f, 0.97f));

        VerticalLayoutGroup layout = contextMenuPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        contextPrimaryButton = CreateButton(contextMenuPanel, out contextPrimaryText);
        SetContextButtonHeight(contextPrimaryButton);
        contextPrimaryButton.onClick.AddListener(OnContextPrimaryAction);

        contextSecondaryButton = CreateButton(contextMenuPanel, out contextSecondaryText);
        SetContextButtonHeight(contextSecondaryButton);
        contextSecondaryButton.onClick.AddListener(OnContextSecondaryAction);

        contextInspectButton = CreateButton(contextMenuPanel, out Text inspectText);
        SetContextButtonHeight(contextInspectButton);
        inspectText.text = "Inspect";
        contextInspectButton.onClick.AddListener(OnContextInspectAction);

        contextSplitButton = CreateButton(contextMenuPanel, out Text splitText);
        SetContextButtonHeight(contextSplitButton);
        splitText.text = "Split";
        contextSplitButton.onClick.AddListener(OnContextSplitAction);

        contextDropButton = CreateButton(contextMenuPanel, out Text dropText);
        SetContextButtonHeight(contextDropButton);
        dropText.text = "Drop";
        contextDropButton.onClick.AddListener(OnContextDropAction);
    }

    private void BuildContainerPopup()
    {
        containerPopupPanel = CreatePanel(
            "ContainerPopupPanel",
            runtimeRoot,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(248f, 232f),
            new Color(0.08f, 0.10f, 0.13f, 0.98f));

        containerPopupTitleText = CreateText(
            "Title",
            containerPopupPanel,
            "Container",
            16,
            TextAnchor.UpperLeft,
            new Vector2(12f, -10f),
            new Vector2(180f, 24f),
            FontStyle.Bold);

        containerPopupCloseButton = CreateButton(containerPopupPanel, out Text closeText, 26f);
        closeText.text = "X";
        RectTransform closeRect = containerPopupCloseButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-10f, -10f);
        containerPopupCloseButton.onClick.AddListener(CloseContainerPopup);

        RectTransform gridFrameRect = CreateRect(
            "GridFrame",
            containerPopupPanel,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(180f, 180f));
        gridFrameRect.anchoredPosition = new Vector2(12f, -42f);
        Image gridFrameImage = gridFrameRect.gameObject.AddComponent<Image>();
        gridFrameImage.color = GridFrameDefaultColor;

        RectTransform gridRect = CreateRect(
            "Grid",
            gridFrameRect,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(176f, 176f));
        gridRect.anchoredPosition = new Vector2(InventoryGridOuterBorderThickness, -InventoryGridOuterBorderThickness);
        Image gridBackground = gridRect.gameObject.AddComponent<Image>();
        gridBackground.color = new Color(0f, 0f, 0f, 0f);
        gridRect.gameObject.AddComponent<RectMask2D>();

        GridLayoutGroup layout = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(InventoryGridCellSize, InventoryGridCellSize);
        layout.spacing = Vector2.zero;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 1;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.childAlignment = TextAnchor.UpperLeft;

        RectTransform gridLinesRoot = CreateRect(
            "GridLines",
            gridRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0f, 1f),
            Vector2.zero);
        StretchToParent(gridLinesRoot, Vector2.zero, Vector2.zero);
        LayoutElement lineLayout = gridLinesRoot.gameObject.AddComponent<LayoutElement>();
        lineLayout.ignoreLayout = true;

        RectTransform placementsRoot = CreateRect(
            "Placements",
            gridRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0f, 1f),
            Vector2.zero);
        StretchToParent(placementsRoot, Vector2.zero, Vector2.zero);
        LayoutElement placementsLayout = placementsRoot.gameObject.AddComponent<LayoutElement>();
        placementsLayout.ignoreLayout = true;
        placementsRoot.SetAsLastSibling();

        containerPopupView = new GridContainerView
        {
            kind = GridContainerKind.Backpack,
            rect = containerPopupPanel,
            titleText = containerPopupTitleText,
            gridFrameRect = gridFrameRect,
            gridFrameImage = gridFrameImage,
            gridRect = gridRect,
            gridLayout = layout,
            gridLinesRoot = gridLinesRoot,
            placementsRoot = placementsRoot
        };
    }

    private void BuildDropDialog()
    {
        dropDialogPanel = CreatePanel(
            "DropDialogPanel",
            runtimeRoot,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(320f, 220f),
            new Color(0.07f, 0.09f, 0.12f, 0.98f));

        dropDialogTitleText = CreateText("Title", dropDialogPanel, "Drop Item", 24, TextAnchor.UpperCenter, new Vector2(0f, -20f), new Vector2(260f, 32f), FontStyle.Bold);
        dropDialogTitleText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        dropDialogTitleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        dropDialogTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);

        RectTransform qtyRow = CreateRect("QuantityRow", dropDialogPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240f, 54f));
        HorizontalLayoutGroup qtyLayout = qtyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        qtyLayout.spacing = 8f;
        qtyLayout.childAlignment = TextAnchor.MiddleCenter;
        qtyLayout.childControlWidth = false;
        qtyLayout.childControlHeight = false;
        qtyLayout.childForceExpandWidth = false;
        qtyLayout.childForceExpandHeight = false;

        dropMinusButton = CreateButton(qtyRow, out Text minusText, 54f);
        minusText.text = "-";
        dropMinusButton.onClick.AddListener(OnDropMinus);

        RectTransform qtyLabelRect = CreateRect("QuantityLabel", qtyRow, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(120f, 46f));
        Image qtyLabelBackground = qtyLabelRect.gameObject.AddComponent<Image>();
        qtyLabelBackground.color = new Color(0.14f, 0.17f, 0.22f, 1f);
        dropDialogQuantityText = CreateText("Text", qtyLabelRect, "1 / 1", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, FontStyle.Bold);
        StretchToParent(dropDialogQuantityText.rectTransform, Vector2.zero, Vector2.zero);

        dropPlusButton = CreateButton(qtyRow, out Text plusText, 54f);
        plusText.text = "+";
        dropPlusButton.onClick.AddListener(OnDropPlus);

        RectTransform buttonRow = CreateRect("Buttons", dropDialogPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(240f, 50f));
        buttonRow.anchoredPosition = new Vector2(0f, 18f);
        HorizontalLayoutGroup buttonLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 10f;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlWidth = false;
        buttonLayout.childControlHeight = false;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;

        dropConfirmButton = CreateButton(buttonRow, out Text confirmText, 112f);
        confirmText.text = "Confirm";
        dropConfirmButton.onClick.AddListener(OnDropConfirm);

        dropCancelButton = CreateButton(buttonRow, out Text cancelText, 112f);
        cancelText.text = "Cancel";
        dropCancelButton.onClick.AddListener(OnDropCancel);
    }

    private void BuildSplitDialog()
    {
        splitDialogPanel = CreatePanel(
            "SplitDialogPanel",
            runtimeRoot,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(420f, 360f),
            new Color(0.04f, 0.05f, 0.07f, 0.98f));

        splitDialogTitleText = CreateText("Title", splitDialogPanel, "Select Quantity", 26, TextAnchor.UpperCenter, new Vector2(0f, -18f), new Vector2(320f, 34f), FontStyle.Bold);
        splitDialogTitleText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        splitDialogTitleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        splitDialogTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);

        Button closeButton = CreateButton(splitDialogPanel, out Text closeText, 34f);
        closeText.text = "X";
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-14f, -14f);
        closeButton.onClick.AddListener(OnSplitCancel);

        RectTransform itemPreviewRect = CreateRect(
            "ItemPreview",
            splitDialogPanel,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(88f, 88f));
        itemPreviewRect.anchoredPosition = new Vector2(0f, -64f);
        Image itemPreviewBackground = itemPreviewRect.gameObject.AddComponent<Image>();
        itemPreviewBackground.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);
        splitDialogItemIconImage = CreateRect(
            "Icon",
            itemPreviewRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(72f, 72f)).gameObject.AddComponent<Image>();
        splitDialogItemIconImage.preserveAspect = true;
        splitDialogItemIconImage.color = Color.white;

        RectTransform qtyRow = CreateRect("QuantityRow", splitDialogPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(260f, 40f));
        qtyRow.anchoredPosition = new Vector2(0f, -166f);
        HorizontalLayoutGroup qtyLayout = qtyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        qtyLayout.spacing = 10f;
        qtyLayout.childAlignment = TextAnchor.MiddleCenter;
        qtyLayout.childControlWidth = false;
        qtyLayout.childControlHeight = false;
        qtyLayout.childForceExpandWidth = false;
        qtyLayout.childForceExpandHeight = false;

        splitMinusButton = CreateButton(qtyRow, out Text minusText, 38f);
        minusText.text = "-";
        splitMinusButton.onClick.AddListener(OnSplitMinus);

        RectTransform inputRect = CreateRect("QuantityInput", qtyRow, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(110f, 34f));
        Image inputBackground = inputRect.gameObject.AddComponent<Image>();
        inputBackground.color = new Color(0.02f, 0.03f, 0.04f, 1f);
        splitQuantityInput = inputRect.gameObject.AddComponent<InputField>();
        splitQuantityInput.contentType = InputField.ContentType.IntegerNumber;
        splitQuantityInput.textComponent = CreateText("Text", inputRect, "1", 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, FontStyle.Bold);
        StretchToParent(splitQuantityInput.textComponent.rectTransform, new Vector2(4f, 0f), new Vector2(4f, 0f));
        splitQuantityInput.onEndEdit.AddListener(OnSplitInputEndEdit);

        splitPlusButton = CreateButton(qtyRow, out Text plusText, 38f);
        plusText.text = "+";
        splitPlusButton.onClick.AddListener(OnSplitPlus);

        RectTransform sliderRect = CreateRect("QuantitySlider", splitDialogPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(330f, 34f));
        sliderRect.anchoredPosition = new Vector2(0f, -218f);
        splitQuantitySlider = sliderRect.gameObject.AddComponent<Slider>();
        splitQuantitySlider.minValue = 1f;
        splitQuantitySlider.maxValue = 1f;
        splitQuantitySlider.wholeNumbers = true;
        splitQuantitySlider.onValueChanged.AddListener(OnSplitSliderChanged);

        RectTransform sliderBackgroundRect = CreateRect("Background", sliderRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        StretchToParent(sliderBackgroundRect, new Vector2(0f, 15f), new Vector2(0f, 15f));
        Image sliderBackground = sliderBackgroundRect.gameObject.AddComponent<Image>();
        sliderBackground.color = new Color(0.28f, 0.30f, 0.34f, 1f);
        splitQuantitySlider.targetGraphic = sliderBackground;

        RectTransform fillArea = CreateRect("Fill Area", sliderRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        StretchToParent(fillArea, new Vector2(0f, 15f), new Vector2(0f, 15f));
        RectTransform fillRect = CreateRect("Fill", fillArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        StretchToParent(fillRect, Vector2.zero, Vector2.zero);
        Image fillImage = fillRect.gameObject.AddComponent<Image>();
        fillImage.color = new Color(1f, 0.65f, 0.04f, 1f);
        splitQuantitySlider.fillRect = fillRect;

        RectTransform handleArea = CreateRect("Handle Slide Area", sliderRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        StretchToParent(handleArea, new Vector2(8f, 0f), new Vector2(8f, 0f));
        RectTransform handleRect = CreateRect("Handle", handleArea, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(12f, 24f));
        Image handleImage = handleRect.gameObject.AddComponent<Image>();
        handleImage.color = new Color(1f, 0.64f, 0.03f, 1f);
        splitQuantitySlider.handleRect = handleRect;

        splitDialogMaxText = CreateText("Range", splitDialogPanel, "1 / 1", 16, TextAnchor.MiddleCenter, new Vector2(0f, -250f), new Vector2(320f, 24f), FontStyle.Normal);
        splitDialogMaxText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        splitDialogMaxText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        splitDialogMaxText.rectTransform.pivot = new Vector2(0.5f, 1f);

        RectTransform buttonRow = CreateRect("Buttons", splitDialogPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(300f, 48f));
        buttonRow.anchoredPosition = new Vector2(0f, 24f);
        HorizontalLayoutGroup buttonLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 12f;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlWidth = false;
        buttonLayout.childControlHeight = false;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;

        splitConfirmButton = CreateButton(buttonRow, out Text confirmText, 140f);
        confirmText.text = "Split";
        splitConfirmButton.onClick.AddListener(OnSplitConfirm);

        splitCancelButton = CreateButton(buttonRow, out Text cancelText, 120f);
        cancelText.text = "Cancel";
        splitCancelButton.onClick.AddListener(OnSplitCancel);

        splitDialogPanel.gameObject.SetActive(false);
    }

    private void BuildUseProgressPanel()
    {
        useProgressPanel = CreateRect(
            "UseProgressPanel",
            runtimeRoot,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(260f, 290f));
        CanvasGroup canvasGroup = useProgressPanel.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (useProgressCircleSprite == null)
            useProgressCircleSprite = CreateCircleSprite("UseProgressCircle", 128, 0f, 0.5f);
        if (useProgressRingSprite == null)
            useProgressRingSprite = CreateCircleSprite("UseProgressRing", 128, 0.39f, 0.5f);

        RectTransform circleRect = CreateRect(
            "Circle",
            useProgressPanel,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(106f, 106f));
        circleRect.anchoredPosition = new Vector2(0f, 58f);
        useProgressBackgroundImage = circleRect.gameObject.AddComponent<Image>();
        useProgressBackgroundImage.sprite = useProgressCircleSprite;
        useProgressBackgroundImage.color = new Color(0f, 0f, 0f, 0.52f);
        useProgressBackgroundImage.raycastTarget = false;

        RectTransform ringRect = CreateRect(
            "Ring",
            circleRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero);
        StretchToParent(ringRect, Vector2.zero, Vector2.zero);
        useProgressRingImage = ringRect.gameObject.AddComponent<Image>();
        useProgressRingImage.sprite = useProgressRingSprite;
        useProgressRingImage.color = new Color(1f, 0.78f, 0.16f, 0.72f);
        useProgressRingImage.type = Image.Type.Filled;
        useProgressRingImage.fillMethod = Image.FillMethod.Radial360;
        useProgressRingImage.fillOrigin = (int)Image.Origin360.Top;
        useProgressRingImage.fillClockwise = false;
        useProgressRingImage.raycastTarget = false;

        useProgressCountdownText = CreateText(
            "Countdown",
            circleRect,
            "0",
            35,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            Vector2.zero,
            FontStyle.Bold);
        StretchToParent(useProgressCountdownText.rectTransform, Vector2.zero, Vector2.zero);

        RectTransform nameRect = CreateRect(
            "Name",
            useProgressPanel,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(230f, 30f));
        nameRect.anchoredPosition = new Vector2(0f, -20f);
        useProgressItemNameText = nameRect.gameObject.AddComponent<Text>();
        useProgressItemNameText.font = uiFont;
        useProgressItemNameText.fontSize = 19;
        useProgressItemNameText.alignment = TextAnchor.MiddleCenter;
        useProgressItemNameText.color = new Color(0.95f, 0.96f, 0.98f, 1f);
        useProgressItemNameText.fontStyle = FontStyle.Bold;
        useProgressItemNameText.horizontalOverflow = HorizontalWrapMode.Wrap;
        useProgressItemNameText.verticalOverflow = VerticalWrapMode.Truncate;
        useProgressItemNameText.text = "Using";

        RectTransform iconRect = CreateRect(
            "Icon",
            useProgressPanel,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(104f, 104f));
        iconRect.anchoredPosition = new Vector2(0f, -96f);
        useProgressItemIconImage = iconRect.gameObject.AddComponent<Image>();
        useProgressItemIconImage.preserveAspect = true;
        useProgressItemIconImage.raycastTarget = false;

        useProgressPanel.gameObject.SetActive(false);
    }

    private RectTransform CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta,
        Color backgroundColor)
    {
        RectTransform panel = CreateRect(name, parent, anchorMin, anchorMax, pivot, sizeDelta);
        Image image = panel.gameObject.AddComponent<Image>();
        image.color = backgroundColor;
        return panel;
    }

    private RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    private RectTransform CreateSlotRect(string name, Transform parent, Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.13f, 0.15f, 0.19f, 0.92f);
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = size.x;
        element.preferredHeight = size.y;
        return rect;
    }

    private Image CreateStatusBarIcon(Transform parent, string name, string resourcesPath)
    {
        RectTransform iconRect = CreateRect(
            name,
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(36f, 36f));
        iconRect.anchoredPosition = new Vector2(0f, -4f);

        Image iconImage = iconRect.gameObject.AddComponent<Image>();
        iconImage.sprite = LoadStatusIconSprite(resourcesPath);
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        iconImage.enabled = iconImage.sprite != null;
        return iconImage;
    }

    private static Sprite CreateCircleSprite(string spriteName, int size, float innerRadiusRatio, float outerRadiusRatio)
    {
        int textureSize = Mathf.Max(16, size);
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.ARGB32, false)
        {
            name = spriteName + "_Texture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32 transparent = new Color32(255, 255, 255, 0);
        Color32 solid = new Color32(255, 255, 255, 255);
        Color32[] pixels = new Color32[textureSize * textureSize];
        float center = (textureSize - 1) * 0.5f;
        float outerRadius = center * Mathf.Clamp01(outerRadiusRatio * 2f);
        float innerRadius = center * Mathf.Clamp01(innerRadiusRatio * 2f);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                bool inside = distance <= outerRadius && distance >= innerRadius;
                pixels[(y * textureSize) + x] = inside ? solid : transparent;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
        sprite.name = spriteName;
        return sprite;
    }

    private void ApplyHudLabelFont(params Text[] texts)
    {
        if (hudLabelFont == null || texts == null)
            return;

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            texts[i].font = hudLabelFont;
        }
    }

    private void CreateOpenBarFrame(RectTransform parent, Color color, float thickness)
    {
        RectTransform leftLine = CreateRect(
            "FrameLeft",
            parent,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(thickness, 0f));
        leftLine.anchoredPosition = Vector2.zero;
        Image leftImage = leftLine.gameObject.AddComponent<Image>();
        leftImage.color = color;

        RectTransform rightLine = CreateRect(
            "FrameRight",
            parent,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0.5f),
            new Vector2(thickness, 0f));
        rightLine.anchoredPosition = Vector2.zero;
        Image rightImage = rightLine.gameObject.AddComponent<Image>();
        rightImage.color = color;

        RectTransform bottomLine = CreateRect(
            "FrameBottom",
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, thickness));
        bottomLine.anchoredPosition = Vector2.zero;
        Image bottomImage = bottomLine.gameObject.AddComponent<Image>();
        bottomImage.color = color;
    }

    private Sprite LoadStatusIconSprite(string resourcesPath)
    {
        if (string.IsNullOrEmpty(resourcesPath))
            return null;

        if (runtimeStatusIconCache.TryGetValue(resourcesPath, out Sprite cachedSprite))
            return cachedSprite;

        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture == null)
        {
            runtimeStatusIconCache[resourcesPath] = null;
            return null;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = texture.name + "_RuntimeSprite";
        runtimeStatusIconCache[resourcesPath] = sprite;
        return sprite;
    }

    private SlotView CreateSlotView(RectTransform slotRect, string keyLabel)
    {
        SlotView slotView = new SlotView();
        slotView.rect = slotRect;
        slotView.background = slotRect.GetComponent<Image>();

        RectTransform iconFrameRect = CreateRect("IconFrame", slotRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        iconFrameRect.anchoredPosition = Vector2.zero;
        slotView.iconFrame = iconFrameRect.gameObject.AddComponent<Image>();
        slotView.iconFrame.color = new Color(0f, 0f, 0f, 0f);

        RectTransform iconRect = CreateRect("Icon", iconFrameRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 70f));
        iconRect.anchoredPosition = new Vector2(0f, -4f);
        slotView.iconRect = iconRect;
        slotView.iconImage = iconRect.gameObject.AddComponent<Image>();
        slotView.iconImage.color = Color.white;
        slotView.iconImage.preserveAspect = true;
        slotView.iconImage.enabled = false;

        slotView.keyText = CreateText("Key", slotRect, keyLabel, 13, TextAnchor.UpperLeft, new Vector2(8f, -6f), new Vector2(90f, 18f), FontStyle.Bold);
        slotView.keyText.color = new Color(0.84f, 0.88f, 0.93f, 0.94f);
        slotView.itemText = CreateText("Item", slotRect, "Empty", 14, TextAnchor.LowerLeft, new Vector2(8f, 24f), new Vector2(88f, 36f), FontStyle.Normal);
        slotView.itemText.rectTransform.anchorMin = new Vector2(0f, 0f);
        slotView.itemText.rectTransform.anchorMax = new Vector2(0f, 0f);
        slotView.itemText.rectTransform.pivot = new Vector2(0f, 0f);

        slotView.detailText = CreateText("Detail", slotRect, string.Empty, 10, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, FontStyle.Normal);
        slotView.detailText.color = new Color(0f, 0f, 0f, 0f);
        StretchToParent(slotView.detailText.rectTransform, Vector2.zero, Vector2.zero);

        slotView.quantityText = CreateText("Quantity", slotRect, string.Empty, 14, TextAnchor.LowerRight, new Vector2(-8f, 6f), new Vector2(42f, 18f), FontStyle.Bold);
        slotView.quantityText.rectTransform.anchorMin = new Vector2(1f, 0f);
        slotView.quantityText.rectTransform.anchorMax = new Vector2(1f, 0f);
        slotView.quantityText.rectTransform.pivot = new Vector2(1f, 0f);

        return slotView;
    }

    private void HandleGridDragInput()
    {
        if (inventoryPanel == null || !inventoryPanel.gameObject.activeSelf)
        {
            CancelGridDrag();
            return;
        }

        if (dropDialogPanel != null && dropDialogPanel.gameObject.activeSelf)
        {
            CancelGridDrag();
            return;
        }

        if (splitDialogPanel != null && splitDialogPanel.gameObject.activeSelf)
        {
            CancelGridDrag();
            return;
        }

        if (!TryGetPointerScreenPosition(out Vector2 pointerPosition))
            return;

        if (!gridDragActive && itemInspectPanel != null && itemInspectPanel.IsPointerOver(pointerPosition))
            return;

        if (!gridDragActive)
        {
            if (WasLeftPointerPressedThisFrame())
            {
                if (IsPointerOverContextMenu(pointerPosition))
                    return;

                TryBeginGridDragAt(pointerPosition);
            }

            return;
        }

        HandleGridDragRotationInput();
        UpdateGridDragPreviewPosition(pointerPosition);
        UpdateGridDragHoverVisuals(pointerPosition);

        if (!WasLeftPointerReleasedThisFrame())
            return;

        bool moved = TryCompleteGridDrag(pointerPosition);
        CancelGridDrag();

        if (moved)
        {
            equipmentVisuals?.ForceRefreshNow();
            RefreshAll();
        }
    }

    private void HandleGridDragRotationInput()
    {
        if (activeGridDrag == null || activeGridDrag.item == null)
            return;

        if (!GridItemPlacement.CanRotate(activeGridDrag.item))
            return;

        if (!WasGridRotatePressedThisFrame())
            return;

        activeGridDrag.rotated = !activeGridDrag.rotated;
        activeGridDrag.rotationQuarterTurns = activeGridDrag.rotated ? 1 : 0;

        int rowSpan = GridItemPlacement.GetRowSpan(activeGridDrag.item, activeGridDrag.rotated);
        int columnSpan = GridItemPlacement.GetColumnSpan(activeGridDrag.item, activeGridDrag.rotated);
        ApplyGridDragPreviewVisual(
            activeGridDrag.item,
            activeGridDrag.quantity,
            rowSpan,
            columnSpan,
            activeGridDrag.rotationQuarterTurns);
    }

    private bool TryBeginGridDragAt(Vector2 screenPosition)
    {
        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (TryGetEquipmentSlotAt(screenPosition, eventCamera, out EquipmentSlotType equipmentSlotType))
            return BeginEquipmentDrag(equipmentSlotType, screenPosition);

        if (TryGetCorpseEquipmentSlotAt(screenPosition, eventCamera, out EquipmentSlotType corpseEquipmentSlotType))
            return BeginCorpseEquipmentDrag(corpseEquipmentSlotType, screenPosition);

        if (TryGetPopupPlacementAt(screenPosition, eventCamera, out GridPlacementView popupPlacementView))
            return BeginGridDrag(popupPlacementView, true, pointerScreenPosition: screenPosition);

        if (TryGetCarryPlacementAt(screenPosition, eventCamera, out GridPlacementView carryPlacementView))
            return BeginGridDrag(carryPlacementView, false, pointerScreenPosition: screenPosition);

        return false;
    }

    private bool IsPointerOverContextMenu(Vector2 screenPosition)
    {
        if (contextMenuPanel == null || !contextMenuPanel.gameObject.activeInHierarchy)
            return false;

        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(contextMenuPanel, screenPosition, eventCamera);
    }

    private bool BeginEquipmentDrag(EquipmentSlotType slotType, Vector2 pointerScreenPosition)
    {
        if (equipment == null || !CanDragEquipmentSlot(slotType))
            return false;

        InventorySlot sourceSlot = equipment.GetSlot(slotType);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item == null)
            return false;

        activeGridDrag = new GridDragState
        {
            sourceIsEquipment = true,
            sourceIsPopup = false,
            sourceContainerKind = GridContainerKind.Pocket,
            sourceEquipmentSlotType = slotType,
            sourceSlotIndex = -1,
            runtimeInstanceId = sourceSlot.RuntimeInstanceId,
            item = sourceSlot.Item,
            quantity = sourceSlot.Quantity,
            sourceRow = 0,
            sourceColumn = 0,
            sourceRotated = false,
            rotated = false,
            rotationQuarterTurns = 0,
            runtimeData = sourceSlot.GetRuntimeDataForTransfer(sourceSlot.Quantity)
        };

        EnsureGridDragPreview();
        ApplyGridDragPreviewVisual(
            sourceSlot.Item,
            sourceSlot.Quantity,
            Mathf.Max(1, sourceSlot.Item.inventoryRows),
            Mathf.Max(1, sourceSlot.Item.inventoryColumns),
            0);
        UpdateGridDragPreviewPosition(pointerScreenPosition);
        gridDragPreviewRect.gameObject.SetActive(true);
        gridDragPreviewRect.SetAsLastSibling();
        gridDragActive = true;

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        ClearContextSelection();
        return true;
    }

    private bool BeginCorpseEquipmentDrag(EquipmentSlotType slotType, Vector2 pointerScreenPosition)
    {
        if (openedCorpseLoot == null)
            return false;

        InventorySlot sourceSlot = openedCorpseLoot.GetSlot(slotType);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item == null)
            return false;

        activeGridDrag = new GridDragState
        {
            sourceIsCorpseEquipment = true,
            sourceIsEquipment = false,
            sourceIsPopup = false,
            sourceContainerKind = GridContainerKind.CorpsePocket,
            sourceCorpseEquipmentSlotType = slotType,
            sourceSlotIndex = -1,
            runtimeInstanceId = sourceSlot.RuntimeInstanceId,
            item = sourceSlot.Item,
            quantity = sourceSlot.Quantity,
            sourceRow = 0,
            sourceColumn = 0,
            sourceRotated = false,
            rotated = false,
            rotationQuarterTurns = 0,
            runtimeData = sourceSlot.GetRuntimeDataForTransfer(sourceSlot.Quantity)
        };

        EnsureGridDragPreview();
        ApplyGridDragPreviewVisual(
            sourceSlot.Item,
            sourceSlot.Quantity,
            Mathf.Max(1, sourceSlot.Item.inventoryRows),
            Mathf.Max(1, sourceSlot.Item.inventoryColumns),
            0);
        UpdateGridDragPreviewPosition(pointerScreenPosition);
        gridDragPreviewRect.gameObject.SetActive(true);
        gridDragPreviewRect.SetAsLastSibling();
        gridDragActive = true;

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        ClearContextSelection();
        return true;
    }

    private bool BeginGridDrag(GridPlacementView placementView, bool sourceIsPopup, Vector2 pointerScreenPosition)
    {
        if (placementView == null || placementView.item == null)
            return false;

        GridItemPlacement sourcePlacement;
        if (sourceIsPopup)
        {
            sourcePlacement = FindPlacementForView(
                openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null,
                placementView);
        }
        else if (placementView.sourceSlotIndex >= 0)
        {
            sourcePlacement = FindDisplayPlacementForView(placementView);
        }
        else
        {
            GridContainerState actualSourceContainer = GetActualContainerState(placementView.containerKind);
            sourcePlacement = FindPlacementForView(actualSourceContainer, placementView);

            if (sourcePlacement == null || sourcePlacement.IsEmpty)
                sourcePlacement = FindDisplayPlacementForView(placementView);
        }

        if (sourcePlacement == null || sourcePlacement.IsEmpty)
            return false;

        activeGridDrag = new GridDragState
        {
            sourceIsPopup = sourceIsPopup,
            sourceIsExternal = placementView.sourceIsExternal,
            sourceContainerKind = placementView.containerKind,
            sourceSlotIndex = placementView.sourceSlotIndex,
            runtimeInstanceId = placementView.runtimeInstanceId,
            item = sourcePlacement.Item,
            quantity = sourcePlacement.Quantity,
            sourceRow = sourcePlacement.Row,
            sourceColumn = sourcePlacement.Column,
            sourceRotated = sourcePlacement.Rotated,
            rotated = sourcePlacement.Rotated,
            rotationQuarterTurns = sourcePlacement.Rotated ? 1 : 0,
            runtimeData = sourcePlacement.RuntimeData
        };

        EnsureGridDragPreview();
        ApplyGridDragPreviewVisual(sourcePlacement);
        UpdateGridDragPreviewPosition(pointerScreenPosition);
        gridDragPreviewRect.gameObject.SetActive(true);
        gridDragPreviewRect.SetAsLastSibling();
        gridDragActive = true;

        if (contextMenuPanel != null)
            contextMenuPanel.gameObject.SetActive(false);

        ClearContextSelection();
        return true;
    }

    private void EnsureGridDragPreview()
    {
        if (runtimeRoot == null || gridDragPreviewRect != null)
            return;

        gridDragPreviewRect = CreateRect(
            "GridDragPreview",
            runtimeRoot,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(64f, 64f));
        gridDragPreviewRect.gameObject.SetActive(false);
        CanvasGroup canvasGroup = gridDragPreviewRect.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0.92f;

        gridDragPreviewBackground = gridDragPreviewRect.gameObject.AddComponent<Image>();
        gridDragPreviewBackground.color = new Color(0.10f, 0.12f, 0.16f, 0.26f);
        Outline outline = gridDragPreviewRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.74f, 0.80f, 0.88f, 0.78f);
        outline.effectDistance = new Vector2(1f, -1f);

        gridDragPreviewContentRect = CreateRect(
            "Content",
            gridDragPreviewRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(56f, 56f));
        gridDragPreviewContentRect.anchoredPosition = Vector2.zero;

        RectTransform iconRect = CreateRect(
            "Icon",
            gridDragPreviewContentRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(52f, 52f));
        iconRect.anchoredPosition = new Vector2(0f, -2f);
        gridDragPreviewIcon = iconRect.gameObject.AddComponent<Image>();
        gridDragPreviewIcon.color = Color.white;
        gridDragPreviewIcon.preserveAspect = true;
        gridDragPreviewIcon.raycastTarget = false;

        gridDragPreviewNameText = CreateText(
            "Name",
            gridDragPreviewContentRect,
            string.Empty,
            12,
            TextAnchor.UpperLeft,
            new Vector2(4f, -4f),
            new Vector2(48f, 16f),
            FontStyle.Bold);
        gridDragPreviewNameText.color = new Color(0.94f, 0.96f, 0.98f, 0.92f);
        gridDragPreviewNameText.raycastTarget = false;
        Shadow nameShadow = gridDragPreviewNameText.gameObject.AddComponent<Shadow>();
        nameShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        nameShadow.effectDistance = new Vector2(1f, -1f);

        gridDragPreviewQuantityText = CreateText(
            "Quantity",
            gridDragPreviewContentRect,
            string.Empty,
            13,
            TextAnchor.LowerRight,
            new Vector2(-4f, 2f),
            new Vector2(46f, 16f),
            FontStyle.Bold);
        gridDragPreviewQuantityText.rectTransform.anchorMin = new Vector2(1f, 0f);
        gridDragPreviewQuantityText.rectTransform.anchorMax = new Vector2(1f, 0f);
        gridDragPreviewQuantityText.rectTransform.pivot = new Vector2(1f, 0f);
        gridDragPreviewQuantityText.color = new Color(0.90f, 0.95f, 0.98f, 1f);
        gridDragPreviewQuantityText.raycastTarget = false;
        Shadow quantityShadow = gridDragPreviewQuantityText.gameObject.AddComponent<Shadow>();
        quantityShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        quantityShadow.effectDistance = new Vector2(1f, -1f);
    }

    private void EnsureGridDropPreview()
    {
        if (runtimeRoot == null || gridDropPreviewRect != null)
            return;

        gridDropPreviewRect = CreateRect(
            "GridDropPreview",
            runtimeRoot,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(64f, 64f));
        gridDropPreviewRect.gameObject.SetActive(false);

        CanvasGroup canvasGroup = gridDropPreviewRect.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 1f;

        gridDropPreviewBackground = gridDropPreviewRect.gameObject.AddComponent<Image>();
        gridDropPreviewBackground.color = new Color(0.22f, 0.74f, 0.45f, 0.36f);
        gridDropPreviewOutline = gridDropPreviewRect.gameObject.AddComponent<Outline>();
        gridDropPreviewOutline.effectColor = new Color(0.34f, 0.88f, 0.56f, 0.96f);
        gridDropPreviewOutline.effectDistance = new Vector2(1f, -1f);

        gridDropPreviewContentRect = CreateRect(
            "Content",
            gridDropPreviewRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(56f, 56f));
        gridDropPreviewContentRect.anchoredPosition = Vector2.zero;

        RectTransform iconRect = CreateRect(
            "Icon",
            gridDropPreviewContentRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(56f, 56f));
        iconRect.anchoredPosition = Vector2.zero;
        gridDropPreviewIcon = iconRect.gameObject.AddComponent<Image>();
        gridDropPreviewIcon.color = new Color(1f, 1f, 1f, 0.92f);
        gridDropPreviewIcon.preserveAspect = true;
        gridDropPreviewIcon.raycastTarget = false;

        gridDropPreviewNameText = CreateText(
            "Name",
            gridDropPreviewContentRect,
            string.Empty,
            12,
            TextAnchor.UpperLeft,
            new Vector2(4f, -4f),
            new Vector2(48f, 16f),
            FontStyle.Bold);
        gridDropPreviewNameText.color = new Color(0.94f, 0.96f, 0.98f, 0.82f);
        gridDropPreviewNameText.raycastTarget = false;
        Shadow nameShadow = gridDropPreviewNameText.gameObject.AddComponent<Shadow>();
        nameShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        nameShadow.effectDistance = new Vector2(1f, -1f);
    }

    private void ApplyGridDragPreviewVisual(GridItemPlacement placement)
    {
        if (gridDragPreviewRect == null || placement == null || placement.IsEmpty)
            return;

        ApplyGridDragPreviewVisual(placement.Item, placement.Quantity, placement.RowSpan, placement.ColumnSpan, placement.Rotated ? 1 : 0);
    }

    private void ApplyGridDragPreviewVisual(ItemDefinition item, int quantity, int rowSpan, int columnSpan, int rotationQuarterTurns)
    {
        if (gridDragPreviewRect == null || item == null)
            return;

        float width = (columnSpan * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        float height = (rowSpan * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        float baseWidth = (Mathf.Max(1, item.inventoryColumns) * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        float baseHeight = (Mathf.Max(1, item.inventoryRows) * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        gridDragPreviewRect.sizeDelta = new Vector2(width, height);
        gridDragPreviewRect.localEulerAngles = Vector3.zero;

        if (gridDragPreviewContentRect != null)
        {
            gridDragPreviewContentRect.sizeDelta = new Vector2(baseWidth, baseHeight);
            gridDragPreviewContentRect.localEulerAngles = new Vector3(0f, 0f, GetClockwiseRotationDegrees(rotationQuarterTurns));
        }

        if (gridDragPreviewBackground != null)
            gridDragPreviewBackground.color = WithAlpha(GetInventorySlotColor(item), 0.72f);

        if (gridDragPreviewIcon != null)
        {
            RectTransform iconRect = gridDragPreviewIcon.rectTransform;
            iconRect.sizeDelta = new Vector2(Mathf.Max(16f, baseWidth - 2f), Mathf.Max(16f, baseHeight - 2f));
            gridDragPreviewIcon.sprite = item.GetGridInventorySpriteOrFallback();
            gridDragPreviewIcon.enabled = gridDragPreviewIcon.sprite != null;
            gridDragPreviewIcon.color = new Color(1f, 1f, 1f, gridDragPreviewIcon.sprite != null ? 0.98f : 1f);
            iconRect.localScale = item != null && item.ShouldFlipGridDisplaySprite()
                ? new Vector3(-1f, 1f, 1f)
                : Vector3.one;
        }

        if (gridDragPreviewNameText != null)
        {
            gridDragPreviewNameText.text = Shorten(item.displayName, 14);
            gridDragPreviewNameText.rectTransform.sizeDelta = new Vector2(Mathf.Max(24f, baseWidth - 8f), 16f);
        }

        if (gridDragPreviewQuantityText != null)
            gridDragPreviewQuantityText.text = quantity > 1 ? quantity.ToString() : string.Empty;
    }

    private void UpdateGridDragPreviewPosition(Vector2 screenPosition)
    {
        if (gridDragPreviewRect == null || runtimeRoot == null)
            return;

        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(runtimeRoot, screenPosition, eventCamera, out Vector2 localPoint))
            return;

        Rect rootRect = runtimeRoot.rect;
        float x = localPoint.x + 14f;
        float y = localPoint.y - rootRect.height - 14f;
        Vector2 previewSize = gridDragPreviewRect.sizeDelta;
        x = Mathf.Clamp(x, 0f, Mathf.Max(0f, rootRect.width - previewSize.x));
        y = Mathf.Clamp(y, -Mathf.Max(0f, rootRect.height - previewSize.y), 0f);
        gridDragPreviewRect.anchoredPosition = new Vector2(x, y);
    }

    private bool TryCompleteGridDrag(Vector2 screenPosition)
    {
        if (activeGridDrag == null || activeGridDrag.item == null)
            return false;

        if (!TryGetGridDropTargetAt(screenPosition, out GridDropTarget target))
            return false;

        return TryMoveDraggedPlacement(target);
    }

    private void CancelGridDrag()
    {
        gridDragActive = false;
        activeGridDrag = null;
        ClearGridDragHoverVisuals();

        if (gridDragPreviewRect != null)
            gridDragPreviewRect.gameObject.SetActive(false);
    }

    private void UpdateGridDragHoverVisuals(Vector2 screenPosition)
    {
        ClearGridDragHoverVisuals();

        if (activeGridDrag == null)
            return;

        if (!TryGetGridDropTargetAt(screenPosition, out GridDropTarget target))
            return;

        if (target.isEquipmentSlot)
        {
            if (!CanHighlightEquipmentDropTarget(target.equipmentSlotType))
                return;

            if (equipmentSlotViews.TryGetValue(target.equipmentSlotType, out SlotView slotView) && slotView?.background != null)
                slotView.background.color = EquipmentHoverColor;

            return;
        }

        GridContainerView view = target.isPopup ? containerPopupView : GetGridContainerView(target.containerKind);
        if (view == null)
            return;

        if (!IsDraggedFootprintInsideTargetBounds(target))
        {
            if (view.gridFrameImage != null)
                view.gridFrameImage.color = GridFrameInvalidColor;

            return;
        }

        bool canPlace = CanPlaceDraggedAtTarget(target);
        if (view.gridFrameImage != null)
            view.gridFrameImage.color = canPlace ? GridFrameHoverColor : GridFrameInvalidColor;

        ShowGridDropPreview(view, target, canPlace);
    }

    private void ClearGridDragHoverVisuals()
    {
        if (equipment != null)
        {
            foreach (KeyValuePair<EquipmentSlotType, SlotView> pair in equipmentSlotViews)
            {
                SlotView slotView = pair.Value;
                if (slotView?.background == null)
                    continue;

                InventorySlot slot = equipment.GetSlot(pair.Key);
                slotView.background.color = slot == null || slot.IsEmpty
                    ? GetEquipmentSlotEmptyColor(pair.Key)
                    : GetEquipmentSlotFilledColor(pair.Key, slot.Item);
            }
        }

        foreach (GridContainerView containerView in gridContainerViews.Values)
        {
            if (containerView?.gridFrameImage != null)
                containerView.gridFrameImage.color = GridFrameDefaultColor;
        }

        if (externalContainerView?.gridFrameImage != null)
            externalContainerView.gridFrameImage.color = GridFrameDefaultColor;

        if (corpsePocketView?.gridFrameImage != null)
            corpsePocketView.gridFrameImage.color = GridFrameDefaultColor;

        if (containerPopupView?.gridFrameImage != null)
            containerPopupView.gridFrameImage.color = GridFrameDefaultColor;

        if (gridDropPreviewRect != null)
            gridDropPreviewRect.gameObject.SetActive(false);
    }

    private bool CanPlaceDraggedAtTarget(GridDropTarget target)
    {
        if (activeGridDrag == null || activeGridDrag.item == null)
            return false;

        if (target.isEquipmentSlot)
            return CanHighlightEquipmentDropTarget(target.equipmentSlotType);

        if (target.actualContainer == null)
            return false;

        if (CanMergeDraggedAtTarget(target))
            return true;

        if (activeGridDrag.sourceIsEquipment)
        {
            if (IsDraggingEquipmentIntoOwnContainer(target))
                return false;

            return target.actualContainer.CanPlaceStrict(
                activeGridDrag.item,
                activeGridDrag.quantity,
                target.row,
                target.column,
                activeGridDrag.rotated,
                null);
        }

        if (activeGridDrag.sourceIsCorpseEquipment)
        {
            if (IsDraggingCorpseEquipmentIntoOwnContainer(target))
                return false;

            return target.actualContainer.CanPlaceStrict(
                activeGridDrag.item,
                activeGridDrag.quantity,
                target.row,
                target.column,
                activeGridDrag.rotated,
                null);
        }

        if (activeGridDrag.sourceSlotIndex >= 0)
        {
            GridItemPlacement sourceDisplayPlacement = FindMirroredDisplayPlacement(activeGridDrag.sourceContainerKind, activeGridDrag.sourceSlotIndex);
            GridItemPlacement ignoredPlacement = target.containerKind == activeGridDrag.sourceContainerKind
                ? sourceDisplayPlacement
                : null;
            GridContainerState occupancyContainer = target.displayContainer ?? target.actualContainer;
            return occupancyContainer != null && occupancyContainer.CanPlaceStrict(
                activeGridDrag.item,
                activeGridDrag.quantity,
                target.row,
                target.column,
                activeGridDrag.rotated,
                ignoredPlacement);
        }

        GridContainerState sourceActualContainer = activeGridDrag.sourceIsPopup
            ? (openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null)
            : GetActualContainerState(activeGridDrag.sourceContainerKind);
        if (sourceActualContainer == null)
            return false;

        GridContainerState occupancy = target.actualContainer;
        if (occupancy == null)
            return false;

        GridItemPlacement sourceActualPlacement = FindActualDragSourcePlacement(sourceActualContainer);
        GridItemPlacement ignored = FindIgnoredDragPlacementForTarget(target, sourceActualContainer, sourceActualPlacement);

        return occupancy.CanPlaceStrict(
            activeGridDrag.item,
            activeGridDrag.quantity,
            target.row,
            target.column,
            activeGridDrag.rotated,
            ignored);
    }

    private bool IsDraggedFootprintInsideTargetBounds(GridDropTarget target)
    {
        if (activeGridDrag == null || activeGridDrag.item == null || target.actualContainer == null)
            return false;

        int rowSpan = GridItemPlacement.GetRowSpan(activeGridDrag.item, activeGridDrag.rotated);
        int columnSpan = GridItemPlacement.GetColumnSpan(activeGridDrag.item, activeGridDrag.rotated);
        return target.row >= 0
            && target.column >= 0
            && target.row + rowSpan <= target.actualContainer.RowCount
            && target.column + columnSpan <= target.actualContainer.ColumnCount;
    }

    private void ShowGridDropPreview(GridContainerView view, GridDropTarget target, bool canPlace)
    {
        if (view?.placementsRoot == null || activeGridDrag == null || activeGridDrag.item == null)
            return;

        EnsureGridDropPreview();
        if (gridDropPreviewRect == null)
            return;

        if (gridDropPreviewRect.parent != view.placementsRoot)
            gridDropPreviewRect.SetParent(view.placementsRoot, false);

        int rowSpan = GridItemPlacement.GetRowSpan(activeGridDrag.item, activeGridDrag.rotated);
        int columnSpan = GridItemPlacement.GetColumnSpan(activeGridDrag.item, activeGridDrag.rotated);
        float width = (columnSpan * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        float height = (rowSpan * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        float baseWidth = (Mathf.Max(1, activeGridDrag.item.inventoryColumns) * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        float baseHeight = (Mathf.Max(1, activeGridDrag.item.inventoryRows) * InventoryGridCellSize) - (InventoryPlacementInset * 2f);
        gridDropPreviewRect.sizeDelta = new Vector2(width, height);
        gridDropPreviewRect.anchoredPosition = new Vector2(
            (target.column * InventoryGridCellSize) + InventoryPlacementInset,
            -((target.row * InventoryGridCellSize) + InventoryPlacementInset));
        gridDropPreviewRect.localEulerAngles = Vector3.zero;
        gridDropPreviewRect.SetAsLastSibling();

        if (gridDropPreviewContentRect != null)
        {
            gridDropPreviewContentRect.sizeDelta = new Vector2(baseWidth, baseHeight);
            gridDropPreviewContentRect.localEulerAngles = new Vector3(0f, 0f, GetClockwiseRotationDegrees(activeGridDrag.rotationQuarterTurns));
        }

        if (gridDropPreviewBackground != null)
            gridDropPreviewBackground.color = canPlace
                ? new Color(0.22f, 0.74f, 0.45f, 0.34f)
                : new Color(0.82f, 0.28f, 0.28f, 0.26f);

        if (gridDropPreviewOutline != null)
            gridDropPreviewOutline.effectColor = canPlace
                ? new Color(0.34f, 0.88f, 0.56f, 0.96f)
                : new Color(0.92f, 0.34f, 0.34f, 0.96f);

        if (gridDropPreviewIcon != null)
        {
            RectTransform iconRect = gridDropPreviewIcon.rectTransform;
            iconRect.sizeDelta = new Vector2(Mathf.Max(16f, baseWidth - 8f), Mathf.Max(16f, baseHeight - 8f));
            gridDropPreviewIcon.sprite = activeGridDrag.item.GetGridInventorySpriteOrFallback();
            gridDropPreviewIcon.enabled = gridDropPreviewIcon.sprite != null;
            gridDropPreviewIcon.color = new Color(1f, 1f, 1f, canPlace ? 0.92f : 0.68f);
            iconRect.localScale = activeGridDrag.item != null && activeGridDrag.item.ShouldFlipGridDisplaySprite()
                ? new Vector3(-1f, 1f, 1f)
                : Vector3.one;
        }

        if (gridDropPreviewNameText != null)
        {
            gridDropPreviewNameText.text = Shorten(activeGridDrag.item.displayName, 14);
            gridDropPreviewNameText.rectTransform.sizeDelta = new Vector2(Mathf.Max(24f, baseWidth - 8f), 16f);
        }

        gridDropPreviewRect.gameObject.SetActive(true);
    }

    private bool CanHighlightEquipmentDropTarget(EquipmentSlotType slotType)
    {
        if (activeGridDrag == null || activeGridDrag.item == null || equipment == null)
            return false;

        if (activeGridDrag.sourceIsEquipment)
            return slotType == activeGridDrag.sourceEquipmentSlotType;

        InventorySlot slot = equipment.GetSlot(slotType);
        return slot != null && slot.IsEmpty && equipment.CanEquip(slotType, activeGridDrag.item);
    }

    private GridContainerView GetGridContainerView(GridContainerKind kind)
    {
        if (kind == GridContainerKind.External)
            return externalContainerView;

        if (kind == GridContainerKind.CorpsePocket)
            return corpsePocketView;

        return gridContainerViews.TryGetValue(kind, out GridContainerView view) ? view : null;
    }

    private GridContainerState GetActualContainerState(GridContainerKind kind)
    {
        if (kind == GridContainerKind.External)
            return openedSearchableContainer != null ? openedSearchableContainer.ContainerState : null;

        if (kind == GridContainerKind.CorpsePocket)
            return openedCorpseLoot != null ? openedCorpseLoot.PocketContainer : null;

        return gridInventory != null ? gridInventory.GetContainer(kind) : null;
    }

    private bool TryGetGridDropTargetAt(Vector2 screenPosition, out GridDropTarget target)
    {
        target = default;
        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (TryGetEquipmentSlotAt(screenPosition, eventCamera, out EquipmentSlotType equipmentSlotType))
        {
            target = new GridDropTarget(
                true,
                false,
                GridContainerKind.Pocket,
                null,
                null,
                equipmentSlotType,
                0,
                0);
            return true;
        }

        if (containerPopupPanel != null && containerPopupPanel.gameObject.activeSelf && containerPopupView != null &&
            TryResolveGridDropTarget(screenPosition, eventCamera, containerPopupView.gridRect, openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null, containerPopupView.kind, true, out target))
        {
            return true;
        }

        if (externalContainerView != null && externalContainerView.rect != null && externalContainerView.rect.gameObject.activeInHierarchy)
        {
            GridContainerState externalContainer = GetActualContainerState(GridContainerKind.External);
            if (TryResolveGridDropTarget(screenPosition, eventCamera, externalContainerView.gridRect, externalContainer, GridContainerKind.External, false, out target))
            {
                target = new GridDropTarget(false, false, GridContainerKind.External, externalContainer, externalContainer, default, target.row, target.column);
                return true;
            }
        }

        if (corpsePocketView != null && corpseLootPanel != null && corpseLootPanel.gameObject.activeInHierarchy)
        {
            GridContainerState corpsePocket = GetActualContainerState(GridContainerKind.CorpsePocket);
            if (TryResolveGridDropTarget(screenPosition, eventCamera, corpsePocketView.gridRect, corpsePocket, GridContainerKind.CorpsePocket, false, out target))
            {
                target = new GridDropTarget(false, false, GridContainerKind.CorpsePocket, corpsePocket, corpsePocket, default, target.row, target.column);
                return true;
            }
        }

        foreach (GridContainerView containerView in gridContainerViews.Values)
        {
            if (containerView == null || containerView.rect == null || !containerView.rect.gameObject.activeInHierarchy)
                continue;

            GridContainerState actualContainer = GetActualContainerState(containerView.kind);
            GridContainerState displayContainer = gridDisplayStates.TryGetValue(containerView.kind, out GridContainerState displayState) ? displayState : actualContainer;
            if (TryResolveGridDropTarget(screenPosition, eventCamera, containerView.gridRect, actualContainer, containerView.kind, false, out target))
            {
                target = new GridDropTarget(false, false, containerView.kind, actualContainer, displayContainer, default, target.row, target.column);
                return true;
            }
        }

        return false;
    }

    private bool TryResolveGridDropTarget(
        Vector2 screenPosition,
        Camera eventCamera,
        RectTransform gridRect,
        GridContainerState actualContainer,
        GridContainerKind kind,
        bool isPopup,
        out GridDropTarget target)
    {
        target = default;
        if (gridRect == null || actualContainer == null)
            return false;

        if (!RectTransformUtility.RectangleContainsScreenPoint(gridRect, screenPosition, eventCamera))
            return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPosition, eventCamera, out Vector2 localPoint))
            return false;

        int column = Mathf.FloorToInt(localPoint.x / InventoryGridCellSize);
        int row = Mathf.FloorToInt(-localPoint.y / InventoryGridCellSize);
        if (column < 0 || row < 0 || column >= actualContainer.ColumnCount || row >= actualContainer.RowCount)
            return false;

        target = new GridDropTarget(false, isPopup, kind, actualContainer, actualContainer, default, row, column);
        return true;
    }

    private bool TryGetEquipmentSlotAt(Vector2 screenPosition, Camera eventCamera, out EquipmentSlotType slotType)
    {
        slotType = default;
        if (equipment == null)
            return false;

        foreach (KeyValuePair<EquipmentSlotType, SlotView> pair in equipmentSlotViews)
        {
            SlotView slotView = pair.Value;
            if (slotView?.rect == null || !slotView.rect.gameObject.activeInHierarchy)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(slotView.rect, screenPosition, eventCamera))
                continue;

            slotType = pair.Key;
            return true;
        }

        return false;
    }

    private bool TryGetCorpseEquipmentSlotAt(Vector2 screenPosition, Camera eventCamera, out EquipmentSlotType slotType)
    {
        slotType = default;
        if (openedCorpseLoot == null || corpseLootPanel == null || !corpseLootPanel.gameObject.activeInHierarchy)
            return false;

        foreach (KeyValuePair<EquipmentSlotType, SlotView> pair in corpseEquipmentSlotViews)
        {
            SlotView slotView = pair.Value;
            if (slotView?.rect == null || !slotView.rect.gameObject.activeInHierarchy)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(slotView.rect, screenPosition, eventCamera))
                continue;

            slotType = pair.Key;
            return true;
        }

        return false;
    }

    private bool TryGetCarryPlacementAt(Vector2 screenPosition, Camera eventCamera, out GridPlacementView placementView)
    {
        placementView = null;

        if (externalContainerView?.rect != null && externalContainerView.rect.gameObject.activeInHierarchy)
        {
            for (int i = 0; i < externalContainerView.placementViews.Count; i++)
            {
                GridPlacementView candidate = externalContainerView.placementViews[i];
                if (candidate?.rect == null)
                    continue;

                if (!RectTransformUtility.RectangleContainsScreenPoint(candidate.rect, screenPosition, eventCamera))
                    continue;

                placementView = candidate;
                return true;
            }
        }

        if (corpsePocketView != null && corpseLootPanel != null && corpseLootPanel.gameObject.activeInHierarchy)
        {
            for (int i = 0; i < corpsePocketView.placementViews.Count; i++)
            {
                GridPlacementView candidate = corpsePocketView.placementViews[i];
                if (candidate?.rect == null)
                    continue;

                if (!RectTransformUtility.RectangleContainsScreenPoint(candidate.rect, screenPosition, eventCamera))
                    continue;

                placementView = candidate;
                return true;
            }
        }

        foreach (GridContainerView containerView in gridContainerViews.Values)
        {
            if (containerView?.rect == null || !containerView.rect.gameObject.activeInHierarchy)
                continue;

            for (int i = 0; i < containerView.placementViews.Count; i++)
            {
                GridPlacementView candidate = containerView.placementViews[i];
                if (candidate?.rect == null)
                    continue;

                if (!RectTransformUtility.RectangleContainsScreenPoint(candidate.rect, screenPosition, eventCamera))
                    continue;

                placementView = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryGetPopupPlacementAt(Vector2 screenPosition, Camera eventCamera, out GridPlacementView placementView)
    {
        placementView = null;
        if (containerPopupPanel == null || !containerPopupPanel.gameObject.activeSelf || containerPopupView == null)
            return false;

        for (int i = 0; i < containerPopupView.placementViews.Count; i++)
        {
            GridPlacementView candidate = containerPopupView.placementViews[i];
            if (candidate?.rect == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(candidate.rect, screenPosition, eventCamera))
                continue;

            placementView = candidate;
            return true;
        }

        return false;
    }

    private GridItemPlacement FindDisplayPlacementForView(GridPlacementView placementView)
    {
        if (placementView == null)
            return null;

        if (placementView.sourceSlotIndex >= 0)
            return FindMirroredDisplayPlacement(placementView.containerKind, placementView.sourceSlotIndex);

        GridContainerState displayContainer = gridDisplayStates.TryGetValue(placementView.containerKind, out GridContainerState displayState)
            ? displayState
            : null;
        return FindPlacementForView(displayContainer, placementView);
    }

    private GridItemPlacement FindMirroredDisplayPlacement(GridContainerKind kind, int sourceSlotIndex)
    {
        for (int i = 0; i < mirroredGridPlacements.Count; i++)
        {
            GridMirroredPlacement mirroredPlacement = mirroredGridPlacements[i];
            if (mirroredPlacement.containerKind != kind || mirroredPlacement.sourceSlotIndex != sourceSlotIndex)
                continue;

            if (mirroredPlacement.placement != null && !mirroredPlacement.placement.IsEmpty)
                return mirroredPlacement.placement;
        }

        return null;
    }

    private GridItemPlacement FindDisplayPlacementByRuntimeId(GridContainerKind kind, string runtimeInstanceId)
    {
        if (string.IsNullOrWhiteSpace(runtimeInstanceId))
            return null;

        for (int i = 0; i < mirroredGridPlacements.Count; i++)
        {
            GridMirroredPlacement mirroredPlacement = mirroredGridPlacements[i];
            if (mirroredPlacement.containerKind != kind || mirroredPlacement.sourceSlotIndex >= 0)
                continue;

            if (mirroredPlacement.placement != null && !mirroredPlacement.placement.IsEmpty &&
                mirroredPlacement.placement.RuntimeInstanceId == runtimeInstanceId)
            {
                return mirroredPlacement.placement;
            }
        }

        return null;
    }

    private static GridItemPlacement FindPlacementForView(GridContainerState container, GridPlacementView placementView)
    {
        if (container == null || placementView == null)
            return null;

        if (!string.IsNullOrWhiteSpace(placementView.runtimeInstanceId))
            return FindRuntimePlacement(container, placementView.runtimeInstanceId);

        IReadOnlyList<GridItemPlacement> placements = container.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            if (placement.Item == placementView.item &&
                placement.Row == placementView.row &&
                placement.Column == placementView.column &&
                placement.Rotated == placementView.rotated)
            {
                return placement;
            }
        }

        return null;
    }

    private bool CanMergeDraggedAtTarget(GridDropTarget target)
    {
        if (activeGridDrag == null || activeGridDrag.item == null || !activeGridDrag.item.canStack || target.actualContainer == null)
            return false;

        if (activeGridDrag.sourceIsEquipment || activeGridDrag.sourceIsCorpseEquipment || activeGridDrag.sourceSlotIndex >= 0)
            return false;

        GridContainerState sourceActualContainer = activeGridDrag.sourceIsPopup
            ? (openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null)
            : GetActualContainerState(activeGridDrag.sourceContainerKind);
        if (sourceActualContainer == null)
            return false;

        GridItemPlacement sourceActualPlacement = FindActualDragSourcePlacement(sourceActualContainer);
        if (sourceActualPlacement == null || sourceActualPlacement.IsEmpty)
            return false;

        GridItemPlacement targetPlacement = target.actualContainer.GetPlacementAtCell(target.row, target.column);
        if (targetPlacement == null || targetPlacement.IsEmpty)
            return false;

        if (ReferenceEquals(targetPlacement, sourceActualPlacement))
            return true;

        return targetPlacement.Row == target.row
            && targetPlacement.Column == target.column
            && targetPlacement.CanMerge(activeGridDrag.item)
            && targetPlacement.RemainingCapacityFor(activeGridDrag.item) > 0;
    }

    private bool TryMergeDraggedActualPlacementAtTarget(
        GridDropTarget target,
        GridContainerState sourceActualContainer,
        GridItemPlacement sourceActualPlacement)
    {
        if (activeGridDrag == null || activeGridDrag.item == null || !activeGridDrag.item.canStack)
            return false;

        if (target.actualContainer == null || sourceActualContainer == null || sourceActualPlacement == null || sourceActualPlacement.IsEmpty)
            return false;

        GridItemPlacement targetPlacement = target.actualContainer.GetPlacementAtCell(target.row, target.column);
        if (targetPlacement == null || targetPlacement.IsEmpty)
            return false;

        if (ReferenceEquals(targetPlacement, sourceActualPlacement))
            return true;

        if (targetPlacement.Row != target.row || targetPlacement.Column != target.column)
            return false;

        int acceptedQuantity = targetPlacement.Add(activeGridDrag.item, sourceActualPlacement.Quantity);
        if (acceptedQuantity <= 0)
            return false;

        sourceActualPlacement.Remove(acceptedQuantity);
        if (sourceActualPlacement.IsEmpty)
            sourceActualContainer.TryRemovePlacement(sourceActualPlacement);

        return true;
    }

    private bool TryMoveDraggedPlacement(GridDropTarget target)
    {
        if (activeGridDrag == null)
            return false;

        if (target.isEquipmentSlot)
            return TryMoveDraggedPlacementToEquipment(target.equipmentSlotType);

        if (activeGridDrag.sourceIsEquipment)
            return TryMoveEquipmentDraggedPlacementToContainer(target);

        if (activeGridDrag.sourceIsCorpseEquipment)
            return TryMoveCorpseEquipmentDraggedPlacementToContainer(target);

        return activeGridDrag.sourceSlotIndex >= 0
            ? TryMoveMirroredDraggedPlacement(target)
            : TryMoveActualDraggedPlacement(target);
    }

    private bool TryMoveDraggedPlacementToEquipment(EquipmentSlotType slotType)
    {
        if (activeGridDrag == null || activeGridDrag.item == null || equipment == null)
            return false;

        if (activeGridDrag.sourceIsEquipment)
            return slotType == activeGridDrag.sourceEquipmentSlotType;

        if (activeGridDrag.sourceIsCorpseEquipment)
            return TryMoveCorpseEquipmentDraggedPlacementToEquipment(slotType);

        InventorySlot targetSlot = equipment.GetSlot(slotType);
        if (targetSlot == null || !targetSlot.IsEmpty || !equipment.CanEquip(slotType, activeGridDrag.item))
            return false;

        if (activeGridDrag.sourceSlotIndex >= 0)
            return inventory != null && equipment.TryEquipFromInventory(inventory, activeGridDrag.sourceSlotIndex, slotType);

        GridContainerState sourceActualContainer = activeGridDrag.sourceIsPopup
            ? (openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null)
            : GetActualContainerState(activeGridDrag.sourceContainerKind);
        if (sourceActualContainer == null)
            return false;

        GridItemPlacement sourceActualPlacement = FindActualDragSourcePlacement(sourceActualContainer);
        if (sourceActualPlacement == null || sourceActualPlacement.IsEmpty)
            return false;

        if (!sourceActualContainer.TryRemovePlacement(sourceActualPlacement))
            return false;

        if (targetSlot.TrySet(activeGridDrag.item, activeGridDrag.quantity, activeGridDrag.runtimeData))
        {
            CloseContainerPopupIfEquippedItem(activeGridDrag.item, activeGridDrag.runtimeData);
            return true;
        }

        sourceActualContainer.TryPlaceItemAt(
            activeGridDrag.item,
            activeGridDrag.quantity,
            sourceActualPlacement.Row,
            sourceActualPlacement.Column,
            activeGridDrag.runtimeData,
            out _,
            sourceActualPlacement.Rotated);
        return false;
    }

    private bool TryMoveEquipmentDraggedPlacementToContainer(GridDropTarget target)
    {
        if (activeGridDrag == null || !activeGridDrag.sourceIsEquipment || equipment == null || target.actualContainer == null)
            return false;

        if (IsDraggingEquipmentIntoOwnContainer(target))
            return false;

        InventorySlot sourceSlot = equipment.GetSlot(activeGridDrag.sourceEquipmentSlotType);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item == null)
            return false;

        if (!target.actualContainer.CanPlaceStrict(
                sourceSlot.Item,
                sourceSlot.Quantity,
                target.row,
                target.column,
                activeGridDrag.rotated,
                null))
        {
            return false;
        }

        ItemDefinition item = sourceSlot.Item;
        int quantity = sourceSlot.Quantity;
        ItemRuntimeData runtimeData = sourceSlot.GetRuntimeDataForTransfer(quantity);
        sourceSlot.Clear();

        if (target.actualContainer.TryPlaceItemAt(item, quantity, target.row, target.column, runtimeData, out _, activeGridDrag.rotated))
        {
            CloseContainerPopupIfDroppedItem(runtimeData);
            return true;
        }

        sourceSlot.TrySet(item, quantity, runtimeData);
        return false;
    }

    private bool TryMoveCorpseEquipmentDraggedPlacementToEquipment(EquipmentSlotType targetSlotType)
    {
        if (activeGridDrag == null || !activeGridDrag.sourceIsCorpseEquipment || openedCorpseLoot == null || equipment == null)
            return false;

        InventorySlot targetSlot = equipment.GetSlot(targetSlotType);
        InventorySlot sourceSlot = openedCorpseLoot.GetSlot(activeGridDrag.sourceCorpseEquipmentSlotType);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item == null || targetSlot == null)
            return false;

        if (!targetSlot.IsEmpty || !equipment.CanEquip(targetSlotType, sourceSlot.Item))
            return false;

        ItemDefinition item = sourceSlot.Item;
        int quantity = sourceSlot.Quantity;
        ItemRuntimeData runtimeData = sourceSlot.GetRuntimeDataForTransfer(quantity);
        sourceSlot.Clear();

        if (targetSlot.TrySet(item, quantity, runtimeData))
        {
            CloseContainerPopupIfEquippedItem(item, runtimeData);
            return true;
        }

        sourceSlot.TrySet(item, quantity, runtimeData);
        return false;
    }

    private bool TryMoveCorpseEquipmentDraggedPlacementToContainer(GridDropTarget target)
    {
        if (activeGridDrag == null || !activeGridDrag.sourceIsCorpseEquipment || openedCorpseLoot == null || target.actualContainer == null)
            return false;

        if (IsDraggingCorpseEquipmentIntoOwnContainer(target))
            return false;

        InventorySlot sourceSlot = openedCorpseLoot.GetSlot(activeGridDrag.sourceCorpseEquipmentSlotType);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item == null)
            return false;

        if (!target.actualContainer.CanPlaceStrict(
                sourceSlot.Item,
                sourceSlot.Quantity,
                target.row,
                target.column,
                activeGridDrag.rotated,
                null))
        {
            return false;
        }

        ItemDefinition item = sourceSlot.Item;
        int quantity = sourceSlot.Quantity;
        ItemRuntimeData runtimeData = sourceSlot.GetRuntimeDataForTransfer(quantity);
        sourceSlot.Clear();

        if (target.actualContainer.TryPlaceItemAt(item, quantity, target.row, target.column, runtimeData, out _, activeGridDrag.rotated))
        {
            CloseContainerPopupIfDroppedItem(runtimeData);
            return true;
        }

        sourceSlot.TrySet(item, quantity, runtimeData);
        return false;
    }

    private bool IsDraggingEquipmentIntoOwnContainer(GridDropTarget target)
    {
        if (activeGridDrag == null || !activeGridDrag.sourceIsEquipment || activeGridDrag.item == null)
            return false;

        if (target.actualContainer == null)
            return false;

        if (activeGridDrag.item is ContainerItemDefinition containerItem)
            return target.containerKind == containerItem.containerKind;

        if (activeGridDrag.item is ArmorItemDefinition armorItem && armorItem.providedRigContainer != null)
            return target.containerKind == armorItem.providedRigContainer.containerKind;

        return false;
    }

    private bool IsDraggingCorpseEquipmentIntoOwnContainer(GridDropTarget target)
    {
        if (activeGridDrag == null || !activeGridDrag.sourceIsCorpseEquipment || activeGridDrag.item == null)
            return false;

        if (target.actualContainer == null)
            return false;

        if (activeGridDrag.runtimeData == null || activeGridDrag.runtimeData.StoredContainerState == null)
            return false;

        return ReferenceEquals(target.actualContainer, activeGridDrag.runtimeData.StoredContainerState);
    }

    private bool TryMoveMirroredDraggedPlacement(GridDropTarget target)
    {
        if (activeGridDrag == null || target.isPopup || target.displayContainer == null || activeGridDrag.item == null)
            return false;

        GridItemPlacement sourceDisplayPlacement = FindMirroredDisplayPlacement(activeGridDrag.sourceContainerKind, activeGridDrag.sourceSlotIndex);
        if (sourceDisplayPlacement == null || sourceDisplayPlacement.IsEmpty)
            return false;

        if (target.containerKind == activeGridDrag.sourceContainerKind &&
            target.row == sourceDisplayPlacement.Row &&
            target.column == sourceDisplayPlacement.Column &&
            activeGridDrag.rotated == sourceDisplayPlacement.Rotated)
        {
            return true;
        }

        GridItemPlacement ignoredPlacement = target.containerKind == activeGridDrag.sourceContainerKind
            ? sourceDisplayPlacement
            : null;
        if (!target.displayContainer.CanPlaceStrict(
                activeGridDrag.item,
                activeGridDrag.quantity,
                target.row,
                target.column,
                activeGridDrag.rotated,
                ignoredPlacement))
        {
            return false;
        }

        mirroredGridAnchors[activeGridDrag.sourceSlotIndex] = new MirroredGridAnchor(
            activeGridDrag.item.itemId,
            target.containerKind,
            target.row,
            target.column,
            activeGridDrag.rotated);
        return true;
    }

    private bool TryMoveActualDraggedPlacement(GridDropTarget target)
    {
        if (activeGridDrag == null || activeGridDrag.item == null || target.actualContainer == null)
            return false;

        GridContainerState sourceActualContainer = activeGridDrag.sourceIsPopup
            ? (openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null)
            : GetActualContainerState(activeGridDrag.sourceContainerKind);
        if (sourceActualContainer == null)
            return false;

        GridItemPlacement sourceActualPlacement = FindActualDragSourcePlacement(sourceActualContainer);
        if (sourceActualPlacement == null || sourceActualPlacement.IsEmpty)
            return false;

        if (ReferenceEquals(target.actualContainer, sourceActualContainer) &&
            target.row == sourceActualPlacement.Row &&
            target.column == sourceActualPlacement.Column &&
            activeGridDrag.rotated == sourceActualPlacement.Rotated)
        {
            return true;
        }

        if (TryMergeDraggedActualPlacementAtTarget(target, sourceActualContainer, sourceActualPlacement))
            return true;

        GridContainerState occupancyContainer = target.displayContainer ?? target.actualContainer;
        GridItemPlacement ignoredPlacement = FindIgnoredDragPlacementForTarget(target, sourceActualContainer, sourceActualPlacement);

        if (!occupancyContainer.CanPlaceStrict(
                activeGridDrag.item,
                activeGridDrag.quantity,
                target.row,
                target.column,
                activeGridDrag.rotated,
                ignoredPlacement))
        {
            return false;
        }

        if (!sourceActualContainer.TryRemovePlacement(sourceActualPlacement))
            return false;

        if (target.actualContainer.TryPlaceItemAt(
                activeGridDrag.item,
                activeGridDrag.quantity,
                target.row,
                target.column,
                activeGridDrag.runtimeData,
                out _,
                activeGridDrag.rotated))
        {
            return true;
        }

        sourceActualContainer.TryPlaceItemAt(
            activeGridDrag.item,
            activeGridDrag.quantity,
            activeGridDrag.sourceRow,
            activeGridDrag.sourceColumn,
            activeGridDrag.runtimeData,
            out _,
            activeGridDrag.sourceRotated);
        return false;
    }

    private GridItemPlacement FindActualDragSourcePlacement(GridContainerState container)
    {
        if (container == null || activeGridDrag == null)
            return null;

        if (!string.IsNullOrWhiteSpace(activeGridDrag.runtimeInstanceId))
            return FindRuntimePlacement(container, activeGridDrag.runtimeInstanceId);

        IReadOnlyList<GridItemPlacement> placements = container.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            if (placement.Item == activeGridDrag.item &&
                placement.Row == activeGridDrag.sourceRow &&
                placement.Column == activeGridDrag.sourceColumn &&
                placement.Rotated == activeGridDrag.sourceRotated)
            {
                return placement;
            }
        }

        return null;
    }

    private GridItemPlacement FindIgnoredDragPlacementForTarget(
        GridDropTarget target,
        GridContainerState sourceActualContainer,
        GridItemPlacement sourceActualPlacement)
    {
        if (activeGridDrag == null || target.actualContainer == null || sourceActualContainer == null)
            return null;

        if (!ReferenceEquals(target.actualContainer, sourceActualContainer))
            return null;

        GridContainerState occupancyContainer = target.displayContainer ?? target.actualContainer;
        if (occupancyContainer == null)
            return null;

        if (ReferenceEquals(occupancyContainer, sourceActualContainer))
            return sourceActualPlacement;

        GridItemPlacement displayPlacement = FindDisplayPlacementByRuntimeId(
            activeGridDrag.sourceContainerKind,
            activeGridDrag.runtimeInstanceId);

        if (displayPlacement != null)
            return displayPlacement;

        return FindMatchingPlacement(occupancyContainer, sourceActualPlacement);
    }

    private static GridItemPlacement FindMatchingPlacement(GridContainerState container, GridItemPlacement sourcePlacement)
    {
        if (container == null || sourcePlacement == null || sourcePlacement.IsEmpty)
            return null;

        if (!string.IsNullOrWhiteSpace(sourcePlacement.RuntimeInstanceId))
        {
            GridItemPlacement runtimePlacement = FindRuntimePlacement(container, sourcePlacement.RuntimeInstanceId);
            if (runtimePlacement != null)
                return runtimePlacement;
        }

        IReadOnlyList<GridItemPlacement> placements = container.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            if (placement.Item == sourcePlacement.Item &&
                placement.Row == sourcePlacement.Row &&
                placement.Column == sourcePlacement.Column &&
                placement.Rotated == sourcePlacement.Rotated)
            {
                return placement;
            }
        }

        return null;
    }

    private void HandlePointerFallbackInput()
    {
        if (inventoryPanel == null || !inventoryPanel.gameObject.activeSelf)
            return;

        if (gridDragActive)
            return;

        if (dropDialogPanel != null && dropDialogPanel.gameObject.activeSelf)
            return;

        if (!WasRightPointerPressedThisFrame())
            return;

        if (!TryGetPointerScreenPosition(out Vector2 pointerPosition))
            return;

        if (itemInspectPanel != null && itemInspectPanel.IsPointerOver(pointerPosition))
            return;

        if (TryOpenBackpackContextMenuAt(pointerPosition))
            return;

        if (TryOpenEquipmentContextMenuAt(pointerPosition))
            return;

        if (TryOpenCorpseEquipmentContextMenuAt(pointerPosition))
            return;

        if (contextMenuPanel != null && contextMenuPanel.gameObject.activeSelf)
        {
            contextMenuPanel.gameObject.SetActive(false);
            ClearContextSelection();
        }
    }

    private bool TryOpenBackpackContextMenuAt(Vector2 screenPosition)
    {
        if (inventory == null)
            return false;

        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (TryOpenContainerPopupContextMenuAt(screenPosition, eventCamera))
            return true;

        if (TryOpenExternalContainerContextMenuAt(screenPosition, eventCamera))
            return true;

        if (TryOpenCorpsePocketContextMenuAt(screenPosition, eventCamera))
            return true;

        foreach (GridContainerView containerView in gridContainerViews.Values)
        {
            if (containerView?.rect == null || !containerView.rect.gameObject.activeInHierarchy)
                continue;

            for (int i = 0; i < containerView.placementViews.Count; i++)
            {
                GridPlacementView placementView = containerView.placementViews[i];
                if (placementView?.rect == null)
                    continue;

                if (!RectTransformUtility.RectangleContainsScreenPoint(placementView.rect, screenPosition, eventCamera))
                    continue;

                if (placementView.sourceSlotIndex >= 0)
                {
                    InventorySlot slot = inventory.GetSlot(placementView.sourceSlotIndex);
                    if (slot == null || slot.IsEmpty)
                        return false;

                    OpenContextMenuForBackpackSlot(placementView.sourceSlotIndex, screenPosition);
                    return true;
                }

                GridContainerState container = GetActualContainerState(placementView.containerKind);
                GridItemPlacement placement = FindPlacementForView(container, placementView);
                if (placement == null || placement.IsEmpty)
                    return false;

                OpenContextMenuForCarryPlacement(placementView.containerKind, placement, screenPosition);
                return true;
            }
        }

        for (int i = 0; i < backpackSlotViews.Count; i++)
        {
            SlotView slotView = backpackSlotViews[i];
            if (slotView?.rect == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(slotView.rect, screenPosition, eventCamera))
                continue;

            InventorySlot slot = inventory.GetSlot(i);
            if (slot == null || slot.IsEmpty)
                return false;

            OpenContextMenuForBackpackSlot(i, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenBackpackContextMenuAtGui(Vector2 guiPosition)
    {
        if (inventory == null)
            return false;

        if (TryOpenContainerPopupContextMenuAtGui(guiPosition))
            return true;

        if (TryOpenExternalContainerContextMenuAtGui(guiPosition))
            return true;

        if (TryOpenCorpsePocketContextMenuAtGui(guiPosition))
            return true;

        foreach (GridContainerView containerView in gridContainerViews.Values)
        {
            if (containerView?.rect == null || !containerView.rect.gameObject.activeInHierarchy)
                continue;

            for (int i = 0; i < containerView.placementViews.Count; i++)
            {
                GridPlacementView placementView = containerView.placementViews[i];
                if (placementView?.rect == null)
                    continue;

                if (!TryGetGuiRect(placementView.rect, out Rect guiRect))
                    continue;

                if (!guiRect.Contains(guiPosition))
                    continue;

                Vector2 screenPosition = new Vector2(guiPosition.x, Screen.height - guiPosition.y);

                if (placementView.sourceSlotIndex >= 0)
                {
                    InventorySlot slot = inventory.GetSlot(placementView.sourceSlotIndex);
                    if (slot == null || slot.IsEmpty)
                        return false;

                    OpenContextMenuForBackpackSlot(placementView.sourceSlotIndex, screenPosition);
                    return true;
                }

                GridContainerState container = GetActualContainerState(placementView.containerKind);
                GridItemPlacement placement = FindPlacementForView(container, placementView);
                if (placement == null || placement.IsEmpty)
                    return false;

                OpenContextMenuForCarryPlacement(placementView.containerKind, placement, screenPosition);
                return true;
            }
        }

        for (int i = 0; i < backpackSlotViews.Count; i++)
        {
            SlotView slotView = backpackSlotViews[i];
            if (!TryGetGuiRect(slotView?.rect, out Rect guiRect))
                continue;

            if (!guiRect.Contains(guiPosition))
                continue;

            InventorySlot slot = inventory.GetSlot(i);
            if (slot == null || slot.IsEmpty)
                return false;

            Vector2 screenPosition = new Vector2(guiPosition.x, Screen.height - guiPosition.y);
            OpenContextMenuForBackpackSlot(i, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenExternalContainerContextMenuAt(Vector2 screenPosition, Camera eventCamera)
    {
        if (externalContainerView == null || !externalContainerView.rect.gameObject.activeInHierarchy)
            return false;

        GridContainerState container = GetActualContainerState(GridContainerKind.External);
        if (container == null)
            return false;

        for (int i = 0; i < externalContainerView.placementViews.Count; i++)
        {
            GridPlacementView placementView = externalContainerView.placementViews[i];
            if (placementView?.rect == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(placementView.rect, screenPosition, eventCamera))
                continue;

            GridItemPlacement placement = FindPlacementForView(container, placementView);
            if (placement == null || placement.IsEmpty)
                return false;

            OpenContextMenuForCarryPlacement(GridContainerKind.External, placement, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenExternalContainerContextMenuAtGui(Vector2 guiPosition)
    {
        if (externalContainerView == null || !externalContainerView.rect.gameObject.activeInHierarchy)
            return false;

        GridContainerState container = GetActualContainerState(GridContainerKind.External);
        if (container == null)
            return false;

        for (int i = 0; i < externalContainerView.placementViews.Count; i++)
        {
            GridPlacementView placementView = externalContainerView.placementViews[i];
            if (!TryGetGuiRect(placementView?.rect, out Rect guiRect))
                continue;

            if (!guiRect.Contains(guiPosition))
                continue;

            GridItemPlacement placement = FindPlacementForView(container, placementView);
            if (placement == null || placement.IsEmpty)
                return false;

            Vector2 screenPosition = new Vector2(guiPosition.x, Screen.height - guiPosition.y);
            OpenContextMenuForCarryPlacement(GridContainerKind.External, placement, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenCorpsePocketContextMenuAt(Vector2 screenPosition, Camera eventCamera)
    {
        if (corpsePocketView == null || corpseLootPanel == null || !corpseLootPanel.gameObject.activeInHierarchy)
            return false;

        GridContainerState container = GetActualContainerState(GridContainerKind.CorpsePocket);
        if (container == null)
            return false;

        for (int i = 0; i < corpsePocketView.placementViews.Count; i++)
        {
            GridPlacementView placementView = corpsePocketView.placementViews[i];
            if (placementView?.rect == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(placementView.rect, screenPosition, eventCamera))
                continue;

            GridItemPlacement placement = FindPlacementForView(container, placementView);
            if (placement == null || placement.IsEmpty)
                return false;

            OpenContextMenuForCarryPlacement(GridContainerKind.CorpsePocket, placement, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenCorpsePocketContextMenuAtGui(Vector2 guiPosition)
    {
        if (corpsePocketView == null || corpseLootPanel == null || !corpseLootPanel.gameObject.activeInHierarchy)
            return false;

        GridContainerState container = GetActualContainerState(GridContainerKind.CorpsePocket);
        if (container == null)
            return false;

        for (int i = 0; i < corpsePocketView.placementViews.Count; i++)
        {
            GridPlacementView placementView = corpsePocketView.placementViews[i];
            if (!TryGetGuiRect(placementView?.rect, out Rect guiRect))
                continue;

            if (!guiRect.Contains(guiPosition))
                continue;

            GridItemPlacement placement = FindPlacementForView(container, placementView);
            if (placement == null || placement.IsEmpty)
                return false;

            Vector2 screenPosition = new Vector2(guiPosition.x, Screen.height - guiPosition.y);
            OpenContextMenuForCarryPlacement(GridContainerKind.CorpsePocket, placement, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenContainerPopupContextMenuAt(Vector2 screenPosition, Camera eventCamera)
    {
        if (containerPopupPanel == null || !containerPopupPanel.gameObject.activeSelf || containerPopupView == null)
            return false;

        GridContainerState container = openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null;
        if (container == null)
            return false;

        for (int i = 0; i < containerPopupView.placementViews.Count; i++)
        {
            GridPlacementView placementView = containerPopupView.placementViews[i];
            if (placementView?.rect == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(placementView.rect, screenPosition, eventCamera))
                continue;

            GridItemPlacement placement = FindPlacementForView(container, placementView);
            if (placement == null || placement.IsEmpty)
                return false;

            OpenContextMenuForPopupPlacement(placement, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenContainerPopupContextMenuAtGui(Vector2 guiPosition)
    {
        if (containerPopupPanel == null || !containerPopupPanel.gameObject.activeSelf || containerPopupView == null)
            return false;

        GridContainerState container = openedContainerRuntimeData != null ? openedContainerRuntimeData.StoredContainerState : null;
        if (container == null)
            return false;

        for (int i = 0; i < containerPopupView.placementViews.Count; i++)
        {
            GridPlacementView placementView = containerPopupView.placementViews[i];
            if (!TryGetGuiRect(placementView?.rect, out Rect guiRect))
                continue;

            if (!guiRect.Contains(guiPosition))
                continue;

            GridItemPlacement placement = FindPlacementForView(container, placementView);
            if (placement == null || placement.IsEmpty)
                return false;

            Vector2 screenPosition = new Vector2(guiPosition.x, Screen.height - guiPosition.y);
            OpenContextMenuForPopupPlacement(placement, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenEquipmentContextMenuAt(Vector2 screenPosition)
    {
        if (equipment == null)
            return false;

        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        foreach (KeyValuePair<EquipmentSlotType, SlotView> pair in equipmentSlotViews)
        {
            SlotView slotView = pair.Value;
            if (slotView?.rect == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(slotView.rect, screenPosition, eventCamera))
                continue;

            InventorySlot slot = equipment.GetSlot(pair.Key);
            if (slot == null || slot.IsEmpty)
                return false;

            OpenContextMenuForEquipmentSlot(pair.Key, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenEquipmentContextMenuAtGui(Vector2 guiPosition)
    {
        if (equipment == null)
            return false;

        foreach (KeyValuePair<EquipmentSlotType, SlotView> pair in equipmentSlotViews)
        {
            if (!TryGetGuiRect(pair.Value?.rect, out Rect guiRect))
                continue;

            if (!guiRect.Contains(guiPosition))
                continue;

            InventorySlot slot = equipment.GetSlot(pair.Key);
            if (slot == null || slot.IsEmpty)
                return false;

            Vector2 screenPosition = new Vector2(guiPosition.x, Screen.height - guiPosition.y);
            OpenContextMenuForEquipmentSlot(pair.Key, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenCorpseEquipmentContextMenuAt(Vector2 screenPosition)
    {
        if (openedCorpseLoot == null || corpseLootPanel == null || !corpseLootPanel.gameObject.activeInHierarchy)
            return false;

        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        foreach (KeyValuePair<EquipmentSlotType, SlotView> pair in corpseEquipmentSlotViews)
        {
            SlotView slotView = pair.Value;
            if (slotView?.rect == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(slotView.rect, screenPosition, eventCamera))
                continue;

            InventorySlot slot = openedCorpseLoot.GetSlot(pair.Key);
            if (slot == null || slot.IsEmpty)
                return false;

            OpenContextMenuForCorpseEquipmentSlot(pair.Key, screenPosition);
            return true;
        }

        return false;
    }

    private bool TryOpenCorpseEquipmentContextMenuAtGui(Vector2 guiPosition)
    {
        if (openedCorpseLoot == null || corpseLootPanel == null || !corpseLootPanel.gameObject.activeInHierarchy)
            return false;

        foreach (KeyValuePair<EquipmentSlotType, SlotView> pair in corpseEquipmentSlotViews)
        {
            if (!TryGetGuiRect(pair.Value?.rect, out Rect guiRect))
                continue;

            if (!guiRect.Contains(guiPosition))
                continue;

            InventorySlot slot = openedCorpseLoot.GetSlot(pair.Key);
            if (slot == null || slot.IsEmpty)
                return false;

            Vector2 screenPosition = new Vector2(guiPosition.x, Screen.height - guiPosition.y);
            OpenContextMenuForCorpseEquipmentSlot(pair.Key, screenPosition);
            return true;
        }

        return false;
    }

    private bool WasRightPointerPressedThisFrame()
    {
        if (Input.GetMouseButtonDown(1))
            return true;

#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    private bool WasLeftPointerPressedThisFrame()
    {
        if (Input.GetMouseButtonDown(0))
            return true;

#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    private bool WasLeftPointerReleasedThisFrame()
    {
        if (Input.GetMouseButtonUp(0))
            return true;

#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    private bool WasGridRotatePressedThisFrame()
    {
        if (Input.GetKeyDown(KeyCode.R))
            return true;

#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return false;
#endif
    }

    private bool TryGetPointerScreenPosition(out Vector2 pointerPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif
        pointerPosition = Input.mousePosition;
        return pointerPosition != Vector2.zero;
    }

    private void UpdateOverlayCursorState(bool isBlockingOverlayOpen)
    {
        if (isBlockingOverlayOpen)
        {
            if (!overlayCursorStateCaptured)
            {
                previousCursorVisible = Cursor.visible;
                previousCursorLockMode = Cursor.lockState;
                overlayCursorStateCaptured = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (!overlayCursorStateCaptured)
            return;

        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
        overlayCursorStateCaptured = false;
    }

    private Text CreateText(
        string name,
        RectTransform parent,
        string text,
        int fontSize,
        TextAnchor alignment,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        FontStyle fontStyle)
    {
        RectTransform rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), sizeDelta);
        rect.anchoredPosition = anchoredPosition;

        Text uiText = rect.gameObject.AddComponent<Text>();
        uiText.font = uiFont;
        uiText.fontSize = fontSize;
        uiText.alignment = alignment;
        uiText.color = new Color(0.95f, 0.96f, 0.98f, 1f);
        uiText.fontStyle = fontStyle;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Truncate;
        uiText.text = text;
        return uiText;
    }

    private Button CreateButton(Transform parent, out Text labelText, float width = 0f)
    {
        RectTransform rect = CreateRect("Button", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(width > 0f ? width : 0f, 38f));
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        if (width > 0f)
            element.preferredWidth = width;
        element.preferredHeight = 38f;

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.23f, 0.30f, 1f);

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.25f, 0.31f, 0.40f, 1f);
        colors.pressedColor = new Color(0.13f, 0.18f, 0.24f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        labelText = CreateText("Label", rect, "Button", 16, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, FontStyle.Bold);
        StretchToParent(labelText.rectTransform, Vector2.zero, Vector2.zero);
        return button;
    }

    private void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void PositionPanelAtScreenPoint(RectTransform panel, Vector2 screenPoint)
    {
        if (panel == null || rootCanvas == null)
            return;

        if (panel == contextMenuPanel)
            ResizeContextMenuToVisibleButtons();

        RectTransform parentRect = panel.parent as RectTransform;
        if (parentRect == null)
            return;

        Camera cameraForCanvas = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cameraForCanvas, out Vector2 localPoint))
            return;

        Vector2 panelOffset = new Vector2(12f, -12f);
        Vector2 anchoredPosition = new Vector2(
            localPoint.x + panelOffset.x,
            localPoint.y - parentRect.rect.height + panelOffset.y);

        Vector2 canvasSize = parentRect.rect.size;
        Vector2 panelSize = panel.rect.size;

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, 0f, Mathf.Max(0f, canvasSize.x - panelSize.x));
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, -Mathf.Max(0f, canvasSize.y - panelSize.y), 0f);

        panel.anchoredPosition = anchoredPosition;
    }

    private void SetContextButtonHeight(Button button)
    {
        if (button == null)
            return;

        LayoutElement element = button.GetComponent<LayoutElement>();
        if (element == null)
            element = button.gameObject.AddComponent<LayoutElement>();

        element.preferredHeight = 34f;
        element.flexibleHeight = 0f;
    }

    private void ResizeContextMenuToVisibleButtons()
    {
        if (contextMenuPanel == null)
            return;

        VerticalLayoutGroup layout = contextMenuPanel.GetComponent<VerticalLayoutGroup>();
        float padding = layout != null ? layout.padding.top + layout.padding.bottom : 12f;
        float spacing = layout != null ? layout.spacing : 6f;
        int visibleButtonCount = 0;

        if (contextPrimaryButton != null && contextPrimaryButton.gameObject.activeSelf)
            visibleButtonCount++;
        if (contextSecondaryButton != null && contextSecondaryButton.gameObject.activeSelf)
            visibleButtonCount++;
        if (contextInspectButton != null && contextInspectButton.gameObject.activeSelf)
            visibleButtonCount++;
        if (contextSplitButton != null && contextSplitButton.gameObject.activeSelf)
            visibleButtonCount++;
        if (contextDropButton != null && contextDropButton.gameObject.activeSelf)
            visibleButtonCount++;

        float height = padding + visibleButtonCount * 34f + Mathf.Max(0, visibleButtonCount - 1) * spacing;
        contextMenuPanel.sizeDelta = new Vector2(contextMenuPanel.sizeDelta.x, Mathf.Max(46f, height));
        LayoutRebuilder.ForceRebuildLayoutImmediate(contextMenuPanel);
    }

    private bool TryGetGuiRect(RectTransform rect, out Rect guiRect)
    {
        guiRect = default;
        if (rect == null)
            return false;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        Vector3 bottomLeft = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
        Vector3 topRight = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);

        float x = bottomLeft.x;
        float y = Screen.height - topRight.y;
        float width = topRight.x - bottomLeft.x;
        float height = topRight.y - bottomLeft.y;

        guiRect = new Rect(x, y, width, height);
        return width > 0f && height > 0f;
    }

    private EquipmentSlotType GetArmorEquipmentSlot(ArmorSlotType armorSlot)
    {
        return armorSlot switch
        {
            ArmorSlotType.Head => EquipmentSlotType.HeadArmor,
            ArmorSlotType.Chest => EquipmentSlotType.ChestArmor,
            ArmorSlotType.Legs => EquipmentSlotType.LegsArmor,
            ArmorSlotType.Feet => EquipmentSlotType.FeetArmor,
            _ => EquipmentSlotType.ChestArmor
        };
    }

    private EquipmentSlotType GetContainerEquipmentSlot(GridContainerKind containerKind)
    {
        return containerKind switch
        {
            GridContainerKind.Backpack => EquipmentSlotType.Backpack,
            _ => EquipmentSlotType.Backpack
        };
    }

    private void CreateEquipmentSlot(RectTransform parent, EquipmentSlotType slotType, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform slotRect = CreateRect("EquipmentSlot_" + slotType, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), size);
        slotRect.anchoredPosition = anchoredPosition;

        Image image = slotRect.gameObject.AddComponent<Image>();
        image.color = GetEquipmentSlotEmptyColor(slotType);

        SlotView slotView = CreateSlotView(slotRect, GetEquipmentSlotLabel(slotType));
        ApplyEquipmentSlotTextLayout(slotView, slotType, size);
        if (slotView.iconRect != null)
        {
            float iconWidth = Mathf.Max(66f, size.x - 24f);
            float iconHeight = Mathf.Max(58f, size.y - 36f);
            slotView.iconRect.sizeDelta = new Vector2(iconWidth, iconHeight);
            slotView.iconRect.anchoredPosition = new Vector2(0f, -2f);
        }
        ApplyEquipmentSlotIconPresentation(slotView, slotType, null);
        InventorySlotWidget widget = slotRect.gameObject.AddComponent<InventorySlotWidget>();
        widget.Configure(this, (int)slotType, InventorySlotWidgetMode.Equipment);
        equipmentSlotViews[slotType] = slotView;
    }

    private void CreateCorpseEquipmentSlot(RectTransform parent, EquipmentSlotType slotType, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform slotRect = CreateRect("CorpseEquipmentSlot_" + slotType, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), size);
        slotRect.anchoredPosition = anchoredPosition;

        Image image = slotRect.gameObject.AddComponent<Image>();
        image.color = GetEquipmentSlotEmptyColor(slotType);

        SlotView slotView = CreateSlotView(slotRect, GetEquipmentSlotLabel(slotType));
        ApplyEquipmentSlotTextLayout(slotView, slotType, size);
        if (slotView.iconRect != null)
        {
            float iconWidth = Mathf.Max(66f, size.x - 24f);
            float iconHeight = Mathf.Max(58f, size.y - 36f);
            slotView.iconRect.sizeDelta = new Vector2(iconWidth, iconHeight);
            slotView.iconRect.anchoredPosition = new Vector2(0f, -2f);
        }

        ApplyEquipmentSlotIconPresentation(slotView, slotType, null);
        corpseEquipmentSlotViews[slotType] = slotView;
    }

    private void ApplyEquipmentSlotIconPresentation(SlotView slotView, EquipmentSlotType slotType, ItemDefinition item)
    {
        if (slotView?.iconRect == null)
            return;

        Vector2 defaultSize = slotType switch
        {
            EquipmentSlotType.PrimaryWeapon => new Vector2(330f, 76f),
            EquipmentSlotType.SecondaryWeapon => new Vector2(156f, 60f),
            _ => new Vector2(86f, 86f)
        };

        Vector2 defaultPosition = slotType switch
        {
            EquipmentSlotType.PrimaryWeapon => new Vector2(0f, -2f),
            EquipmentSlotType.SecondaryWeapon => new Vector2(0f, -2f),
            _ => new Vector2(0f, -2f)
        };

        slotView.iconRect.localEulerAngles = Vector3.zero;
        slotView.iconRect.localScale = Vector3.one;
        slotView.iconRect.sizeDelta = defaultSize;
        slotView.iconRect.anchoredPosition = defaultPosition;

        if (item is not WeaponItemDefinition weapon)
            return;

        if (weapon.weaponCategory == WeaponCategory.Pistol)
            return;

        if (slotType != EquipmentSlotType.PrimaryWeapon && slotType != EquipmentSlotType.SecondaryWeapon)
            return;

        if (weapon.equipmentSlotIcon != null)
        {
            slotView.iconRect.localEulerAngles = Vector3.zero;
            slotView.iconRect.localScale = new Vector3(-1f, 1f, 1f);
            slotView.iconRect.sizeDelta = slotType == EquipmentSlotType.PrimaryWeapon
                ? new Vector2(332f, 80f)
                : new Vector2(186f, 58f);
            slotView.iconRect.anchoredPosition = slotType == EquipmentSlotType.PrimaryWeapon
                ? new Vector2(18f, -2f)
                : new Vector2(8f, -2f);
            return;
        }

        slotView.iconRect.localEulerAngles = new Vector3(0f, 0f, 28f);
        slotView.iconRect.sizeDelta = slotType == EquipmentSlotType.PrimaryWeapon
            ? new Vector2(374f, 92f)
            : new Vector2(214f, 70f);
        slotView.iconRect.anchoredPosition = slotType == EquipmentSlotType.PrimaryWeapon
            ? new Vector2(32f, -2f)
            : new Vector2(10f, -2f);
    }

    private void ApplyEquipmentSlotPlaceholderPresentation(SlotView slotView, EquipmentSlotType slotType)
    {
        if (slotView?.iconRect == null)
            return;

        slotView.iconRect.localEulerAngles = Vector3.zero;
        slotView.iconRect.localScale = Vector3.one;

        slotView.iconRect.sizeDelta = slotType switch
        {
            EquipmentSlotType.HeadArmor => new Vector2(70f, 70f),
            EquipmentSlotType.Backpack => new Vector2(68f, 68f),
            EquipmentSlotType.ChestArmor => new Vector2(76f, 62f),
            EquipmentSlotType.SecondaryWeapon => new Vector2(96f, 42f),
            EquipmentSlotType.PrimaryWeapon => new Vector2(246f, 70f),
            _ => new Vector2(86f, 86f)
        };

        slotView.iconRect.anchoredPosition = slotType switch
        {
            EquipmentSlotType.PrimaryWeapon => new Vector2(8f, -2f),
            EquipmentSlotType.SecondaryWeapon => new Vector2(0f, -2f),
            _ => new Vector2(0f, -2f)
        };
    }

    private void SetSlotIcon(SlotView slotView, Sprite icon)
    {
        if (slotView == null || slotView.iconImage == null)
            return;

        slotView.iconImage.sprite = icon;
        slotView.iconImage.enabled = icon != null;
    }

    private void SetSlotIconTint(SlotView slotView, Color tint)
    {
        if (slotView == null || slotView.iconImage == null)
            return;

        slotView.iconImage.color = tint;
    }

    private void SetSlotIconPreserveAspect(SlotView slotView, bool preserveAspect)
    {
        if (slotView == null || slotView.iconImage == null)
            return;

        slotView.iconImage.preserveAspect = preserveAspect;
    }

    private Sprite GetEquipmentDisplayIcon(ItemDefinition item)
    {
        if (item is WeaponItemDefinition weapon && weapon.equipmentSlotIcon != null)
            return weapon.equipmentSlotIcon;

        return item != null ? item.icon : null;
    }

    private Sprite GetEquipmentPlaceholderIcon(EquipmentSlotType slotType)
    {
        if (equipmentPlaceholderIconCache.TryGetValue(slotType, out Sprite cachedSprite))
            return cachedSprite;

        string resourcesPath = slotType switch
        {
            EquipmentSlotType.HeadArmor => "UI/SlotPlaceholders/Placeholder_Helmet",
            EquipmentSlotType.ChestArmor => "UI/SlotPlaceholders/Placeholder_ChestRig",
            EquipmentSlotType.Backpack => "UI/SlotPlaceholders/Placeholder_Backpack",
            EquipmentSlotType.SecondaryWeapon => "UI/SlotPlaceholders/Placeholder_Sidearm",
            EquipmentSlotType.PrimaryWeapon => "UI/SlotPlaceholders/Placeholder_PrimaryWeapon",
            _ => null
        };

        Sprite sprite = string.IsNullOrEmpty(resourcesPath)
            ? null
            : LoadStatusIconSprite(resourcesPath);
        equipmentPlaceholderIconCache[slotType] = sprite;
        return sprite;
    }

    private string GetEquipmentSlotLabel(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.PrimaryWeapon => "PRIMARY",
            EquipmentSlotType.SecondaryWeapon => "SECONDARY",
            EquipmentSlotType.HeadArmor => "HEAD",
            EquipmentSlotType.ChestArmor => "CHEST",
            EquipmentSlotType.LegsArmor => "LEGS",
            EquipmentSlotType.FeetArmor => "FEET",
            EquipmentSlotType.QuickUseMedical => "QUICK USE",
            EquipmentSlotType.Backpack => "BACKPACK",
            _ => slotType.ToString().ToUpperInvariant()
        };
    }

    private string GetEquipmentSlotPlaceholder(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.PrimaryWeapon => "Primary Weapon",
            EquipmentSlotType.SecondaryWeapon => "Sidearm",
            EquipmentSlotType.HeadArmor => "Helmet / Headgear",
            EquipmentSlotType.ChestArmor => "Chest Rig / Armor",
            EquipmentSlotType.Backpack => "Backpack",
            _ => "Empty"
        };
    }

    private bool UseInlineEquipmentName(EquipmentSlotType slotType)
    {
        return slotType == EquipmentSlotType.HeadArmor
            || slotType == EquipmentSlotType.ChestArmor
            || slotType == EquipmentSlotType.Backpack;
    }

    private void ApplyEquipmentSlotTextLayout(SlotView slotView, EquipmentSlotType slotType, Vector2 size)
    {
        if (!UseInlineEquipmentName(slotType) || slotView?.itemText == null)
            return;

        slotView.itemText.alignment = TextAnchor.UpperLeft;
        slotView.itemText.fontStyle = FontStyle.Bold;
        slotView.itemText.fontSize = 12;
        slotView.itemText.rectTransform.anchorMin = new Vector2(0f, 1f);
        slotView.itemText.rectTransform.anchorMax = new Vector2(0f, 1f);
        slotView.itemText.rectTransform.pivot = new Vector2(0f, 1f);
        slotView.itemText.rectTransform.anchoredPosition = new Vector2(8f, -8f);
        slotView.itemText.rectTransform.sizeDelta = new Vector2(Mathf.Max(60f, size.x - 16f), 30f);
    }

    private Color GetInventorySlotColor(ItemDefinition item)
    {
        if (item != null)
            return GetValueTierColor(item.valueTier);

        return new Color(0.19f, 0.22f, 0.27f, 0.98f);
    }

    private static Color GetValueTierColor(ItemValueTier valueTier)
    {
        return valueTier switch
        {
            ItemValueTier.Gold => ValueTierGoldColor,
            ItemValueTier.Red => ValueTierRedColor,
            _ => ValueTierBlueColor
        };
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private Color GetEquipmentSlotEmptyColor(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.PrimaryWeapon => new Color(0.14f, 0.14f, 0.16f, 0.98f),
            EquipmentSlotType.SecondaryWeapon => new Color(0.14f, 0.14f, 0.16f, 0.98f),
            EquipmentSlotType.QuickUseMedical => new Color(0.14f, 0.18f, 0.15f, 0.98f),
            _ => new Color(0.14f, 0.14f, 0.16f, 0.98f)
        };
    }

    private Color GetEquipmentSlotFilledColor(EquipmentSlotType slotType, ItemDefinition item)
    {
        return GetInventorySlotColor(item);
    }

    private void ClearContextSelection()
    {
        selectedBackpackSlotIndex = -1;
        selectedEquipmentSlotTypeIndex = -1;
        selectedCorpseEquipmentSlotTypeIndex = -1;
        selectedCarryRuntimeInstanceId = string.Empty;
        selectedPopupRuntimeInstanceId = string.Empty;
        selectedCarryRow = -1;
        selectedCarryColumn = -1;
        selectedCarryRotated = false;
        selectedCarryItem = null;
        selectedPopupRow = -1;
        selectedPopupColumn = -1;
        selectedPopupRotated = false;
        selectedPopupItem = null;
        contextMenuTargetsEquipmentSlot = false;
        contextMenuTargetsCorpseEquipmentSlot = false;
        contextMenuTargetsCarryPlacement = false;
        contextMenuTargetsPopupPlacement = false;
    }

    private bool CanOpenContainerItem(ItemDefinition item, ItemRuntimeData runtimeData)
    {
        if (item == null || runtimeData == null || runtimeData.StoredContainerState == null)
            return false;

        if (item is ContainerItemDefinition)
            return true;

        return item is ArmorItemDefinition armor && armor.providedRigContainer != null;
    }

    private Vector2 GetAdjacentPanelScreenPoint(RectTransform panel)
    {
        if (panel == null)
            return Vector2.zero;

        Vector3[] corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;
        Vector3 topRight = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
        return new Vector2(topRight.x + 8f, topRight.y - 8f);
    }

    private string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength - 3) + "...";
    }

    private string FormatFireMode(WeaponFireModeType fireMode)
    {
        return fireMode switch
        {
            WeaponFireModeType.SemiAutomatic => "SEMI",
            WeaponFireModeType.FullAutomatic => "AUTO",
            WeaponFireModeType.Burst => "BURST",
            WeaponFireModeType.PumpAction => "PUMP",
            WeaponFireModeType.BoltAction => "BOLT",
            _ => fireMode.ToString().ToUpperInvariant()
        };
    }

    private string FormatWeaponCategory(WeaponCategory category)
    {
        return category switch
        {
            WeaponCategory.AssaultRifle => "RIFLE",
            WeaponCategory.SubmachineGun => "SMG",
            WeaponCategory.Shotgun => "SHOTGUN",
            WeaponCategory.SniperRifle => "SNIPER",
            WeaponCategory.Pistol => "SIDEARM",
            WeaponCategory.LightMachineGun => "LMG",
            _ => category.ToString().ToUpperInvariant()
        };
    }

    private Color GetWeaponHudAccentColor(WeaponCategory category)
    {
        return new Color(0.78f, 0.48f, 0.16f, 0.98f);
    }

    private void ResolveReferences()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponent<Canvas>();

        GameObject player = gameplayInput != null ? gameplayInput.gameObject : GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        if (gameplayInput == null)
            gameplayInput = player.GetComponent<PlayerGameplayInput>();

        if (inventory == null)
            inventory = player.GetComponent<PlayerInventory>();

        if (gridInventory == null)
            gridInventory = player.GetComponent<PlayerGridInventory>();

        if (equipment == null)
            equipment = player.GetComponent<PlayerEquipment>();

        if (equipmentVisuals == null)
            equipmentVisuals = player.GetComponent<CharacterEquipmentVisuals>();

        if (quickbar == null)
            quickbar = player.GetComponent<PlayerQuickbar>();

        if (itemUse == null)
            itemUse = player.GetComponent<PlayerItemUse>();

        if (itemDrop == null)
            itemDrop = player.GetComponent<PlayerItemDrop>();

        if (weaponSelection == null)
            weaponSelection = player.GetComponent<PlayerWeaponSelection>();

        if (minimapSystem == null)
            minimapSystem = GetComponent<RuntimeMinimapSystem>();

        if (juCharacter == null)
            juCharacter = player.GetComponent<JUCharacterController>();

        if (juInteractionSystem == null)
            juInteractionSystem = player.GetComponent<JUInteractionSystem>();

        if (juHealth == null)
            juHealth = player.GetComponent<JUHealth>();

        if (playerStats == null)
            playerStats = player.GetComponent<PlayerStats>();

        if (playerRigidbody == null)
            playerRigidbody = player.GetComponent<Rigidbody>();

        if (uiBuilt)
            InitializeOperatorPreview();
    }
}
