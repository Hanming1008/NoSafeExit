using UnityEngine;

[DisallowMultipleComponent]
public class PlayerGridInventory : MonoBehaviour
{
    [Header("Default Containers")]
    [Min(1)]
    public int pocketRows = 1;
    [Min(1)]
    public int pocketColumns = 4;

    [Header("Equipped Containers")]
    [SerializeField] private ContainerItemDefinition equippedRig;
    [SerializeField] private ContainerItemDefinition equippedBackpack;
    [SerializeReference] private ItemRuntimeData equippedRigRuntime;
    [SerializeReference] private ItemRuntimeData equippedBackpackRuntime;

    [Header("Prototype Fallbacks")]
    [SerializeField] private bool usePrototypeRigWhenUnassigned = false;
    [Min(1)]
    [SerializeField] private int prototypeRigRows = 3;
    [Min(1)]
    [SerializeField] private int prototypeRigColumns = 3;
    [SerializeField] private bool usePrototypeBackpackWhenUnassigned = false;
    [Min(1)]
    [SerializeField] private int prototypeBackpackRows = 4;
    [Min(1)]
    [SerializeField] private int prototypeBackpackColumns = 4;

    [Header("Runtime State")]
    [SerializeField] private GridContainerState pocketContainer = new GridContainerState();
    [SerializeReference] private GridContainerState rigContainer = new GridContainerState();
    [SerializeReference] private GridContainerState backpackContainer = new GridContainerState();

    [System.NonSerialized] private bool runtimeDisablePrototypeRig;
    [System.NonSerialized] private bool runtimeDisablePrototypeBackpack;

    public ContainerItemDefinition EquippedRig => equippedRig;
    public ContainerItemDefinition EquippedBackpack => equippedBackpack;
    public ItemRuntimeData EquippedRigRuntime => equippedRigRuntime;
    public ItemRuntimeData EquippedBackpackRuntime => equippedBackpackRuntime;
    public GridContainerState PocketContainer => pocketContainer;
    public GridContainerState RigContainer => rigContainer;
    public GridContainerState BackpackContainer => backpackContainer;
    public bool UsePrototypeRigWhenUnassigned => usePrototypeRigWhenUnassigned && !runtimeDisablePrototypeRig;
    public bool UsePrototypeBackpackWhenUnassigned => usePrototypeBackpackWhenUnassigned && !runtimeDisablePrototypeBackpack;
    public bool HasRigContainer => equippedRig != null || UsePrototypeRigWhenUnassigned;
    public bool HasBackpackContainer => equippedBackpack != null || UsePrototypeBackpackWhenUnassigned;
    public float TotalWeight => pocketContainer.TotalWeight + rigContainer.TotalWeight + backpackContainer.TotalWeight;

    void Awake()
    {
        EnsureContainers();
    }

    void OnValidate()
    {
        EnsureContainers();
    }

