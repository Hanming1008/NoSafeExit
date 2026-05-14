using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RaidFlowController : MonoBehaviour
{
    public static RaidFlowController Instance { get; private set; }
    public static bool IsRaidActive => Instance != null && Instance.isInRaid;

    [Header("Scene Anchors")]
    [SerializeField] private string shelterAnchorName = "SM_Prop_Crate_Stack_02 (1)";
    [SerializeField] private string raidSpawnAnchorName = "SM_Env_Dock_01 (2)";
    [SerializeField] private Transform shelterAnchor;
    [SerializeField] private Transform raidSpawnAnchor;

    [Header("Raid Flow")]
    [SerializeField] private bool startAtShelterOnPlay = true;
    [SerializeField] private float shelterInteractionRadius = 3.5f;
    [SerializeField] private float holdToDeploySeconds = 3f;
    [SerializeField] private Vector3 shelterOffset = new Vector3(0f, 0f, 2.2f);
    [SerializeField] private Vector3 raidSpawnOffset = new Vector3(0f, 0f, 3.5f);
    [SerializeField] private bool rerollLootOnRaidStart = true;

    private Transform player;
    private PlayerMove playerMove;
    private CharacterController characterController;
    private Rigidbody playerRigidbody;
    private LootSpawnManager lootSpawnManager;

    private Canvas overlayCanvas;
    private RectTransform shelterPromptPanel;
    private Image shelterHoldFill;
    private Text shelterPromptText;
    private Text shelterHoldText;
    private RectTransform extractionPanel;
    private Image extractionFill;
    private Text extractionText;
    private Text toastText;

    private bool isInRaid;
    private float holdTimer;
    private float toastHideAt;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        BuildOverlay();
    }

    void Start()
    {
        ResolveReferences();

        if (startAtShelterOnPlay && player != null)
        {
            TeleportPlayerToAnchor(shelterAnchor, shelterOffset);
            isInRaid = false;
        }
        else if (player != null)
        {
            isInRaid = true;
        }
    }

    void Update()
    {
        ResolveReferences();
        UpdateShelterPrompt();
        UpdateToast();
    }

    public void SetExtractionProgress(bool active, float elapsedSeconds, float requiredSeconds)
    {
        if (extractionPanel == null)
            return;

        extractionPanel.gameObject.SetActive(active);
        if (!active)
            return;

        float duration = Mathf.Max(0.01f, requiredSeconds);
        float remaining = Mathf.Max(0f, duration - elapsedSeconds);
        float fill = Mathf.Clamp01(remaining / duration);

        if (extractionFill != null)
            extractionFill.fillAmount = fill;

        if (extractionText != null)
            extractionText.text = $"EXTRACTING  {remaining:0.0}s";
    }

    public void CompleteExtraction(Transform extractingPlayer)
    {
        Transform resolvedPlayer = ResolvePlayerTransform(extractingPlayer);
        if (resolvedPlayer != null)
            AssignPlayer(resolvedPlayer);
        else
            ResolveReferences();

        SetExtractionProgress(false, 0f, 1f);
        TeleportPlayerToAnchor(shelterAnchor, shelterOffset);
        isInRaid = false;
        holdTimer = 0f;
        ShowToast("Extraction complete");
    }

    private void UpdateShelterPrompt()
    {
        if (shelterPromptPanel == null)
            return;

        if (player == null || shelterAnchor == null || isInRaid)
        {
            HideShelterPrompt();
            return;
        }

        float distance = Vector3.Distance(Flatten(player.position), Flatten(shelterAnchor.position));
        bool inRange = distance <= shelterInteractionRadius;
        shelterPromptPanel.gameObject.SetActive(inRange);

        if (!inRange)
        {
            holdTimer = 0f;
            UpdateShelterHoldFill();
            return;
        }

        if (Input.GetKey(KeyCode.F))
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= holdToDeploySeconds)
                EnterRaid();
        }
        else
        {
            holdTimer = Mathf.Max(0f, holdTimer - (Time.deltaTime * 2f));
        }

        UpdateShelterHoldFill();
    }

    private void HideShelterPrompt()
    {
        shelterPromptPanel.gameObject.SetActive(false);
        holdTimer = 0f;
        UpdateShelterHoldFill();
    }

    private void UpdateShelterHoldFill()
    {
        float duration = Mathf.Max(0.01f, holdToDeploySeconds);
        float progress = Mathf.Clamp01(holdTimer / duration);
        float fill = 1f - progress;

        if (shelterHoldFill != null)
            shelterHoldFill.fillAmount = fill;

        if (shelterHoldText != null)
            shelterHoldText.text = progress > 0f ? $"{holdToDeploySeconds - holdTimer:0.0}s" : "HOLD F";
    }

    private void EnterRaid()
    {
        HideShelterPrompt();
        TeleportPlayerToAnchor(raidSpawnAnchor, raidSpawnOffset);
        isInRaid = true;
        holdTimer = 0f;

        if (rerollLootOnRaidStart)
        {
            if (lootSpawnManager == null)
                lootSpawnManager = FindFirstObjectByType<LootSpawnManager>(FindObjectsInactive.Include);

            if (lootSpawnManager != null)
                lootSpawnManager.RerollAllContainers();
        }

        ShowToast("Raid started");
    }

    private void TeleportPlayerToAnchor(Transform anchor, Vector3 localOffset)
    {
        if (player == null || anchor == null)
            return;

        Vector3 target = anchor.TransformPoint(localOffset);
        target = ProjectToGround(target);

        if (characterController == null)
            characterController = player.GetComponent<CharacterController>();

        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
            characterController.enabled = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        player.position = target;
        player.rotation = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);

        if (controllerWasEnabled)
            characterController.enabled = true;
    }

    private Vector3 ProjectToGround(Vector3 target)
    {
        Vector3 origin = target + (Vector3.up * 20f);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point + (Vector3.up * 0.08f);

        return target;
    }

    private void ShowToast(string message)
    {
        if (toastText == null)
            return;

        toastText.text = message;
        toastText.gameObject.SetActive(true);
        toastHideAt = Time.unscaledTime + 2f;
    }

    private void UpdateToast()
    {
        if (toastText != null && toastText.gameObject.activeSelf && Time.unscaledTime >= toastHideAt)
            toastText.gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (player == null || !IsValidPlayerTransform(player))
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                AssignPlayer(playerObject.transform);
        }

        if (shelterAnchor == null)
            shelterAnchor = FindSceneTransformByName(shelterAnchorName, preferMapRoot: false);

        if (raidSpawnAnchor == null)
            raidSpawnAnchor = FindSceneTransformByName(raidSpawnAnchorName, preferMapRoot: true);

        if (lootSpawnManager == null)
            lootSpawnManager = FindFirstObjectByType<LootSpawnManager>(FindObjectsInactive.Include);
    }

    private void AssignPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        player = playerTransform;
        playerMove = player.GetComponent<PlayerMove>();
        characterController = player.GetComponent<CharacterController>();
        playerRigidbody = player.GetComponent<Rigidbody>();
    }

    private Transform ResolvePlayerTransform(Transform candidate)
    {
        if (candidate == null)
            return null;

        if (IsValidPlayerTransform(candidate))
            return candidate;

        PlayerMove move = candidate.GetComponentInParent<PlayerMove>();
        if (move != null)
            return move.transform;

        PlayerStats stats = candidate.GetComponentInParent<PlayerStats>();
        if (stats != null)
            return stats.transform;

        return null;
    }

    private bool IsValidPlayerTransform(Transform candidate)
    {
        if (candidate == null)
            return false;

        return candidate.CompareTag("Player")
            || candidate.GetComponent<PlayerMove>() != null
            || candidate.GetComponent<PlayerStats>() != null;
    }

    private Transform FindSceneTransformByName(string targetName, bool preferMapRoot)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform fallback = null;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != targetName)
                continue;

            if (!preferMapRoot)
                return candidate;

            fallback ??= candidate;
            if (candidate.parent != null && candidate.parent.name == "PolygonBattleRoyaleMap_Runtime")
                return candidate;
        }

        return fallback;
    }

    private void BuildOverlay()
    {
        if (overlayCanvas != null)
            return;

        GameObject canvasObject = new GameObject("RaidFlowOverlayCanvas");
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 9000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform canvasRect = overlayCanvas.GetComponent<RectTransform>();
        BuildShelterPrompt(canvasRect, font);
        BuildExtractionBar(canvasRect, font);
        BuildToast(canvasRect, font);

        HideShelterPrompt();
        SetExtractionProgress(false, 0f, 1f);
    }

    private void BuildShelterPrompt(RectTransform parent, Font font)
    {
        shelterPromptPanel = CreateRect("ShelterDeployPrompt", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 118f));
        shelterPromptPanel.anchoredPosition = new Vector2(0f, -72f);
        Image background = shelterPromptPanel.gameObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.06f, 0.05f, 0.78f);

        shelterPromptText = CreateText("PromptText", shelterPromptPanel, font, "HOLD F TO START RAID", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        shelterPromptText.rectTransform.anchorMin = new Vector2(0f, 0.45f);
        shelterPromptText.rectTransform.anchorMax = new Vector2(1f, 1f);
        shelterPromptText.rectTransform.offsetMin = new Vector2(18f, 0f);
        shelterPromptText.rectTransform.offsetMax = new Vector2(-18f, -8f);

        RectTransform barFrame = CreateRect("HoldFrame", shelterPromptPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(340f, 18f));
        barFrame.anchoredPosition = new Vector2(0f, 24f);
        Image frameImage = barFrame.gameObject.AddComponent<Image>();
        frameImage.color = new Color(0.10f, 0.18f, 0.14f, 0.95f);

        shelterHoldFill = CreateRect("HoldFill", barFrame, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero).gameObject.AddComponent<Image>();
        shelterHoldFill.color = new Color(0.10f, 0.82f, 0.33f, 0.95f);
        shelterHoldFill.type = Image.Type.Filled;
        shelterHoldFill.fillMethod = Image.FillMethod.Horizontal;
        shelterHoldFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        shelterHoldText = CreateText("HoldText", shelterPromptPanel, font, "HOLD F", 16, FontStyle.Bold, TextAnchor.MiddleCenter);
        shelterHoldText.rectTransform.anchorMin = new Vector2(0f, 0f);
        shelterHoldText.rectTransform.anchorMax = new Vector2(1f, 0.28f);
        shelterHoldText.rectTransform.offsetMin = new Vector2(0f, 0f);
        shelterHoldText.rectTransform.offsetMax = new Vector2(0f, 0f);
    }

    private void BuildExtractionBar(RectTransform parent, Font font)
    {
        extractionPanel = CreateRect("ExtractionProgressPanel", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(520f, 52f));
        extractionPanel.anchoredPosition = new Vector2(0f, -92f);
        Image background = extractionPanel.gameObject.AddComponent<Image>();
        background.color = new Color(0.03f, 0.12f, 0.06f, 0.84f);

        extractionFill = CreateRect("ExtractionFill", extractionPanel, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero).gameObject.AddComponent<Image>();
        extractionFill.color = new Color(0.07f, 0.78f, 0.24f, 0.88f);
        extractionFill.type = Image.Type.Filled;
        extractionFill.fillMethod = Image.FillMethod.Horizontal;
        extractionFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        extractionText = CreateText("ExtractionText", extractionPanel, font, "EXTRACTING", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        StretchToParent(extractionText.rectTransform, Vector2.zero, Vector2.zero);
    }

    private void BuildToast(RectTransform parent, Font font)
    {
        toastText = CreateText("RaidToast", parent, font, string.Empty, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        toastText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        toastText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        toastText.rectTransform.pivot = new Vector2(0.5f, 1f);
        toastText.rectTransform.anchoredPosition = new Vector2(0f, -158f);
        toastText.rectTransform.sizeDelta = new Vector2(460f, 42f);
        toastText.color = new Color(0.74f, 1f, 0.76f, 1f);
        toastText.gameObject.SetActive(false);
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        return rect;
    }

    private Text CreateText(string name, Transform parent, Font font, string content, int size, FontStyle style, TextAnchor alignment)
    {
        Text text = CreateRect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero).gameObject.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
