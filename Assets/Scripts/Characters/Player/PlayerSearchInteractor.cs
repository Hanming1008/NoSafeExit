using JUTPS;
using JUTPS.JUInputSystem;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class PlayerSearchInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameplayUIRoot uiRoot;
    [SerializeField] private JUPlayerCharacterInputAsset inputAsset;

    [Header("Search")]
    [SerializeField] private LayerMask containerLayerMask = ~0;
    [SerializeField] private Vector3 checkOffset = new Vector3(0f, 0.75f, 0f);
    [SerializeField] private float fallbackInteractionRadius = 2f;

    private readonly Collider[] overlapResults = new Collider[24];
    private int lastConsumedInteractFrame = -1;

    public SearchableContainer CurrentContainer { get; private set; }
    public EnemyCorpseLoot CurrentCorpse { get; private set; }

    public bool WasInteractConsumedThisFrame()
    {
        return lastConsumedInteractFrame == Time.frameCount;
    }

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();
        fallbackInteractionRadius = Mathf.Max(0.25f, fallbackInteractionRadius);
    }

    void Update()
    {
        CurrentContainer = null;
        CurrentCorpse = null;
        FindNearestSearchTarget(out SearchableContainer nearestContainer, out EnemyCorpseLoot nearestCorpse);
        CurrentContainer = nearestContainer;
        CurrentCorpse = nearestCorpse;

        if (CurrentContainer == null && CurrentCorpse == null)
            return;

        if (!IsInteractTriggered())
            return;

        if (uiRoot == null)
            return;

        if (CurrentCorpse != null)
            uiRoot.OpenInventoryWithCorpse(CurrentCorpse);
        else
            uiRoot.OpenInventoryWithExternalContainer(CurrentContainer);

        lastConsumedInteractFrame = Time.frameCount;
    }

    private void FindNearestSearchTarget(out SearchableContainer nearestContainer, out EnemyCorpseLoot nearestCorpse)
    {
        nearestContainer = null;
        nearestCorpse = null;

        Vector3 center = transform.TransformPoint(checkOffset);
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            fallbackInteractionRadius,
            overlapResults,
            containerLayerMask,
            QueryTriggerInteraction.Collide);

        float nearestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            overlapResults[i] = null;
            if (hit == null)
                continue;

            EnemyCorpseLoot corpse = hit.GetComponent<EnemyCorpseLoot>();
            if (corpse == null)
                corpse = hit.GetComponentInParent<EnemyCorpseLoot>();

            if (corpse != null && corpse.IsSearchable)
            {
                float allowedRadius = Mathf.Max(fallbackInteractionRadius, corpse.InteractionRadius);
                float distanceSqr = (corpse.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr <= allowedRadius * allowedRadius && distanceSqr < nearestDistanceSqr)
                {
                    nearestContainer = null;
                    nearestCorpse = corpse;
                    nearestDistanceSqr = distanceSqr;
                }
            }

            SearchableContainer container = hit.GetComponent<SearchableContainer>();
            if (container == null)
                container = hit.GetComponentInParent<SearchableContainer>();

            if (container == null)
                continue;

            float containerAllowedRadius = Mathf.Max(fallbackInteractionRadius, container.InteractionRadius);
            float containerDistanceSqr = (container.transform.position - transform.position).sqrMagnitude;
            if (containerDistanceSqr > containerAllowedRadius * containerAllowedRadius || containerDistanceSqr >= nearestDistanceSqr)
                continue;

            nearestContainer = container;
            nearestCorpse = null;
            nearestDistanceSqr = containerDistanceSqr;
        }
    }

    private bool IsInteractTriggered()
    {
        if (inputAsset != null)
            return inputAsset.IsInteractTriggered;

        return Input.GetKeyDown(KeyCode.F);
    }

    private void ResolveReferences()
    {
        if (uiRoot == null)
            uiRoot = FindFirstObjectByType<GameplayUIRoot>(FindObjectsInactive.Include);

        if (inputAsset == null)
        {
            JUCharacterController controller = GetComponent<JUCharacterController>();
            if (controller != null && controller.Inputs != null)
                inputAsset = controller.Inputs;
        }
    }
}