    [ContextMenu("Ensure Grid Containers")]
    public void EnsureContainers()
    {
        if (pocketRows < 1)
            pocketRows = 1;

        if (pocketColumns < 1)
            pocketColumns = 1;

        if (prototypeRigRows < 1)
            prototypeRigRows = 1;

        if (prototypeRigColumns < 1)
            prototypeRigColumns = 1;

        if (prototypeBackpackRows < 1)
            prototypeBackpackRows = 1;

        if (prototypeBackpackColumns < 1)
            prototypeBackpackColumns = 1;

        if (pocketContainer == null)
            pocketContainer = new GridContainerState();
        if (rigContainer == null)
            rigContainer = new GridContainerState();
        if (backpackContainer == null)
            backpackContainer = new GridContainerState();

        bool rigPrototypeEnabled = UsePrototypeRigWhenUnassigned;
        bool backpackPrototypeEnabled = UsePrototypeBackpackWhenUnassigned;

        pocketContainer.Configure(GridContainerKind.Pocket, pocketRows, pocketColumns);
        rigContainer = ResolveActiveContainerState(
            equippedRig,
            equippedRigRuntime,
            rigContainer,
            GridContainerKind.Rig,
            rigPrototypeEnabled ? prototypeRigRows : 1,
            rigPrototypeEnabled ? prototypeRigColumns : 1);
        backpackContainer = ResolveActiveContainerState(
            equippedBackpack,
            equippedBackpackRuntime,
            backpackContainer,
            GridContainerKind.Backpack,
            backpackPrototypeEnabled ? prototypeBackpackRows : 1,
            backpackPrototypeEnabled ? prototypeBackpackColumns : 1);

        if (equippedRig == null && !rigPrototypeEnabled)
            rigContainer = CreateEmptyContainer(GridContainerKind.Rig, 1, 1);

        if (equippedBackpack == null && !backpackPrototypeEnabled)
            backpackContainer = CreateEmptyContainer(GridContainerKind.Backpack, 1, 1);

        rigContainer.Configure(
            GridContainerKind.Rig,
            equippedRig != null ? equippedRig.gridRows : (rigPrototypeEnabled ? prototypeRigRows : 1),
            equippedRig != null ? equippedRig.gridColumns : (rigPrototypeEnabled ? prototypeRigColumns : 1));
        backpackContainer.Configure(
            GridContainerKind.Backpack,
            equippedBackpack != null ? equippedBackpack.gridRows : (backpackPrototypeEnabled ? prototypeBackpackRows : 1),
            equippedBackpack != null ? equippedBackpack.gridColumns : (backpackPrototypeEnabled ? prototypeBackpackColumns : 1));
    }

    public GridContainerState GetContainer(GridContainerKind containerKind)
    {
        switch (containerKind)
        {
            case GridContainerKind.Pocket:
                return pocketContainer;
            case GridContainerKind.Rig:
                return rigContainer;
            case GridContainerKind.Backpack:
                return backpackContainer;
            default:
                return pocketContainer;
        }
    }

    public void EquipRig(ContainerItemDefinition rigDefinition)
    {
        EquipRig(rigDefinition, null);
    }

    public void EquipRig(ContainerItemDefinition rigDefinition, ItemRuntimeData runtimeData)
    {
        equippedRig = rigDefinition;
        equippedRigRuntime = PrepareRuntimeData(rigDefinition, runtimeData);
        EnsureContainers();
    }

    public void EquipBackpack(ContainerItemDefinition backpackDefinition)
    {
        EquipBackpack(backpackDefinition, null);
    }

    public void EquipBackpack(ContainerItemDefinition backpackDefinition, ItemRuntimeData runtimeData)
    {
        equippedBackpack = backpackDefinition;
        equippedBackpackRuntime = PrepareRuntimeData(backpackDefinition, runtimeData);
        EnsureContainers();
    }

    public void UnequipRig()
    {
        if (equippedRigRuntime != null && ReferenceEquals(rigContainer, equippedRigRuntime.StoredContainerState))
            rigContainer = CreateEmptyContainer(GridContainerKind.Rig, 1, 1);

        equippedRig = null;
        equippedRigRuntime = null;
        EnsureContainers();
    }

    public void UnequipBackpack()
    {
        if (equippedBackpackRuntime != null && ReferenceEquals(backpackContainer, equippedBackpackRuntime.StoredContainerState))
            backpackContainer = CreateEmptyContainer(GridContainerKind.Backpack, 1, 1);

        equippedBackpack = null;
        equippedBackpackRuntime = null;
        EnsureContainers();
    }

    public void SetPrototypeFallbacks(bool rigEnabled, bool backpackEnabled)
    {
        usePrototypeRigWhenUnassigned = rigEnabled;
        usePrototypeBackpackWhenUnassigned = backpackEnabled;
        runtimeDisablePrototypeRig = !rigEnabled;
        runtimeDisablePrototypeBackpack = !backpackEnabled;
        EnsureContainers();
    }

