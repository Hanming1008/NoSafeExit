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
        GameObject pickupObject = null;
        try
        {
            TryCreateModelPickup(definition, groundedPosition, rotation, out pickupObject);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"WorldItemPickup: Failed to create model pickup for {definition.displayName}. Falling back.\n{exception}");
            if (pickupObject != null)
                Destroy(pickupObject);

            pickupObject = null;
        }

        if (pickupObject == null && HasModelPickupSource(definition))
            Debug.LogWarning($"WorldItemPickup: {definition.displayName} has model pickup data but could not create a model, using sprite fallback.");

        if (pickupObject == null && !TryCreateSpritePickup(definition, groundedPosition, out pickupObject))
        {
            pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pickupObject.transform.SetPositionAndRotation(groundedPosition, rotation);
            pickupObject.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        }

        try
        {
            WorldItemPickup pickup = pickupObject.GetComponent<WorldItemPickup>();
            if (pickup == null)
                pickup = pickupObject.AddComponent<WorldItemPickup>();

            pickup.Configure(definition, itemQuantity, runtimeData);
            return pickup;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"WorldItemPickup: Failed to finalize pickup for {definition.displayName}. {exception.Message}");
            if (pickupObject != null)
                Destroy(pickupObject);

            return null;
        }
    }

    private void EnsurePickupCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            BoxCollider fallbackCollider = gameObject.AddComponent<BoxCollider>();
            if (TryGetRendererBounds(gameObject, out Bounds rendererBounds))
            {
                fallbackCollider.center = transform.InverseTransformPoint(rendererBounds.center);
                Vector3 localSize = transform.InverseTransformVector(rendererBounds.size);
                fallbackCollider.size = new Vector3(
                    Mathf.Max(0.2f, Mathf.Abs(localSize.x)),
                    Mathf.Max(0.2f, Mathf.Abs(localSize.y)),
                    Mathf.Max(0.2f, Mathf.Abs(localSize.z)));
            }

            fallbackCollider.isTrigger = true;
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
                continue;

            if (collider is MeshCollider meshCollider)
                meshCollider.convex = true;

            collider.isTrigger = true;
        }
    }

    private static bool TryCreateModelPickup(
        ItemDefinition definition,
        Vector3 groundedPosition,
        Quaternion baseRotation,
        out GameObject pickupObject)
    {
        pickupObject = null;

        if (definition.worldPrefab != null)
        {
            pickupObject = InstantiateGameObject(
                definition.worldPrefab,
                groundedPosition,
                ResolveModelPickupRotation(definition, baseRotation));
            if (pickupObject == null)
                return false;

            PrepareModelPickup(definition, pickupObject, groundedPosition, baseRotation);
            return true;
        }

        if (definition is WeaponItemDefinition weapon && weapon.equippedPrefab != null)
        {
            pickupObject = InstantiateGameObject(
                weapon.equippedPrefab,
                groundedPosition,
                ResolveModelPickupRotation(definition, baseRotation));
            if (pickupObject == null)
                return false;

            PrepareModelPickup(definition, pickupObject, groundedPosition, baseRotation);
            return true;
        }

        if (TryCreateCharacterVisualPickup(definition, groundedPosition, baseRotation, out pickupObject))
            return true;

        GameObject[] visualPrefabs = GetCompositeWorldVisualPrefabs(definition);
        if (visualPrefabs == null || visualPrefabs.Length == 0)
            return false;

        pickupObject = new GameObject($"Pickup_{definition.displayName}_Model");
        pickupObject.transform.SetPositionAndRotation(
            groundedPosition,
            ResolveModelPickupRotation(definition, baseRotation));

        for (int i = 0; i < visualPrefabs.Length; i++)
        {
            if (visualPrefabs[i] == null)
                continue;

            GameObject visual = InstantiateGameObject(visualPrefabs[i], pickupObject.transform);
            if (visual == null)
                continue;

            visual.name = visualPrefabs[i].name;
        }

        if (!TryGetRendererBounds(pickupObject, out _))
        {
            Destroy(pickupObject);
            pickupObject = null;
            return false;
        }

        PrepareModelPickup(definition, pickupObject, groundedPosition, baseRotation);
        return true;
    }

    private static bool TryCreateCharacterVisualPickup(
        ItemDefinition definition,
        Vector3 groundedPosition,
        Quaternion baseRotation,
        out GameObject pickupObject)
    {
        pickupObject = null;
        if (definition is not ArmorItemDefinition && definition is not ContainerItemDefinition)
            return false;

        CharacterEquipmentVisuals equipmentVisuals = FindFirstObjectByType<CharacterEquipmentVisuals>(FindObjectsInactive.Include);
        if (equipmentVisuals == null)
            return false;

        pickupObject = new GameObject($"Pickup_{definition.displayName}_Model");
        pickupObject.transform.SetPositionAndRotation(
            groundedPosition,
            ResolveModelPickupRotation(definition, baseRotation));

        if (!equipmentVisuals.TryBuildWorldPickupVisual(definition, pickupObject.transform)
            || !TryGetRendererBounds(pickupObject, out _))
        {
            Destroy(pickupObject);
            pickupObject = null;
            return false;
        }

        PrepareModelPickup(definition, pickupObject, groundedPosition, baseRotation);
        return true;
    }

    private static GameObject InstantiateGameObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        return Instantiate(prefab, position, rotation);
    }

    private static GameObject InstantiateGameObject(GameObject prefab, Transform parent)
    {
        if (prefab == null)
            return null;

        return Instantiate(prefab, parent);
    }

    private static GameObject[] GetCompositeWorldVisualPrefabs(ItemDefinition definition)
    {
        if (definition is ArmorItemDefinition armor)
            return armor.worldVisualPrefabs;

        if (definition is ContainerItemDefinition container)
            return container.worldVisualPrefabs;

        return null;
    }

    private static bool HasModelPickupSource(ItemDefinition definition)
    {
        if (definition == null)
            return false;

        if (definition.worldPrefab != null)
            return true;

        if (definition is WeaponItemDefinition weapon && weapon.equippedPrefab != null)
            return true;

        GameObject[] visualPrefabs = GetCompositeWorldVisualPrefabs(definition);
        if (visualPrefabs == null)
            return false;

        for (int i = 0; i < visualPrefabs.Length; i++)
        {
            if (visualPrefabs[i] != null)
                return true;
        }

        return false;
    }

    private static Quaternion ResolveModelPickupRotation(ItemDefinition definition, Quaternion baseRotation)
    {
        Vector3 baseEuler = baseRotation.eulerAngles;
        Quaternion yawOnly = Quaternion.Euler(0f, baseEuler.y, 0f);

        if (definition is WeaponItemDefinition)
            return yawOnly * Quaternion.Euler(0f, 0f, 90f);

        return yawOnly;
    }

    private static void PrepareModelPickup(ItemDefinition definition, GameObject pickupObject, Vector3 groundedPosition, Quaternion baseRotation)
    {
        if (pickupObject == null)
            return;

        DisablePickupModelBehaviours(pickupObject);
        DisablePickupModelRigidbodies(pickupObject);

        if (definition is WeaponItemDefinition)
            OrientWeaponFlatOnGround(pickupObject, baseRotation);

        MoveBottomToGround(pickupObject, groundedPosition);
    }

    private static void DisablePickupModelBehaviours(GameObject pickupObject)
    {
        MonoBehaviour[] behaviours = pickupObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour is WorldItemPickup)
                continue;

            behaviour.enabled = false;
        }
    }

    private static void DisablePickupModelRigidbodies(GameObject pickupObject)
    {
        Rigidbody[] rigidbodies = pickupObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (body == null)
                continue;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    private static void OrientWeaponFlatOnGround(GameObject pickupObject, Quaternion baseRotation)
    {
        if (!TryGetRendererBounds(pickupObject, out _))
            return;

        Quaternion yawOnly = Quaternion.Euler(0f, baseRotation.eulerAngles.y, 0f);
        Quaternion[] candidates =
        {
            yawOnly,
            yawOnly * Quaternion.Euler(90f, 0f, 0f),
            yawOnly * Quaternion.Euler(-90f, 0f, 0f),
            yawOnly * Quaternion.Euler(0f, 0f, 90f),
            yawOnly * Quaternion.Euler(0f, 0f, -90f),
            yawOnly * Quaternion.Euler(90f, 90f, 0f),
            yawOnly * Quaternion.Euler(0f, 90f, 90f)
        };

        Quaternion bestRotation = pickupObject.transform.rotation;
        float bestHeight = float.MaxValue;
        for (int i = 0; i < candidates.Length; i++)
        {
            pickupObject.transform.rotation = candidates[i];
            if (!TryGetRendererBounds(pickupObject, out Bounds bounds))
                continue;

            if (bounds.size.y < bestHeight)
            {
                bestHeight = bounds.size.y;
                bestRotation = candidates[i];
            }
        }

        pickupObject.transform.rotation = bestRotation;
    }

    private static void MoveBottomToGround(GameObject pickupObject, Vector3 groundedPosition)
    {
        if (!TryGetRendererBounds(pickupObject, out Bounds bounds))
            return;

        Vector3 position = pickupObject.transform.position;
        position.y += groundedPosition.y - bounds.min.y + 0.02f;
        pickupObject.transform.position = position;
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
            if (renderer == null)
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
