using UnityEngine;

[CreateAssetMenu(fileName = "AmmoItem_", menuName = "NoSafeExit/Items/Ammo")]
public class AmmoItemDefinition : ItemDefinition
{
    [Header("Ammo")]
    public string ammoCategory = "5.56x45mm";

    public override ItemType Type => ItemType.Ammo;

    protected override void OnValidate()
    {
        canStack = true;
        if (maxStackSize < 1)
            maxStackSize = 60;

        base.OnValidate();
    }
}