    public bool TryAddToCarriedContainers(
        ItemDefinition item,
        int quantity,
        ItemRuntimeData runtimeData,
        bool includePocket,
        out GridContainerKind containerKind,
        out GridItemPlacement placement)
    {
        containerKind = GridContainerKind.Pocket;
        placement = null;

        EnsureContainers();

        if (HasRigContainer && rigContainer != null && rigContainer.TryAddItem(item, quantity, runtimeData, out placement))
        {
            containerKind = GridContainerKind.Rig;
            return true;
        }

        if (HasBackpackContainer && backpackContainer != null && backpackContainer.TryAddItem(item, quantity, runtimeData, out placement))
        {
            containerKind = GridContainerKind.Backpack;
            return true;
        }

        if (includePocket && pocketContainer != null && pocketContainer.TryAddItem(item, quantity, runtimeData, out placement))
        {
            containerKind = GridContainerKind.Pocket;
            return true;
        }

        return false;
    }

    public int GetQuantity(ItemDefinition item)
    {
        if (item == null)
            return 0;

        EnsureContainers();

        int totalQuantity = 0;
        if (HasRigContainer && rigContainer != null)
            totalQuantity += rigContainer.GetQuantity(item);

        if (HasBackpackContainer && backpackContainer != null)
            totalQuantity += backpackContainer.GetQuantity(item);

        if (pocketContainer != null)
            totalQuantity += pocketContainer.GetQuantity(item);

        return totalQuantity;
    }

    public int RemoveItem(ItemDefinition item, int quantity)
    {
        if (item == null || quantity <= 0)
            return 0;

        EnsureContainers();

        int removedQuantity = 0;
        int remainingQuantity = quantity;

        if (HasRigContainer && rigContainer != null)
        {
            removedQuantity += rigContainer.RemoveItemIncludingNested(item, remainingQuantity);
            remainingQuantity = quantity - removedQuantity;
        }

        if (remainingQuantity > 0 && HasBackpackContainer && backpackContainer != null)
        {
            removedQuantity += backpackContainer.RemoveItemIncludingNested(item, remainingQuantity);
            remainingQuantity = quantity - removedQuantity;
        }

        if (remainingQuantity > 0 && pocketContainer != null)
            removedQuantity += pocketContainer.RemoveItemIncludingNested(item, remainingQuantity);

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

        EnsureContainers();

        if (HasRigContainer && rigContainer != null && rigContainer.TryFindFirstPlacement(item, out sourceContainer, out sourcePlacement))
            return true;

        if (HasBackpackContainer && backpackContainer != null && backpackContainer.TryFindFirstPlacement(item, out sourceContainer, out sourcePlacement))
            return true;

        return pocketContainer != null && pocketContainer.TryFindFirstPlacement(item, out sourceContainer, out sourcePlacement);
    }

    private GridContainerState ResolveActiveContainerState(
        ContainerItemDefinition definition,
        ItemRuntimeData runtimeData,
        GridContainerState currentState,
        GridContainerKind kind,
        int fallbackRows,
        int fallbackColumns)
    {
        if (definition != null)
        {
            ItemRuntimeData preparedRuntimeData = PrepareRuntimeData(definition, runtimeData);
            if (preparedRuntimeData != null && preparedRuntimeData.StoredContainerState != null)
                return preparedRuntimeData.StoredContainerState;
        }

        currentState ??= new GridContainerState();
        currentState.Configure(kind, Mathf.Max(1, fallbackRows), Mathf.Max(1, fallbackColumns));
        return currentState;
    }

    private static ItemRuntimeData PrepareRuntimeData(ItemDefinition definition, ItemRuntimeData runtimeData)
    {
        if (definition == null)
            return null;

        ItemRuntimeData preparedRuntimeData = runtimeData ?? ItemRuntimeData.CreateFor(definition);
        if (definition is ContainerItemDefinition containerDefinition)
        {
            preparedRuntimeData.EnsureContainer(
                containerDefinition.containerKind,
                containerDefinition.gridRows,
                containerDefinition.gridColumns);
        }
        else
        {
            preparedRuntimeData.EnsureFor(definition);
        }

        return preparedRuntimeData;
    }

    private static GridContainerState CreateEmptyContainer(GridContainerKind kind, int rows, int columns)
    {
        GridContainerState emptyContainer = new GridContainerState();
        emptyContainer.Configure(kind, rows, columns);
        emptyContainer.Clear();
        return emptyContainer;
    }
}
