using JUTPS.WeaponSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Weapon))]
public class EnemyWeaponLoadout : MonoBehaviour
{
    [SerializeField] private WeaponItemDefinition weaponDefinition;
    [SerializeField] private Weapon weapon;
    [SerializeField, Min(0)] private int reserveMagazineCount = 3;
    [SerializeField] private bool refillMagazineOnAwake = true;
    [SerializeField] private bool applyOnAwake = true;

    public WeaponItemDefinition WeaponDefinition => weaponDefinition;

    private void Awake()
    {
        CacheWeapon();

        if (applyOnAwake)
            ApplyLoadout(refillMagazineOnAwake);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheWeapon();

        if (!Application.isPlaying)
            ApplyLoadout(false);
    }
#endif

    [ContextMenu("Apply Enemy Weapon Loadout")]
    public void ApplyLoadout()
    {
        ApplyLoadout(true);
    }

    private void ApplyLoadout(bool refillMagazine)
    {
        if (weaponDefinition == null || weapon == null)
            return;

        WeaponReferenceAutoBinder referenceBinder = GetComponent<WeaponReferenceAutoBinder>();
        if (referenceBinder != null)
            referenceBinder.BindReferences();

        if (weaponDefinition.gripProfile != null)
            WeaponGripProfileApplier.ApplyToWeapon(weapon, weaponDefinition.gripProfile);

        int magazineSize = Mathf.Max(1, weaponDefinition.magazineSize);
        weapon.BulletsPerMagazine = magazineSize;
        weapon.BulletBaseDamage = Mathf.Max(0.01f, weaponDefinition.baseDamage);
        weapon.Fire_Rate = weaponDefinition.shotsPerSecond > 0.01f
            ? 1f / weaponDefinition.shotsPerSecond
            : 60f / Mathf.Max(1, weaponDefinition.roundsPerMinute);
        weapon.FireMode = MapFireMode(weaponDefinition.fireMode);
        weapon.ContinuousUseItem = weaponDefinition.fireMode == WeaponFireModeType.FullAutomatic;
        weapon.RecoilForceRotation = weaponDefinition.recoil;
        weapon.RecoilForce = Mathf.Clamp(weaponDefinition.recoil / 100f, 0.02f, 0.3f);
        weapon.InfiniteAmmo = false;

        if (weapon.FireMode != Weapon.WeaponFireMode.Shotgun)
            weapon.NumberOfShotgunBulletsPerShot = 1;

        if (weaponDefinition.shotAudio != null)
            weapon.ShootAudio = weaponDefinition.shotAudio;

        if (weaponDefinition.reloadAudio != null)
            weapon.ReloadAudio = weaponDefinition.reloadAudio;

        if (weaponDefinition.icon != null)
            weapon.ItemIcon = weaponDefinition.icon;

        weapon.ItemName = string.IsNullOrWhiteSpace(weaponDefinition.pluginItemName)
            ? weaponDefinition.displayName
            : weaponDefinition.pluginItemName;
        weapon.ItemFilterTag = weaponDefinition.weaponCategory == WeaponCategory.Pistol ? "Hand Gun" : "General";

        if (refillMagazine)
            weapon.BulletsAmounts = magazineSize;
        else
            weapon.BulletsAmounts = Mathf.Clamp(weapon.BulletsAmounts, 0, magazineSize);

        weapon.TotalBullets = Mathf.Max(0, magazineSize * reserveMagazineCount);
        weapon.RefreshItemDependencies();
    }

    private void CacheWeapon()
    {
        if (weapon == null)
            weapon = GetComponent<Weapon>();
    }

    private static Weapon.WeaponFireMode MapFireMode(WeaponFireModeType fireMode)
    {
        return fireMode switch
        {
            WeaponFireModeType.FullAutomatic => Weapon.WeaponFireMode.Auto,
            WeaponFireModeType.BoltAction => Weapon.WeaponFireMode.BoltAction,
            WeaponFireModeType.PumpAction => Weapon.WeaponFireMode.Shotgun,
            _ => Weapon.WeaponFireMode.SemiAuto
        };
    }
}
