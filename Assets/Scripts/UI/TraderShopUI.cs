using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TraderShopUI : MonoBehaviour
{
    private sealed class CartEntry
    {
        public ItemDefinition item;
        public int quantity;
        public bool isSellEntry;
        public GridItemPlacement sourcePlacement;
        public string sourceRuntimeInstanceId;
        public int sourceRow;
        public int sourceColumn;
        public bool sourceRotated;
        public Text quantityText;
        public InputField quantityInput;
    }

    private sealed class GoodsCategory
    {
        public string title;
        public readonly List<ItemDefinition> items = new List<ItemDefinition>();
    }

    private struct GoodsPlacement
    {
        public ItemDefinition item;
        public int row;
        public int column;
    }

    private static readonly Color PanelColor = new Color(0.045f, 0.06f, 0.07f, 0.94f);
    private static readonly Color PanelSoftColor = new Color(0.08f, 0.105f, 0.13f, 0.96f);
    private static readonly Color SlotColor = new Color(0.13f, 0.15f, 0.18f, 0.98f);
    private static readonly Color LineColor = new Color(0.58f, 0.66f, 0.76f, 0.75f);
    private static readonly Color AccentColor = new Color(0.88f, 0.67f, 0.08f, 1f);
    private const int GoodsGridColumns = 10;
    private const float GoodsGridCellSize = 54f;
    private const float GoodsSectionTitleHeight = 24f;
    private const float GoodsSectionGap = 18f;
    private const float GoodsCardInset = 2f;
    private const float SellPriceMultiplier = 0.8f;

    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private RectTransform rootPanel;
    private ScrollRect goodsScroll;
    private RectTransform goodsContent;
    private RectTransform cartDropArea;
    private RectTransform cartRowsRoot;
    private Text cartHintText;
    private RectTransform stashGridFrame;
    private RectTransform stashCellsRoot;
    private RectTransform stashPlacementsRoot;
    private RectTransform dragGhost;
    private Image dragGhostIcon;
    private RectTransform shopContextMenuPanel;
    private Button contextPrimaryButton;
    private Text contextPrimaryText;
    private ItemInspectPanel itemInspectPanel;
    private Text titleText;
    private Text cashText;
    private Text totalText;
    private Text statusText;
    private Button tradeButton;
    private Font uiFont;

    private readonly List<CartEntry> cartEntries = new List<CartEntry>();
    private readonly List<GameObject> dynamicObjects = new List<GameObject>();
    private ShelterTraderStation activeStation;
    private ShelterStashStation activeStashStation;
    private ItemDefinition draggedItem;
    private GridItemPlacement draggedStashPlacement;
    private bool draggingStashPlacement;
    private ItemDefinition contextItem;
    private int contextQuantity = 1;
    private ItemRuntimeData contextRuntimeData;
    private GridItemPlacement contextPlacement;
    private bool contextPrimarySells;
    private GameplayUIRoot gameplayUIRoot;
    private Canvas gameplayCanvas;
    private bool gameplayCanvasWasEnabled;
    private bool gameplayCanvasSuppressed;
    private CrosshairCursor crosshairCursor;
    private bool crosshairCursorWasEnabled;
    private bool crosshairCursorSuppressed;
    private PlayerMove playerMove;
    private PlayerFaceMouse playerFaceMouse;
    private PlayerShoot playerShoot;
    private bool playerMoveWasEnabled;
    private bool playerFaceMouseWasEnabled;
    private bool playerShootWasEnabled;

    public static bool IsAnyOpen { get; private set; }
    public bool IsOpen => rootPanel != null && rootPanel.gameObject.activeSelf;

    void Awake()
    {
        BuildUI();
        SetVisible(false);
    }

    void Update()
    {
        if (!IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            Close();

        if (dragGhost != null && dragGhost.gameObject.activeSelf)
            dragGhost.position = Input.mousePosition;
    }

    public void Open(ShelterTraderStation station)
    {
        if (station == null)
            return;

        activeStation = station;
        activeStation.EnsureCatalog();
        activeStashStation = station.StashStation != null
            ? station.StashStation
            : FindFirstObjectByType<ShelterStashStation>(FindObjectsInactive.Include);

        if (activeStashStation != null)
            activeStashStation.EnsureStashContainer();

        BuildGoodsList();
        RefreshCart();
        RefreshStashPreview();
        SetVisible(true);
        LockPlayerInput(true);
    }

    public void Close()
    {
        SetVisible(false);
        draggedItem = null;
        draggedStashPlacement = null;
        draggingStashPlacement = false;
        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);
        if (shopContextMenuPanel != null)
            shopContextMenuPanel.gameObject.SetActive(false);
        if (itemInspectPanel != null)
            itemInspectPanel.Hide();
        cartEntries.Clear();

        LockPlayerInput(false);
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject canvasObject = new GameObject("TraderShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 170;
        raycaster = canvasObject.GetComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        rootPanel = CreateRect("TraderRoot", canvasObject.transform);
        StretchToParent(rootPanel);
        Image rootImage = rootPanel.gameObject.AddComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.72f);

        titleText = CreateText("Title", rootPanel, "TRADER", 28, TextAnchor.UpperLeft, FontStyle.Bold);
        SetAnchored(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -28f), new Vector2(360f, 38f));

        Button closeButton = CreateButton("Close", rootPanel, "X", 20, FontStyle.Bold);
        SetAnchored(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -28f), new Vector2(42f, 42f));
        closeButton.onClick.AddListener(Close);

        RectTransform goodsPanel = CreatePanel("GoodsPanel", rootPanel, "Goods", new Vector2(0f, 0f), new Vector2(0.34f, 0.92f), new Vector2(0f, 1f), new Vector2(28f, 28f), new Vector2(-10f, -86f));
        RectTransform cartPanel = CreatePanel("CartPanel", rootPanel, "Purchase Order", new Vector2(0.34f, 0f), new Vector2(0.62f, 0.92f), new Vector2(0f, 1f), new Vector2(10f, 28f), new Vector2(-10f, -86f));
        RectTransform stashPanel = CreatePanel("StashPanel", rootPanel, "Your Stash", new Vector2(0.62f, 0f), new Vector2(1f, 0.92f), new Vector2(0f, 1f), new Vector2(10f, 28f), new Vector2(-28f, -86f));

        BuildGoodsPanel(goodsPanel);
        BuildCartPanel(cartPanel);
        BuildStashPanel(stashPanel);
        BuildDragGhost(rootPanel);
        BuildShopContextMenu(rootPanel);
        itemInspectPanel = ItemInspectPanel.Create(rootPanel, canvas, uiFont);
    }

    private void BuildGoodsPanel(RectTransform goodsPanel)
    {
        goodsScroll = CreateScrollRect("GoodsScroll", goodsPanel, out RectTransform viewport, out goodsContent);
        goodsScroll.scrollSensitivity = 70f;
        SetAnchored(goodsScroll.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-4f, -22f), new Vector2(-50f, -64f));

        Scrollbar scrollbar = CreateVerticalScrollbar("GoodsScrollbar", goodsPanel);
        SetAnchored(scrollbar.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-16f, -22f), new Vector2(10f, -64f));
        goodsScroll.verticalScrollbar = scrollbar;
        goodsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        goodsScroll.verticalScrollbarSpacing = 5f;
    }

    private void BuildCartPanel(RectTransform cartPanel)
    {
        cartDropArea = CreateRect("CartDropArea", cartPanel);
        SetAnchored(cartDropArea, new Vector2(0f, 0.24f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(-34f, -92f));
        Image dropImage = cartDropArea.gameObject.AddComponent<Image>();
        dropImage.color = new Color(0.04f, 0.05f, 0.055f, 0.72f);
        AddBorder(cartDropArea, LineColor, 2f);

        cartHintText = CreateText("CartHint", cartDropArea, "Drag goods here", 20, TextAnchor.MiddleCenter, FontStyle.Bold);
        cartHintText.color = new Color(0.85f, 0.88f, 0.92f, 0.36f);
        StretchToParent(cartHintText.rectTransform);

        cartRowsRoot = CreateRect("CartRows", cartDropArea);
        StretchToParent(cartRowsRoot, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        VerticalLayoutGroup rowsLayout = cartRowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = false;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        rowsLayout.spacing = 6f;

        totalText = CreateText("Total", cartPanel, "Total: $0", 22, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetAnchored(totalText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(20f, 140f), new Vector2(-40f, 32f));
        totalText.color = AccentColor;

        statusText = CreateText("Status", cartPanel, string.Empty, 16, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetAnchored(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(20f, 108f), new Vector2(-40f, 24f));
        statusText.color = new Color(0.95f, 0.38f, 0.32f, 0.95f);

        tradeButton = CreateButton("TradeButton", cartPanel, "TRADE", 22, FontStyle.Bold);
        SetAnchored(tradeButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(260f, 54f));
        tradeButton.onClick.AddListener(TryCompleteTrade);
    }

    private void BuildStashPanel(RectTransform stashPanel)
    {
        cashText = CreateText("Cash", stashPanel, "$0", 24, TextAnchor.UpperRight, FontStyle.Bold);
        SetAnchored(cashText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -14f), new Vector2(220f, 36f));

        stashGridFrame = CreateRect("StashGridFrame", stashPanel);
        SetAnchored(stashGridFrame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(420f, 620f));
        Image gridImage = stashGridFrame.gameObject.AddComponent<Image>();
        gridImage.color = new Color(0.08f, 0.10f, 0.12f, 0.85f);
        AddBorder(stashGridFrame, LineColor, 2f);

        stashCellsRoot = CreateRect("Cells", stashGridFrame);
        StretchToParent(stashCellsRoot, Vector2.zero, Vector2.zero);
        stashPlacementsRoot = CreateRect("Placements", stashGridFrame);
        StretchToParent(stashPlacementsRoot, Vector2.zero, Vector2.zero);
    }

    private void BuildDragGhost(RectTransform parent)
    {
        dragGhost = CreateRect("DragGhost", parent);
        dragGhost.sizeDelta = new Vector2(74f, 74f);
        dragGhost.pivot = new Vector2(0.5f, 0.5f);
        Image bg = dragGhost.gameObject.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.20f, 0.24f, 0.78f);
        AddBorder(dragGhost, AccentColor, 2f);

        RectTransform iconRect = CreateRect("Icon", dragGhost);
        StretchToParent(iconRect, new Vector2(5f, 5f), new Vector2(-5f, -5f));
        dragGhostIcon = iconRect.gameObject.AddComponent<Image>();
        dragGhostIcon.color = Color.white;
        dragGhostIcon.preserveAspect = true;
        dragGhostIcon.raycastTarget = false;
        dragGhost.gameObject.SetActive(false);
    }

    private void BuildShopContextMenu(RectTransform parent)
    {
        shopContextMenuPanel = CreateRect("ShopContextMenu", parent);
        shopContextMenuPanel.pivot = new Vector2(0f, 1f);
        shopContextMenuPanel.sizeDelta = new Vector2(122f, 64f);
        Image background = shopContextMenuPanel.gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.04f, 0.045f, 0.98f);
        AddBorder(shopContextMenuPanel, new Color(0.70f, 0.76f, 0.84f, 0.42f), 1f);

        VerticalLayoutGroup layout = shopContextMenuPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Button inspectButton = CreateButton("Inspect", shopContextMenuPanel, "Inspect", 12, FontStyle.Bold);
        LayoutElement inspectLayout = inspectButton.gameObject.AddComponent<LayoutElement>();
        inspectLayout.preferredHeight = 26f;
        inspectLayout.flexibleHeight = 0f;
        inspectButton.onClick.AddListener(InspectContextItem);

        contextPrimaryButton = CreateButton("ContextPrimary", shopContextMenuPanel, "Add", 12, FontStyle.Bold);
        LayoutElement primaryLayout = contextPrimaryButton.gameObject.AddComponent<LayoutElement>();
        primaryLayout.preferredHeight = 26f;
        primaryLayout.flexibleHeight = 0f;
        contextPrimaryText = contextPrimaryButton.GetComponentInChildren<Text>();
        contextPrimaryButton.onClick.AddListener(ExecuteContextPrimaryAction);

        shopContextMenuPanel.gameObject.SetActive(false);
    }

    private void BuildGoodsList()
    {
        ClearChildren(goodsContent);
        if (activeStation == null || activeStation.StockItems == null)
            return;

        List<GoodsCategory> categories = BuildStockCategories(activeStation.StockItems);
        float y = -8f;
        float gridWidth = GoodsGridColumns * GoodsGridCellSize;

        for (int i = 0; i < categories.Count; i++)
        {
            GoodsCategory category = categories[i];
            if (category == null || category.items.Count == 0)
                continue;

            Text title = CreateText(category.title + "_Title", goodsContent, category.title, 16, TextAnchor.UpperLeft, FontStyle.Bold);
            SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, y), new Vector2(gridWidth, GoodsSectionTitleHeight));
            title.color = new Color(0.92f, 0.95f, 0.98f, 0.98f);
            y -= GoodsSectionTitleHeight;

            List<GoodsPlacement> placements = PackGoodsCategory(category.items, out int usedRows);
            RectTransform sectionFrame = CreateRect(category.title + "_Grid", goodsContent);
            sectionFrame.anchorMin = new Vector2(0f, 1f);
            sectionFrame.anchorMax = new Vector2(0f, 1f);
            sectionFrame.pivot = new Vector2(0f, 1f);
            sectionFrame.anchoredPosition = new Vector2(8f, y);
            sectionFrame.sizeDelta = new Vector2(gridWidth, usedRows * GoodsGridCellSize);
            Image sectionImage = sectionFrame.gameObject.AddComponent<Image>();
            sectionImage.color = new Color(0.075f, 0.088f, 0.105f, 0.46f);
            AddBorder(sectionFrame, new Color(0.55f, 0.62f, 0.70f, 0.36f), 1f);

            CreateGoodsGridCells(sectionFrame, usedRows);
            for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
            {
                GoodsPlacement placement = placements[placementIndex];
                CreateGoodsCard(placement.item, sectionFrame, placement.row, placement.column);
            }

            y -= usedRows * GoodsGridCellSize + GoodsSectionGap;
        }

        goodsContent.sizeDelta = new Vector2(0f, Mathf.Abs(y) + 24f);
    }

    private List<GoodsCategory> BuildStockCategories(IReadOnlyList<ItemDefinition> stock)
    {
        List<GoodsCategory> categories = new List<GoodsCategory>
        {
            new GoodsCategory { title = "Weapons" },
            new GoodsCategory { title = "Ammo" },
            new GoodsCategory { title = "Armor" },
            new GoodsCategory { title = "Containers" },
            new GoodsCategory { title = "Medical" },
            new GoodsCategory { title = "Consumables" }
        };

        for (int i = 0; i < stock.Count; i++)
        {
            ItemDefinition item = stock[i];
            if (!IsBuyableStockItem(item))
                continue;

            GoodsCategory category = FindCategoryForItem(categories, item);
            if (category != null)
                category.items.Add(item);
        }

        for (int i = categories.Count - 1; i >= 0; i--)
        {
            if (categories[i].items.Count == 0)
                categories.RemoveAt(i);
        }

        return categories;
    }

    private GoodsCategory FindCategoryForItem(List<GoodsCategory> categories, ItemDefinition item)
    {
        if (item == null)
            return null;

        string title = item.Type switch
        {
            ItemType.Weapon => "Weapons",
            ItemType.Ammo => "Ammo",
            ItemType.Armor => "Armor",
            ItemType.Container => "Containers",
            ItemType.Medical => "Medical",
            ItemType.Consumable => "Consumables",
            _ => string.Empty
        };

        for (int i = 0; i < categories.Count; i++)
        {
            if (categories[i].title == title)
                return categories[i];
        }

        return null;
    }

    private List<GoodsPlacement> PackGoodsCategory(IReadOnlyList<ItemDefinition> items, out int usedRows)
    {
        List<GoodsPlacement> placements = new List<GoodsPlacement>();
        List<bool[]> occupied = new List<bool[]>();

        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition item = items[i];
            if (item == null)
                continue;

            int rowSpan = Mathf.Max(1, item.inventoryRows);
            int columnSpan = Mathf.Min(GoodsGridColumns, Mathf.Max(1, item.inventoryColumns));
            bool placed = false;

            for (int row = 0; !placed; row++)
            {
                EnsureGoodsRows(occupied, row + rowSpan);
                for (int column = 0; column <= GoodsGridColumns - columnSpan; column++)
                {
                    if (!CanPlaceGoods(occupied, row, column, rowSpan, columnSpan))
                        continue;

                    MarkGoodsCells(occupied, row, column, rowSpan, columnSpan);
                    placements.Add(new GoodsPlacement { item = item, row = row, column = column });
                    placed = true;
                    break;
                }
            }
        }

        usedRows = Mathf.Max(1, occupied.Count);
        return placements;
    }

    private static void EnsureGoodsRows(List<bool[]> occupied, int rows)
    {
        while (occupied.Count < rows)
            occupied.Add(new bool[GoodsGridColumns]);
    }

    private static bool CanPlaceGoods(List<bool[]> occupied, int row, int column, int rowSpan, int columnSpan)
    {
        for (int r = row; r < row + rowSpan; r++)
        {
            for (int c = column; c < column + columnSpan; c++)
            {
                if (occupied[r][c])
                    return false;
            }
        }

        return true;
    }

    private static void MarkGoodsCells(List<bool[]> occupied, int row, int column, int rowSpan, int columnSpan)
    {
        for (int r = row; r < row + rowSpan; r++)
        {
            for (int c = column; c < column + columnSpan; c++)
                occupied[r][c] = true;
        }
    }

    private void CreateGoodsGridCells(RectTransform parent, int rows)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < GoodsGridColumns; column++)
            {
                RectTransform cell = CreateRect($"Cell_{row}_{column}", parent);
                cell.anchorMin = new Vector2(0f, 1f);
                cell.anchorMax = new Vector2(0f, 1f);
                cell.pivot = new Vector2(0f, 1f);
                cell.anchoredPosition = new Vector2(column * GoodsGridCellSize, -(row * GoodsGridCellSize));
                cell.sizeDelta = new Vector2(GoodsGridCellSize, GoodsGridCellSize);
                Image image = cell.gameObject.AddComponent<Image>();
                image.color = new Color(0.10f, 0.12f, 0.145f, 0.70f);
                image.raycastTarget = false;
                AddBorder(cell, new Color(0.19f, 0.24f, 0.30f, 0.52f), 1f);
            }
        }
    }

    private void CreateGoodsCard(ItemDefinition item, RectTransform parent, int row, int column)
    {
        int rowSpan = Mathf.Max(1, item.inventoryRows);
        int columnSpan = Mathf.Min(GoodsGridColumns, Mathf.Max(1, item.inventoryColumns));
        RectTransform card = CreateRect(item.itemId, parent);
        card.anchorMin = new Vector2(0f, 1f);
        card.anchorMax = new Vector2(0f, 1f);
        card.pivot = new Vector2(0f, 1f);
        card.anchoredPosition = new Vector2(column * GoodsGridCellSize + GoodsCardInset, -(row * GoodsGridCellSize + GoodsCardInset));
        card.sizeDelta = new Vector2(columnSpan * GoodsGridCellSize - GoodsCardInset * 2f, rowSpan * GoodsGridCellSize - GoodsCardInset * 2f);

        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = GetTierColor(item.valueTier);
        AddBorder(card, new Color(0.75f, 0.82f, 0.92f, 0.46f), 1f);

        RectTransform iconRect = CreateRect("Icon", card);
        StretchToParent(iconRect, new Vector2(5f, 16f), new Vector2(-5f, -12f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = item.GetGridInventorySpriteOrFallback();
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        if (item.ShouldFlipGridDisplaySprite())
            icon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        Text price = CreateText("Price", card, FormatMoney(item.moneyValue), 13, TextAnchor.UpperLeft, FontStyle.Bold);
        SetAnchored(price.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(6f, -4f), new Vector2(-8f, 20f));
        price.color = AccentColor;

        Text name = CreateText("Name", card, Shorten(GetCompactItemName(item), 18), 11, TextAnchor.LowerLeft, FontStyle.Bold);
        SetAnchored(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(6f, 5f), new Vector2(-6f, 20f));

        EventTrigger trigger = card.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerClick, data => OnGoodsCardClicked(item, (PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.Scroll, data => ForwardGoodsScroll((PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.BeginDrag, data => BeginDragItem(item, (PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.Drag, data => DragItem((PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.EndDrag, data => EndDragItem((PointerEventData)data));
    }

    private void AddToCart(ItemDefinition item, int amount)
    {
        if (!IsBuyableStockItem(item) || amount <= 0)
            return;

        CartEntry entry = cartEntries.Find(candidate => candidate.item == item && !candidate.isSellEntry);
        if (entry == null)
        {
            entry = new CartEntry { item = item, quantity = 0, isSellEntry = false };
            cartEntries.Add(entry);
        }

        entry.quantity = Mathf.Clamp(entry.quantity + amount, 1, 999);
        RefreshCart();
    }

    private void AddSellToCart(GridItemPlacement placement)
    {
        if (placement == null || placement.IsEmpty || !CanSellItem(placement.Item))
            return;

        if (cartEntries.Exists(entry => entry != null && entry.isSellEntry && IsSameSourcePlacement(entry, placement)))
        {
            SetStatus("Item is already in the trade order.");
            return;
        }

        cartEntries.Add(new CartEntry
        {
            item = placement.Item,
            quantity = placement.Quantity,
            isSellEntry = true,
            sourcePlacement = placement,
            sourceRuntimeInstanceId = placement.RuntimeInstanceId,
            sourceRow = placement.Row,
            sourceColumn = placement.Column,
            sourceRotated = placement.Rotated
        });

        RefreshCart();
    }

    private void RefreshCart()
    {
        ClearChildren(cartRowsRoot);

        for (int i = 0; i < cartEntries.Count; i++)
        {
            CartEntry entry = cartEntries[i];
            if (entry == null || entry.item == null || entry.quantity <= 0)
                continue;

            CreateCartRow(entry);
        }

        totalText.text = BuildBalanceLine();
        if (cartHintText != null)
            cartHintText.gameObject.SetActive(cartEntries.Count == 0);
        UpdateTradeButton();
    }

    private void CreateCartRow(CartEntry entry)
    {
        RectTransform row = CreateRect(entry.item.itemId + "_CartRow", cartRowsRoot);
        row.sizeDelta = new Vector2(0f, 52f);
        LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 52f;

        Image rowImage = row.gameObject.AddComponent<Image>();
        rowImage.color = new Color(0.12f, 0.14f, 0.17f, 0.96f);
        AddBorder(row, new Color(0.7f, 0.78f, 0.88f, 0.22f), 1f);

        RectTransform iconRect = CreateRect("Icon", row);
        SetAnchored(iconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(42f, 42f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = entry.item.GetGridInventorySpriteOrFallback();
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        string compactName = GetCompactItemName(entry.item);
        string rowName = entry.isSellEntry ? "Sell  " + Shorten(compactName, 16) : Shorten(compactName, 18);
        Text name = CreateText("Name", row, rowName, 15, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetAnchored(name.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(-260f, 0f));

        Text price = CreateText("LinePrice", row, FormatMoney(GetEntryDisplayValue(entry)), 14, TextAnchor.MiddleRight, FontStyle.Bold);
        SetAnchored(price.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-46f, 0f), new Vector2(92f, 0f));
        price.color = entry.isSellEntry ? new Color(0.35f, 0.95f, 0.5f, 0.98f) : AccentColor;

        if (!entry.isSellEntry)
        {
            Button minus = CreateButton("Minus", row, "-", 16, FontStyle.Bold);
            SetAnchored(minus.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-222f, 0f), new Vector2(26f, 26f));
            minus.onClick.AddListener(() => ChangeCartQuantity(entry, -1));

            RectTransform inputRect = CreateRect("QuantityInput", row);
            SetAnchored(inputRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-184f, 0f), new Vector2(42f, 28f));
            Image inputImage = inputRect.gameObject.AddComponent<Image>();
            inputImage.color = new Color(0.04f, 0.05f, 0.06f, 0.9f);
            AddBorder(inputRect, new Color(0.6f, 0.67f, 0.75f, 0.5f), 1f);
            InputField input = inputRect.gameObject.AddComponent<InputField>();
            Text inputText = CreateText("Text", inputRect, entry.quantity.ToString(), 14, TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchToParent(inputText.rectTransform);
            input.textComponent = inputText;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.text = entry.quantity.ToString();
            input.onEndEdit.AddListener(value => SetCartQuantityFromInput(entry, value));
            entry.quantityInput = input;

            Button plus = CreateButton("Plus", row, "+", 16, FontStyle.Bold);
            SetAnchored(plus.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-146f, 0f), new Vector2(26f, 26f));
            plus.onClick.AddListener(() => ChangeCartQuantity(entry, 1));
        }
        else
        {
            Text quantity = CreateText("SellQuantity", row, "x" + entry.quantity, 14, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetAnchored(quantity.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-184f, 0f), new Vector2(76f, 28f));
            quantity.color = new Color(0.72f, 0.95f, 0.74f, 0.98f);
        }

        Button remove = CreateButton("Remove", row, "X", 14, FontStyle.Bold);
        SetAnchored(remove.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(26f, 26f));
        remove.onClick.AddListener(() => RemoveCartEntry(entry));
    }

    private void ChangeCartQuantity(CartEntry entry, int delta)
    {
        if (entry == null)
            return;

        entry.quantity += delta;
        if (entry.quantity <= 0)
            cartEntries.Remove(entry);
        else
            entry.quantity = Mathf.Clamp(entry.quantity, 1, 999);

        RefreshCart();
    }

    private void SetCartQuantityFromInput(CartEntry entry, string value)
    {
        if (entry == null)
            return;

        if (!int.TryParse(value, out int parsed))
            parsed = entry.quantity;

        entry.quantity = Mathf.Clamp(parsed, 1, 999);
        RefreshCart();
    }

    private void RemoveCartEntry(CartEntry entry)
    {
        if (entry != null)
            cartEntries.Remove(entry);

        RefreshCart();
    }

    private void TryCompleteTrade()
    {
        if (activeStashStation == null || activeStashStation.StashContainer == null)
        {
            SetStatus("No stash found.");
            return;
        }

        SearchableContainer stashContainer = activeStashStation.StashContainer;
        stashContainer.EnsureInitialized();
        GridContainerState stashState = stashContainer.ContainerState;
        if (stashState == null)
        {
            SetStatus("Stash is unavailable.");
            return;
        }

        if (cartEntries.Count == 0)
        {
            SetStatus("Cart is empty.");
            return;
        }

        ItemDefinition currency = activeStation != null ? activeStation.CurrencyItem : null;
        if (currency == null)
        {
            SetStatus("Currency item is missing.");
            return;
        }

        int buyCost = Mathf.CeilToInt(GetBuyTotal());
        int sellRevenue = Mathf.FloorToInt(GetSellTotal());
        int cash = currency != null ? stashState.GetQuantity(currency, true) : 0;
        int netCost = Mathf.Max(0, buyCost - sellRevenue);
        if (cash < netCost)
        {
            SetStatus("Not enough cash in stash.");
            return;
        }

        GridContainerState simulation = stashState.DeepClone();
        for (int i = 0; i < cartEntries.Count; i++)
        {
            CartEntry entry = cartEntries[i];
            if (entry == null || !entry.isSellEntry)
                continue;

            GridItemPlacement simulatedPlacement = FindMatchingPlacement(simulation, entry);
            if (simulatedPlacement == null || !simulation.TryRemovePlacement(simulatedPlacement))
            {
                SetStatus("Sale item is no longer in stash.");
                return;
            }
        }

        if (sellRevenue > 0 && !simulation.TryAddItem(currency, sellRevenue, out _))
        {
            SetStatus("Not enough stash space for cash.");
            return;
        }

        if (buyCost > 0 && simulation.RemoveItemIncludingNested(currency, buyCost) < buyCost)
        {
            SetStatus("Not enough cash in stash.");
            return;
        }

        for (int i = 0; i < cartEntries.Count; i++)
        {
            CartEntry entry = cartEntries[i];
            if (entry?.item == null || entry.quantity <= 0 || entry.isSellEntry)
                continue;

            if (!simulation.TryAddItem(entry.item, entry.quantity, out _))
            {
                SetStatus("Not enough stash space.");
                return;
            }
        }

        for (int i = 0; i < cartEntries.Count; i++)
        {
            CartEntry entry = cartEntries[i];
            if (entry == null || !entry.isSellEntry)
                continue;

            GridItemPlacement placement = FindMatchingPlacement(stashState, entry);
            if (placement != null)
                stashState.TryRemovePlacement(placement);
        }

        if (sellRevenue > 0)
            stashState.TryAddItem(currency, sellRevenue, out _);

        if (buyCost > 0)
            stashState.RemoveItemIncludingNested(currency, buyCost);

        for (int i = 0; i < cartEntries.Count; i++)
        {
            CartEntry entry = cartEntries[i];
            if (entry?.item == null || entry.quantity <= 0 || entry.isSellEntry)
                continue;

            stashState.TryAddItem(entry.item, entry.quantity, out _);
        }

        cartEntries.Clear();
        SetStatus("Trade complete.", new Color(0.35f, 0.95f, 0.5f, 0.98f));
        RefreshCart();
        RefreshStashPreview();
    }

    private void RefreshStashPreview()
    {
        ClearChildren(stashCellsRoot);
        ClearChildren(stashPlacementsRoot);

        GridContainerState state = activeStashStation != null && activeStashStation.StashContainer != null
            ? activeStashStation.StashContainer.ContainerState
            : null;

        ItemDefinition currency = activeStation != null ? activeStation.CurrencyItem : null;
        int cash = state != null && currency != null ? state.GetQuantity(currency, true) : 0;
        if (cashText != null)
            cashText.text = FormatCashShort(cash);

        if (state == null || stashGridFrame == null)
            return;

        int rows = Mathf.Max(1, state.RowCount);
        int columns = Mathf.Max(1, state.ColumnCount);
        float maxWidth = 520f;
        float maxHeight = 650f;
        float cell = Mathf.Floor(Mathf.Min(48f, maxWidth / columns, maxHeight / rows));
        cell = Mathf.Max(24f, cell);

        stashGridFrame.sizeDelta = new Vector2(columns * cell, rows * cell);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                RectTransform cellRect = CreateRect($"Cell_{row}_{column}", stashCellsRoot);
                cellRect.anchorMin = new Vector2(0f, 1f);
                cellRect.anchorMax = new Vector2(0f, 1f);
                cellRect.pivot = new Vector2(0f, 1f);
                cellRect.anchoredPosition = new Vector2(column * cell, -(row * cell));
                cellRect.sizeDelta = new Vector2(cell, cell);
                Image image = cellRect.gameObject.AddComponent<Image>();
                image.color = new Color(0.10f, 0.12f, 0.145f, 0.72f);
                AddBorder(cellRect, new Color(0.20f, 0.25f, 0.31f, 0.68f), 1f);
            }
        }

        IReadOnlyList<GridItemPlacement> placements = state.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            CreateStashPlacementView(placement, cell);
        }
    }

    private void CreateStashPlacementView(GridItemPlacement placement, float cell)
    {
        RectTransform rect = CreateRect(placement.Item.itemId + "_Stash", stashPlacementsRoot);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(placement.Column * cell + 1f, -(placement.Row * cell + 1f));
        rect.sizeDelta = new Vector2((placement.ColumnSpan * cell) - 2f, (placement.RowSpan * cell) - 2f);

        Image background = rect.gameObject.AddComponent<Image>();
        background.color = GetTierColor(placement.Item.valueTier);
        AddBorder(rect, new Color(0.78f, 0.86f, 0.96f, 0.58f), 1f);

        RectTransform iconRect = CreateRect("Icon", rect);
        StretchToParent(iconRect, new Vector2(4f, 16f), new Vector2(-4f, -4f));
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = placement.Item.GetGridInventorySpriteOrFallback();
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        if (placement.Item.ShouldFlipGridDisplaySprite())
            icon.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        Text name = CreateText("Name", rect, Shorten(GetCompactItemName(placement.Item), 14), 10, TextAnchor.UpperLeft, FontStyle.Bold);
        SetAnchored(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(4f, -2f), new Vector2(-4f, 14f));

        if (placement.Quantity > 1)
        {
            Text quantity = CreateText("Quantity", rect, placement.Quantity.ToString(), 12, TextAnchor.LowerRight, FontStyle.Bold);
            StretchToParent(quantity.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        }

        EventTrigger trigger = rect.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerClick, data => OnStashPlacementClicked(placement, (PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.BeginDrag, data => BeginDragStashPlacement(placement, (PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.Drag, data => DragItem((PointerEventData)data));
        AddTrigger(trigger, EventTriggerType.EndDrag, data => EndDragItem((PointerEventData)data));
    }

    private void UpdateTradeButton()
    {
        if (tradeButton == null)
            return;

        bool hasCart = cartEntries.Count > 0;
        bool canTrade = hasCart && HasEnoughCashForTrade();
        tradeButton.interactable = canTrade;

        if (!hasCart)
            SetStatus(string.Empty);
        else if (!canTrade)
            SetStatus("Not enough cash in stash.");
        else
            SetStatus(string.Empty);
    }

    private bool HasEnoughCashForTrade()
    {
        if (activeStation == null || activeStashStation == null || activeStashStation.StashContainer == null)
            return false;

        ItemDefinition currency = activeStation.CurrencyItem;
        if (currency == null)
            return false;

        GridContainerState state = activeStashStation.StashContainer.ContainerState;
        int cash = state != null ? state.GetQuantity(currency, true) : 0;
        int buyCost = Mathf.CeilToInt(GetBuyTotal());
        int sellRevenue = Mathf.FloorToInt(GetSellTotal());
        return cash >= Mathf.Max(0, buyCost - sellRevenue);
    }

    private float GetBuyTotal()
    {
        float total = 0f;
        for (int i = 0; i < cartEntries.Count; i++)
        {
            CartEntry entry = cartEntries[i];
            if (entry?.item != null && !entry.isSellEntry)
                total += entry.item.GetTotalMoneyValue(entry.quantity);
        }

        return total;
    }

    private float GetSellTotal()
    {
        float total = 0f;
        for (int i = 0; i < cartEntries.Count; i++)
        {
            CartEntry entry = cartEntries[i];
            if (entry?.item != null && entry.isSellEntry)
                total += GetEntryDisplayValue(entry);
        }

        return total;
    }

    private float GetEntryDisplayValue(CartEntry entry)
    {
        if (entry?.item == null)
            return 0f;

        float value = entry.item.GetTotalMoneyValue(entry.quantity);
        return entry.isSellEntry ? value * SellPriceMultiplier : value;
    }

    private string BuildBalanceLine()
    {
        int cash = 0;
        if (activeStation != null && activeStashStation != null && activeStashStation.StashContainer != null && activeStation.CurrencyItem != null)
        {
            GridContainerState state = activeStashStation.StashContainer.ContainerState;
            if (state != null)
                cash = state.GetQuantity(activeStation.CurrencyItem, true);
        }

        int buyCost = Mathf.CeilToInt(GetBuyTotal());
        int sellRevenue = Mathf.FloorToInt(GetSellTotal());
        int netCost = buyCost - sellRevenue;

        if (buyCost <= 0 && sellRevenue <= 0)
            return $"Balance: {FormatCashShort(cash)}";

        string tradeSummary = buyCost > 0 && sellRevenue > 0
            ? $"Buy {FormatMoney(buyCost)} | Sell {FormatMoney(sellRevenue)}"
            : buyCost > 0
                ? $"Buy {FormatMoney(buyCost)}"
                : $"Sell {FormatMoney(sellRevenue)}";

        int projectedCash = netCost >= 0 ? Mathf.Max(0, cash - netCost) : cash + Mathf.Abs(netCost);
        return $"{tradeSummary}    Balance: {FormatCashShort(cash)} -> {FormatCashShort(projectedCash)}";
    }

    private void OnStashPlacementClicked(GridItemPlacement placement, PointerEventData eventData)
    {
        if (placement == null || placement.IsEmpty || eventData.button != PointerEventData.InputButton.Right)
            return;

        ShowItemContext(placement.Item, placement.Quantity, placement.RuntimeData, placement, true, eventData.position);
    }

    private void ShowItemContext(
        ItemDefinition item,
        int quantity,
        ItemRuntimeData runtimeData,
        GridItemPlacement placement,
        bool sellContext,
        Vector2 screenPosition)
    {
        if (shopContextMenuPanel == null || item == null)
            return;

        contextItem = item;
        contextQuantity = Mathf.Max(1, quantity);
        contextRuntimeData = runtimeData;
        contextPlacement = placement;
        contextPrimarySells = sellContext;

        if (contextPrimaryText != null)
            contextPrimaryText.text = sellContext ? "Sell" : "Add to Cart";

        if (contextPrimaryButton != null)
            contextPrimaryButton.gameObject.SetActive(!sellContext || CanSellItem(item));

        PositionContextMenu(screenPosition);
        shopContextMenuPanel.SetAsLastSibling();
        shopContextMenuPanel.gameObject.SetActive(true);
    }

    private void InspectContextItem()
    {
        if (itemInspectPanel != null && contextItem != null)
            itemInspectPanel.Show(contextItem, contextQuantity, contextRuntimeData);

        if (shopContextMenuPanel != null)
            shopContextMenuPanel.gameObject.SetActive(false);
    }

    private void ExecuteContextPrimaryAction()
    {
        if (contextPrimarySells)
            AddSellToCart(contextPlacement);
        else
            AddToCart(contextItem, 1);

        if (shopContextMenuPanel != null)
            shopContextMenuPanel.gameObject.SetActive(false);
    }

    private void PositionContextMenu(Vector2 screenPosition)
    {
        if (shopContextMenuPanel == null || rootPanel == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootPanel, screenPosition, null, out Vector2 localPosition);
        Rect rootRect = rootPanel.rect;
        Vector2 size = shopContextMenuPanel.sizeDelta;
        localPosition.x = Mathf.Clamp(localPosition.x, rootRect.xMin + 8f, rootRect.xMax - size.x - 8f);
        localPosition.y = Mathf.Clamp(localPosition.y, rootRect.yMin + size.y + 8f, rootRect.yMax - 8f);

        shopContextMenuPanel.anchorMin = new Vector2(0.5f, 0.5f);
        shopContextMenuPanel.anchorMax = new Vector2(0.5f, 0.5f);
        shopContextMenuPanel.anchoredPosition = localPosition;
    }

    private void BeginDragItem(ItemDefinition item, PointerEventData eventData)
    {
        draggingStashPlacement = false;
        draggedStashPlacement = null;
        draggedItem = item;
        if (dragGhost == null || dragGhostIcon == null || item == null)
            return;

        dragGhostIcon.sprite = item.GetGridInventorySpriteOrFallback();
        dragGhostIcon.enabled = dragGhostIcon.sprite != null;
        dragGhostIcon.rectTransform.localScale = item.ShouldFlipGridDisplaySprite()
            ? new Vector3(-1f, 1f, 1f)
            : Vector3.one;
        dragGhost.position = eventData.position;
        dragGhost.gameObject.SetActive(true);
        dragGhost.SetAsLastSibling();
    }

    private void BeginDragStashPlacement(GridItemPlacement placement, PointerEventData eventData)
    {
        if (placement == null || placement.IsEmpty || !CanSellItem(placement.Item))
            return;

        draggingStashPlacement = true;
        draggedStashPlacement = placement;
        draggedItem = placement.Item;
        if (dragGhost == null || dragGhostIcon == null)
            return;

        dragGhostIcon.sprite = placement.Item.GetGridInventorySpriteOrFallback();
        dragGhostIcon.enabled = dragGhostIcon.sprite != null;
        dragGhostIcon.rectTransform.localScale = placement.Item.ShouldFlipGridDisplaySprite()
            ? new Vector3(-1f, 1f, 1f)
            : Vector3.one;
        dragGhost.position = eventData.position;
        dragGhost.gameObject.SetActive(true);
        dragGhost.SetAsLastSibling();
    }

    private void DragItem(PointerEventData eventData)
    {
        if (dragGhost != null)
            dragGhost.position = eventData.position;
    }

    private void EndDragItem(PointerEventData eventData)
    {
        if (dragGhost != null)
            dragGhost.gameObject.SetActive(false);

        if (IsPointerOverRect(cartDropArea, eventData.position))
        {
            if (draggingStashPlacement)
                AddSellToCart(draggedStashPlacement);
            else if (draggedItem != null)
                AddToCart(draggedItem, 1);
        }

        draggingStashPlacement = false;
        draggedStashPlacement = null;
        draggedItem = null;
    }

    private void OnGoodsCardClicked(ItemDefinition item, PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            ShowItemContext(item, 1, null, null, false, eventData.position);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
            AddToCart(item, 1);
    }

    private void ForwardGoodsScroll(PointerEventData eventData)
    {
        if (goodsScroll == null || eventData == null)
            return;

        goodsScroll.OnScroll(eventData);
    }

    private bool IsPointerOverRect(RectTransform rect, Vector2 screenPosition)
    {
        if (rect == null || canvas == null)
            return false;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera);
    }

    private void SetStatus(string value)
    {
        SetStatus(value, new Color(0.95f, 0.38f, 0.32f, 0.95f));
    }

    private void SetStatus(string value, Color color)
    {
        if (statusText == null)
            return;

        statusText.text = value ?? string.Empty;
        statusText.color = color;
    }

    private void SetVisible(bool visible)
    {
        if (rootPanel != null)
            rootPanel.gameObject.SetActive(visible);

        IsAnyOpen = visible;
        SuppressGameplayUi(visible);
        if (visible)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    private void SuppressGameplayUi(bool suppress)
    {
        if (suppress)
        {
            if (!gameplayCanvasSuppressed)
            {
                if (gameplayUIRoot == null)
                    gameplayUIRoot = FindFirstObjectByType<GameplayUIRoot>(FindObjectsInactive.Include);

                if (gameplayUIRoot != null)
                    gameplayCanvas = gameplayUIRoot.GetComponent<Canvas>();

                if (gameplayCanvas != null)
                {
                    gameplayCanvasWasEnabled = gameplayCanvas.enabled;
                    gameplayCanvas.enabled = false;
                    gameplayCanvasSuppressed = true;
                }
            }

            if (!crosshairCursorSuppressed)
            {
                crosshairCursor = CrosshairCursor.Instance != null
                    ? CrosshairCursor.Instance
                    : FindFirstObjectByType<CrosshairCursor>(FindObjectsInactive.Include);

                if (crosshairCursor != null)
                {
                    crosshairCursorWasEnabled = crosshairCursor.enabled;
                    crosshairCursor.enabled = false;
                    crosshairCursorSuppressed = true;
                }
            }

            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        if (gameplayCanvasSuppressed && gameplayCanvas != null)
            gameplayCanvas.enabled = gameplayCanvasWasEnabled;

        gameplayCanvasSuppressed = false;

        if (crosshairCursorSuppressed && crosshairCursor != null)
        {
            crosshairCursor.enabled = crosshairCursorWasEnabled;
            if (crosshairCursorWasEnabled)
                crosshairCursor.ApplyCursor();
        }

        crosshairCursorSuppressed = false;
    }

    private void LockPlayerInput(bool locked)
    {
        if (locked)
        {
            if (playerMove == null)
                playerMove = FindFirstObjectByType<PlayerMove>(FindObjectsInactive.Exclude);
            if (playerFaceMouse == null)
                playerFaceMouse = FindFirstObjectByType<PlayerFaceMouse>(FindObjectsInactive.Exclude);
            if (playerShoot == null)
                playerShoot = FindFirstObjectByType<PlayerShoot>(FindObjectsInactive.Exclude);

            if (playerMove != null)
            {
                playerMoveWasEnabled = playerMove.enabled;
                playerMove.enabled = false;
            }

            if (playerFaceMouse != null)
            {
                playerFaceMouseWasEnabled = playerFaceMouse.enabled;
                playerFaceMouse.enabled = false;
            }

            if (playerShoot != null)
            {
                playerShootWasEnabled = playerShoot.enabled;
                playerShoot.enabled = false;
            }
        }
        else
        {
            if (playerMove != null)
                playerMove.enabled = playerMoveWasEnabled;

            if (playerFaceMouse != null)
                playerFaceMouse.enabled = playerFaceMouseWasEnabled;

            if (playerShoot != null)
                playerShoot.enabled = playerShootWasEnabled;
        }
    }

    private bool IsBuyableStockItem(ItemDefinition item)
    {
        return item != null
            && item.Type != ItemType.Loot
            && item.Type != ItemType.Currency
            && !IsDebugItem(item);
    }

    private bool CanSellItem(ItemDefinition item)
    {
        return item != null && item.Type != ItemType.Currency;
    }

    private bool IsSameSourcePlacement(CartEntry entry, GridItemPlacement placement)
    {
        if (entry == null || placement == null || placement.IsEmpty)
            return false;

        if (entry.sourcePlacement == placement)
            return true;

        if (!string.IsNullOrEmpty(entry.sourceRuntimeInstanceId) && entry.sourceRuntimeInstanceId == placement.RuntimeInstanceId)
            return true;

        return string.IsNullOrEmpty(entry.sourceRuntimeInstanceId)
            && string.IsNullOrEmpty(placement.RuntimeInstanceId)
            && entry.item == placement.Item
            && entry.quantity == placement.Quantity
            && entry.sourceRow == placement.Row
            && entry.sourceColumn == placement.Column
            && entry.sourceRotated == placement.Rotated;
    }

    private GridItemPlacement FindMatchingPlacement(GridContainerState container, CartEntry entry)
    {
        if (container == null || entry == null || entry.item == null)
            return null;

        IReadOnlyList<GridItemPlacement> placements = container.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty || placement.Item != entry.item)
                continue;

            if (!string.IsNullOrEmpty(entry.sourceRuntimeInstanceId) && entry.sourceRuntimeInstanceId == placement.RuntimeInstanceId)
                return placement;

            if (string.IsNullOrEmpty(entry.sourceRuntimeInstanceId)
                && string.IsNullOrEmpty(placement.RuntimeInstanceId)
                && placement.Quantity == entry.quantity
                && placement.Row == entry.sourceRow
                && placement.Column == entry.sourceColumn
                && placement.Rotated == entry.sourceRotated)
            {
                return placement;
            }
        }

        return null;
    }

    private static bool IsDebugItem(ItemDefinition item)
    {
        if (item == null)
            return true;

        return (!string.IsNullOrWhiteSpace(item.itemId) && item.itemId.ToLowerInvariant().Contains("debug"))
            || item.name.ToLowerInvariant().Contains("debug");
    }

    private static string GetCompactItemName(ItemDefinition item)
    {
        if (item == null)
            return string.Empty;

        if (item.Type == ItemType.Currency)
            return "US";

        if (item is AmmoItemDefinition ammo)
        {
            string label = !string.IsNullOrWhiteSpace(ammo.ammoCategory)
                ? ammo.ammoCategory
                : item.displayName;

            return CompactAmmoName(label);
        }

        string id = item.itemId != null ? item.itemId.ToLowerInvariant() : string.Empty;
        string display = item.displayName != null ? item.displayName.ToLowerInvariant() : string.Empty;

        if (item.Type == ItemType.Medical)
        {
            if (id.Contains("bandage") || display.Contains("bandage"))
                return "Band.";

            if (id.Contains("medkit") || display.Contains("medkit"))
                return "Medkit";
        }

        if (item.Type == ItemType.Consumable)
        {
            if (id.Contains("food") || display.Contains("food") || display.Contains("canned"))
                return "Food";

            if (id.Contains("water") || display.Contains("water"))
                return "Water";
        }

        return item.displayName;
    }

    private static string CompactAmmoName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string compact = value.Trim();
        compact = compact.Replace("×", "*");
        compact = compact.Replace("x", "*");
        compact = compact.Replace("X", "*");
        compact = compact.Replace(" mm", string.Empty);
        compact = compact.Replace("MM", string.Empty);
        compact = compact.Replace("mm", string.Empty);
        compact = compact.Replace("毫米", string.Empty);

        int firstDigit = -1;
        int lastAllowed = -1;
        for (int i = 0; i < compact.Length; i++)
        {
            char ch = compact[i];
            bool allowed = char.IsDigit(ch) || ch == '.' || ch == '*' || ch == '-';
            if (allowed)
            {
                if (firstDigit < 0 && char.IsDigit(ch))
                    firstDigit = i;
                lastAllowed = i;
            }
        }

        if (firstDigit >= 0 && lastAllowed >= firstDigit)
            compact = compact.Substring(firstDigit, lastAllowed - firstDigit + 1);

        return compact.Trim();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private Scrollbar CreateVerticalScrollbar(string objectName, RectTransform parent)
    {
        RectTransform trackRect = CreateRect(objectName, parent);
        Image trackImage = trackRect.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0.045f, 0.055f, 0.07f, 0.82f);

        Scrollbar scrollbar = trackRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform handleRect = CreateRect("Handle", trackRect);
        StretchToParent(handleRect, new Vector2(1f, 1f), new Vector2(-1f, -1f));
        Image handleImage = handleRect.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.70f, 0.76f, 0.84f, 0.82f);

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

        ColorBlock colors = scrollbar.colors;
        colors.normalColor = handleImage.color;
        colors.highlightedColor = new Color(0.88f, 0.92f, 0.98f, 0.95f);
        colors.pressedColor = AccentColor;
        colors.disabledColor = new Color(0.18f, 0.20f, 0.23f, 0.35f);
        scrollbar.colors = colors;
        return scrollbar;
    }

    private ScrollRect CreateScrollRect(string objectName, RectTransform parent, out RectTransform viewport, out RectTransform content)
    {
        RectTransform scrollRect = CreateRect(objectName, parent);
        ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        viewport = CreateRect("Viewport", scrollRect);
        StretchToParent(viewport);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.05f);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        scroll.viewport = viewport;
        scroll.content = content;
        return scroll;
    }

    private RectTransform CreatePanel(
        string objectName,
        RectTransform parent,
        string title,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        RectTransform panel = CreateRect(objectName, parent);
        panel.anchorMin = anchorMin;
        panel.anchorMax = anchorMax;
        panel.pivot = pivot;
        panel.offsetMin = offsetMin;
        panel.offsetMax = offsetMax;

        Image image = panel.gameObject.AddComponent<Image>();
        image.color = PanelColor;

        Text titleLabel = CreateText("Title", panel, title, 20, TextAnchor.UpperLeft, FontStyle.Bold);
        SetAnchored(titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(-18f, 30f));
        return panel;
    }

    private Button CreateButton(string objectName, Transform parent, string text, int fontSize, FontStyle style)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = objectName == "TradeButton" ? AccentColor : PanelSoftColor;

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.disabledColor = new Color(0.30f, 0.25f, 0.11f, 0.55f);
        button.colors = colors;

        Text label = CreateText("Text", rect, text, fontSize, TextAnchor.MiddleCenter, style);
        StretchToParent(label.rectTransform);
        label.color = objectName == "TradeButton" ? new Color(0.06f, 0.05f, 0.03f, 1f) : Color.white;
        return button;
    }

    private Text CreateText(string objectName, Transform parent, string text, int size, TextAnchor alignment, FontStyle style)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Text uiText = rect.gameObject.AddComponent<Text>();
        uiText.font = uiFont;
        uiText.text = text;
        uiText.fontSize = size;
        uiText.alignment = alignment;
        uiText.fontStyle = style;
        uiText.color = new Color(0.94f, 0.96f, 0.98f, 0.98f);
        uiText.raycastTarget = false;
        uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;
        return uiText;
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private static void StretchToParent(RectTransform rect)
    {
        StretchToParent(rect, Vector2.zero, Vector2.zero);
    }

    private static void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private void AddBorder(RectTransform parent, Color color, float thickness)
    {
        AddBorderLine("Top", parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        AddBorderLine("Bottom", parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, thickness));
        AddBorderLine("Left", parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), Vector2.zero, new Vector2(thickness, 0f));
        AddBorderLine("Right", parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-thickness, 0f), Vector2.zero);
    }

    private void AddBorderLine(string objectName, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform line = CreateRect(objectName, parent);
        line.anchorMin = anchorMin;
        line.anchorMax = anchorMax;
        line.pivot = pivot;
        line.offsetMin = offsetMin;
        line.offsetMax = offsetMax;
        Image image = line.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static Color GetTierColor(ItemValueTier tier)
    {
        return tier switch
        {
            ItemValueTier.Gold => new Color(0.82f, 0.63f, 0.06f, 0.92f),
            ItemValueTier.Red => new Color(0.46f, 0.08f, 0.10f, 0.92f),
            _ => new Color(0.12f, 0.25f, 0.39f, 0.92f)
        };
    }

    private static string FormatMoney(float value)
    {
        if (value >= 1000f)
            return "$" + (value / 1000f).ToString("0.#") + "k";

        if (Mathf.Abs(value - Mathf.Round(value)) > 0.01f)
            return "$" + value.ToString("0.#");

        return "$" + Mathf.RoundToInt(value).ToString();
    }

    private static string FormatCashShort(int value)
    {
        if (value >= 1000000)
            return "$ " + (value / 1000000f).ToString("0.#") + "M";
        if (value >= 1000)
            return "$ " + (value / 1000f).ToString("0.#") + "k";
        return "$ " + value.ToString();
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value.Substring(0, Mathf.Max(1, maxLength - 3)) + "...";
    }

    private static void ClearChildren(RectTransform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }
}
