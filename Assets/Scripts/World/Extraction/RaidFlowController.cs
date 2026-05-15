using System.Collections;
using System.Collections.Generic;
using JUTPS;
using JUTPS.PhysicsScripts;
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
    private PlayerStats playerStats;
    private JUHealth playerHealth;
    private JUCharacterController playerCharacter;
    private CharacterController characterController;
    private Rigidbody playerRigidbody;
    private PlayerInventory playerInventory;
    private PlayerEquipment playerEquipment;
    private PlayerGridInventory playerGridInventory;
    private CharacterEquipmentVisuals playerEquipmentVisuals;
    private PlayerWeaponSelection playerWeaponSelection;
    private LootSpawnManager lootSpawnManager;
    private int playerDefaultLayer = -1;

    private Canvas overlayCanvas;
    private RectTransform shelterPromptPanel;
    private RectTransform shelterHoldFillRect;
    private Image shelterHoldFill;
    private Text shelterPromptText;
    private Text shelterHoldText;
    private RectTransform extractionPanel;
    private RectTransform extractionFillRect;
    private Image extractionFill;
    private Text extractionText;
    private Text toastText;
    private RectTransform extractionResultPanel;
    private Text extractionResultTitleText;
    private Text extractionResultDurationText;
    private Text extractionResultValueText;
    private Text extractionResultButtonText;

    private bool isInRaid;
    private float holdTimer;
    private float toastHideAt;
    private float raidStartedAt;
    private bool deathSequencePending;
    private bool waitingForRespawnConfirm;
    private Coroutine deathSequenceCoroutine;
    private bool raidBaselineCaptured;
    private readonly HashSet<string> raidBaselineInstanceIds = new HashSet<string>();
    private readonly Dictionary<ItemDefinition, int> raidBaselineStackQuantities = new Dictionary<ItemDefinition, int>();
    private readonly HashSet<GridContainerState> raidBaselineContainerStates = new HashSet<GridContainerState>();

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
        UpdatePlayerDeathState();
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
        if (extractionFillRect != null)
            extractionFillRect.anchorMax = new Vector2(fill, 1f);

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
        float raidDuration = raidStartedAt > 0f ? Mathf.Max(0f, Time.time - raidStartedAt) : 0f;
        float extractedValue = CalculateExtractedValue(resolvedPlayer != null ? resolvedPlayer : player);
        TeleportPlayerToAnchor(shelterAnchor, shelterOffset);
        isInRaid = false;
        holdTimer = 0f;
        raidBaselineCaptured = false;
        ShowExtractionResult(true, raidDuration, extractedValue);
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

        if (shelterHoldFill != null)
            shelterHoldFill.fillAmount = progress;
        if (shelterHoldFillRect != null)
            shelterHoldFillRect.anchorMax = new Vector2(progress, 1f);

        if (shelterHoldText != null)
            shelterHoldText.text = progress > 0f ? $"{holdToDeploySeconds - holdTimer:0.0}s" : "HOLD F";
    }

    private void EnterRaid()
    {
        HideShelterPrompt();
        TeleportPlayerToAnchor(raidSpawnAnchor, raidSpawnOffset);
        isInRaid = true;
        deathSequencePending = false;
        waitingForRespawnConfirm = false;
        holdTimer = 0f;
        raidStartedAt = Time.time;
        CaptureRaidBaseline(player);
        HideExtractionResult();

        if (rerollLootOnRaidStart)
        {
            if (lootSpawnManager == null)
                lootSpawnManager = FindFirstObjectByType<LootSpawnManager>(FindObjectsInactive.Include);

            if (lootSpawnManager != null)
                lootSpawnManager.RerollAllContainers();
        }

        ShowToast("Raid started");
    }

    private void UpdatePlayerDeathState()
    {
        if (!isInRaid || deathSequencePending || waitingForRespawnConfirm)
            return;

        bool statsDead = playerStats != null && !playerStats.IsAlive;
        bool healthDead = playerHealth != null && playerHealth.IsDead;
        if (!statsDead && !healthDead)
            return;

        BeginDeathSequence();
    }

    private void BeginDeathSequence()
    {
        deathSequencePending = true;
        SetExtractionProgress(false, 0f, 1f);
        HideShelterPrompt();

        if (deathSequenceCoroutine != null)
            StopCoroutine(deathSequenceCoroutine);

        deathSequenceCoroutine = StartCoroutine(ResolveDeathAfterDelay());
    }

    private IEnumerator ResolveDeathAfterDelay()
    {
        float raidDuration = raidStartedAt > 0f ? Mathf.Max(0f, Time.time - raidStartedAt) : 0f;
        yield return new WaitForSecondsRealtime(4f);

        ResolveReferences();
        holdTimer = 0f;
        deathSequencePending = false;
        deathSequenceCoroutine = null;

        ShowExtractionResult(false, raidDuration, 0f);
    }

    private void RevivePlayerAtShelter()
    {
        if (player == null)
            return;

        if (!player.gameObject.activeSelf)
            player.gameObject.SetActive(true);

        ResolveReferences();
        ClearPlayerCarriedLoadout();
        ResetPlayerSurvivalState();
        playerWeaponSelection?.SelectSecondaryWeapon();
        TeleportPlayerToAnchor(shelterAnchor, shelterOffset);
    }

    private void ClearPlayerCarriedLoadout()
    {
        if (playerInventory == null && player != null)
            playerInventory = player.GetComponent<PlayerInventory>();
        if (playerEquipment == null && player != null)
            playerEquipment = player.GetComponent<PlayerEquipment>();
        if (playerGridInventory == null && player != null)
            playerGridInventory = player.GetComponent<PlayerGridInventory>();

        playerInventory?.ClearAll();
        playerEquipment?.ClearAllEquipment();

        if (playerGridInventory != null)
        {
            playerGridInventory.PocketContainer?.Clear();
            playerGridInventory.UnequipRig();
            playerGridInventory.UnequipBackpack();
            playerGridInventory.PocketContainer?.Clear();
        }

        if (playerEquipmentVisuals == null && player != null)
            playerEquipmentVisuals = player.GetComponent<CharacterEquipmentVisuals>();
        playerEquipmentVisuals?.ForceRefreshNow();

        if (playerWeaponSelection == null && player != null)
            playerWeaponSelection = player.GetComponent<PlayerWeaponSelection>();
        playerWeaponSelection?.SelectSecondaryWeapon();
    }

    private void ResetPlayerSurvivalState()
    {
        if (playerStats == null && player != null)
            playerStats = player.GetComponent<PlayerStats>();
        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<JUHealth>();
        if (playerCharacter == null && player != null)
            playerCharacter = player.GetComponent<JUCharacterController>();

        if (playerStats != null)
            playerStats.ReviveFull();

        if (playerHealth != null)
            playerHealth.ResetHealth();

        if (playerCharacter != null)
        {
            playerCharacter.enabled = true;
            playerCharacter.IsDead = false;
            playerCharacter.DisableAllMove = false;
            playerCharacter.CanMove = true;
        }

        if (playerMove != null)
            playerMove.enabled = true;

        PlayerFaceMouse playerFaceMouse = player != null ? player.GetComponent<PlayerFaceMouse>() : null;
        if (playerFaceMouse != null)
            playerFaceMouse.enabled = true;

        PlayerShoot playerShoot = player != null ? player.GetComponent<PlayerShoot>() : null;
        if (playerShoot != null)
            playerShoot.enabled = true;

        if (characterController == null && player != null)
            characterController = player.GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = true;

        Collider rootCollider = player != null ? player.GetComponent<Collider>() : null;
        if (rootCollider != null)
        {
            rootCollider.enabled = true;
            rootCollider.isTrigger = false;
        }

        if (playerRigidbody == null && player != null)
            playerRigidbody = player.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity = true;
            playerRigidbody.isKinematic = false;
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        RestorePlayerLayer();
        ResetPlayerAnimatorState();
        DisablePlayerRagdoll();
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
        playerStats = player.GetComponent<PlayerStats>();
        playerHealth = player.GetComponent<JUHealth>();
        playerCharacter = player.GetComponent<JUCharacterController>();
        characterController = player.GetComponent<CharacterController>();
        playerRigidbody = player.GetComponent<Rigidbody>();
        playerInventory = player.GetComponent<PlayerInventory>();
        playerEquipment = player.GetComponent<PlayerEquipment>();
        playerGridInventory = player.GetComponent<PlayerGridInventory>();
        playerEquipmentVisuals = player.GetComponent<CharacterEquipmentVisuals>();
        playerWeaponSelection = player.GetComponent<PlayerWeaponSelection>();

        if (playerDefaultLayer < 0 && player != null && player.gameObject.layer != 2)
            playerDefaultLayer = player.gameObject.layer;
    }

    private void RestorePlayerLayer()
    {
        if (player == null)
            return;

        if (playerDefaultLayer >= 0)
        {
            player.gameObject.layer = playerDefaultLayer;
            return;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        player.gameObject.layer = playerLayer >= 0 ? playerLayer : 0;
    }

    private void ResetPlayerAnimatorState()
    {
        if (player == null)
            return;

        Animator animator = player.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;

        animator.enabled = true;
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            string parameterName = parameter.name.ToLowerInvariant();
            if (parameter.type == AnimatorControllerParameterType.Bool
                && (parameterName.Contains("dead")
                    || parameterName.Contains("death")
                    || parameterName.Contains("dying")
                    || parameterName.Contains("ragdoll")))
            {
                animator.SetBool(parameter.nameHash, false);
            }
        }

        for (int i = 1; i < animator.layerCount; i++)
            animator.SetLayerWeight(i, 0f);
    }

    private void DisablePlayerRagdoll()
    {
        if (player == null)
            return;

        AdvancedRagdollController[] ragdollers = player.GetComponentsInChildren<AdvancedRagdollController>(true);
        for (int i = 0; i < ragdollers.Length; i++)
        {
            AdvancedRagdollController ragdoller = ragdollers[i];
            if (ragdoller == null)
                continue;

            if (ragdoller.Hips != null && ragdoller.HipsParent != null)
                ragdoller.Hips.SetParent(ragdoller.HipsParent);

            ragdoller.SetActiveRagdoll(false);
            ragdoller.TimeToGetUp = 0f;
            ragdoller.BlendAmount = 0f;
            ragdoller.State = AdvancedRagdollController.RagdollState.Animated;
        }
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
        BuildExtractionResult(canvasRect, font);
        BuildToast(canvasRect, font);

        HideShelterPrompt();
        SetExtractionProgress(false, 0f, 1f);
        HideExtractionResult();
    }

    private void BuildShelterPrompt(RectTransform parent, Font font)
    {
        shelterPromptPanel = CreateRect("ShelterDeployPrompt", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 86f));
        shelterPromptPanel.anchoredPosition = new Vector2(0f, -72f);
        Image background = shelterPromptPanel.gameObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.06f, 0.05f, 0.78f);

        shelterPromptText = CreateText("PromptText", shelterPromptPanel, font, "HOLD F TO START RAID", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        shelterPromptText.rectTransform.anchorMin = new Vector2(0f, 0.45f);
        shelterPromptText.rectTransform.anchorMax = new Vector2(1f, 1f);
        shelterPromptText.rectTransform.offsetMin = new Vector2(16f, 0f);
        shelterPromptText.rectTransform.offsetMax = new Vector2(-16f, -4f);

        RectTransform barFrame = CreateRect("HoldFrame", shelterPromptPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(292f, 14f));
        barFrame.anchoredPosition = new Vector2(0f, 20f);
        Image frameImage = barFrame.gameObject.AddComponent<Image>();
        frameImage.color = new Color(0.10f, 0.18f, 0.14f, 0.95f);

        shelterHoldFillRect = CreateRect("HoldFill", barFrame, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero);
        shelterHoldFill = shelterHoldFillRect.gameObject.AddComponent<Image>();
        shelterHoldFill.color = new Color(0.10f, 0.82f, 0.33f, 0.95f);

        shelterHoldText = CreateText("HoldText", shelterPromptPanel, font, "HOLD F", 13, FontStyle.Bold, TextAnchor.MiddleCenter);
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

        extractionFillRect = CreateRect("ExtractionFill", extractionPanel, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero);
        extractionFill = extractionFillRect.gameObject.AddComponent<Image>();
        extractionFill.color = new Color(0.07f, 0.78f, 0.24f, 0.88f);

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

    private void BuildExtractionResult(RectTransform parent, Font font)
    {
        extractionResultPanel = CreateRect("ExtractionResultPanel", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
        Image dimmer = extractionResultPanel.gameObject.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.72f);

        RectTransform card = CreateRect("ResultCard", extractionResultPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(440f, 560f));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.08f, 0.09f, 0.09f, 0.98f);

        extractionResultTitleText = CreateText("Title", card, font, "EXTRACTED", 54, FontStyle.Bold, TextAnchor.MiddleCenter);
        extractionResultTitleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        extractionResultTitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        extractionResultTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        extractionResultTitleText.rectTransform.anchoredPosition = new Vector2(0f, -92f);
        extractionResultTitleText.rectTransform.sizeDelta = new Vector2(0f, 72f);

        extractionResultDurationText = CreateText("Duration", card, font, "Duration: 00:00:00", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        extractionResultDurationText.rectTransform.anchorMin = new Vector2(0f, 1f);
        extractionResultDurationText.rectTransform.anchorMax = new Vector2(1f, 1f);
        extractionResultDurationText.rectTransform.pivot = new Vector2(0.5f, 1f);
        extractionResultDurationText.rectTransform.anchoredPosition = new Vector2(0f, -188f);
        extractionResultDurationText.rectTransform.sizeDelta = new Vector2(0f, 44f);

        extractionResultValueText = CreateText("Value", card, font, "Raid Value: $0", 34, FontStyle.Bold, TextAnchor.MiddleCenter);
        extractionResultValueText.rectTransform.anchorMin = new Vector2(0f, 1f);
        extractionResultValueText.rectTransform.anchorMax = new Vector2(1f, 1f);
        extractionResultValueText.rectTransform.pivot = new Vector2(0.5f, 1f);
        extractionResultValueText.rectTransform.anchoredPosition = new Vector2(0f, -250f);
        extractionResultValueText.rectTransform.sizeDelta = new Vector2(0f, 54f);

        Button continueButton = CreateRect("ContinueButton", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(360f, 72f)).gameObject.AddComponent<Button>();
        RectTransform buttonRect = continueButton.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(0f, 56f);
        Image buttonImage = continueButton.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.30f, 0.90f, 0.43f, 0.96f);
        extractionResultButtonText = CreateText("Text", buttonRect, font, "CONTINUE", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
        StretchToParent(extractionResultButtonText.rectTransform, Vector2.zero, Vector2.zero);
        continueButton.targetGraphic = buttonImage;
        continueButton.onClick.AddListener(HandleResultButtonClicked);
    }

    private void ShowExtractionResult(bool extracted, float durationSeconds, float extractedValue)
    {
        if (extractionResultPanel == null)
        {
            ShowToast(extracted ? "Extraction complete" : "Action failed");
            return;
        }

        if (extractionResultTitleText != null)
            extractionResultTitleText.text = extracted ? "EXTRACTED" : "ACTION FAILED";

        if (extractionResultDurationText != null)
            extractionResultDurationText.text = "Duration: " + FormatDuration(durationSeconds);

        if (extractionResultValueText != null)
            extractionResultValueText.text = extracted ? "Raid Value: " + FormatMoney(extractedValue) : "Raid Value: $0";

        if (extractionResultButtonText != null)
            extractionResultButtonText.text = extracted ? "CONTINUE" : "RESPAWN";

        waitingForRespawnConfirm = !extracted;
        extractionResultPanel.gameObject.SetActive(true);
        extractionResultPanel.SetAsLastSibling();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void HandleResultButtonClicked()
    {
        if (waitingForRespawnConfirm)
        {
            waitingForRespawnConfirm = false;
            RevivePlayerAtShelter();
            isInRaid = false;
            raidBaselineCaptured = false;
        }

        HideExtractionResult();
    }

    private void HideExtractionResult()
    {
        if (extractionResultPanel != null)
            extractionResultPanel.gameObject.SetActive(false);

        if (CrosshairCursor.Instance != null)
            CrosshairCursor.Instance.ApplyCursor();
    }

    private void CaptureRaidBaseline(Transform playerTransform)
    {
        raidBaselineInstanceIds.Clear();
        raidBaselineStackQuantities.Clear();
        raidBaselineContainerStates.Clear();
        raidBaselineCaptured = true;

        if (playerTransform == null)
            return;

        PlayerInventory inventory = playerTransform.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            for (int i = 0; i < inventory.SlotCount; i++)
                CaptureSlotBaseline(inventory.GetSlot(i));
        }

        PlayerEquipment equipment = playerTransform.GetComponent<PlayerEquipment>();
        if (equipment != null)
        {
            for (int i = 0; i < equipment.EquippedSlots.Count; i++)
                CaptureSlotBaseline(equipment.EquippedSlots[i].slot);
        }

        PlayerGridInventory gridInventory = playerTransform.GetComponent<PlayerGridInventory>();
        if (gridInventory != null)
        {
            CaptureContainerBaseline(gridInventory.PocketContainer);
            if (gridInventory.HasRigContainer)
                CaptureContainerBaseline(gridInventory.RigContainer);
            if (gridInventory.HasBackpackContainer)
                CaptureContainerBaseline(gridInventory.BackpackContainer);
        }
    }

    private void CaptureSlotBaseline(InventorySlot slot)
    {
        if (slot == null || slot.IsEmpty || slot.Item == null)
            return;

        CaptureItemBaseline(slot.Item, slot.Quantity, slot.RuntimeData);
    }

    private void CaptureContainerBaseline(GridContainerState container)
    {
        if (container == null)
            return;
        if (!raidBaselineContainerStates.Add(container))
            return;

        IReadOnlyList<GridItemPlacement> placements = container.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty || placement.Item == null)
                continue;

            CaptureItemBaseline(placement.Item, placement.Quantity, placement.RuntimeData);
        }
    }

    private void CaptureItemBaseline(ItemDefinition item, int quantity, ItemRuntimeData runtimeData)
    {
        if (item == null || quantity <= 0)
            return;

        if (item.canStack)
        {
            if (!raidBaselineStackQuantities.ContainsKey(item))
                raidBaselineStackQuantities[item] = 0;

            raidBaselineStackQuantities[item] += quantity;
        }
        else if (runtimeData != null && !string.IsNullOrWhiteSpace(runtimeData.InstanceId))
        {
            raidBaselineInstanceIds.Add(runtimeData.InstanceId);
        }

        if (runtimeData?.StoredContainerState != null)
            CaptureContainerBaseline(runtimeData.StoredContainerState);
    }

    private float CalculateExtractedValue(Transform playerTransform)
    {
        if (playerTransform == null)
            return 0f;

        float totalValue = 0f;
        Dictionary<ItemDefinition, int> stackBaselineRemaining = raidBaselineCaptured
            ? new Dictionary<ItemDefinition, int>(raidBaselineStackQuantities)
            : new Dictionary<ItemDefinition, int>();

        PlayerInventory playerInventory = playerTransform.GetComponent<PlayerInventory>();
        if (playerInventory != null)
        {
            for (int i = 0; i < playerInventory.SlotCount; i++)
                totalValue += CalculateSlotValue(playerInventory.GetSlot(i), includeNested: true, stackBaselineRemaining);
        }

        PlayerEquipment playerEquipment = playerTransform.GetComponent<PlayerEquipment>();
        if (playerEquipment != null)
        {
            for (int i = 0; i < playerEquipment.EquippedSlots.Count; i++)
                totalValue += CalculateSlotValue(playerEquipment.EquippedSlots[i].slot, includeNested: false, stackBaselineRemaining);
        }

        PlayerGridInventory playerGridInventory = playerTransform.GetComponent<PlayerGridInventory>();
        if (playerGridInventory != null)
        {
            totalValue += CalculateContainerValue(playerGridInventory.PocketContainer, stackBaselineRemaining);
            if (playerGridInventory.HasRigContainer)
                totalValue += CalculateContainerValue(playerGridInventory.RigContainer, stackBaselineRemaining);
            if (playerGridInventory.HasBackpackContainer)
                totalValue += CalculateContainerValue(playerGridInventory.BackpackContainer, stackBaselineRemaining);
        }

        return totalValue;
    }

    private float CalculateSlotValue(InventorySlot slot, bool includeNested, Dictionary<ItemDefinition, int> stackBaselineRemaining)
    {
        if (slot == null || slot.IsEmpty || slot.Item == null)
            return 0f;

        float value = CalculateItemValue(slot.Item, slot.Quantity, slot.RuntimeData, stackBaselineRemaining);
        if (includeNested && slot.RuntimeData?.StoredContainerState != null)
            value += CalculateContainerValue(slot.RuntimeData.StoredContainerState, stackBaselineRemaining);

        return value;
    }

    private float CalculateContainerValue(GridContainerState container, Dictionary<ItemDefinition, int> stackBaselineRemaining)
    {
        if (container == null)
            return 0f;

        float value = 0f;
        IReadOnlyList<GridItemPlacement> placements = container.Placements;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty || placement.Item == null)
                continue;

            value += CalculateItemValue(placement.Item, placement.Quantity, placement.RuntimeData, stackBaselineRemaining);
            if (placement.RuntimeData?.StoredContainerState != null)
                value += CalculateContainerValue(placement.RuntimeData.StoredContainerState, stackBaselineRemaining);
        }

        return value;
    }

    private float CalculateItemValue(
        ItemDefinition item,
        int quantity,
        ItemRuntimeData runtimeData,
        Dictionary<ItemDefinition, int> stackBaselineRemaining)
    {
        if (item == null || quantity <= 0)
            return 0f;

        if (item.canStack)
        {
            int countedQuantity = quantity;
            if (stackBaselineRemaining != null
                && stackBaselineRemaining.TryGetValue(item, out int baselineQuantity)
                && baselineQuantity > 0)
            {
                int consumedBaseline = Mathf.Min(quantity, baselineQuantity);
                countedQuantity -= consumedBaseline;
                stackBaselineRemaining[item] = baselineQuantity - consumedBaseline;
            }

            return countedQuantity > 0 ? item.GetTotalMoneyValue(countedQuantity) : 0f;
        }

        if (runtimeData != null
            && !string.IsNullOrWhiteSpace(runtimeData.InstanceId)
            && raidBaselineInstanceIds.Contains(runtimeData.InstanceId))
        {
            return 0f;
        }

        return item.GetTotalMoneyValue(quantity);
    }

    private static string FormatDuration(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds / 60) % 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{hours:00}:{minutes:00}:{remainingSeconds:00}";
    }

    private static string FormatMoney(float value)
    {
        return "$" + Mathf.RoundToInt(Mathf.Max(0f, value)).ToString("N0");
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
