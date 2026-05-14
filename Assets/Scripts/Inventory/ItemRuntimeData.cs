using System;
using UnityEngine;

[Serializable]
public class ItemRuntimeData
{
    [SerializeField] private string instanceId;
    [SerializeReference] private GridContainerState storedContainerState;
    [SerializeField] private int weaponMagazineAmmo = -1;
    [SerializeField] private float armorDurability = -1f;

    public string InstanceId => instanceId;
    public GridContainerState StoredContainerState => storedContainerState;
    public int WeaponMagazineAmmo => weaponMagazineAmmo;
    public bool HasWeaponMagazineAmmo => weaponMagazineAmmo >= 0;
    public float ArmorDurability => armorDurability;
    public bool HasArmorDurability => armorDurability >= 0f;
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
        }
        else
        {
            storedContainerState = null;
        }

        if (definition is WeaponItemDefinition weapon)
            EnsureWeaponMagazineAmmo(weapon);
        else
            weaponMagazineAmmo = -1;

        if (definition is ArmorItemDefinition armor)
            EnsureArmorDurability(armor);
        else
            armorDurability = -1f;
    }

    public void EnsureContainer(GridContainerKind containerKind, int rows, int columns)
    {
        EnsureInstanceId();

        if (storedContainerState == null)
            storedContainerState = new GridContainerState();

        storedContainerState.Configure(containerKind, Mathf.Max(1, rows), Mathf.Max(1, columns));
    }

    public int EnsureWeaponMagazineAmmo(WeaponItemDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
        {
            weaponMagazineAmmo = -1;
            return 0;
        }

        int magazineSize = Mathf.Max(1, weaponDefinition.magazineSize);
        if (weaponMagazineAmmo < 0)
            weaponMagazineAmmo = magazineSize;
        else
            weaponMagazineAmmo = Mathf.Clamp(weaponMagazineAmmo, 0, magazineSize);

        return weaponMagazineAmmo;
    }

    public void SetWeaponMagazineAmmo(WeaponItemDefinition weaponDefinition, int ammo)
    {
        if (weaponDefinition == null)
        {
            weaponMagazineAmmo = -1;
            return;
        }

        weaponMagazineAmmo = Mathf.Clamp(ammo, 0, Mathf.Max(1, weaponDefinition.magazineSize));
    }

    public float EnsureArmorDurability(ArmorItemDefinition armorDefinition)
    {
        if (armorDefinition == null)
        {
            armorDurability = -1f;
            return 0f;
        }

        float maxDurability = Mathf.Max(1f, armorDefinition.maxDurability);
        if (armorDurability < 0f)
            armorDurability = maxDurability;
        else
            armorDurability = Mathf.Clamp(armorDurability, 0f, maxDurability);

        return armorDurability;
    }

    public float DamageArmorDurability(ArmorItemDefinition armorDefinition, float amount)
    {
        if (armorDefinition == null || amount <= 0f)
            return 0f;

        float previousDurability = EnsureArmorDurability(armorDefinition);
        armorDurability = Mathf.Max(0f, previousDurability - amount);
        return previousDurability - armorDurability;
    }

    public ItemRuntimeData DeepClone(bool preserveIdentity = true)
    {
        ItemRuntimeData clone = new ItemRuntimeData
        {
            instanceId = preserveIdentity && !string.IsNullOrWhiteSpace(instanceId)
                ? instanceId
                : Guid.NewGuid().ToString("N"),
            storedContainerState = storedContainerState != null ? storedContainerState.DeepClone() : null,
            weaponMagazineAmmo = weaponMagazineAmmo,
            armorDurability = armorDurability
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
