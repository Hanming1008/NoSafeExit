using UnityEngine;

[CreateAssetMenu(fileName = "MedicalItem_", menuName = "NoSafeExit/Items/Medical")]
public class MedicalItemDefinition : ItemDefinition
{
    [Header("Medical")]
    [Min(0.01f)]
    public float healAmount = 35f;
    [Min(0f)]
    public float staminaRestoreAmount;
    [Min(0.01f)]
    public float useDuration = 2f;

    public override ItemType Type => ItemType.Medical;

    protected override void OnValidate()
    {
        canStack = true;
        if (maxStackSize < 1)
            maxStackSize = 4;

        if (healAmount < 0.01f)
            healAmount = 0.01f;

        if (staminaRestoreAmount < 0f)
            staminaRestoreAmount = 0f;

        if (useDuration < 0.01f)
            useDuration = 0.01f;

        base.OnValidate();
    }
}
