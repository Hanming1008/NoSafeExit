using System;
using UnityEngine;

[Serializable]
public class ItemRuntimeData
{
    [SerializeField] private string instanceId;
    [SerializeReference] private GridContainerState storedContainerState;

    public string InstanceId => instanceId;
    public GridContainerState StoredContainerState => storedContainerState;
    public float NestedWeight => storedContainerState != null ? storedContainerState.TotalWeight : 0f;

    public static ItemRuntimeData CreateFor(ItemDefinition definition)
    {
        ItemRuntimeData runtimeData = new ItemRuntimeData();
        runtimeData.EnsureFor(definition);
        return runtimeData;
    }

    public void EnsureFor(ItemDefinition definition)
    {
        EnsureInstanceId();

        if (TryGetProvidedContainer(definition, out GridContainerKind containerKind, out int rows, out int columns))
        {
            if (storedContainerState == null)
                storedContainerState = new GridContainerState();

            storedContainerState.Configure(containerKind, rows, columns);
            return;
        }

        storedContainerState = null;
    }

    public ItemRuntimeData DeepClone(bool preserveIdentity = true)
    {
        ItemRuntimeData clone = new ItemRuntimeData
        {
            instanceId = preserveIdentity && !string.IsNullOrWhiteSpace(instanceId)
                ? instanceId
                : Guid.NewGuid().ToString("N"),
            storedContainerState = storedContainerState != null ? storedContainerState.DeepClone() : null
        };

        return clone;
    }

    public static bool TryGetProvidedContainer(
        ItemDefinition definition,
        out GridContainerKind containerKind,
        out int rows,
        out int columns)
    {
        if (definition is ContainerItemDefinition containerItem)
        {
            containerKind = containerItem.containerKind;
            rows = Mathf.Max(1, containerItem.gridRows);
            columns = Mathf.Max(1, containerItem.gridColumns);
            return true;
        }

        if (definition is ArmorItemDefinition armorItem && armorItem.providedRigContainer != null)
        {
            containerKind = armorItem.providedRigContainer.containerKind;
            rows = Mathf.Max(1, armorItem.providedRigContainer.gridRows);
            columns = Mathf.Max(1, armorItem.providedRigContainer.gridColumns);
            return true;
        }

        containerKind = GridContainerKind.Pocket;
        rows = 0;
        columns = 0;
        return false;
    }

    private void EnsureInstanceId()
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            instanceId = Guid.NewGuid().ToString("N");
    }
}
