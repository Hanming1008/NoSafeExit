using UnityEngine;

[DisallowMultipleComponent]
public class PlayerItemDrop : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;

    [Header("Drop")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.75f, 1.1f);
    [SerializeField] private bool logDebugMessages = true;

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    public bool TryDropFirstOccupiedSlot(int requestedQuantity = 1)
    {
        if (inventory == null)
            return false;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (slot == null || slot.IsEmpty)
                continue;

            return TryDropFromInventorySlot(i, requestedQuantity);
        }

        Log("Drop failed because the backpack is empty.");
        return false;
    }

    public bool TryDropFromInventorySlot(int slotIndex, int requestedQuantity = 1)
    {
        if (inventory == null)
        {
            Log("Drop failed because PlayerInventory is missing.");
            return false;
        }

        InventorySlot slot = inventory.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
        {
            Log($"Drop failed because slot {slotIndex} is empty.");
            return false;
        }

        ItemDefinition item = slot.Item;
        int quantityToDrop = item.canStack
            ? Mathf.Clamp(requestedQuantity, 1, slot.Quantity)
            : 1;
        ItemRuntimeData runtimeData = slot.GetRuntimeDataForTransfer(quantityToDrop);

        if (quantityToDrop <= 0)
            return false;

        if (!inventory.TryRemoveFromSlot(slotIndex, quantityToDrop))
        {
            Log($"Drop failed because {item.displayName} could not be removed from slot {slotIndex}.");
            return false;
        }

        WorldItemPickup droppedPickup = WorldItemPickup.Spawn(
            item,
            quantityToDrop,
            runtimeData,
            transform.TransformPoint(dropOffset),
            transform.rotation);

        if (droppedPickup == null)
        {
            inventory.TryAddItem(item, quantityToDrop, runtimeData);
            Log($"Drop failed because the world pickup for {item.displayName} could not be created.");
            return false;
        }

        Log($"Dropped {item.displayName} x{quantityToDrop} from slot {slotIndex}.");
        return true;
    }

    public bool TryDropFromEquipmentSlot(EquipmentSlotType slotType)
    {
        if (equipment == null)
        {
            Log("Drop failed because PlayerEquipment is missing.");
            return false;
        }

        InventorySlot slot = equipment.GetSlot(slotType);
        if (slot == null || slot.IsEmpty)
        {
            Log($"Drop failed because equipment slot {slotType} is empty.");
            return false;
        }

        ItemDefinition item = slot.Item;
        int quantityToDrop = slot.Quantity;
        ItemRuntimeData runtimeData = slot.GetRuntimeDataForTransfer(quantityToDrop);

        slot.Clear();

        WorldItemPickup droppedPickup = WorldItemPickup.Spawn(
            item,
            quantityToDrop,
            runtimeData,
            transform.TransformPoint(dropOffset),
            transform.rotation);

        if (droppedPickup == null)
        {
            slot.TrySet(item, quantityToDrop, runtimeData);
            Log($"Drop failed because the world pickup for equipped {item.displayName} could not be created.");
            return false;
        }

        Log($"Dropped equipped {item.displayName} x{quantityToDrop} from {slotType}.");
        return true;
    }

    public WorldItemPickup SpawnWorldPickup(ItemDefinition item, int quantity, ItemRuntimeData runtimeData)
    {
        if (item == null || quantity <= 0)
            return null;

        return WorldItemPickup.Spawn(
            item,
            quantity,
            runtimeData,
            transform.TransformPoint(dropOffset),
            transform.rotation);
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (equipment == null)
            equipment = GetComponent<PlayerEquipment>();
    }

    private void Log(string message)
    {
        if (!logDebugMessages)
            return;

        Debug.Log($"PlayerItemDrop: {message}", this);
    }
}
