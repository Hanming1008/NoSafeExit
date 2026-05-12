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
        CurrentContainer = FindNearestContainer();
        if (CurrentContainer == null)
            return;

        if (!IsInteractTriggered())
            return;

        if (uiRoot == null)
            return;

        uiRoot.OpenInventoryWithExternalContainer(CurrentContainer);
        lastConsumedInteractFrame = Time.frameCount;
    }

    private SearchableContainer FindNearestContainer()
    {
        Vector3 center = transform.TransformPoint(checkOffset);
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            fallbackInteractionRadius,
            overlapResults,
            containerLayerMask,
            QueryTriggerInteraction.Collide);

        SearchableContainer nearest = null;
        float nearestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            overlapResults[i] = null;
            if (hit == null)
                continue;

            SearchableContainer container = hit.GetComponent<SearchableContainer>();
            if (container == null)
                container = hit.GetComponentInParent<SearchableContainer>();

            if (container == null)
                continue;

            float allowedRadius = Mathf.Max(fallbackInteractionRadius, container.InteractionRadius);
            float distanceSqr = (container.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr > allowedRadius * allowedRadius || distanceSqr >= nearestDistanceSqr)
                continue;

            nearest = container;
            nearestDistanceSqr = distanceSqr;
        }

        return nearest;
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
