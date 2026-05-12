using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GridContainerState
{
    [SerializeField] private GridContainerKind containerKind = GridContainerKind.Pocket;
    [Min(1)]
    [SerializeField] private int rowCount = 1;
    [Min(1)]
    [SerializeField] private int columnCount = 1;
    [SerializeField] private List<GridItemPlacement> placements = new List<GridItemPlacement>();

    public GridContainerKind ContainerKind => containerKind;
    public int RowCount => rowCount;
    public int ColumnCount => columnCount;
    public IReadOnlyList<GridItemPlacement> Placements => placements;
    public float TotalWeight
    {
        get
        {
            float totalWeight = 0f;
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i] != null)
                    totalWeight += placements[i].TotalWeight;
            }

            return totalWeight;
        }
    }

    public void Configure(GridContainerKind kind, int rows, int columns)
    {
        containerKind = kind;
        ApplyDimensionsPreservingContents(rows, columns);
        EnsurePlacementList();
    }

    public void ApplyDimensionsPreservingContents(int rows, int columns)
    {
        rowCount = Mathf.Max(rows, GetMinimumRequiredRows());
        columnCount = Mathf.Max(columns, GetMinimumRequiredColumns());
    }

    public GridItemPlacement GetPlacementAtCell(int targetRow, int targetColumn)
    {
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement != null && placement.OccupiesCell(targetRow, targetColumn))
                return placement;
        }

        return null;
    }

    public bool HasAnyPlacement()
    {
        for (int i = 0; i < placements.Count; i++)
        {
            if (placements[i] != null && !placements[i].IsEmpty)
                return true;
        }

        return false;
    }

    public bool CanPlace(ItemDefinition item, int quantity, int targetRow, int targetColumn, bool rotated = false, GridItemPlacement ignoredPlacement = null)
    {
        if (item == null || quantity <= 0)
            return false;

        int rowSpan = GridItemPlacement.GetRowSpan(item, rotated);
        int columnSpan = GridItemPlacement.GetColumnSpan(item, rotated);

        if (targetRow < 0 || targetColumn < 0)
            return false;

        if (targetRow + rowSpan > rowCount || targetColumn + columnSpan > columnCount)
            return false;

        for (int row = targetRow; row < targetRow + rowSpan; row++)
        {
            for (int column = targetColumn; column < targetColumn + columnSpan; column++)
            {
                GridItemPlacement occupyingPlacement = GetPlacementAtCell(row, column);
                if (occupyingPlacement == null || occupyingPlacement == ignoredPlacement)
                    continue;

                if (occupyingPlacement.CanMerge(item) && occupyingPlacement.Row == targetRow && occupyingPlacement.Column == targetColumn)
                    continue;

                return false;
            }
        }

        return true;
    }

    public bool CanPlaceStrict(ItemDefinition item, int quantity, int targetRow, int targetColumn, bool rotated = false, GridItemPlacement ignoredPlacement = null)
    {
        if (item == null || quantity <= 0)
            return false;

        int rowSpan = GridItemPlacement.GetRowSpan(item, rotated);
        int columnSpan = GridItemPlacement.GetColumnSpan(item, rotated);

        if (targetRow < 0 || targetColumn < 0)
            return false;

        if (targetRow + rowSpan > rowCount || targetColumn + columnSpan > columnCount)
            return false;

        for (int row = targetRow; row < targetRow + rowSpan; row++)
        {
            for (int column = targetColumn; column < targetColumn + columnSpan; column++)
            {
                GridItemPlacement occupyingPlacement = GetPlacementAtCell(row, column);
                if (occupyingPlacement == null || occupyingPlacement == ignoredPlacement)
                    continue;

                return false;
            }
        }

        return true;
    }

    public bool TryAddItem(ItemDefinition item, int quantity, out GridItemPlacement placement)
    {
        return TryAddItem(item, quantity, null, out placement);
    }

    public bool TryAddItem(ItemDefinition item, int quantity, ItemRuntimeData runtimeData, out GridItemPlacement placement)
    {
        placement = null;
        if (item == null || quantity <= 0)
            return false;

        EnsurePlacementList();

        int remainingQuantity = quantity;
        ItemRuntimeData pendingRuntimeData = runtimeData;
        if (item.canStack && runtimeData == null)
        {
            for (int i = 0; i < placements.Count && remainingQuantity > 0; i++)
            {
                GridItemPlacement existingPlacement = placements[i];
                if (existingPlacement == null || !existingPlacement.CanMerge(item))
                    continue;

                remainingQuantity -= existingPlacement.Add(item, remainingQuantity);
                placement = existingPlacement;
            }
        }

        while (remainingQuantity > 0)
        {
            int stackQuantity = item.canStack
                ? Mathf.Min(remainingQuantity, item.maxStackSize)
                : 1;

            if (!TryFindOpenPosition(item, false, out int targetRow, out int targetColumn))
                return remainingQuantity != quantity;

            GridItemPlacement newPlacement = new GridItemPlacement();
            ItemRuntimeData runtimeForPlacement = ResolveRuntimeDataForPlacement(item, ref pendingRuntimeData);
            if (!newPlacement.TrySet(item, stackQuantity, targetRow, targetColumn, false, runtimeForPlacement))
                return remainingQuantity != quantity;

            placements.Add(newPlacement);
            placement = newPlacement;
            remainingQuantity -= stackQuantity;
        }

        return true;
    }

    public bool TryPlaceNewItem(ItemDefinition item, int quantity, out GridItemPlacement placement, bool rotated = false)
    {
        return TryPlaceNewItem(item, quantity, null, out placement, rotated);
    }

    public bool TryPlaceNewItem(ItemDefinition item, int quantity, ItemRuntimeData runtimeData, out GridItemPlacement placement, bool rotated = false)
    {
        placement = null;
        if (item == null || quantity <= 0)
            return false;

        EnsurePlacementList();

        if (!TryFindOpenPosition(item, rotated, out int targetRow, out int targetColumn))
            return false;

        placement = new GridItemPlacement();
        if (!placement.TrySet(item, quantity, targetRow, targetColumn, rotated, runtimeData))
        {
            placement = null;
            return false;
        }

        placements.Add(placement);
        return true;
    }

    public bool TryPlaceItemAt(ItemDefinition item, int quantity, int targetRow, int targetColumn, out GridItemPlacement placement, bool rotated = false)
    {
        return TryPlaceItemAt(item, quantity, targetRow, targetColumn, null, out placement, rotated);
    }

    public bool TryPlaceItemAt(ItemDefinition item, int quantity, int targetRow, int targetColumn, ItemRuntimeData runtimeData, out GridItemPlacement placement, bool rotated = false)
    {
        placement = null;
        if (item == null || quantity <= 0)
            return false;

        EnsurePlacementList();
        if (!CanPlace(item, quantity, targetRow, targetColumn, rotated))
            return false;

        placement = new GridItemPlacement();
        if (!placement.TrySet(item, quantity, targetRow, targetColumn, rotated, runtimeData))
        {
            placement = null;
            return false;
        }

        placements.Add(placement);
        return true;
    }

    public bool TryRemovePlacement(GridItemPlacement placement)
    {
        if (placement == null)
            return false;

        return placements.Remove(placement);
    }

    public int RemoveItem(ItemDefinition item, int quantity)
    {
        if (item == null || quantity <= 0)
            return 0;

        int remainingQuantity = quantity;

        for (int i = placements.Count - 1; i >= 0 && remainingQuantity > 0; i--)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty || placement.Item != item)
                continue;

            remainingQuantity -= placement.Remove(remainingQuantity);
            if (placement.IsEmpty)
                placements.RemoveAt(i);
        }

        return quantity - remainingQuantity;
    }

    public void Clear()
    {
        placements.Clear();
    }

    public GridContainerState DeepClone()
    {
        GridContainerState clone = new GridContainerState
        {
            containerKind = containerKind,
            rowCount = rowCount,
            columnCount = columnCount,
            placements = new List<GridItemPlacement>()
        };

        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            clone.placements.Add(placement.DeepClone());
        }

        return clone;
    }

    private bool TryFindOpenPosition(ItemDefinition item, bool rotated, out int targetRow, out int targetColumn)
    {
        targetRow = -1;
        targetColumn = -1;

        int rowSpan = GridItemPlacement.GetRowSpan(item, rotated);
        int columnSpan = GridItemPlacement.GetColumnSpan(item, rotated);

        for (int row = 0; row <= rowCount - rowSpan; row++)
        {
            for (int column = 0; column <= columnCount - columnSpan; column++)
            {
                if (!CanPlace(item, 1, row, column, rotated))
                    continue;

                targetRow = row;
                targetColumn = column;
                return true;
            }
        }

        return false;
    }

    private int GetMinimumRequiredRows()
    {
        int requiredRows = 1;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            requiredRows = Mathf.Max(requiredRows, placement.Row + placement.RowSpan);
        }

        return requiredRows;
    }

    private int GetMinimumRequiredColumns()
    {
        int requiredColumns = 1;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            requiredColumns = Mathf.Max(requiredColumns, placement.Column + placement.ColumnSpan);
        }

        return requiredColumns;
    }

    private void EnsurePlacementList()
    {
        if (placements == null)
            placements = new List<GridItemPlacement>();

        for (int i = placements.Count - 1; i >= 0; i--)
        {
            if (placements[i] == null || placements[i].IsEmpty)
                placements.RemoveAt(i);
        }
    }

    private static ItemRuntimeData ResolveRuntimeDataForPlacement(ItemDefinition item, ref ItemRuntimeData pendingRuntimeData)
    {
        if (item == null || item.canStack)
            return pendingRuntimeData;

        if (pendingRuntimeData != null)
        {
            ItemRuntimeData runtimeForPlacement = pendingRuntimeData;
            pendingRuntimeData = null;
            runtimeForPlacement.EnsureFor(item);
            return runtimeForPlacement;
        }

        return ItemRuntimeData.CreateFor(item);
    }
}
