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
    [SerializeField] private bool useDefinitionVisualOverride = true;

    private GameObject visualOverrideRoot;
    private Renderer[] originalRenderers;
    private WeaponItemDefinition visualOverrideDefinition;

    public WeaponItemDefinition WeaponDefinition => weaponDefinition;
    public Weapon Weapon => weapon;
    public int ReserveMagazineCount => Mathf.Max(0, reserveMagazineCount);

    private void Awake()
    {
        CacheWeapon();

        if (applyOnAwake)
            ApplyLoadout(refillMagazineOnAwake);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || weaponDefinition == null || weapon == null)
            return;

        if (useDefinitionVisualOverride && (visualOverrideRoot == null || visualOverrideDefinition != weaponDefinition))
            ApplyDefinitionVisualOverride(weaponDefinition);

        if (visualOverrideRoot != null)
            SetRenderersVisible(originalRenderers, false);
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

    public void Configure(WeaponItemDefinition definition, int reserveMagazines, bool refillMagazine = true)
    {
        weaponDefinition = definition;
        reserveMagazineCount = Mathf.Max(0, reserveMagazines);
        ApplyLoadout(refillMagazine);
    }

    private void ApplyLoadout(bool refillMagazine)
    {
        if (weapon == null)
            return;

        if (weaponDefinition == null)
        {
            ClearVisualOverride();
            originalRenderers = weapon.GetComponentsInChildren<Renderer>(true);
            SetRenderersVisible(originalRenderers, false);
            return;
        }

        ApplyDefinitionVisualOverride(weaponDefinition);

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

    private void ApplyDefinitionVisualOverride(WeaponItemDefinition definition)
    {
        if (!Application.isPlaying)
            return;

        if (!useDefinitionVisualOverride || weapon == null || definition == null || definition.equippedPrefab == null)
        {
            ClearVisualOverride();
            return;
        }

        if (visualOverrideRoot != null && visualOverrideDefinition == definition)
        {
            SetRenderersVisible(originalRenderers, false);
            return;
        }

        ClearVisualOverride();

        Transform weaponRoot = weapon.transform;
        originalRenderers = weaponRoot.GetComponentsInChildren<Renderer>(true);
        visualOverrideRoot = Instantiate(definition.equippedPrefab, weaponRoot);
        visualOverrideRoot.name = definition.displayName + "_EnemyVisual";
        visualOverrideRoot.transform.localPosition = Vector3.zero;
        visualOverrideRoot.transform.localRotation = Quaternion.identity;
        visualOverrideRoot.transform.localScale = Vector3.one;
        visualOverrideDefinition = definition;

        SetLayerRecursively(visualOverrideRoot, weaponRoot.gameObject.layer);
        StripGameplayComponents(visualOverrideRoot);
        SetRenderersVisible(originalRenderers, false);
    }

    private void ClearVisualOverride()
    {
        if (visualOverrideRoot == null)
            return;

        SetRenderersVisible(originalRenderers, true);
        DestroyRuntimeObject(visualOverrideRoot);
        visualOverrideRoot = null;
        visualOverrideDefinition = null;
        originalRenderers = null;
    }

    private static void SetRenderersVisible(Renderer[] renderers, bool visible)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = visible;
            renderer.forceRenderingOff = !visible;
        }
    }

    private static void StripGameplayComponents(GameObject root)
    {
        if (root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            DestroyRuntimeObject(colliders[i]);

        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            DestroyRuntimeObject(rigidbodies[i]);

        AudioSource[] audioSources = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
            DestroyRuntimeObject(audioSources[i]);

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            DestroyRuntimeObject(behaviours[i]);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
            SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
