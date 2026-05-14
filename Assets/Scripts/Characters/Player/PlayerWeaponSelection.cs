using System.Collections.Generic;
using JUTPS;
using JUTPS.InventorySystem;
using JUTPS.ItemSystem;
using JUTPS.WeaponSystem;
using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public class PlayerWeaponSelection : MonoBehaviour
{
    private sealed class WeaponVisualOverrideState
    {
        public WeaponItemDefinition Definition;
        public GameObject VisualRoot;
        public Renderer[] OriginalRenderers = System.Array.Empty<Renderer>();
    }

    [Header("References")]
    [SerializeField] private PlayerGameplayInput gameplayInput;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerGridInventory gridInventory;
    [SerializeField] private JUCharacterController juCharacter;
    [SerializeField] private JUInventory juInventory;
    [SerializeField] private Animator animator;

    [Header("Settings")]
    [SerializeField] private bool switchToUnarmedWhenSecondaryMissing = true;

    private float defaultLeftElbowAdjustWeight;
    private WeaponItemDefinition lastPrimaryDefinition;
    private WeaponItemDefinition lastSecondaryDefinition;
    private JUHoldableItem lastPrimaryWeapon;
    private JUHoldableItem lastSecondaryWeapon;
    private JUHoldableItem lastInvalidCurrentWeapon;
    private WeaponItemDefinition lastActiveWeaponDefinition;
    private bool wasReloadingLastFrame;
    private Weapon pendingReloadWeapon;
    private WeaponItemDefinition pendingReloadDefinition;
    private int pendingReloadMagazineBefore;
    private int pendingReloadReserveBefore;
    private EquipmentSlotType lastRequestedWeaponSlot = EquipmentSlotType.PrimaryWeapon;
    private readonly Dictionary<Transform, WeaponVisualOverrideState> weaponVisualOverrides = new Dictionary<Transform, WeaponVisualOverrideState>();

    void Awake()
    {
        ResolveReferences();
        if (juCharacter != null)
            defaultLeftElbowAdjustWeight = juCharacter.LeftElbowAdjustWeight;
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || juCharacter == null)
            return;

        WeaponItemDefinition activeDefinition = GetCurrentWeaponDefinition();
        WeaponGripProfile gripProfile = activeDefinition != null ? activeDefinition.gripProfile : null;
        if (gripProfile == null || !gripProfile.overrideLeftElbowHint)
            return;

        if (juCharacter.PivotItemRotation == null)
            return;

        float weight = Mathf.Clamp01(gripProfile.leftElbowAdjustWeight);
        Transform pivot = juCharacter.PivotItemRotation.transform;
        Vector3 hintOffset = gripProfile.leftElbowHintOffset;
        Vector3 hintPosition =
            pivot.position +
            pivot.right * hintOffset.x +
            pivot.up * hintOffset.y +
            pivot.forward * hintOffset.z;

        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, weight);
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, hintPosition);
    }

    void Update()
    {
        if (NeedsReferenceResolution())
            ResolveReferences();

        if (gameplayInput == null || equipment == null || inventory == null || juCharacter == null || juInventory == null)
            return;

        SyncPluginInventoryWithEquipment();
        SyncWeaponReserveAmmo();
        SyncReloadState();
        SyncEquippedWeaponMagazineState();
        SyncActiveWeaponIKSettings();
        SyncVisibleWeaponModels();

        if (gameplayInput.IsPrimaryWeaponPressed())
        {
            SelectPrimaryWeapon();
            return;
        }

        if (gameplayInput.IsSecondaryWeaponPressed())
            SelectSecondaryWeapon();
    }

    public WeaponItemDefinition GetCurrentWeaponDefinition()
    {
        JUHoldableItem currentHoldable = juInventory != null ? juInventory.HoldableItemInUseInRightHand : null;
        if (currentHoldable == null)
            return null;

        WeaponItemDefinition primaryDefinition = equipment != null
            ? equipment.GetEquippedWeaponDefinition(EquipmentSlotType.PrimaryWeapon)
            : null;
        if (MatchesDefinition(currentHoldable, primaryDefinition))
            return primaryDefinition;

        WeaponItemDefinition secondaryDefinition = equipment != null
            ? equipment.GetEquippedWeaponDefinition(EquipmentSlotType.SecondaryWeapon)
            : null;
        if (MatchesDefinition(currentHoldable, secondaryDefinition))
            return secondaryDefinition;

        return null;
    }

    public Weapon GetCurrentWeaponComponent()
    {
        return juInventory != null ? juInventory.HoldableItemInUseInRightHand as Weapon : null;
    }

    public int GetReserveAmmoFor(WeaponItemDefinition definition)
    {
        return GetReserveAmmo(definition);
    }

    public WeaponItemDefinition GetPreviewWeaponDefinition()
    {
        WeaponItemDefinition activeDefinition = GetCurrentWeaponDefinition();
        if (activeDefinition != null)
            return activeDefinition;

        if (equipment == null)
            return null;

        WeaponItemDefinition preferredDefinition = equipment.GetEquippedWeaponDefinition(lastRequestedWeaponSlot);
        if (preferredDefinition != null)
            return preferredDefinition;

        WeaponItemDefinition primaryDefinition = equipment.GetEquippedWeaponDefinition(EquipmentSlotType.PrimaryWeapon);
        if (primaryDefinition != null)
            return primaryDefinition;

        return equipment.GetEquippedWeaponDefinition(EquipmentSlotType.SecondaryWeapon);
    }

    public void SelectPrimaryWeapon()
    {
        lastRequestedWeaponSlot = EquipmentSlotType.PrimaryWeapon;
        JUHoldableItem targetWeapon = GetMappedWeaponFromEquipmentSlot(EquipmentSlotType.PrimaryWeapon);

        if (targetWeapon == null)
            return;

        juCharacter.SwitchToItem(targetWeapon.ItemSwitchID, true);
    }

    public void SelectSecondaryWeapon()
    {
        lastRequestedWeaponSlot = EquipmentSlotType.SecondaryWeapon;
        JUHoldableItem targetWeapon = GetMappedWeaponFromEquipmentSlot(EquipmentSlotType.SecondaryWeapon);

        if (targetWeapon != null)
        {
            juCharacter.SwitchToItem(targetWeapon.ItemSwitchID, true);
            return;
        }

        if (switchToUnarmedWhenSecondaryMissing)
            juCharacter.SwitchToItem(-1, true);
    }

    private void SyncPluginInventoryWithEquipment()
    {
        WeaponItemDefinition primaryDefinition = equipment.GetEquippedWeaponDefinition(EquipmentSlotType.PrimaryWeapon);
        WeaponItemDefinition secondaryDefinition = equipment.GetEquippedWeaponDefinition(EquipmentSlotType.SecondaryWeapon);

        JUHoldableItem primaryWeapon = GetMappedWeaponFromEquipmentSlot(EquipmentSlotType.PrimaryWeapon);
        JUHoldableItem secondaryWeapon = GetMappedWeaponFromEquipmentSlot(EquipmentSlotType.SecondaryWeapon);

        bool mappingChanged =
            primaryDefinition != lastPrimaryDefinition ||
            secondaryDefinition != lastSecondaryDefinition ||
            primaryWeapon != lastPrimaryWeapon ||
            secondaryWeapon != lastSecondaryWeapon;

        if (mappingChanged)
        {
            if (lastPrimaryWeapon != null && lastPrimaryWeapon != primaryWeapon)
                ClearWeaponVisualOverride(lastPrimaryWeapon.transform);
            if (lastSecondaryWeapon != null && lastSecondaryWeapon != secondaryWeapon)
                ClearWeaponVisualOverride(lastSecondaryWeapon.transform);

            ApplyDefinitionToWeapon(primaryDefinition, primaryWeapon as Weapon, EquipmentSlotType.PrimaryWeapon);
            ApplyDefinitionToWeapon(secondaryDefinition, secondaryWeapon as Weapon, EquipmentSlotType.SecondaryWeapon);
            ApplyDefinitionVisualOverride(primaryDefinition, primaryWeapon as Weapon);
            ApplyDefinitionVisualOverride(secondaryDefinition, secondaryWeapon as Weapon);

            for (int i = 0; i < juInventory.HoldableItensRightHand.Length; i++)
            {
                JUHoldableItem holdableItem = juInventory.HoldableItensRightHand[i];
                if (holdableItem == null)
                    continue;

                bool shouldBeAvailable = holdableItem == primaryWeapon || holdableItem == secondaryWeapon;
                holdableItem.Unlocked = shouldBeAvailable;
                holdableItem.ItemQuantity = shouldBeAvailable ? Mathf.Max(holdableItem.ItemQuantity, 1) : 0;
            }

            if (juInventory.SequenceSlot != null)
            {
                for (int i = 0; i < juInventory.SequenceSlot.Length; i++)
                    juInventory.SequenceSlot[i].ItemInThisSlot = null;

                juInventory.SetSequentialSlotItem(JUInventory.SequentialSlotsEnum.first, primaryWeapon);
                juInventory.SetSequentialSlotItem(JUInventory.SequentialSlotsEnum.second, secondaryWeapon);
            }
        }

        JUHoldableItem currentWeapon = juInventory.HoldableItemInUseInRightHand;
        bool currentWeaponInvalid = currentWeapon != null && currentWeapon != primaryWeapon && currentWeapon != secondaryWeapon;
        if (currentWeaponInvalid && currentWeapon != lastInvalidCurrentWeapon)
        {
            juCharacter.SwitchToItem(-1, true);
            lastInvalidCurrentWeapon = currentWeapon;
        }
        else if (!currentWeaponInvalid)
        {
            lastInvalidCurrentWeapon = null;
        }

        lastPrimaryDefinition = primaryDefinition;
        lastSecondaryDefinition = secondaryDefinition;
        lastPrimaryWeapon = primaryWeapon;
        lastSecondaryWeapon = secondaryWeapon;
    }

    private void SyncWeaponReserveAmmo()
    {
        if (equipment == null)
            return;

        SyncWeaponReserveAmmoForSlot(EquipmentSlotType.PrimaryWeapon);
        SyncWeaponReserveAmmoForSlot(EquipmentSlotType.SecondaryWeapon);
    }

    private void SyncWeaponReserveAmmoForSlot(EquipmentSlotType slotType)
    {
        WeaponItemDefinition definition = equipment.GetEquippedWeaponDefinition(slotType);
        Weapon weapon = GetMappedWeaponFromEquipmentSlot(slotType) as Weapon;
        if (weapon == null || !UsesManagedAmmo(definition))
            return;

        weapon.TotalBullets = GetReserveAmmo(definition);
        weapon.BulletsAmounts = Mathf.Clamp(weapon.BulletsAmounts, 0, weapon.BulletsPerMagazine);
    }

    private void SyncEquippedWeaponMagazineState()
    {
        if (juCharacter != null && juCharacter.IsReloading)
            return;

        SyncEquippedWeaponMagazineStateForSlot(EquipmentSlotType.PrimaryWeapon);
        SyncEquippedWeaponMagazineStateForSlot(EquipmentSlotType.SecondaryWeapon);
    }

    private void SyncEquippedWeaponMagazineStateForSlot(EquipmentSlotType slotType)
    {
        if (equipment == null)
            return;

        WeaponItemDefinition definition = equipment.GetEquippedWeaponDefinition(slotType);
        Weapon weapon = GetMappedWeaponFromEquipmentSlot(slotType) as Weapon;
        if (definition == null || weapon == null)
            return;

        InventorySlot slot = equipment.GetSlot(slotType);
        ItemRuntimeData runtimeData = slot != null ? slot.RuntimeData : null;
        if (runtimeData == null)
            return;

        runtimeData.SetWeaponMagazineAmmo(
            definition,
            Mathf.Clamp(weapon.BulletsAmounts, 0, Mathf.Max(1, weapon.BulletsPerMagazine)));
    }

    private void SyncReloadState()
    {
        bool isReloading = juCharacter != null && juCharacter.IsReloading;

        if (isReloading && !wasReloadingLastFrame)
            CaptureReloadState();
        else if (!isReloading && wasReloadingLastFrame)
            ResolveReloadState();

        wasReloadingLastFrame = isReloading;
    }

    private void CaptureReloadState()
    {
        WeaponItemDefinition activeDefinition = GetCurrentWeaponDefinition();
        Weapon activeWeapon = GetCurrentWeaponComponent();
        if (activeWeapon == null || !UsesManagedAmmo(activeDefinition))
        {
            ClearPendingReloadState();
            return;
        }

        pendingReloadWeapon = activeWeapon;
        pendingReloadDefinition = activeDefinition;
        pendingReloadMagazineBefore = Mathf.Clamp(activeWeapon.BulletsAmounts, 0, activeWeapon.BulletsPerMagazine);
        pendingReloadReserveBefore = GetReserveAmmo(activeDefinition);
    }

    private void ResolveReloadState()
    {
        if (pendingReloadWeapon == null || !UsesManagedAmmo(pendingReloadDefinition))
        {
            ClearPendingReloadState();
            return;
        }

        int magazineCapacity = Mathf.Max(1, pendingReloadWeapon.BulletsPerMagazine);
        int bulletsNeeded = Mathf.Max(0, magazineCapacity - pendingReloadMagazineBefore);
        int bulletsLoaded = Mathf.Min(bulletsNeeded, pendingReloadReserveBefore);
        int bulletsRemoved = bulletsLoaded > 0
            ? RemoveReserveAmmo(pendingReloadDefinition, bulletsLoaded)
            : 0;
        int correctedMagazine = pendingReloadMagazineBefore + bulletsRemoved;
        int correctedReserve = GetReserveAmmo(pendingReloadDefinition);

        pendingReloadWeapon.BulletsAmounts = correctedMagazine;
        pendingReloadWeapon.TotalBullets = correctedReserve;

        ClearPendingReloadState();
    }

    private void ClearPendingReloadState()
    {
        pendingReloadWeapon = null;
        pendingReloadDefinition = null;
        pendingReloadMagazineBefore = 0;
        pendingReloadReserveBefore = 0;
    }

    private void SyncActiveWeaponIKSettings()
    {
        WeaponItemDefinition activeDefinition = GetCurrentWeaponDefinition();
        if (activeDefinition == lastActiveWeaponDefinition)
            return;

        float leftElbowWeight = defaultLeftElbowAdjustWeight;
        if (activeDefinition != null && activeDefinition.gripProfile != null)
            leftElbowWeight = activeDefinition.gripProfile.leftElbowAdjustWeight;

        juCharacter.LeftElbowAdjustWeight = Mathf.Clamp01(leftElbowWeight);
        lastActiveWeaponDefinition = activeDefinition;
    }

    private void SyncVisibleWeaponModels()
    {
        if (juInventory?.HoldableItensRightHand == null)
            return;

        JUHoldableItem visibleWeapon = juInventory.HoldableItemInUseInRightHand;
        if (visibleWeapon == null)
        {
            visibleWeapon = GetMappedWeaponFromEquipmentSlot(lastRequestedWeaponSlot)
                ?? GetMappedWeaponFromEquipmentSlot(EquipmentSlotType.PrimaryWeapon)
                ?? GetMappedWeaponFromEquipmentSlot(EquipmentSlotType.SecondaryWeapon)
                ?? GetFirstAvailableRightHandWeapon();
        }

        for (int i = 0; i < juInventory.HoldableItensRightHand.Length; i++)
        {
            JUHoldableItem holdableItem = juInventory.HoldableItensRightHand[i];
            if (holdableItem == null)
                continue;

            bool shouldShow = holdableItem == visibleWeapon;
            ApplyWeaponRendererVisibility(holdableItem.transform, shouldShow);
        }
    }

    private void ApplyWeaponRendererVisibility(Transform weaponRoot, bool visible)
    {
        if (weaponRoot == null)
            return;

        if (weaponVisualOverrides.TryGetValue(weaponRoot, out WeaponVisualOverrideState overrideState)
            && overrideState != null
            && overrideState.VisualRoot != null)
        {
            SetRenderersVisible(overrideState.OriginalRenderers, false);
            SetVisualRootVisible(overrideState.VisualRoot, visible);
            return;
        }

        Renderer[] renderers = weaponRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = visible && IsWeaponRendererVisibleByDefault(renderer, weaponRoot);
            renderer.forceRenderingOff = false;
        }
    }

    private bool IsWeaponRendererVisibleByDefault(Renderer renderer, Transform weaponRoot)
    {
        if (renderer == null || weaponRoot == null)
            return false;

        if (renderer.transform != weaponRoot)
            return true;

        Transform explicitModelChild = FindChildContaining(weaponRoot, "_Model");
        return explicitModelChild == null;
    }

    private void ApplyDefinitionVisualOverride(WeaponItemDefinition definition, Weapon weapon)
    {
        if (weapon == null)
            return;

        Transform weaponRoot = weapon.transform;
        if (!ShouldUseDefinitionVisualOverride(definition))
        {
            ClearWeaponVisualOverride(weaponRoot);
            return;
        }

        if (weaponVisualOverrides.TryGetValue(weaponRoot, out WeaponVisualOverrideState existingState)
            && existingState != null
            && existingState.Definition == definition
            && existingState.VisualRoot != null)
        {
            SetRenderersVisible(existingState.OriginalRenderers, false);
            return;
        }

        ClearWeaponVisualOverride(weaponRoot);

        Renderer[] originalRenderers = weaponRoot.GetComponentsInChildren<Renderer>(true);
        GameObject visualRoot = Instantiate(definition.equippedPrefab, weaponRoot);
        visualRoot.name = definition.displayName + "_Visual";
        visualRoot.transform.localPosition = Vector3.zero;
        visualRoot.transform.localRotation = Quaternion.identity;
        visualRoot.transform.localScale = Vector3.one;
        SetLayerRecursively(visualRoot, weaponRoot.gameObject.layer);
        StripGameplayComponents(visualRoot);

        WeaponVisualOverrideState state = new WeaponVisualOverrideState
        {
            Definition = definition,
            VisualRoot = visualRoot,
            OriginalRenderers = originalRenderers
        };

        SetRenderersVisible(originalRenderers, false);
        weaponVisualOverrides[weaponRoot] = state;
    }

    private bool ShouldUseDefinitionVisualOverride(WeaponItemDefinition definition)
    {
        if (definition == null || definition.equippedPrefab == null)
            return false;

        string itemId = definition.itemId;
        return itemId != "weapon_hk416"
            && itemId != "weapon_ak47"
            && itemId != "weapon_glock";
    }

    private void ClearWeaponVisualOverride(Transform weaponRoot)
    {
        if (weaponRoot == null || !weaponVisualOverrides.TryGetValue(weaponRoot, out WeaponVisualOverrideState state))
            return;

        weaponVisualOverrides.Remove(weaponRoot);
        if (state != null)
        {
            SetRenderersVisible(state.OriginalRenderers, true);
            DestroyRuntimeObject(state.VisualRoot);
        }
    }

    private void SetVisualRootVisible(GameObject visualRoot, bool visible)
    {
        if (visualRoot == null)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        SetRenderersVisible(renderers, visible);
    }

    private void SetRenderersVisible(Renderer[] renderers, bool visible)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = visible;
            renderer.forceRenderingOff = false;
        }
    }

    private void StripGameplayComponents(GameObject visualRoot)
    {
        if (visualRoot == null)
            return;

        Collider[] colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            DestroyRuntimeObject(colliders[i]);

        Rigidbody[] rigidbodies = visualRoot.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            DestroyRuntimeObject(rigidbodies[i]);

        AudioSource[] audioSources = visualRoot.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
            DestroyRuntimeObject(audioSources[i]);

        MonoBehaviour[] behaviours = visualRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            DestroyRuntimeObject(behaviours[i]);
    }

    private void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
            SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
    }

    private void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private JUHoldableItem GetFirstAvailableRightHandWeapon()
    {
        if (juInventory?.HoldableItensRightHand == null)
            return null;

        for (int i = 0; i < juInventory.HoldableItensRightHand.Length; i++)
        {
            JUHoldableItem holdableItem = juInventory.HoldableItensRightHand[i];
            if (holdableItem == null)
                continue;

            if (holdableItem.Unlocked && holdableItem.ItemQuantity > 0)
                return holdableItem;
        }

        return null;
    }

    private JUHoldableItem GetMappedWeaponFromEquipmentSlot(EquipmentSlotType slotType)
    {
        WeaponItemDefinition weaponDefinition = equipment.GetEquippedWeaponDefinition(slotType);
        if (weaponDefinition == null)
            return null;

        return FindMappedWeapon(weaponDefinition);
    }

    private JUHoldableItem FindMappedWeapon(WeaponItemDefinition weaponDefinition)
    {
        if (weaponDefinition == null || juInventory?.HoldableItensRightHand == null)
            return null;

        if (string.IsNullOrWhiteSpace(weaponDefinition.pluginItemName))
            return null;

        for (int i = 0; i < juInventory.HoldableItensRightHand.Length; i++)
        {
            JUHoldableItem holdableItem = juInventory.HoldableItensRightHand[i];
            if (holdableItem == null)
                continue;

            if (string.Equals(holdableItem.ItemName, weaponDefinition.pluginItemName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(holdableItem.name, weaponDefinition.pluginItemName, System.StringComparison.OrdinalIgnoreCase))
                return holdableItem;
        }

        return null;
    }

    private bool MatchesDefinition(JUHoldableItem holdableItem, WeaponItemDefinition definition)
    {
        if (holdableItem == null || definition == null || string.IsNullOrWhiteSpace(definition.pluginItemName))
            return false;

        return string.Equals(holdableItem.ItemName, definition.pluginItemName, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(holdableItem.name, definition.pluginItemName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyDefinitionToWeapon(WeaponItemDefinition definition, Weapon weapon, EquipmentSlotType slotType)
    {
        if (definition == null || weapon == null)
            return;

        EnsureWeaponRuntimeReferences(weapon);

        if (definition.gripProfile != null)
            WeaponGripProfileApplier.ApplyToWeapon(weapon, definition.gripProfile);

        weapon.BulletsPerMagazine = Mathf.Max(1, definition.magazineSize);
        weapon.BulletBaseDamage = Mathf.Max(0.01f, definition.baseDamage);
        weapon.Fire_Rate = definition.shotsPerSecond > 0.01f
            ? 1f / definition.shotsPerSecond
            : 60f / Mathf.Max(1, definition.roundsPerMinute);
        weapon.FireMode = MapFireMode(definition.fireMode);
        weapon.ContinuousUseItem = definition.fireMode == WeaponFireModeType.FullAutomatic;
        weapon.RecoilForceRotation = definition.recoil;
        weapon.RecoilForce = Mathf.Clamp(definition.recoil / 100f, 0.02f, 0.3f);

        if (definition.shotAudio != null)
            weapon.ShootAudio = definition.shotAudio;

        if (definition.reloadAudio != null)
            weapon.ReloadAudio = definition.reloadAudio;

        if (definition.icon != null)
            weapon.ItemIcon = definition.icon;

        weapon.ItemFilterTag = definition.weaponCategory == WeaponCategory.Pistol ? "Hand Gun" : "General";
        weapon.BulletsAmounts = GetMagazineAmmoForEquippedWeapon(definition, slotType);
        weapon.TotalBullets = UsesManagedAmmo(definition)
            ? GetReserveAmmo(definition)
            : Mathf.Max(0, weapon.TotalBullets);
    }

    private int GetMagazineAmmoForEquippedWeapon(WeaponItemDefinition definition, EquipmentSlotType slotType)
    {
        int magazineSize = Mathf.Max(1, definition != null ? definition.magazineSize : 1);
        InventorySlot slot = equipment != null ? equipment.GetSlot(slotType) : null;
        ItemRuntimeData runtimeData = slot != null ? slot.RuntimeData : null;
        return runtimeData != null
            ? runtimeData.EnsureWeaponMagazineAmmo(definition)
            : magazineSize;
    }

    private void EnsureWeaponRuntimeReferences(Weapon weapon)
    {
        if (weapon == null)
            return;

        if (weapon.Shoot_Position == null)
            weapon.Shoot_Position = FindChildByName(weapon.transform, "Shoot_Position");

        if (weapon.OppositeHandPosition == null)
            weapon.OppositeHandPosition = FindChildByName(weapon.transform, "LeftHandIK");

        if (weapon.GunSlider == null)
            weapon.GunSlider = FindChildContaining(weapon.transform, "Slide");
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private Transform FindChildContaining(Transform root, string partialName)
    {
        if (root == null || string.IsNullOrWhiteSpace(partialName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null
                && children[i].name.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return children[i];
        }

        return null;
    }

    private Weapon.WeaponFireMode MapFireMode(WeaponFireModeType fireMode)
    {
        return fireMode switch
        {
            WeaponFireModeType.FullAutomatic => Weapon.WeaponFireMode.Auto,
            WeaponFireModeType.BoltAction => Weapon.WeaponFireMode.BoltAction,
            WeaponFireModeType.PumpAction => Weapon.WeaponFireMode.Shotgun,
            _ => Weapon.WeaponFireMode.SemiAuto
        };
    }

    private bool UsesManagedAmmo(WeaponItemDefinition definition)
    {
        return definition != null && definition.usesAmmo && definition.compatibleAmmo != null;
    }

    private int GetReserveAmmo(WeaponItemDefinition definition)
    {
        if (!UsesManagedAmmo(definition))
            return 0;

        int reserveAmmo = 0;
        if (gridInventory != null)
            reserveAmmo += gridInventory.GetQuantity(definition.compatibleAmmo);

        if (inventory != null)
            reserveAmmo += inventory.GetQuantity(definition.compatibleAmmo);

        return Mathf.Max(0, reserveAmmo);
    }

    private int RemoveReserveAmmo(WeaponItemDefinition definition, int quantity)
    {
        if (!UsesManagedAmmo(definition) || quantity <= 0)
            return 0;

        int removedQuantity = 0;
        int remainingQuantity = quantity;

        if (gridInventory != null)
        {
            removedQuantity += gridInventory.RemoveItem(definition.compatibleAmmo, remainingQuantity);
            remainingQuantity = quantity - removedQuantity;
        }

        if (remainingQuantity > 0 && inventory != null)
            removedQuantity += inventory.RemoveItem(definition.compatibleAmmo, remainingQuantity);

        return removedQuantity;
    }

    private bool NeedsReferenceResolution()
    {
        return gameplayInput == null || equipment == null || inventory == null || gridInventory == null || juCharacter == null || juInventory == null || animator == null;
    }

    private void ResolveReferences()
    {
        if (gameplayInput == null)
            gameplayInput = GetComponent<PlayerGameplayInput>();

        if (equipment == null)
            equipment = GetComponent<PlayerEquipment>();

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (gridInventory == null)
            gridInventory = GetComponent<PlayerGridInventory>();

        if (juCharacter == null)
            juCharacter = GetComponent<JUCharacterController>();

        if (juInventory == null)
            juInventory = GetComponent<JUInventory>();

        if (animator == null)
            animator = GetComponent<Animator>();
    }
}
