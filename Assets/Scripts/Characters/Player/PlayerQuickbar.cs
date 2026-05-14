using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerQuickbar : MonoBehaviour
{
    [Serializable]
    public class QuickbarEntry
    {
        public ItemDefinition item;
    }

    [Header("Quickbar")]
    [Min(1)]
    [SerializeField] private int slotCount = 6;
    [SerializeField] private List<QuickbarEntry> slots = new List<QuickbarEntry>();

    public int SlotCount => slots.Count;
    public IReadOnlyList<QuickbarEntry> Slots => slots;

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
            slots = new List<QuickbarEntry>();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                slots[i] = new QuickbarEntry();
        }

        while (slots.Count < slotCount)
            slots.Add(new QuickbarEntry());

        if (slots.Count > slotCount)
            slots.RemoveRange(slotCount, slots.Count - slotCount);
    }

    public ItemDefinition GetAssignedItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return null;

        return slots[slotIndex]?.item;
    }

    public bool CanAssign(ItemDefinition item)
    {
        return item is MedicalItemDefinition || item is ConsumableItemDefinition;
    }

    public bool TryAssignItem(int slotIndex, ItemDefinition item)
    {
        if (!CanAssign(item) || slotIndex < 0 || slotIndex >= slots.Count)
            return false;

        EnsureSlotCount();
        slots[slotIndex].item = item;
        return true;
    }

    public bool TryAssignItemToFirstAvailableSlot(ItemDefinition item, out int assignedSlotIndex)
    {
        assignedSlotIndex = -1;
        if (!CanAssign(item))
            return false;

        EnsureSlotCount();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item == item)
            {
                assignedSlotIndex = i;
                return true;
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item != null)
                continue;

            slots[i].item = item;
            assignedSlotIndex = i;
            return true;
        }

        return false;
    }

    public void ClearSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return;

        slots[slotIndex].item = null;
    }
}
