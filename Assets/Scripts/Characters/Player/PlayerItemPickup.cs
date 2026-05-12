using JUTPS;
using JUTPS.InteractionSystem;
using JUTPS.JUInputSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerItemPickup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerGridInventory gridInventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private CharacterEquipmentVisuals equipmentVisuals;
    [SerializeField] private PlayerSearchInteractor searchInteractor;
    [SerializeField] private JUPlayerCharacterInputAsset inputAsset;

    [Header("Pickup Search")]
    [SerializeField] private LayerMask pickupLayerMask = ~0;
    [SerializeField] private Vector3 checkOffset = new Vector3(0f, 0.75f, 0f);
    [SerializeField] private float pickupRadius = 1.4f;
    [SerializeField] private bool logDebugMessages = true;

    private readonly Collider[] overlapResults = new Collider[16];

    public WorldItemPickup CurrentPickup { get; private set; }

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();

        if (pickupRadius < 0.1f)
            pickupRadius = 0.1f;
    }

    void Update()
    {
        CurrentPickup = FindNearestPickup();

        if (searchInteractor != null && searchInteractor.WasInteractConsumedThisFrame())
            return;

        if (CurrentPickup == null)
            return;

        if (IsInteractTriggered())
            TryPickUp(CurrentPickup);
    }

    public bool TryPickUp(WorldItemPickup pickup)
    {
        if (pickup == null || !pickup.CanBePickedUp)
            return false;

        if (gridInventory == null && inventory == null)
        {
            Log("Pickup failed because no inventory receiver is available.");
            return false;
        }

        int quantityToAdd = pickup.Quantity;
        ItemRuntimeData runtimeData = pickup.Quantity <= 1 ? pickup.RuntimeData : null;
        int addedQuantity = TryReceivePickup(pickup.ItemDefinition, quantityToAdd, runtimeData);
        if (addedQuantity <= 0)
        {
            Log($"Pickup failed for {pickup.DisplayName}.");
            return false;
        }

        pickup.Consume(addedQuantity);
        equipmentVisuals?.ForceRefreshNow();
        Log($"Picked up {pickup.DisplayName} x{addedQuantity}.");
        return true;
    }

    private int TryReceivePickup(ItemDefinition itemDefinition, int quantityToAdd, ItemRuntimeData runtimeData)
    {
        if (itemDefinition == null || quantityToAdd <= 0)
            return 0;

        if (quantityToAdd == 1)
        {
            if (TryAutoEquipPickup(itemDefinition, runtimeData))
                return 1;
        }

        if (gridInventory != null)
        {
            bool includePocket = itemDefinition is not ArmorItemDefinition
                && itemDefinition is not ContainerItemDefinition;

            if (gridInventory.TryAddToCarriedContainers(itemDefinition, quantityToAdd, runtimeData, includePocket, out _, out _))
                return quantityToAdd;

            return 0;
        }

        return inventory != null ? inventory.AddItem(itemDefinition, quantityToAdd, runtimeData) : 0;
    }

    private bool TryAutoEquipPickup(ItemDefinition itemDefinition, ItemRuntimeData runtimeData)
    {
        if (equipment == null || itemDefinition == null)
            return false;

        EquipmentSlotType? targetSlot = GetAutoEquipSlot(itemDefinition);
        if (!targetSlot.HasValue)
            return false;

        InventorySlot slot = equipment.GetSlot(targetSlot.Value);
        if (slot == null || !slot.IsEmpty)
            return false;

        return equipment.TryAssignEquippedItem(targetSlot.Value, itemDefinition, 1, true)
            && ApplyRuntimeToEquippedSlot(targetSlot.Value, itemDefinition, runtimeData);
    }

    private bool ApplyRuntimeToEquippedSlot(EquipmentSlotType slotType, ItemDefinition itemDefinition, ItemRuntimeData runtimeData)
    {
        if (equipment == null || itemDefinition == null)
            return false;

        InventorySlot slot = equipment.GetSlot(slotType);
        if (slot == null || slot.IsEmpty || slot.Item != itemDefinition)
            return false;

        if (runtimeData == null)
            return true;

        return slot.TrySet(itemDefinition, slot.Quantity, runtimeData);
    }

    private static EquipmentSlotType? GetAutoEquipSlot(ItemDefinition itemDefinition)
    {
        if (itemDefinition is WeaponItemDefinition weaponItem)
        {
            return weaponItem.weaponCategory == WeaponCategory.Pistol
                ? EquipmentSlotType.SecondaryWeapon
                : EquipmentSlotType.PrimaryWeapon;
        }

        if (itemDefinition is ArmorItemDefinition armorItem)
        {
            return armorItem.armorSlot switch
            {
                ArmorSlotType.Head => EquipmentSlotType.HeadArmor,
                ArmorSlotType.Chest => EquipmentSlotType.ChestArmor,
                _ => null
            };
        }

        if (itemDefinition is ContainerItemDefinition containerItem && containerItem.containerKind == GridContainerKind.Backpack)
            return EquipmentSlotType.Backpack;

        return null;
    }

    private WorldItemPickup FindNearestPickup()
    {
        Vector3 checkCenter = transform.TransformPoint(checkOffset);
        int hitCount = Physics.OverlapSphereNonAlloc(
            checkCenter,
            pickupRadius,
            overlapResults,
            pickupLayerMask,
            QueryTriggerInteraction.Collide);

        WorldItemPickup nearestPickup = null;
        float nearestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapResults[i];
            overlapResults[i] = null;

            if (hit == null)
                continue;

            WorldItemPickup pickup = hit.GetComponent<WorldItemPickup>();
            if (pickup == null)
                pickup = hit.GetComponentInParent<WorldItemPickup>();

            if (pickup == null || !pickup.CanBePickedUp)
                continue;

            float distanceSqr = (pickup.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearestPickup = pickup;
            }
        }

        return nearestPickup;
    }

    private bool IsInteractTriggered()
    {
        if (inputAsset != null)
            return inputAsset.IsInteractTriggered;

        return Input.GetKeyDown(KeyCode.F);
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (gridInventory == null)
            gridInventory = GetComponent<PlayerGridInventory>();

        if (equipment == null)
            equipment = GetComponent<PlayerEquipment>();

        if (equipmentVisuals == null)
            equipmentVisuals = GetComponent<CharacterEquipmentVisuals>();

        if (searchInteractor == null)
            searchInteractor = GetComponent<PlayerSearchInteractor>();

        if (inputAsset == null)
        {
            JUCharacterController characterController = GetComponent<JUCharacterController>();
            if (characterController != null && characterController.Inputs != null)
            {
                inputAsset = characterController.Inputs;
                return;
            }

            JUInteractionSystem interactionSystem = GetComponent<JUInteractionSystem>();
            if (interactionSystem != null && interactionSystem.Inputs != null)
            {
                inputAsset = interactionSystem.Inputs;
                return;
            }
        }
    }

    private void Log(string message)
    {
        if (!logDebugMessages)
            return;

        Debug.Log($"PlayerItemPickup: {message}", this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
        Gizmos.DrawSphere(transform.TransformPoint(checkOffset), pickupRadius);
    }
}
