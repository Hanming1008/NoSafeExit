using UnityEngine;

[CreateAssetMenu(fileName = "ContainerItem_", menuName = "NoSafeExit/Items/Container")]
public class ContainerItemDefinition : ItemDefinition
{
    [Header("Container")]
    public GridContainerKind containerKind = GridContainerKind.Backpack;
    [Min(1)]
    public int gridRows = 4;
    [Min(1)]
    public int gridColumns = 4;
    public GameObject equippedVisualPrefab;
    public string[] equippedVisualObjectNames;
    public string[] hiddenVisualObjectNames;

    [Header("World Pickup")]
    public GameObject[] worldVisualPrefabs;

    public override ItemType Type => ItemType.Container;

    protected override void OnValidate()
    {
        canStack = false;

        if (gridRows < 1)
            gridRows = 1;

        if (gridColumns < 1)
            gridColumns = 1;

        inventoryRows = gridRows;
        inventoryColumns = gridColumns;

        base.OnValidate();
    }
}
