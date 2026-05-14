using System;
using UnityEngine;

[Serializable]
public class GridItemPlacement
{
    [SerializeField] private ItemDefinition item;
    [Min(0)]
    [SerializeField] private int quantity;
    [Min(0)]
    [SerializeField] private int row;
    [Min(0)]
    [SerializeField] private int column;
    [SerializeField] private bool rotated;
    [SerializeReference] private ItemRuntimeData runtimeData;

    public ItemDefinition Item => item;
    public int Quantity => IsEmpty ? 0 : quantity;
    public int Row => row;
    public int Column => column;
    public bool Rotated => rotated;
    public ItemRuntimeData RuntimeData => runtimeData;
    public string RuntimeInstanceId => runtimeData != null ? runtimeData.InstanceId : string.Empty;
    public bool IsEmpty => item == null || quantity <= 0;
    public int RowSpan => GetRowSpan(item, rotated);
    public int ColumnSpan => GetColumnSpan(item, rotated);
    public float TotalWeight => IsEmpty ? 0f : (item.weight * quantity) + (runtimeData != null ? runtimeData.NestedWeight : 0f);

    public bool CanMerge(ItemDefinition definition)
    {
        return definition != null
            && !IsEmpty
            && item == definition
            && runtimeData == null
            && item.canStack
            && quantity < item.maxStackSize;
    }

    public int RemainingCapacityFor(ItemDefinition definition)
    {
        if (definition == null)
            return 0;

        if (IsEmpty)
            return definition.canStack ? definition.maxStackSize : 1;

        if (!CanMerge(definition))
            return 0;

        return Mathf.Max(0, definition.maxStackSize - quantity);
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

    public bool TrySet(ItemDefinition definition, int amount, int targetRow, int targetColumn, bool isRotated = false)
    {
        return TrySet(definition, amount, targetRow, targetColumn, isRotated, null);
    }

    public bool TrySet(ItemDefinition definition, int amount, int targetRow, int targetColumn, bool isRotated, ItemRuntimeData itemRuntimeData)
    {
        if (definition == null || amount <= 0 || targetRow < 0 || targetColumn < 0)
            return false;

        item = definition;
        quantity = definition.canStack
            ? Mathf.Min(amount, definition.maxStackSize)
            : 1;
        row = targetRow;
        column = targetColumn;
        rotated = CanRotate(definition) && isRotated;
        runtimeData = ResolveRuntimeData(definition, itemRuntimeData);
        return true;
    }

    public bool OccupiesCell(int targetRow, int targetColumn)
    {
        return !IsEmpty
            && targetRow >= row
            && targetRow < row + RowSpan
            && targetColumn >= column
            && targetColumn < column + ColumnSpan;
    }

    public bool FitsWithin(int totalRows, int totalColumns)
    {
        return !IsEmpty
            && row >= 0
            && column >= 0
            && row + RowSpan <= totalRows
            && column + ColumnSpan <= totalColumns;
    }

    public void Clear()
    {
        item = null;
        quantity = 0;
        row = 0;
        column = 0;
        rotated = false;
        runtimeData = null;
    }

    public GridItemPlacement DeepClone()
    {
        GridItemPlacement clone = new GridItemPlacement();
        if (!IsEmpty)
            clone.TrySet(item, quantity, row, column, rotated, runtimeData != null ? runtimeData.DeepClone() : null);

        return clone;
    }

    public static int GetRowSpan(ItemDefinition definition, bool isRotated)
    {
        if (definition == null)
            return 0;

        return isRotated && CanRotate(definition)
            ? Mathf.Max(1, definition.inventoryColumns)
            : Mathf.Max(1, definition.inventoryRows);
    }

    public static int GetColumnSpan(ItemDefinition definition, bool isRotated)
    {
        if (definition == null)
            return 0;

        return isRotated && CanRotate(definition)
            ? Mathf.Max(1, definition.inventoryRows)
            : Mathf.Max(1, definition.inventoryColumns);
    }

    public static bool CanRotate(ItemDefinition definition)
    {
        if (definition == null)
            return false;

        return definition.canRotateInGrid || definition.inventoryRows > 1 || definition.inventoryColumns > 1;
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
