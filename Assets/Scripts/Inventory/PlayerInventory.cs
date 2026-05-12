using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory")]
    [Min(1)]
    public int slotCount = 16;

    [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

    public IReadOnlyList<InventorySlot> Slots => slots;
    public int SlotCount => slots.Count;
    public float TotalWeight
    {
        get
        {
            float totalWeight = 0f;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                    totalWeight += slots[i].TotalWeight;
            }

            return totalWeight;
        }
    }

    void Awake()
    {
        EnsureSlotCount();
    }

    void OnValidate()
    {
        EnsureSlotCount();
    }

    [ContextMenu("Ensure Slot Count")]
    public void EnsureSlotCount()
    {
        if (slotCount < 1)
            slotCount = 1;

        if (slots == null)
            slots = new List<InventorySlot>();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                slots[i] = new InventorySlot();
        }

        while (slots.Count < slotCount)
        {
            slots.Add(new InventorySlot());
        }

        if (slots.Count > slotCount)
        {
            bool hasItemsOutsideConfiguredRange = false;
            for (int i = slotCount; i < slots.Count; i++)
            {
                if (slots[i] != null && !slots[i].IsEmpty)
                {
                    hasItemsOutsideConfiguredRange = true;
                    break;
                }
            }

            if (hasItemsOutsideConfiguredRange)
            {
                slotCount = slots.Count;
            }
            else
            {
                slots.RemoveRange(slotCount, slots.Count - slotCount);
            }
        }
    }

    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
            return null;

        return slots[index];
    }

    public bool HasItem(ItemDefinition item, int quantity = 1)
    {
        return GetQuantity(item) >= quantity;
    }

    public int GetQuantity(ItemDefinition item)
    {
        if (item == null)
            return 0;

        int totalQuantity = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot != null && slot.Contains(item))
                totalQuantity += slot.Quantity;
        }

        return totalQuantity;
    }

    public bool TryAddItem(ItemDefinition item, int quantity = 1)
    {
        return TryAddItem(item, quantity, null);
    }

    public bool TryAddItem(ItemDefinition item, int quantity, ItemRuntimeData runtimeData)
    {
        return AddItem(item, quantity, runtimeData) == quantity;
    }

    public int AddItem(ItemDefinition item, int quantity = 1)
    {
        return AddItem(item, quantity, null);
    }

    public int AddItem(ItemDefinition item, int quantity, ItemRuntimeData runtimeData)
    {
        if (item == null || quantity <= 0)
            return 0;

        EnsureSlotCount();

        int remainingQuantity = quantity;
        ItemRuntimeData pendingRuntimeData = runtimeData;

        if (item.canStack && runtimeData == null)
        {
            for (int i = 0; i < slots.Count && remainingQuantity > 0; i++)
            {
                InventorySlot slot = slots[i];
                if (slot == null || !slot.CanMerge(item))
                    continue;

                remainingQuantity -= slot.Add(item, remainingQuantity);
            }
        }

        for (int i = 0; i < slots.Count && remainingQuantity > 0; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || !slot.IsEmpty)
                continue;

            int quantityForSlot = item.canStack ? remainingQuantity : 1;
            ItemRuntimeData runtimeForSlot = ResolveRuntimeDataForPlacement(item, ref pendingRuntimeData);
            remainingQuantity -= slot.Add(item, quantityForSlot, runtimeForSlot);
        }

        return quantity - remainingQuantity;
    }

    public bool TryRemoveItem(ItemDefinition item, int quantity = 1)
    {
        return RemoveItem(item, quantity) == quantity;
    }

    public int RemoveItem(ItemDefinition item, int quantity = 1)
    {
        if (item == null || quantity <= 0)
            return 0;

        int remainingQuantity = quantity;

        for (int i = 0; i < slots.Count && remainingQuantity > 0; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || !slot.Contains(item))
                continue;

            remainingQuantity -= slot.Remove(remainingQuantity);
        }

        return quantity - remainingQuantity;
    }

    public bool TryRemoveFromSlot(int slotIndex, int quantity = 1)
    {
        return RemoveFromSlot(slotIndex, quantity) == quantity;
    }

    public int RemoveFromSlot(int slotIndex, int quantity = 1)
    {
        InventorySlot slot = GetSlot(slotIndex);
        if (slot == null)
            return 0;

        return slot.Remove(quantity);
    }

    public void ClearAll()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                slots[i].Clear();
        }
    }

    private static ItemRuntimeData ResolveRuntimeDataForPlacement(ItemDefinition item, ref ItemRuntimeData pendingRuntimeData)
    {
        if (item == null || item.canStack)
            return pendingRuntimeData;

        if (pendingRuntimeData != null)
        {
            ItemRuntimeData runtimeForSlot = pendingRuntimeData;
            pendingRuntimeData = null;
            runtimeForSlot.EnsureFor(item);
            return runtimeForSlot;
        }

        return ItemRuntimeData.CreateFor(item);
    }
}
