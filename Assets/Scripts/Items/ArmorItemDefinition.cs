using UnityEngine;

[CreateAssetMenu(fileName = "ArmorItem_", menuName = "NoSafeExit/Items/Armor")]
public class ArmorItemDefinition : ItemDefinition
{
    [Header("Armor")]
    public ArmorSlotType armorSlot = ArmorSlotType.Chest;
    [Range(0f, 1f)]
    public float damageReduction = 0.15f;
    [Min(1f)]
    public float maxDurability = 100f;
    public ContainerItemDefinition providedRigContainer;
    public GameObject equippedVisualPrefab;
    public string[] equippedVisualObjectNames;
    public string[] hiddenVisualObjectNames;

    public override ItemType Type => ItemType.Armor;

    protected override void OnValidate()
    {
        canStack = false;

        if (damageReduction < 0f)
            damageReduction = 0f;
        else if (damageReduction > 1f)
            damageReduction = 1f;

        if (maxDurability < 1f)
            maxDurability = 1f;

        base.OnValidate();
    }
}
