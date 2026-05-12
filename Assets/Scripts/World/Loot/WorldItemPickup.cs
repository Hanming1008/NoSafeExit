using UnityEngine;

[DisallowMultipleComponent]
public class WorldItemPickup : MonoBehaviour
{
    [Header("Item")]
    [SerializeField] private ItemDefinition itemDefinition;
    [Min(1)]
    [SerializeField] private int quantity = 1;
    [SerializeReference] private ItemRuntimeData runtimeData;
    [SerializeField] private bool destroyWhenEmpty = true;

    public ItemDefinition ItemDefinition => itemDefinition;
    public int Quantity => Mathf.Max(0, quantity);
    public ItemRuntimeData RuntimeData => runtimeData;
    public bool CanBePickedUp => itemDefinition != null && quantity > 0;
    public string DisplayName => itemDefinition != null ? itemDefinition.displayName : name;

    void OnValidate()
    {
        if (quantity < 1)
            quantity = 1;
    }

    public void Configure(ItemDefinition definition, int itemQuantity, bool shouldDestroyWhenEmpty = true)
    {
        Configure(definition, itemQuantity, null, shouldDestroyWhenEmpty);
    }

    public void Configure(ItemDefinition definition, int itemQuantity, ItemRuntimeData itemRuntimeData, bool shouldDestroyWhenEmpty = true)
    {
        itemDefinition = definition;
        quantity = Mathf.Max(1, itemQuantity);
        runtimeData = ResolveRuntimeData(definition, itemRuntimeData);
        destroyWhenEmpty = shouldDestroyWhenEmpty;

        if (definition != null)
            name = $"Pickup_{definition.displayName}";

        EnsurePickupCollider();
    }

    public int Consume(int requestedAmount)
    {
        if (!CanBePickedUp || requestedAmount <= 0)
            return 0;

        int consumedAmount = Mathf.Min(quantity, requestedAmount);
        quantity -= consumedAmount;

        if (quantity <= 0)
        {
            quantity = 0;
            runtimeData = null;

            if (destroyWhenEmpty)
                Destroy(gameObject);
        }

        return consumedAmount;
    }

    public static WorldItemPickup Spawn(ItemDefinition definition, int itemQuantity, Vector3 position, Quaternion rotation)
    {
        return Spawn(definition, itemQuantity, null, position, rotation);
    }

    public static WorldItemPickup Spawn(ItemDefinition definition, int itemQuantity, ItemRuntimeData runtimeData, Vector3 position, Quaternion rotation)
    {
        if (definition == null || itemQuantity <= 0)
            return null;

        Vector3 groundedPosition = ResolveGroundedSpawnPosition(position);
        GameObject pickupObject;
        if (definition.worldPrefab != null)
        {
            pickupObject = Instantiate(definition.worldPrefab, groundedPosition, rotation);
        }
        else if (TryCreateSpritePickup(definition, groundedPosition, out pickupObject))
        {
        }
        else
        {
            pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickupObject.transform.SetPositionAndRotation(groundedPosition, rotation);
            pickupObject.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        }

        WorldItemPickup pickup = pickupObject.GetComponent<WorldItemPickup>();
        if (pickup == null)
            pickup = pickupObject.AddComponent<WorldItemPickup>();

        pickup.Configure(definition, itemQuantity, runtimeData);
        return pickup;
    }

    private void EnsurePickupCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            BoxCollider fallbackCollider = gameObject.AddComponent<BoxCollider>();
            fallbackCollider.isTrigger = true;
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
                continue;

            collider.isTrigger = true;
        }
    }

    private static bool TryCreateSpritePickup(ItemDefinition definition, Vector3 position, out GameObject pickupObject)
    {
        pickupObject = null;
        Sprite sprite = definition.GetGridInventorySpriteOrFallback();
        if (sprite == null)
            return false;

        pickupObject = new GameObject($"Pickup_{definition.displayName}_Sprite");
        pickupObject.transform.SetPositionAndRotation(
            position + new Vector3(0f, 0.03f, 0f),
            ResolveSpritePickupRotation());

        SpriteRenderer renderer = pickupObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 10;
        renderer.flipX = definition.ShouldFlipGridDisplaySprite();

        Vector2 spriteSize = sprite.bounds.size;
        float maxDimension = Mathf.Max(spriteSize.x, spriteSize.y);
        if (maxDimension > 0.0001f)
        {
            float targetWorldSize = definition.GetWorldSpriteTargetSize();
            float uniformScale = targetWorldSize / maxDimension;
            pickupObject.transform.localScale = Vector3.one * uniformScale;
        }
        else
        {
            pickupObject.transform.localScale = Vector3.one * 0.4f;
        }

        BoxCollider collider = pickupObject.AddComponent<BoxCollider>();
        Vector3 localSize;
        if (spriteSize.sqrMagnitude <= 0.0001f)
            localSize = new Vector3(0.4f, 0.4f, 0.05f);
        else
            localSize = new Vector3(Mathf.Max(0.2f, spriteSize.x), Mathf.Max(0.2f, spriteSize.y), 0.08f);

        collider.size = localSize;
        collider.center = Vector3.zero;
        collider.isTrigger = true;
        return true;
    }

    private static Quaternion ResolveSpritePickupRotation()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return Quaternion.Euler(42f, 0f, 0f);

        Vector3 flatFacing = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
        if (flatFacing.sqrMagnitude <= 0.0001f)
            flatFacing = Vector3.forward;

        Quaternion yawOnly = Quaternion.LookRotation(flatFacing.normalized, Vector3.up);
        Quaternion tilt = Quaternion.Euler(42f, 0f, 0f);
        return yawOnly * tilt;
    }

    private static Vector3 ResolveGroundedSpawnPosition(Vector3 desiredPosition)
    {
        Vector3 rayOrigin = desiredPosition + Vector3.up * 6f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point;

        return desiredPosition;
    }

    private static ItemRuntimeData ResolveRuntimeData(ItemDefinition definition, ItemRuntimeData itemRuntimeData)
    {
        if (definition == null)
            return null;

        if (definition.canStack)
            return itemRuntimeData;

        ItemRuntimeData resolvedRuntimeData = itemRuntimeData ?? ItemRuntimeData.CreateFor(definition);
        resolvedRuntimeData.EnsureFor(definition);
        return resolvedRuntimeData;
    }

}
