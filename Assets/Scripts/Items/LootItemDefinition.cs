using UnityEngine;

[CreateAssetMenu(fileName = "LootItem_", menuName = "NoSafeExit/Items/Loot")]
public class LootItemDefinition : ItemDefinition
{
    [Header("Loot")]
    public bool isCurrency;

    public override ItemType Type => isCurrency ? ItemType.Currency : ItemType.Loot;

    protected override void OnValidate()
    {
        if (isCurrency)
        {
            canStack = true;
            if (maxStackSize < 1)
                maxStackSize = 10000;
        }
        else
        {
            canStack = false;
            maxStackSize = 1;
        }

        base.OnValidate();
    }
}
