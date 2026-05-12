using System;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    [SerializeField] private ItemDefinition item;
    [Min(0)]
    [SerializeField] private int quantity;
    [SerializeReference] private ItemRuntimeData runtimeData;

    public ItemDefinition Item => item;
    public int Quantity => IsEmpty ? 0 : quantity;
    public ItemRuntimeData RuntimeData => runtimeData;
    public string RuntimeInstanceId => runtimeData != null ? runtimeData.InstanceId : string.Empty;
    public bool IsEmpty => item == null || quantity <= 0;
    public bool HasItem => !IsEmpty;
    public float TotalWeight => IsEmpty ? 0f : (item.weight * quantity) + (runtimeData != null ? runtimeData.NestedWeight : 0f);

    public bool Contains(ItemDefinition definition)
    {
        return !IsEmpty && item == definition;
    }

    public bool CanMerge(ItemDefinition definition)
    {
        return definition != null
            && !IsEmpty
            && item == definition
            && runtimeData == null
            && item.canStack
            && quantity < item.maxStackSize;
    }

    public bool CanAccept(ItemDefinition definition)
    {
        return definition != null && (IsEmpty || CanMerge(definition));
    }

    public int RemainingCapacityFor(ItemDefinition definition)
    {
        if (definition == null)
            return 0;

        if (IsEmpty)
            return definition.canStack ? definition.maxStackSize : 1;

        if (item != definition || !item.canStack)
            return 0;

        return Mathf.Max(0, item.maxStackSize - quantity);
    }

    public int Add(ItemDefinition definition, int amount)
    {
        return Add(definition, amount, null);
    }

    public int Add(ItemDefinition definition, int amount, ItemRuntimeData itemRuntimeData)
    {
        if (definition == null || amount <= 0)
            return 0;

        if (itemRuntimeData != null && !IsEmpty)
            return 0;

        int acceptedAmount = Mathf.Min(amount, RemainingCapacityFor(definition));
        if (acceptedAmount <= 0)
            return 0;

        if (IsEmpty)
        {
            item = definition;
            quantity = definition.canStack ? acceptedAmount : 1;
            runtimeData = ResolveRuntimeData(definition, itemRuntimeData);
        }
        else
        {
            quantity += acceptedAmount;
        }

        return acceptedAmount;
    }

    public int Remove(int amount)
    {
        if (IsEmpty || amount <= 0)
            return 0;

        int removedAmount = Mathf.Min(amount, quantity);
        quantity -= removedAmount;

        if (quantity <= 0)
            Clear();

        return removedAmount;
    }

    public bool TrySet(ItemDefinition definition, int amount)
    {
        return TrySet(definition, amount, null);
    }

    public bool TrySet(ItemDefinition definition, int amount, ItemRuntimeData itemRuntimeData)
    {
        if (definition == null || amount <= 0)
            return false;

        item = definition;
        quantity = definition.canStack
            ? Mathf.Min(amount, definition.maxStackSize)
            : 1;
        runtimeData = ResolveRuntimeData(definition, itemRuntimeData);

        return true;
    }

    public void CopyFrom(InventorySlot other)
    {
        if (other == null || other.IsEmpty)
        {
            Clear();
            return;
        }

        TrySet(other.item, other.quantity, other.runtimeData != null ? other.runtimeData.DeepClone() : null);
    }

    public ItemRuntimeData GetRuntimeDataForTransfer(int amount)
    {
        if (IsEmpty || amount <= 0 || amount < quantity)
            return null;

        return runtimeData;
    }

    public void Clear()
    {
        item = null;
        quantity = 0;
        runtimeData = null;
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
