using UnityEngine;

[DisallowMultipleComponent]
public class ShelterStashStation : MonoBehaviour
{
    [Header("Stash")]
    [SerializeField] private string stashDisplayName = "Your Stash";
    [Min(1)] [SerializeField] private int stashRows = 12;
    [Min(1)] [SerializeField] private int stashColumns = 8;
    [Min(0.25f)] [SerializeField] private float interactionRadius = 2.5f;
    [SerializeField] private Vector3 playerCheckOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Prompt")]
    [SerializeField] private bool showPrompt = true;
    [SerializeField] private string promptText = "F  Open Stash";

    private SearchableContainer stashContainer;
    private GameplayUIRoot uiRoot;
    private Transform player;
    private bool playerNear;

    public SearchableContainer StashContainer => stashContainer;
    public bool IsPlayerNear => playerNear;

    void Awake()
    {
        EnsureStashContainer();
        ResolveReferences();
    }

    void OnValidate()
    {
        stashRows = Mathf.Max(1, stashRows);
        stashColumns = Mathf.Max(1, stashColumns);
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
    }

    void Update()
    {
        ResolveReferences();
        playerNear = IsPlayerWithinRange();

        if (!playerNear || uiRoot == null)
            return;

        if (Input.GetKeyDown(KeyCode.F))
            uiRoot.OpenInventoryWithExternalContainer(stashContainer);
    }

    void OnGUI()
    {
        // The shared world interaction prompt handles stash prompts now.
        if (FindFirstObjectByType<WorldPickupPromptUI>() != null)
            return;

        if (!showPrompt || !playerNear || Cursor.visible)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(0.9f, 0.96f, 0.9f, 0.94f);

        Rect rect = new Rect((Screen.width - 320f) * 0.5f, Screen.height * 0.62f, 320f, 34f);
        GUI.Label(rect, promptText, style);
    }

    public void EnsureStashContainer()
    {
        if (stashContainer == null)
            stashContainer = GetComponent<SearchableContainer>();

        if (stashContainer == null)
            stashContainer = gameObject.AddComponent<SearchableContainer>();

        stashContainer.SetDisplayName(stashDisplayName);
        stashContainer.SetDimensions(stashRows, stashColumns);
        stashContainer.ConfigureRandomLoot(null, false, false, false);
        stashContainer.EnsureInitialized();
    }

    private bool IsPlayerWithinRange()
    {
        if (player == null)
            return false;

        Vector3 center = player.TransformPoint(playerCheckOffset);
        float distanceSqr = (transform.position - center).sqrMagnitude;
        return distanceSqr <= interactionRadius * interactionRadius;
    }

    private void ResolveReferences()
    {
        if (uiRoot == null)
            uiRoot = FindFirstObjectByType<GameplayUIRoot>(FindObjectsInactive.Include);

        if (player == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                player = taggedPlayer.transform;
        }

        if (player == null)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Exclude);
            if (stats != null)
                player = stats.transform;
        }

    }
}
