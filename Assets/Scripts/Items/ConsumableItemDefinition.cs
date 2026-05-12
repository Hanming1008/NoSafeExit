using UnityEngine;

[CreateAssetMenu(fileName = "ConsumableItem_", menuName = "NoSafeExit/Items/Consumable")]
public class ConsumableItemDefinition : ItemDefinition
{
    [Header("Consumable")]
    [Min(0f)]
    public float hydrationRestoreAmount = 25f;
    [Min(0f)]
    public float hungerRestoreAmount = 25f;
    [Min(0.01f)]
    public float useDuration = 1.5f;

    public override ItemType Type => ItemType.Consumable;

    protected override void OnValidate()
    {
        canStack = true;
        if (maxStackSize < 1)
            maxStackSize = 3;

        if (hydrationRestoreAmount < 0f)
            hydrationRestoreAmount = 0f;

        if (hungerRestoreAmount < 0f)
            hungerRestoreAmount = 0f;

        if (useDuration < 0.01f)
            useDuration = 0.01f;

        base.OnValidate();
    }
}
