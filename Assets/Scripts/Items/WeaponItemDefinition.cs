using UnityEngine;

[CreateAssetMenu(fileName = "WeaponItem_", menuName = "NoSafeExit/Items/Weapon")]
public class WeaponItemDefinition : ItemDefinition
{
    [Header("Classification")]
    public WeaponCategory weaponCategory = WeaponCategory.AssaultRifle;
    public WeaponFireModeType fireMode = WeaponFireModeType.FullAutomatic;

    [Header("Weapon")]
    public GameObject equippedPrefab;
    public AmmoItemDefinition compatibleAmmo;
    public bool usesAmmo = true;
    [Min(1)]
    public int magazineSize = 30;
    [Min(0)]
    public int roundsPerMinute;
    [Min(0.01f)]
    public float baseDamage = 20f;
    [Min(0f)]
    public float recoil = 15f;
    [Min(0.01f)]
    public float shotsPerSecond = 6f;

    [Header("Audio")]
    public AudioClip shotAudio;
    public AudioClip reloadAudio;

    [Header("Grip")]
    public WeaponGripProfile gripProfile;

    [Header("UI")]
    public Sprite equipmentSlotIcon;

    [Header("Plugin Bridge")]
    public string pluginItemName;

    public override ItemType Type => ItemType.Weapon;

    public bool UsesLongGunDisplaySprite => weaponCategory != WeaponCategory.Pistol;

    protected override void OnValidate()
    {
        canStack = false;
        if (magazineSize < 1)
            magazineSize = 1;

        if (roundsPerMinute < 1 && shotsPerSecond > 0.01f)
            roundsPerMinute = Mathf.Max(1, Mathf.RoundToInt(shotsPerSecond * 60f));

        if (roundsPerMinute < 1)
            roundsPerMinute = 1;

        if (baseDamage < 0.01f)
            baseDamage = 0.01f;

        if (recoil < 0f)
            recoil = 0f;

        shotsPerSecond = Mathf.Max(0.01f, roundsPerMinute / 60f);

        if (!usesAmmo)
            compatibleAmmo = null;

        base.OnValidate();
    }
}
