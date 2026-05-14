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

        GridItemPlacement mergeTarget = GetPlacementAtCell(targetRow, targetColumn);
        if (mergeTarget != null
            && mergeTarget.Row == targetRow
            && mergeTarget.Column == targetColumn
            && mergeTarget.CanMerge(item)
            && mergeTarget.RemainingCapacityFor(item) >= quantity
            && runtimeData == null)
        {
            mergeTarget.Add(item, quantity);
            placement = mergeTarget;
            return true;
        }

        if (!CanPlaceStrict(item, quantity, targetRow, targetColumn, rotated))
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

    public bool TryMergeItemAt(ItemDefinition item, int quantity, int targetRow, int targetColumn, out int acceptedQuantity)
    {
        acceptedQuantity = 0;
        if (item == null || quantity <= 0 || !item.canStack)
            return false;

        EnsurePlacementList();

        GridItemPlacement targetPlacement = GetPlacementAtCell(targetRow, targetColumn);
        if (targetPlacement == null || targetPlacement.Row != targetRow || targetPlacement.Column != targetColumn)
            return false;

        acceptedQuantity = targetPlacement.Add(item, quantity);
        return acceptedQuantity > 0;
    }

    public bool TryPlaceNewItemNear(
        ItemDefinition item,
        int quantity,
        int originRow,
        int originColumn,
        ItemRuntimeData runtimeData,
        out GridItemPlacement placement,
        bool rotated = false,
        GridItemPlacement ignoredPlacement = null)
    {
        placement = null;
        if (item == null || quantity <= 0)
            return false;

        EnsurePlacementList();

        if (!TryFindNearestOpenPosition(item, rotated, originRow, originColumn, ignoredPlacement, out int targetRow, out int targetColumn))
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

    public int GetQuantity(ItemDefinition item, bool includeNestedContainers = true)
    {
        if (item == null)
            return 0;

        EnsurePlacementList();

        int totalQuantity = 0;
        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            if (placement.Item == item)
                totalQuantity += placement.Quantity;

            if (!includeNestedContainers)
                continue;

            GridContainerState nestedContainer = placement.RuntimeData != null
                ? placement.RuntimeData.StoredContainerState
                : null;
            if (nestedContainer != null)
                totalQuantity += nestedContainer.GetQuantity(item, true);
        }

        return totalQuantity;
    }

    public int RemoveItem(ItemDefinition item, int quantity)
    {
        if (item == null || quantity <= 0)
            return 0;

        EnsurePlacementList();

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

    public int RemoveItemIncludingNested(ItemDefinition item, int quantity)
    {
        if (item == null || quantity <= 0)
            return 0;

        int removedQuantity = RemoveItem(item, quantity);
        int remainingQuantity = quantity - removedQuantity;
        if (remainingQuantity <= 0)
            return removedQuantity;

        EnsurePlacementList();

        for (int i = placements.Count - 1; i >= 0 && remainingQuantity > 0; i--)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty || placement.RuntimeData == null)
                continue;

            GridContainerState nestedContainer = placement.RuntimeData.StoredContainerState;
            if (nestedContainer == null)
                continue;

            int nestedRemovedQuantity = nestedContainer.RemoveItemIncludingNested(item, remainingQuantity);
            removedQuantity += nestedRemovedQuantity;
            remainingQuantity -= nestedRemovedQuantity;
        }

        return removedQuantity;
    }

    public bool TryFindFirstPlacement(
        ItemDefinition item,
        out GridContainerState sourceContainer,
        out GridItemPlacement sourcePlacement)
    {
        sourceContainer = null;
        sourcePlacement = null;

        if (item == null)
            return false;

        EnsurePlacementList();

        for (int i = 0; i < placements.Count; i++)
        {
            GridItemPlacement placement = placements[i];
            if (placement == null || placement.IsEmpty)
                continue;

            if (placement.Item == item)
            {
                sourceContainer = this;
                sourcePlacement = placement;
                return true;
            }

            GridContainerState nestedContainer = placement.RuntimeData != null
                ? placement.RuntimeData.StoredContainerState
                : null;
            if (nestedContainer != null && nestedContainer.TryFindFirstPlacement(item, out sourceContainer, out sourcePlacement))
                return true;
        }

        return false;
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
                if (!CanPlaceStrict(item, 1, row, column, rotated))
                    continue;

                targetRow = row;
                targetColumn = column;
                return true;
            }
        }

        return false;
    }

    private bool TryFindNearestOpenPosition(
        ItemDefinition item,
        bool rotated,
        int originRow,
        int originColumn,
        GridItemPlacement ignoredPlacement,
        out int targetRow,
        out int targetColumn)
    {
        targetRow = -1;
        targetColumn = -1;

        int rowSpan = GridItemPlacement.GetRowSpan(item, rotated);
        int columnSpan = GridItemPlacement.GetColumnSpan(item, rotated);
        int maxRow = rowCount - rowSpan;
        int maxColumn = columnCount - columnSpan;
        if (maxRow < 0 || maxColumn < 0)
            return false;

        int clampedOriginRow = Mathf.Clamp(originRow, 0, maxRow);
        int clampedOriginColumn = Mathf.Clamp(originColumn, 0, maxColumn);
        int maxDistance = rowCount + columnCount;

        for (int distance = 0; distance <= maxDistance; distance++)
        {
            for (int row = 0; row <= maxRow; row++)
            {
                for (int column = 0; column <= maxColumn; column++)
                {
                    int manhattan = Mathf.Abs(row - clampedOriginRow) + Mathf.Abs(column - clampedOriginColumn);
                    if (manhattan != distance)
                        continue;

                    if (!CanPlaceStrict(item, 1, row, column, rotated, ignoredPlacement))
                        continue;

                    targetRow = row;
                    targetColumn = column;
                    return true;
                }
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
