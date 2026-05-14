using System;
using System.Collections.Generic;
using JUTPS;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyCorpseLoot : MonoBehaviour
{
    [Serializable]
    public class CorpseEquipmentEntry
    {
        public EquipmentSlotType slotType;
        public InventorySlot slot = new InventorySlot();

        public CorpseEquipmentEntry(EquipmentSlotType slotType)
        {
            this.slotType = slotType;
        }
    }

    [Header("Identity")]
    [SerializeField] private string enemyTypeDisplayName = "Militia";
    [SerializeField] private string displayName = "Enemy Corpse";

    [Header("Search")]
    [SerializeField] private float interactionRadius = 2.2f;
    [SerializeField] private bool searchableOnlyWhenDead = true;

    [Header("Initial Equipment")]
    [SerializeField] private WeaponItemDefinition primaryWeaponDefinition;
    [SerializeField] private WeaponItemDefinition secondaryWeaponDefinition;
    [SerializeField] private ArmorItemDefinition headArmorDefinition;
    [SerializeField] private ArmorItemDefinition chestArmorDefinition;
    [SerializeField] private ContainerItemDefinition backpackDefinition;

    [Header("Runtime State")]
    [SerializeField] private List<CorpseEquipmentEntry> equippedSlots = new List<CorpseEquipmentEntry>();
    [SerializeReference] private GridContainerState pocketContainer = new GridContainerState();

    private JUHealth health;
    private bool initialized;

    public string EnemyTypeDisplayName => string.IsNullOrWhiteSpace(enemyTypeDisplayName) ? "Enemy" : enemyTypeDisplayName;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EnemyTypeDisplayName : displayName;
    public float InteractionRadius => Mathf.Max(0.25f, interactionRadius);
    public GridContainerState PocketContainer => pocketContainer;
    public IReadOnlyList<CorpseEquipmentEntry> EquippedSlots => equippedSlots;
    public bool IsSearchable
    {
        get
        {
            if (health == null)
                ResolveReferences();

            return !searchableOnlyWhenDead || health == null || health.IsDead;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureInitialized();
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
        EnsureEquipmentSlots();
        EnsurePocket();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (health != null)
            health.OnDeath.AddListener(OnDeath);
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath.RemoveListener(OnDeath);
    }

    public void EnsureInitialized()
    {
        EnsureEquipmentSlots();
        EnsurePocket();

        if (initialized)
            return;

        SeedInitialEquipment();
        initialized = true;
    }

    public InventorySlot GetSlot(EquipmentSlotType slotType)
    {
        CorpseEquipmentEntry entry = GetEntry(slotType);
        return entry != null ? entry.slot : null;
    }

    public bool CanEquip(EquipmentSlotType slotType, ItemDefinition item)
    {
        if (item == null)
            return false;

        return slotType switch
        {
            EquipmentSlotType.PrimaryWeapon => item is WeaponItemDefinition weapon && weapon.weaponCategory != WeaponCategory.Pistol,
            EquipmentSlotType.SecondaryWeapon => item is WeaponItemDefinition weapon && weapon.weaponCategory == WeaponCategory.Pistol,
            EquipmentSlotType.Backpack => item is ContainerItemDefinition container && container.containerKind == GridContainerKind.Backpack,
            EquipmentSlotType.HeadArmor => item is ArmorItemDefinition armor && armor.armorSlot == ArmorSlotType.Head,
            EquipmentSlotType.ChestArmor => item is ArmorItemDefinition armor && armor.armorSlot == ArmorSlotType.Chest,
            _ => false
        };
    }

    public bool TryClearSlot(EquipmentSlotType slotType, out ItemDefinition item, out int quantity, out ItemRuntimeData runtimeData)
    {
        item = null;
        quantity = 0;
        runtimeData = null;

        InventorySlot slot = GetSlot(slotType);
        if (slot == null || slot.IsEmpty)
            return false;

        item = slot.Item;
        quantity = slot.Quantity;
        runtimeData = slot.GetRuntimeDataForTransfer(slot.Quantity);
        slot.Clear();
        return true;
    }

    public bool TryRestoreSlot(EquipmentSlotType slotType, ItemDefinition item, int quantity, ItemRuntimeData runtimeData)
    {
        InventorySlot slot = GetSlot(slotType);
        return slot != null && slot.TrySet(item, quantity, runtimeData);
    }

    private void OnDeath()
    {
        EnsureInitialized();
    }

    private void ResolveReferences()
    {
        if (health == null)
            health = GetComponent<JUHealth>();

        if (health == null)
            health = GetComponentInParent<JUHealth>();
    }

    private void SeedInitialEquipment()
    {
        WeaponItemDefinition resolvedPrimary = primaryWeaponDefinition;
        if (resolvedPrimary == null)
        {
            EnemyWeaponLoadout loadout = GetComponentInChildren<EnemyWeaponLoadout>(true);
            if (loadout != null)
                resolvedPrimary = loadout.WeaponDefinition;
        }

        TrySeedSlot(EquipmentSlotType.PrimaryWeapon, resolvedPrimary);
        TrySeedSlot(EquipmentSlotType.SecondaryWeapon, secondaryWeaponDefinition);
        TrySeedSlot(EquipmentSlotType.HeadArmor, headArmorDefinition);
        TrySeedSlot(EquipmentSlotType.ChestArmor, chestArmorDefinition);
        TrySeedSlot(EquipmentSlotType.Backpack, backpackDefinition);
    }

    private void TrySeedSlot(EquipmentSlotType slotType, ItemDefinition item)
    {
        if (item == null || !CanEquip(slotType, item))
            return;

        InventorySlot slot = GetSlot(slotType);
        if (slot == null || !slot.IsEmpty)
            return;

        slot.TrySet(item, 1, ItemRuntimeData.CreateFor(item));
    }

    private void EnsureEquipmentSlots()
    {
        if (equippedSlots == null)
            equippedSlots = new List<CorpseEquipmentEntry>();

        EnsureEquipmentEntry(EquipmentSlotType.HeadArmor);
        EnsureEquipmentEntry(EquipmentSlotType.ChestArmor);
        EnsureEquipmentEntry(EquipmentSlotType.Backpack);
        EnsureEquipmentEntry(EquipmentSlotType.SecondaryWeapon);
        EnsureEquipmentEntry(EquipmentSlotType.PrimaryWeapon);
        equippedSlots.Sort((left, right) => left.slotType.CompareTo(right.slotType));
    }

    private void EnsureEquipmentEntry(EquipmentSlotType slotType)
    {
        CorpseEquipmentEntry entry = GetEntry(slotType);
        if (entry == null)
        {
            equippedSlots.Add(new CorpseEquipmentEntry(slotType));
            return;
        }

        if (entry.slot == null)
            entry.slot = new InventorySlot();
    }

    private void EnsurePocket()
    {
        if (pocketContainer == null)
            pocketContainer = new GridContainerState();

        pocketContainer.Configure(GridContainerKind.CorpsePocket, 1, 4);
    }

    private CorpseEquipmentEntry GetEntry(EquipmentSlotType slotType)
    {
        for (int i = 0; i < equippedSlots.Count; i++)
        {
            CorpseEquipmentEntry entry = equippedSlots[i];
            if (entry != null && entry.slotType == slotType)
                return entry;
        }

        return null;
    }
}
