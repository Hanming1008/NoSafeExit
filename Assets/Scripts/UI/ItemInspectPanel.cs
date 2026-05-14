using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class ItemInspectPanel : MonoBehaviour
{
    private const int PreviewLayer = 31;
    private static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.06f, 0.96f);
    private static readonly Color SurfaceColor = new Color(0.03f, 0.04f, 0.05f, 0.84f);
    private static readonly Color LineColor = new Color(0.38f, 0.43f, 0.50f, 0.58f);

    private RectTransform panelRect;
    private RectTransform previewRect;
    private RawImage previewImage;
    private Text titleText;
    private Text typeText;
    private Text priceText;
    private Text weightText;
    private RectTransform infoRowsRoot;
    private Text fallbackText;
    private Button closeButton;
    private Camera previewCamera;
    private Light keyLight;
    private Light fillLight;
    private RenderTexture previewTexture;
    private GameObject modelRoot;
    private ItemDefinition currentItem;
    private int currentQuantity = 1;
    private ItemRuntimeData currentRuntimeData;
    private Font uiFont;
    private Canvas rootCanvas;
    private float zoomMultiplier = 1f;
    private Vector2 rotationAngles = new Vector2(-18f, -28f);
    private bool draggingPreview;
    private Vector2 lastPointerPosition;
    private float nextInfoRefreshTime;

    private struct InfoAttribute
    {
        public readonly string label;
        public readonly string value;

        public InfoAttribute(string label, string value)
        {
            this.label = label;
            this.value = value;
        }
    }

    public bool IsOpen => panelRect != null && panelRect.gameObject.activeSelf;

    public static ItemInspectPanel Create(RectTransform parent, Canvas canvas, Font font)
    {
        if (parent == null)
            return null;

        GameObject panelObject = new GameObject("ItemInspectPanel", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);
        ItemInspectPanel panel = panelObject.AddComponent<ItemInspectPanel>();
        panel.rootCanvas = canvas;
        panel.uiFont = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        panel.BuildUi();
        panel.Hide();
        return panel;
    }

    void Update()
    {
        if (!IsOpen)
            return;

        HandlePreviewInput();
        RefreshDynamicInfoIfNeeded();
        RenderPreview();
    }

    void OnDestroy()
    {
        ReleasePreviewResources();
    }

    public void Show(ItemDefinition item, int quantity, ItemRuntimeData runtimeData)
    {
        if (item == null || panelRect == null)
            return;

        currentItem = item;
        currentQuantity = Mathf.Max(1, quantity);
        currentRuntimeData = runtimeData;
        panelRect.gameObject.SetActive(true);
        panelRect.SetAsLastSibling();

        RefreshDisplayedItemInfo();
        fallbackText.gameObject.SetActive(false);

        zoomMultiplier = 1f;
        rotationAngles = GetDefaultRotation(item);
        RebuildPreviewModel(item);
        RenderPreview();
    }

    public void Hide()
    {
        draggingPreview = false;
        currentItem = null;
        currentQuantity = 1;
        currentRuntimeData = null;
        ReleaseModel();
        if (panelRect != null)
            panelRect.gameObject.SetActive(false);
    }

    public bool IsPointerOver(Vector2 screenPosition)
    {
        if (!IsOpen || panelRect == null)
            return false;

        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPosition, eventCamera);
    }

    private void BuildUi()
    {
        panelRect = GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 620f);
        panelRect.anchoredPosition = new Vector2(110f, -16f);

        Image background = panelRect.gameObject.AddComponent<Image>();
        background.color = PanelColor;

        Outline outline = panelRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.76f, 0.80f, 0.86f, 0.24f);
        outline.effectDistance = new Vector2(1f, -1f);

        titleText = CreateText("Title", panelRect, "Item", 24, TextAnchor.UpperLeft, new Vector2(22f, -18f), new Vector2(420f, 30f), FontStyle.Bold);
        typeText = CreateText("Type", panelRect, "Type", 14, TextAnchor.UpperLeft, new Vector2(22f, -48f), new Vector2(300f, 22f), FontStyle.Normal);
        typeText.color = new Color(0.72f, 0.78f, 0.86f, 0.95f);
        priceText = CreateText("Price", panelRect, "$0", 16, TextAnchor.UpperLeft, new Vector2(22f, -74f), new Vector2(260f, 22f), FontStyle.Bold);
        priceText.color = new Color(0.92f, 0.76f, 0.26f, 1f);

        closeButton = CreateButton(panelRect, "X", new Vector2(-22f, -18f), new Vector2(44f, 38f), 22);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeButton.onClick.AddListener(Hide);

        RectTransform weightIconRect = CreateRect("WeightIcon", panelRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(20f, 20f));
        weightIconRect.anchoredPosition = new Vector2(-82f, -70f);
        Image weightIcon = weightIconRect.gameObject.AddComponent<Image>();
        weightIcon.sprite = LoadSprite("UI/Status/Weight");
        weightIcon.color = new Color(0.88f, 0.90f, 0.94f, weightIcon.sprite != null ? 0.92f : 0f);
        weightIcon.preserveAspect = true;

        weightText = CreateText("Weight", panelRect, "0 kg", 16, TextAnchor.UpperRight, new Vector2(-22f, -72f), new Vector2(54f, 24f), FontStyle.Bold);
        weightText.rectTransform.anchorMin = new Vector2(1f, 1f);
        weightText.rectTransform.anchorMax = new Vector2(1f, 1f);
        weightText.rectTransform.pivot = new Vector2(1f, 1f);

        previewRect = CreatePanel("PreviewSurface", panelRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 320f), SurfaceColor);
        previewRect.offsetMin = new Vector2(22f, previewRect.offsetMin.y);
        previewRect.offsetMax = new Vector2(-22f, previewRect.offsetMax.y);
        previewRect.anchoredPosition = new Vector2(0f, -110f);

        RectTransform previewImageRect = CreateRect("PreviewImage", previewRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        StretchToParent(previewImageRect, Vector2.zero, Vector2.zero);
        previewImage = previewImageRect.gameObject.AddComponent<RawImage>();
        previewImage.color = Color.white;
        previewImage.raycastTarget = true;

        fallbackText = CreateText("PreviewFallback", previewRect, "No 3D preview", 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, FontStyle.Bold);
        StretchToParent(fallbackText.rectTransform, Vector2.zero, Vector2.zero);
        fallbackText.color = new Color(0.72f, 0.76f, 0.82f, 0.78f);

        CreateLine("Separator", panelRect, new Vector2(22f, -450f), new Vector2(716f, 2f));
        infoRowsRoot = CreateRect("InfoRows", panelRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(716f, 132f));
        infoRowsRoot.anchoredPosition = new Vector2(22f, -466f);
    }

    private void RefreshDynamicInfoIfNeeded()
    {
        if (currentItem == null || Time.unscaledTime < nextInfoRefreshTime)
            return;

        nextInfoRefreshTime = Time.unscaledTime + 0.2f;
        RefreshDisplayedItemInfo();
    }

    private void RefreshDisplayedItemInfo()
    {
        if (currentItem == null)
            return;

        titleText.text = currentItem.displayName;
        typeText.text = GetTypeLabel(currentItem);
        priceText.text = FormatMoneyValue(currentItem, currentQuantity);
        weightText.text = FormatWeight(currentItem, currentQuantity, currentRuntimeData);
        PopulateInfoRows(BuildInfoAttributes(currentItem, currentRuntimeData));
    }

    private void RebuildPreviewModel(ItemDefinition item)
    {
        ReleaseModel();
        EnsurePreviewCamera();

        modelRoot = new GameObject("ItemInspectModel_" + item.displayName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        modelRoot.transform.position = new Vector3(10000f, 10000f, 10000f);
        SetLayerRecursive(modelRoot, PreviewLayer);

        bool created = TryCreateModel(item, modelRoot.transform);
        if (!created)
            created = TryCreateSpriteFallback(item, modelRoot.transform);

        if (!created)
        {
            fallbackText.gameObject.SetActive(true);
            return;
        }

        DisableBehavioursAndPhysics(modelRoot);
        SetLayerRecursive(modelRoot, PreviewLayer);
        NormalizeModel(modelRoot);
        ApplyModelRotation();
    }

    private bool TryCreateModel(ItemDefinition item, Transform parent)
    {
        GameObject sourcePrefab = null;
        if (item.worldPrefab != null)
            sourcePrefab = item.worldPrefab;
        else if (item is WeaponItemDefinition weapon && weapon.equippedPrefab != null)
            sourcePrefab = weapon.equippedPrefab;

        if (sourcePrefab != null)
        {
            GameObject instance = Instantiate(sourcePrefab, parent, false);
            instance.name = sourcePrefab.name;
            return HasRenderer(instance);
        }

        if (item is ArmorItemDefinition || item is ContainerItemDefinition)
        {
            CharacterEquipmentVisuals visuals = FindFirstObjectByType<CharacterEquipmentVisuals>(FindObjectsInactive.Include);
            if (visuals != null && visuals.TryBuildWorldPickupVisual(item, parent))
                return HasRenderer(parent.gameObject);
        }

        GameObject[] visualPrefabs = GetCompositeWorldVisualPrefabs(item);
        if (visualPrefabs != null)
        {
            bool createdAny = false;
            for (int i = 0; i < visualPrefabs.Length; i++)
            {
                if (visualPrefabs[i] == null)
                    continue;

                GameObject instance = Instantiate(visualPrefabs[i], parent, false);
                instance.name = visualPrefabs[i].name;
                createdAny = true;
            }

            if (createdAny)
                return HasRenderer(parent.gameObject);
        }

        return false;
    }

    private bool TryCreateSpriteFallback(ItemDefinition item, Transform parent)
    {
        Sprite sprite = item.GetGridInventorySpriteOrFallback();
        if (sprite == null)
            return false;

        GameObject spriteObject = new GameObject("SpritePreview");
        spriteObject.transform.SetParent(parent, false);
        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.flipX = item.ShouldFlipGridDisplaySprite();
        renderer.sortingOrder = 1;
        return true;
    }

    private void NormalizeModel(GameObject root)
    {
        if (!TryGetRendererBounds(root, out Bounds bounds))
            return;

        Vector3 centerOffset = bounds.center - root.transform.position;
        for (int i = 0; i < root.transform.childCount; i++)
            root.transform.GetChild(i).position -= centerOffset;

        if (!TryGetRendererBounds(root, out bounds))
            return;

        Vector3 bottomOffset = new Vector3(0f, bounds.min.y - root.transform.position.y, 0f);
        for (int i = 0; i < root.transform.childCount; i++)
            root.transform.GetChild(i).position -= bottomOffset;
    }

    private void EnsurePreviewCamera()
    {
        if (previewTexture == null)
        {
            previewTexture = new RenderTexture(1024, 512, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            previewTexture.Create();
        }

        if (previewImage != null)
            previewImage.texture = previewTexture;

        if (previewCamera == null)
        {
            GameObject cameraObject = new GameObject("ItemInspectCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.orthographic = true;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 50f;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;
            previewCamera.targetTexture = previewTexture;
        }

        if (keyLight == null)
            keyLight = CreatePreviewLight("ItemInspectKeyLight", new Vector3(35f, -25f, 0f), 1.0f);

        if (fillLight == null)
            fillLight = CreatePreviewLight("ItemInspectFillLight", new Vector3(330f, 155f, 0f), 0.32f);
    }

    private void RenderPreview()
    {
        if (previewCamera == null || modelRoot == null || !TryGetRendererBounds(modelRoot, out Bounds bounds))
            return;

        Vector3 center = bounds.center;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.25f);
        previewCamera.transform.position = center + new Vector3(0f, radius * 0.15f, -radius * 4.4f);
        previewCamera.transform.LookAt(center);
        previewCamera.orthographicSize = Mathf.Clamp(radius * 1.18f * zoomMultiplier, 0.22f, 8f);
        previewCamera.Render();
    }

    private void HandlePreviewInput()
    {
        if (previewRect == null || modelRoot == null || !TryGetPointerScreenPosition(out Vector2 pointerPosition))
            return;

        Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;
        bool pointerOverPreview = RectTransformUtility.RectangleContainsScreenPoint(previewRect, pointerPosition, eventCamera);

        if (pointerOverPreview)
        {
            float scroll = GetScrollDelta();
            if (Mathf.Abs(scroll) > 0.001f)
                zoomMultiplier = Mathf.Clamp(zoomMultiplier * (scroll > 0f ? 0.88f : 1.12f), 0.42f, 2.8f);
        }

        if (WasLeftPressedThisFrame() && pointerOverPreview)
        {
            draggingPreview = true;
            lastPointerPosition = pointerPosition;
        }

        if (draggingPreview && IsLeftPressed())
        {
            Vector2 delta = pointerPosition - lastPointerPosition;
            lastPointerPosition = pointerPosition;
            rotationAngles.x = Mathf.Clamp(rotationAngles.x + delta.y * 0.28f, -72f, 72f);
            rotationAngles.y -= delta.x * 0.38f;
            ApplyModelRotation();
        }

        if (WasLeftReleasedThisFrame())
            draggingPreview = false;
    }

    private void ApplyModelRotation()
    {
        if (modelRoot == null)
            return;

        modelRoot.transform.rotation = Quaternion.Euler(rotationAngles.x, rotationAngles.y, 0f);
    }

    private static Vector2 GetDefaultRotation(ItemDefinition item)
    {
        if (item is WeaponItemDefinition)
            return new Vector2(8f, -88f);

        if (item is ArmorItemDefinition || item is ContainerItemDefinition)
            return new Vector2(-12f, -28f);

        return new Vector2(-18f, -28f);
    }

    private List<InfoAttribute> BuildInfoAttributes(ItemDefinition item, ItemRuntimeData runtimeData)
    {
        List<InfoAttribute> attributes = new List<InfoAttribute>();

        if (item is WeaponItemDefinition weapon)
        {
            string ammoType = weapon.compatibleAmmo != null ? weapon.compatibleAmmo.ammoCategory : "None";
            attributes.Add(new InfoAttribute("Weapon Type", weapon.weaponCategory.ToString()));
            attributes.Add(new InfoAttribute("Fire Mode", weapon.fireMode.ToString()));
            attributes.Add(new InfoAttribute("Fire Rate", weapon.roundsPerMinute + " RPM"));
            attributes.Add(new InfoAttribute("Damage", weapon.baseDamage.ToString("0.#")));
            attributes.Add(new InfoAttribute("Ammo Type", ammoType));
            return attributes;
        }

        if (item is ArmorItemDefinition armor)
        {
            attributes.Add(new InfoAttribute("Damage Reduction", (armor.damageReduction * 100f).ToString("0.#") + "%"));
            string durabilityText = armor.maxDurability.ToString("0.#");
            if (runtimeData != null)
            {
                float currentDurability = runtimeData.EnsureArmorDurability(armor);
                durabilityText = currentDurability.ToString("0.#") + " / " + armor.maxDurability.ToString("0.#");
            }

            attributes.Add(new InfoAttribute("Durability", durabilityText));

            if (armor.armorSlot == ArmorSlotType.Chest && armor.providedRigContainer != null)
                attributes.Add(new InfoAttribute("Capacity", armor.providedRigContainer.gridColumns + " x " + armor.providedRigContainer.gridRows));

            return attributes;
        }

        if (item is ContainerItemDefinition container)
        {
            attributes.Add(new InfoAttribute("Capacity", container.gridColumns + " x " + container.gridRows));
            return attributes;
        }

        if (item is MedicalItemDefinition medical)
        {
            attributes.Add(new InfoAttribute("Use Time", medical.useDuration.ToString("0.#") + "s"));
            attributes.Add(new InfoAttribute("Health Restore", medical.healAmount.ToString("0.#")));
            return attributes;
        }

        if (item is ConsumableItemDefinition consumable)
        {
            attributes.Add(new InfoAttribute("Use Time", consumable.useDuration.ToString("0.#") + "s"));

            if (consumable.hydrationRestoreAmount > 0f)
                attributes.Add(new InfoAttribute("Water Restore", consumable.hydrationRestoreAmount.ToString("0.#")));
            if (consumable.hungerRestoreAmount > 0f)
                attributes.Add(new InfoAttribute("Hunger Restore", consumable.hungerRestoreAmount.ToString("0.#")));

            return attributes;
        }

        if (item is AmmoItemDefinition ammo)
        {
            attributes.Add(new InfoAttribute("Ammo Type", ammo.ammoCategory));
            attributes.Add(new InfoAttribute("Stack Limit", item.maxStackSize.ToString()));
            return attributes;
        }

        return attributes;
    }

    private void PopulateInfoRows(List<InfoAttribute> attributes)
    {
        if (infoRowsRoot == null)
            return;

        for (int i = infoRowsRoot.childCount - 1; i >= 0; i--)
            Destroy(infoRowsRoot.GetChild(i).gameObject);

        if (attributes == null || attributes.Count == 0)
            return;

        const float columnWidth = 338f;
        const float columnGap = 40f;
        const float rowHeight = 38f;

        for (int i = 0; i < attributes.Count; i++)
        {
            int column = i % 2;
            int row = i / 2;
            float x = column * (columnWidth + columnGap);
            float y = -row * rowHeight;

            RectTransform cell = CreateRect("Info_" + attributes[i].label, infoRowsRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(columnWidth, 34f));
            cell.anchoredPosition = new Vector2(x, y);

            Text labelText = CreateText("Label", cell, attributes[i].label, 15, TextAnchor.UpperLeft, new Vector2(0f, -2f), new Vector2(154f, 26f), FontStyle.Bold);
            labelText.color = new Color(0.78f, 0.82f, 0.88f, 0.96f);

            Text valueText = CreateText("Value", cell, attributes[i].value, 16, TextAnchor.UpperRight, new Vector2(columnWidth, -2f), new Vector2(178f, 26f), FontStyle.Bold);
            valueText.rectTransform.anchorMin = new Vector2(1f, 1f);
            valueText.rectTransform.anchorMax = new Vector2(1f, 1f);
            valueText.rectTransform.pivot = new Vector2(1f, 1f);
            valueText.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            valueText.color = new Color(0.95f, 0.96f, 0.98f, 0.98f);

            CreateLine("Underline", cell, new Vector2(0f, -30f), new Vector2(columnWidth, 1f));
        }
    }

    private static string GetTypeLabel(ItemDefinition item)
    {
        if (item is WeaponItemDefinition weapon)
            return weapon.weaponCategory + " / " + weapon.fireMode;

        if (item is ArmorItemDefinition armor)
            return armor.armorSlot == ArmorSlotType.Head ? "Helmet" : "Body Armor";

        if (item is ContainerItemDefinition container)
            return container.containerKind.ToString();

        return item.Type.ToString();
    }

    private static string FormatMoneyValue(ItemDefinition item, int quantity)
    {
        float totalValue = item.GetTotalMoneyValue(quantity);
        if (quantity > 1)
            return "Value: $" + totalValue.ToString("0.#") + "  ($" + item.moneyValue.ToString("0.##") + " each)";

        return "Value: $" + totalValue.ToString("0.#");
    }

    private static string FormatWeight(ItemDefinition item, int quantity, ItemRuntimeData runtimeData)
    {
        float totalWeight = (item.weight * Mathf.Max(1, quantity)) + (runtimeData != null ? runtimeData.NestedWeight : 0f);
        return totalWeight.ToString("0.###") + " kg";
    }

    private static GameObject[] GetCompositeWorldVisualPrefabs(ItemDefinition item)
    {
        if (item is ArmorItemDefinition armor)
            return armor.worldVisualPrefabs;

        if (item is ContainerItemDefinition container)
            return container.worldVisualPrefabs;

        return null;
    }

    private void ReleasePreviewResources()
    {
        ReleaseModel();

        if (previewCamera != null)
            Destroy(previewCamera.gameObject);
        if (keyLight != null)
            Destroy(keyLight.gameObject);
        if (fillLight != null)
            Destroy(fillLight.gameObject);
        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
        }

        previewCamera = null;
        keyLight = null;
        fillLight = null;
        previewTexture = null;
    }

    private void ReleaseModel()
    {
        if (modelRoot != null)
            Destroy(modelRoot);

        modelRoot = null;
    }

    private Light CreatePreviewLight(string name, Vector3 eulerAngles, float intensity)
    {
        GameObject lightObject = new GameObject(name)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.88f, 0.91f, 0.98f, 1f);
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << PreviewLayer;
        lightObject.transform.rotation = Quaternion.Euler(eulerAngles);
        return light;
    }

    private Button CreateButton(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size, int fontSize)
    {
        RectTransform rect = CreateRect("Button_" + label, parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), size);
        rect.anchoredPosition = anchoredPosition;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.13f, 0.16f, 0.21f, 0.96f);
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.25f, 0.30f, 0.38f, 0.98f);
        colors.pressedColor = new Color(0.08f, 0.10f, 0.14f, 0.98f);
        button.colors = colors;

        Text labelText = CreateText("Label", rect, label, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, FontStyle.Bold);
        StretchToParent(labelText.rectTransform, Vector2.zero, Vector2.zero);
        return button;
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
        uiText.color = new Color(0.94f, 0.96f, 0.98f, 1f);
        uiText.fontStyle = fontStyle;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Truncate;
        uiText.text = text;
        return uiText;
    }

    private RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Color color)
    {
        RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, sizeDelta);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return rect;
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
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

    private void CreateLine(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform line = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), size);
        line.anchoredPosition = anchoredPosition;
        Image image = line.gameObject.AddComponent<Image>();
        image.color = LineColor;
    }

    private void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static Sprite LoadSprite(string resourcesPath)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcesPath);
        if (texture == null)
            return null;

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void DisableBehavioursAndPhysics(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = false;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null)
                continue;

            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static bool HasRenderer(GameObject root)
    {
        return root != null && root.GetComponentInChildren<Renderer>(true) != null;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
    }

    private static bool TryGetPointerScreenPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif
        screenPosition = Input.mousePosition;
        return true;
    }

    private static bool WasLeftPressedThisFrame()
    {
        if (Input.GetMouseButtonDown(0))
            return true;
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    private static bool IsLeftPressed()
    {
        if (Input.GetMouseButton(0))
            return true;
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return false;
#endif
    }

    private static bool WasLeftReleasedThisFrame()
    {
        if (Input.GetMouseButtonUp(0))
            return true;
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    private static float GetScrollDelta()
    {
        float delta = Input.mouseScrollDelta.y;
#if ENABLE_INPUT_SYSTEM
        if (Mathf.Abs(delta) <= 0.001f && Mouse.current != null)
            delta = Mouse.current.scroll.ReadValue().y;
#endif
        return delta;
    }
}
