using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(75)]
[DisallowMultipleComponent]
public class PlayerStartingLoadout : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerGridInventory gridInventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private CharacterEquipmentVisuals equipmentVisuals;
    [SerializeField] private PlayerWeaponSelection weaponSelection;

    [Header("Startup Loadout")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool clearExistingLoadout = true;
    [SerializeField] private bool selectPrimaryAfterApply = true;
    [SerializeField] private ArmorItemDefinition headArmor;
    [SerializeField] private ArmorItemDefinition chestArmor;
    [SerializeField] private WeaponItemDefinition primaryWeapon;
    [SerializeField] private AmmoItemDefinition reserveAmmo;
    [SerializeField, Min(0)] private int reserveAmmoQuantity = 90;
    [SerializeField] private MedicalItemDefinition medicalItem;
    [SerializeField, Min(0)] private int medicalQuantity = 2;

    void Awake()
    {
        ResolveReferences();
        ResolveDefaultDefinitions();
    }

    void Start()
    {
        if (!applyOnStart)
            return;

        ApplyLoadout();

        if (selectPrimaryAfterApply)
            StartCoroutine(SelectPrimaryNextFrame());
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    [ContextMenu("Apply Starting Loadout")]
    public void ApplyLoadout()
    {
        ResolveReferences();
        ResolveDefaultDefinitions();

        if (equipment == null || gridInventory == null)
        {
            Debug.LogWarning("PlayerStartingLoadout: Missing PlayerEquipment or PlayerGridInventory.", this);
            return;
        }

        if (clearExistingLoadout)
            ClearCurrentLoadout();

        equipment.EnsureEquipmentSlots();
        gridInventory.EnsureContainers();

        if (headArmor != null)
            equipment.TryAssignEquippedItem(EquipmentSlotType.HeadArmor, headArmor, 1, true);

        if (chestArmor != null)
            equipment.TryAssignEquippedItem(EquipmentSlotType.ChestArmor, chestArmor, 1, true);

        SyncChestRigContainer();

        if (primaryWeapon != null)
            equipment.TryAssignEquippedItem(EquipmentSlotType.PrimaryWeapon, primaryWeapon, 1, true);

        gridInventory.EnsureContainers();

        TryAddCarriedItem(reserveAmmo, reserveAmmoQuantity);
        TryAddCarriedItem(medicalItem, medicalQuantity);

        equipmentVisuals?.ForceRefreshNow();
    }

    private void ClearCurrentLoadout()
    {
        inventory?.ClearAll();
        equipment?.ClearAllEquipment();

        if (gridInventory == null)
            return;

        gridInventory.PocketContainer?.Clear();
        gridInventory.UnequipRig();
        gridInventory.UnequipBackpack();
        gridInventory.PocketContainer?.Clear();
    }

    private void SyncChestRigContainer()
    {
        if (gridInventory == null || equipment == null)
            return;

        InventorySlot chestSlot = equipment.GetSlot(EquipmentSlotType.ChestArmor);
        ArmorItemDefinition equippedChest = chestSlot?.Item as ArmorItemDefinition;
        if (equippedChest == null || equippedChest.providedRigContainer == null)
            return;

        gridInventory.EquipRig(equippedChest.providedRigContainer, chestSlot.RuntimeData);
    }

    private void TryAddCarriedItem(ItemDefinition item, int quantity)
    {
        if (gridInventory == null || item == null || quantity <= 0)
            return;

        int before = gridInventory.GetQuantity(item);
        gridInventory.TryAddToCarriedContainers(
            item,
            quantity,
            null,
            includePocket: true,
            out _,
            out _);

        int added = gridInventory.GetQuantity(item) - before;
        if (added < quantity)
        {
            Debug.LogWarning(
                $"PlayerStartingLoadout: Only added {added}/{quantity} of {item.displayName}; carried containers are full.",
                this);
        }
    }

    private IEnumerator SelectPrimaryNextFrame()
    {
        yield return null;
        weaponSelection?.SelectPrimaryWeapon();
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (gridInventory == null)
            gridInventory = GetComponent<PlayerGridInventory>();

        if (equipment == null)
            equipment = GetComponent<PlayerEquipment>();

        if (equipmentVisuals == null)
            equipmentVisuals = GetComponent<CharacterEquipmentVisuals>();

        if (weaponSelection == null)
            weaponSelection = GetComponent<PlayerWeaponSelection>();
    }

    private void ResolveDefaultDefinitions()
    {
#if UNITY_EDITOR
        headArmor ??= LoadAsset<ArmorItemDefinition>("Assets/Data/Items/Armor/Armor_Helmet_LevelIII.asset");
        chestArmor ??= LoadAsset<ArmorItemDefinition>("Assets/Data/Items/Armor/Armor_Body_LevelIII.asset");
        primaryWeapon ??= LoadAsset<WeaponItemDefinition>("Assets/Data/Items/Weapons/Weapon_Groza.asset");
        reserveAmmo ??= LoadAsset<AmmoItemDefinition>("Assets/Data/Items/Ammo/Ammo_762x39mm.asset");
        medicalItem ??= LoadAsset<MedicalItemDefinition>("Assets/Data/Items/Debug/Debug_Medkit.asset");
#endif
    }

#if UNITY_EDITOR
    private static T LoadAsset<T>(string path) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
#endif
}
