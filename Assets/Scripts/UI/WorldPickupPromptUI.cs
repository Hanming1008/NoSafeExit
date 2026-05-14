using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WorldPickupPromptUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerItemPickup pickupSource;
    [SerializeField] private PlayerSearchInteractor searchSource;
    [SerializeField] private GameplayUIRoot gameplayUIRoot;
    [SerializeField] private Camera targetCamera;

    [Header("Layout")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private Vector3 corpseWorldOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField] private Vector2 screenOffset = new Vector2(190f, -28f);
    [SerializeField] private Vector2 panelSize = new Vector2(220f, 32f);
    [SerializeField] private Vector2 screenPadding = new Vector2(18f, 18f);

    [Header("Style")]
    [SerializeField] private string keyLabel = "F";
    [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private Color keyBoxColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.96f);

    private Canvas canvas;
    private RectTransform panelRect;
    private Text keyText;
    private Text itemText;
    private Font uiFont;
    private ShelterStashStation[] stashStations;
    private ShelterServiceStation[] serviceStations;
    private ShelterTraderStation[] traderStations;
    private readonly System.Collections.Generic.List<GameObject> requestedHighlightRoots = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<GameObject> highlightedRoots = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<InteractionTargetOutline> activeOutlines = new System.Collections.Generic.List<InteractionTargetOutline>();

    void Awake()
    {
        ApplyCompactStyleDefaults();
        ResolveReferences();
        BuildUI();
        SetVisible(false);
    }

    void LateUpdate()
    {
        if (pickupSource == null || searchSource == null || targetCamera == null || gameplayUIRoot == null)
            ResolveReferences();

        if ((gameplayUIRoot != null && gameplayUIRoot.IsGameplayOverlayOpen) || TraderShopUI.IsAnyOpen)
        {
            SetVisible(false);
            ClearHighlight();
            return;
        }

        if (!TryGetPromptTarget(out GameObject boundsRoot, out Transform fallbackTarget, out string displayText)
            || !TryResolveScreenPoint(boundsRoot, fallbackTarget, out Vector3 screenPoint))
        {
            SetVisible(false);
            ClearHighlight();
            return;
        }

        SetHighlight(requestedHighlightRoots);
        UpdateText(displayText);
        SetPanelPosition(screenPoint);
        SetVisible(true);
    }

    private void ResolveReferences()
    {
        if (pickupSource == null)
            pickupSource = FindFirstObjectByType<PlayerItemPickup>();

        if (searchSource == null)
            searchSource = FindFirstObjectByType<PlayerSearchInteractor>();

        if (gameplayUIRoot == null)
            gameplayUIRoot = FindFirstObjectByType<GameplayUIRoot>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (stashStations == null || stashStations.Length == 0)
        {
            stashStations = FindObjectsByType<ShelterStashStation>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }

        if (serviceStations == null || serviceStations.Length == 0)
        {
            serviceStations = FindObjectsByType<ShelterServiceStation>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }

        if (traderStations == null || traderStations.Length == 0)
        {
            traderStations = FindObjectsByType<ShelterTraderStation>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }
    }

    private void ApplyCompactStyleDefaults()
    {
        screenOffset = new Vector2(190f, -28f);
        panelSize = new Vector2(220f, 32f);
        borderColor = new Color(1f, 1f, 1f, 0.92f);
        keyBoxColor = new Color(1f, 1f, 1f, 0f);
        textColor = new Color(1f, 1f, 1f, 0.96f);
    }

    private void BuildUI()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject canvasObject = new GameObject("World Pickup Prompt Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panelRect = CreateRect("PickupPrompt", canvasObject.transform, panelSize);
        panelRect.pivot = new Vector2(0.5f, 0.5f);

        HorizontalLayoutGroup layout = panelRect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RectTransform keyBox = CreateRect("KeyBox", panelRect, new Vector2(28f, 28f));
        Image keyBoxImage = keyBox.gameObject.AddComponent<Image>();
        keyBoxImage.color = keyBoxColor;
        keyBoxImage.raycastTarget = false;
        AddRectBorder(keyBox, new Color(0f, 0f, 0f, 0.82f), 4f);
        AddRectBorder(keyBox, borderColor, 2f);
        keyText = CreateText("Key", keyBox, keyLabel, 16, TextAnchor.MiddleCenter, FontStyle.Bold, textColor);
        StretchToParent(keyText.rectTransform);
        Shadow keyShadow = keyText.gameObject.AddComponent<Shadow>();
        keyShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        keyShadow.effectDistance = new Vector2(1f, -1f);

        RectTransform labelStack = CreateRect("LabelStack", panelRect, new Vector2(176f, 28f));
        itemText = CreateText("Item", labelStack, string.Empty, 17, TextAnchor.MiddleLeft, FontStyle.Bold, textColor);
        StretchToParent(itemText.rectTransform);
        Shadow itemShadow = itemText.gameObject.AddComponent<Shadow>();
        itemShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        itemShadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private RectTransform CreateRect(string objectName, Transform parent, Vector2 size)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);

        RectTransform rect = rectObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return rect;
    }

    private Text CreateText(string objectName, Transform parent, string text, int size, TextAnchor alignment, FontStyle style, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent, Vector2.zero);
        Text uiText = rect.gameObject.AddComponent<Text>();
        uiText.font = uiFont;
        uiText.text = text;
        uiText.fontSize = size;
        uiText.alignment = alignment;
        uiText.fontStyle = style;
        uiText.color = color;
        uiText.raycastTarget = false;
        uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;
        return uiText;
    }

    private void AddRectBorder(RectTransform parent, Color color, float thickness)
    {
        AddBorderLine("Top", parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -thickness), new Vector2(0f, 0f));
        AddBorderLine("Bottom", parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, thickness));
        AddBorderLine("Left", parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), Vector2.zero, new Vector2(thickness, 0f));
        AddBorderLine("Right", parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-thickness, 0f), Vector2.zero);
    }

    private void AddBorderLine(
        string objectName,
        RectTransform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        RectTransform line = CreateRect(objectName, parent, Vector2.zero);
        line.anchorMin = anchorMin;
        line.anchorMax = anchorMax;
        line.pivot = pivot;
        line.offsetMin = offsetMin;
        line.offsetMax = offsetMax;

        Image image = line.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static void StretchToParent(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private bool TryGetPromptTarget(out GameObject boundsRoot, out Transform fallbackTarget, out string displayText)
    {
        boundsRoot = null;
        fallbackTarget = null;
        displayText = string.Empty;
        requestedHighlightRoots.Clear();

        EnemyCorpseLoot corpse = searchSource != null ? searchSource.CurrentCorpse : null;
        if (corpse != null)
        {
            boundsRoot = corpse.gameObject;
            fallbackTarget = corpse.transform;
            displayText = $"Search {corpse.EnemyTypeDisplayName}";
            requestedHighlightRoots.Add(boundsRoot);
            return true;
        }

        SearchableContainer container = searchSource != null ? searchSource.CurrentContainer : null;
        if (container != null)
        {
            boundsRoot = container.gameObject;
            fallbackTarget = container.transform;
            displayText = $"Open {container.DisplayName}";
            requestedHighlightRoots.Add(boundsRoot);
            return true;
        }

        ShelterServiceStation serviceStation = FindActiveServiceStation();
        if (serviceStation != null)
        {
            boundsRoot = serviceStation.BoundsRoot;
            fallbackTarget = serviceStation.FallbackTransform;
            displayText = serviceStation.PromptText;
            serviceStation.GetHighlightRoots(requestedHighlightRoots);
            if (requestedHighlightRoots.Count == 0 && boundsRoot != null)
                requestedHighlightRoots.Add(boundsRoot);
            return true;
        }

        ShelterStashStation stashStation = FindActiveStashStation();
        if (stashStation != null)
        {
            boundsRoot = stashStation.gameObject;
            fallbackTarget = stashStation.transform;
            string stashName = stashStation.StashContainer != null ? stashStation.StashContainer.DisplayName : "Stash";
            displayText = $"Open {stashName}";
            requestedHighlightRoots.Add(boundsRoot);
            return true;
        }

        ShelterTraderStation traderStation = FindActiveTraderStation();
        if (traderStation != null)
        {
            boundsRoot = traderStation.BoundsRoot;
            fallbackTarget = traderStation.FallbackTransform;
            displayText = traderStation.PromptText;
            traderStation.GetHighlightRoots(requestedHighlightRoots);
            if (requestedHighlightRoots.Count == 0 && boundsRoot != null)
                requestedHighlightRoots.Add(boundsRoot);
            return true;
        }

        WorldItemPickup pickup = pickupSource != null ? pickupSource.CurrentPickup : null;
        if (pickup == null || !pickup.CanBePickedUp)
            return false;

        boundsRoot = pickup.gameObject;
        fallbackTarget = pickup.transform;
        displayText = pickup.DisplayName;
        if (pickup.Quantity > 1)
            displayText = $"{displayText} x{pickup.Quantity}";
        requestedHighlightRoots.Add(boundsRoot);

        return true;
    }

    private ShelterServiceStation FindActiveServiceStation()
    {
        if (serviceStations == null || serviceStations.Length == 0)
            return null;

        for (int i = 0; i < serviceStations.Length; i++)
        {
            ShelterServiceStation station = serviceStations[i];
            if (station != null && station.isActiveAndEnabled && station.IsPlayerNear)
                return station;
        }

        return null;
    }

    private ShelterStashStation FindActiveStashStation()
    {
        if (stashStations == null || stashStations.Length == 0)
            return null;

        for (int i = 0; i < stashStations.Length; i++)
        {
            ShelterStashStation station = stashStations[i];
            if (station != null && station.isActiveAndEnabled && station.IsPlayerNear)
                return station;
        }

        return null;
    }

    private ShelterTraderStation FindActiveTraderStation()
    {
        if (traderStations == null || traderStations.Length == 0)
            return null;

        for (int i = 0; i < traderStations.Length; i++)
        {
            ShelterTraderStation station = traderStations[i];
            if (station != null && station.isActiveAndEnabled && station.IsPlayerNear)
                return station;
        }

        return null;
    }

    private bool TryResolveScreenPoint(GameObject boundsRoot, Transform fallbackTarget, out Vector3 screenPoint)
    {
        screenPoint = default;
        if (fallbackTarget == null || targetCamera == null)
            return false;

        Vector3 worldPoint = fallbackTarget.position + worldOffset;
        if (boundsRoot != null && boundsRoot.GetComponentInParent<EnemyCorpseLoot>() != null)
        {
            if (TryGetColliderBounds(boundsRoot, out Bounds corpseColliderBounds))
                worldPoint = new Vector3(corpseColliderBounds.center.x, corpseColliderBounds.min.y, corpseColliderBounds.center.z) + corpseWorldOffset;
            else if (TryGetRendererBounds(boundsRoot, out Bounds corpseRendererBounds))
                worldPoint = new Vector3(corpseRendererBounds.center.x, corpseRendererBounds.min.y, corpseRendererBounds.center.z) + corpseWorldOffset;
            else
                worldPoint = fallbackTarget.position + corpseWorldOffset;
        }
        else if (TryGetRendererBounds(boundsRoot, out Bounds bounds))
        {
            worldPoint = bounds.center + worldOffset;
        }

        screenPoint = targetCamera.WorldToScreenPoint(worldPoint);
        return screenPoint.z > 0f;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static bool TryGetColliderBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private void UpdateText(string displayText)
    {
        if (keyText != null)
            keyText.text = keyLabel;

        if (itemText == null)
            return;

        itemText.text = displayText;
    }

    private void SetPanelPosition(Vector3 screenPoint)
    {
        if (panelRect == null)
            return;

        float halfWidth = panelSize.x * 0.5f;
        float halfHeight = panelSize.y * 0.5f;
        float x = Mathf.Clamp(screenPoint.x + screenOffset.x, screenPadding.x + halfWidth, Screen.width - screenPadding.x - halfWidth);
        float y = Mathf.Clamp(screenPoint.y + screenOffset.y, screenPadding.y + halfHeight, Screen.height - screenPadding.y - halfHeight);
        panelRect.position = new Vector3(x, y, 0f);
    }

    private void SetVisible(bool visible)
    {
        if (panelRect != null && panelRect.gameObject.activeSelf != visible)
            panelRect.gameObject.SetActive(visible);
    }

    private void SetHighlight(System.Collections.Generic.List<GameObject> roots)
    {
        if (AreHighlightRootsSame(roots))
            return;

        ClearHighlight();

        if (roots == null)
            return;

        for (int i = 0; i < roots.Count; i++)
        {
            GameObject root = roots[i];
            if (root == null || highlightedRoots.Contains(root))
                continue;

            InteractionTargetOutline outline = root.GetComponent<InteractionTargetOutline>();
            if (outline == null)
                outline = root.AddComponent<InteractionTargetOutline>();

            outline.SetHighlighted(true);
            highlightedRoots.Add(root);
            activeOutlines.Add(outline);
        }
    }

    private bool AreHighlightRootsSame(System.Collections.Generic.List<GameObject> roots)
    {
        if (roots == null || roots.Count != highlightedRoots.Count)
            return false;

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] != highlightedRoots[i])
                return false;
        }

        return true;
    }

    private void ClearHighlight()
    {
        for (int i = 0; i < activeOutlines.Count; i++)
        {
            if (activeOutlines[i] != null)
                activeOutlines[i].SetHighlighted(false);
        }

        activeOutlines.Clear();
        highlightedRoots.Clear();
    }

    void OnDisable()
    {
        ClearHighlight();
    }
}
