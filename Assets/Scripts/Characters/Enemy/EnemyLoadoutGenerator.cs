using System;
using System.Collections.Generic;
using JUTPS;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum EnemyArchetype
{
    Militia,
    Mercenary
}

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyCorpseLoot))]
public class EnemyLoadoutGenerator : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] private EnemyArchetype archetype = EnemyArchetype.Militia;
    [SerializeField] private bool generateOnAwake = true;
    [SerializeField] private int seedOffset;

    [Header("References")]
    [SerializeField] private EnemyCorpseLoot corpseLoot;
    [SerializeField] private EnemyWeaponLoadout weaponLoadout;
    [SerializeField] private EnemyEquipmentVisuals equipmentVisuals;

    [Header("Weapons")]
    [SerializeField] private WeaponItemDefinition glock;
    [SerializeField] private WeaponItemDefinition ak47;
    [SerializeField] private WeaponItemDefinition svd;
    [SerializeField] private WeaponItemDefinition hk416;
    [SerializeField] private WeaponItemDefinition mcx;

    [Header("Ammo")]
    [SerializeField] private AmmoItemDefinition ammo556;
    [SerializeField] private AmmoItemDefinition ammo762;
    [SerializeField] private AmmoItemDefinition ammo9mm;

    [Header("Armor")]
    [SerializeField] private ArmorItemDefinition helmetLevelI;
    [SerializeField] private ArmorItemDefinition helmetLevelII;
    [SerializeField] private ArmorItemDefinition helmetLevelIII;
    [SerializeField] private ArmorItemDefinition bodyArmorLevelI;
    [SerializeField] private ArmorItemDefinition bodyArmorLevelII;
    [SerializeField] private ArmorItemDefinition bodyArmorLevelIII;

    [Header("Backpacks")]
    [SerializeField] private ContainerItemDefinition basicBackpack;
    [SerializeField] private ContainerItemDefinition largeBackpack;

    [Header("Misc Loot")]
    [SerializeField] private ItemDefinition[] blueMiscLoot;
    [SerializeField] private ItemDefinition[] goldMiscLoot;
    [SerializeField] private ItemDefinition[] redMiscLoot;

    [Header("Generated Ammo")]
    [SerializeField, Min(0)] private int militiaReserveMagazineMin = 1;
    [SerializeField, Min(0)] private int militiaReserveMagazineMax = 3;
    [SerializeField, Min(0)] private int mercenaryReserveMagazineMin = 2;
    [SerializeField, Min(0)] private int mercenaryReserveMagazineMax = 4;

    private JUHealth health;
    private AmmoItemDefinition generatedPrimaryAmmo;
    private int generatedPrimaryReserveRounds;

    public EnemyArchetype Archetype => archetype;

    public void SetArchetype(EnemyArchetype newArchetype, bool regenerate = true)
    {
        archetype = newArchetype;
        if (regenerate)
            Generate();
    }

    private void Awake()
    {
        ResolveReferences();
        AutoConfigureMissingReferences();

        if (generateOnAwake)
            Generate();
    }

    private void OnValidate()
    {
        ResolveReferences();
        AutoConfigureMissingReferences();

        militiaReserveMagazineMax = Mathf.Max(militiaReserveMagazineMin, militiaReserveMagazineMax);
        mercenaryReserveMagazineMax = Mathf.Max(mercenaryReserveMagazineMin, mercenaryReserveMagazineMax);
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

    [ContextMenu("Generate Enemy Loadout")]
    public void Generate()
    {
        ResolveReferences();
        AutoConfigureMissingReferences();

        if (corpseLoot == null)
            return;

        System.Random random = new System.Random(Environment.TickCount ^ GetInstanceID() ^ seedOffset);

        corpseLoot.EnsureInitialized();
        corpseLoot.ClearAllLoot();
        corpseLoot.SetIdentity(GetEnemyTypeName(), GetEnemyTypeName() + " Corpse");

        WeaponItemDefinition primary = RollPrimaryWeapon(random);
        WeaponItemDefinition secondary = RollSecondaryWeapon(random);
        ArmorItemDefinition headArmor = RollHeadArmor(random);
        ArmorItemDefinition chestArmor = RollChestArmor(random);
        ContainerItemDefinition backpack = RollBackpack(random);

        TryEquip(EquipmentSlotType.PrimaryWeapon, primary);
        TryEquip(EquipmentSlotType.SecondaryWeapon, secondary);
        TryEquip(EquipmentSlotType.HeadArmor, headArmor);
        TryEquip(EquipmentSlotType.ChestArmor, chestArmor);
        TryEquip(EquipmentSlotType.Backpack, backpack);

        WeaponItemDefinition activeWeapon = primary != null ? primary : secondary;
        int reserveMagazineCount = GetReserveMagazineCount(random);
        if (weaponLoadout != null)
            weaponLoadout.Configure(activeWeapon, reserveMagazineCount, true);

        generatedPrimaryAmmo = null;
        generatedPrimaryReserveRounds = 0;
        AddReserveAmmoForWeapon(primary, reserveMagazineCount, true);
        AddReserveAmmoForWeapon(secondary, Mathf.Max(1, reserveMagazineCount - 1), false);
        AddMiscLoot(random);

        if (equipmentVisuals != null)
            equipmentVisuals.ForceRefreshNow();
    }

    private void OnDeath()
    {
        SyncPrimaryReserveAmmoWithWeapon();
    }

    private void ResolveReferences()
    {
        if (corpseLoot == null)
            corpseLoot = GetComponent<EnemyCorpseLoot>();

        if (weaponLoadout == null)
            weaponLoadout = GetComponentInChildren<EnemyWeaponLoadout>(true);

        if (equipmentVisuals == null)
            equipmentVisuals = GetComponent<EnemyEquipmentVisuals>();

        if (health == null)
            health = GetComponent<JUHealth>();
    }

    private bool TryEquip(EquipmentSlotType slotType, ItemDefinition item)
    {
        if (item == null || corpseLoot == null)
            return false;

        return corpseLoot.TrySetSlot(slotType, item, 1, ItemRuntimeData.CreateFor(item));
    }

    private WeaponItemDefinition RollPrimaryWeapon(System.Random random)
    {
        if (archetype == EnemyArchetype.Mercenary)
        {
            double value = random.NextDouble();
            if (value < 0.50d)
                return mcx;

            return hk416;
        }

        double militiaValue = random.NextDouble();
        if (militiaValue < 0.70d)
            return ak47;

        return svd;
    }

    private WeaponItemDefinition RollSecondaryWeapon(System.Random random)
    {
        double chance = archetype == EnemyArchetype.Mercenary ? 0.70d : 0.45d;
        return random.NextDouble() < chance ? glock : null;
    }

    private ArmorItemDefinition RollHeadArmor(System.Random random)
    {
        double value = random.NextDouble();
        if (archetype == EnemyArchetype.Mercenary)
        {
            if (value < 0.10d)
                return null;
            if (value < 0.25d)
                return helmetLevelI;
            if (value < 0.85d)
                return helmetLevelII;
            return helmetLevelIII;
        }

        if (value < 0.45d)
            return null;
        if (value < 0.85d)
            return helmetLevelI;
        return helmetLevelII;
    }

    private ArmorItemDefinition RollChestArmor(System.Random random)
    {
        double value = random.NextDouble();
        if (archetype == EnemyArchetype.Mercenary)
        {
            if (value < 0.10d)
                return null;
            if (value < 0.20d)
                return bodyArmorLevelI;
            if (value < 0.85d)
                return bodyArmorLevelII;
            return bodyArmorLevelIII;
        }

        if (value < 0.25d)
            return null;
        if (value < 0.85d)
            return bodyArmorLevelI;
        return bodyArmorLevelII;
    }

    private ContainerItemDefinition RollBackpack(System.Random random)
    {
        double value = random.NextDouble();
        if (archetype == EnemyArchetype.Mercenary)
        {
            if (value < 0.50d)
                return null;
            if (value < 0.80d)
                return basicBackpack;
            return largeBackpack;
        }

        return value < 0.25d ? basicBackpack : null;
    }

    private int GetReserveMagazineCount(System.Random random)
    {
        int min = archetype == EnemyArchetype.Mercenary ? mercenaryReserveMagazineMin : militiaReserveMagazineMin;
        int max = archetype == EnemyArchetype.Mercenary ? mercenaryReserveMagazineMax : militiaReserveMagazineMax;
        return random.Next(Mathf.Max(0, min), Mathf.Max(min, max) + 1);
    }

    private void AddReserveAmmoForWeapon(WeaponItemDefinition weapon, int magazineCount, bool trackAsPrimary)
    {
        if (weapon == null || !weapon.usesAmmo || weapon.compatibleAmmo == null || magazineCount <= 0)
            return;

        int reserveRounds = Mathf.Max(0, weapon.magazineSize * magazineCount);
        if (reserveRounds <= 0)
            return;

        TryPlaceQuantityInEnemyContainers(weapon.compatibleAmmo, reserveRounds, PreferRigFirstContainers());

        if (trackAsPrimary)
        {
            generatedPrimaryAmmo = weapon.compatibleAmmo;
            generatedPrimaryReserveRounds = reserveRounds;
        }
    }

    private void AddMiscLoot(System.Random random)
    {
        int lootCount = archetype == EnemyArchetype.Mercenary
            ? random.Next(1, 4)
            : random.Next(0, 3);

        for (int i = 0; i < lootCount; i++)
        {
            ItemDefinition item = RollMiscLoot(random);
            if (item == null)
                continue;

            TryPlaceQuantityInEnemyContainers(item, GetLootQuantity(item, random), RandomizedContainers(random));
        }
    }

    private ItemDefinition RollMiscLoot(System.Random random)
    {
        double redChance = archetype == EnemyArchetype.Mercenary ? 0.03d : 0d;
        double goldChance = archetype == EnemyArchetype.Mercenary ? 0.15d : 0.06d;
        double value = random.NextDouble();

        if (value < redChance)
            return PickRandom(redMiscLoot, random);
        if (value < redChance + goldChance)
            return PickRandom(goldMiscLoot, random);

        return PickRandom(blueMiscLoot, random);
    }

    private static int GetLootQuantity(ItemDefinition item, System.Random random)
    {
        if (item == null || !item.canStack)
            return 1;

        if (item.Type == ItemType.Currency)
            return random.Next(50, Mathf.Min(item.maxStackSize, 500) + 1);

        return random.Next(1, Mathf.Min(item.maxStackSize, 3) + 1);
    }

    private GridContainerState[] PreferRigFirstContainers()
    {
        return new[]
        {
            corpseLoot.GetEquippedContainer(EquipmentSlotType.ChestArmor),
            corpseLoot.GetEquippedContainer(EquipmentSlotType.Backpack),
            corpseLoot.PocketContainer
        };
    }

    private GridContainerState[] RandomizedContainers(System.Random random)
    {
        List<GridContainerState> containers = new List<GridContainerState>
        {
            corpseLoot.GetEquippedContainer(EquipmentSlotType.ChestArmor),
            corpseLoot.GetEquippedContainer(EquipmentSlotType.Backpack),
            corpseLoot.PocketContainer
        };

        for (int i = 0; i < containers.Count; i++)
        {
            int swapIndex = random.Next(i, containers.Count);
            GridContainerState current = containers[i];
            containers[i] = containers[swapIndex];
            containers[swapIndex] = current;
        }

        return containers.ToArray();
    }

    private static bool TryPlaceQuantityInEnemyContainers(ItemDefinition item, int quantity, GridContainerState[] containers)
    {
        if (item == null || quantity <= 0 || containers == null)
            return false;

        int remaining = quantity;
        while (remaining > 0)
        {
            int stackQuantity = item.canStack
                ? Mathf.Min(remaining, Mathf.Max(1, item.maxStackSize))
                : 1;

            ItemRuntimeData runtimeData = item.canStack ? null : ItemRuntimeData.CreateFor(item);
            bool placed = false;
            for (int i = 0; i < containers.Length; i++)
            {
                GridContainerState container = containers[i];
                if (container == null)
                    continue;

                if (container.TryPlaceNewItem(item, stackQuantity, runtimeData, out _))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
                return remaining != quantity;

            remaining -= stackQuantity;
        }

        return true;
    }

    private void SyncPrimaryReserveAmmoWithWeapon()
    {
        if (generatedPrimaryAmmo == null || weaponLoadout == null || weaponLoadout.Weapon == null)
            return;

        int remainingReserve = Mathf.Clamp(weaponLoadout.Weapon.TotalBullets, 0, generatedPrimaryReserveRounds);
        RemoveAmmoFromCarriedContainers(generatedPrimaryAmmo);

        if (remainingReserve > 0)
            TryPlaceQuantityInEnemyContainers(generatedPrimaryAmmo, remainingReserve, PreferRigFirstContainers());
    }

    private void RemoveAmmoFromCarriedContainers(AmmoItemDefinition ammo)
    {
        if (ammo == null)
            return;

        GridContainerState rig = corpseLoot.GetEquippedContainer(EquipmentSlotType.ChestArmor);
        GridContainerState backpack = corpseLoot.GetEquippedContainer(EquipmentSlotType.Backpack);
        rig?.RemoveItemIncludingNested(ammo, int.MaxValue);
        backpack?.RemoveItemIncludingNested(ammo, int.MaxValue);
        corpseLoot.PocketContainer?.RemoveItemIncludingNested(ammo, int.MaxValue);
    }

    private string GetEnemyTypeName()
    {
        return archetype == EnemyArchetype.Mercenary ? "Mercenary" : "Militia";
    }

    private static T PickRandom<T>(T[] items, System.Random random) where T : class
    {
        if (items == null || items.Length == 0)
            return null;

        List<T> validItems = new List<T>();
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
                validItems.Add(items[i]);
        }

        return validItems.Count > 0 ? validItems[random.Next(0, validItems.Count)] : null;
    }

    private void AutoConfigureMissingReferences()
    {
#if UNITY_EDITOR
        glock ??= LoadItem<WeaponItemDefinition>("Assets/Data/Items/Weapons/Weapon_Glock.asset");
        ak47 ??= LoadItem<WeaponItemDefinition>("Assets/Data/Items/Weapons/Weapon_AK47.asset");
        svd ??= LoadItem<WeaponItemDefinition>("Assets/Data/Items/Weapons/Weapon_SVD.asset");
        hk416 ??= LoadItem<WeaponItemDefinition>("Assets/Data/Items/Weapons/Weapon_HK416.asset");
        mcx ??= LoadItem<WeaponItemDefinition>("Assets/Data/Items/Weapons/Weapon_MCX.asset");

        ammo556 ??= LoadItem<AmmoItemDefinition>("Assets/Data/Items/Ammo/Ammo_556x45mm.asset");
        ammo762 ??= LoadItem<AmmoItemDefinition>("Assets/Data/Items/Ammo/Ammo_762x39mm.asset");
        ammo9mm ??= LoadItem<AmmoItemDefinition>("Assets/Data/Items/Ammo/Ammo_9x19mm.asset");

        helmetLevelI ??= LoadItem<ArmorItemDefinition>("Assets/Data/Items/Armor/Armor_Helmet_LevelI.asset");
        helmetLevelII ??= LoadItem<ArmorItemDefinition>("Assets/Data/Items/Armor/Armor_Helmet_Operator.asset");
        helmetLevelIII ??= LoadItem<ArmorItemDefinition>("Assets/Data/Items/Armor/Armor_Helmet_LevelIII.asset");
        bodyArmorLevelI ??= LoadItem<ArmorItemDefinition>("Assets/Data/Items/Armor/Armor_Body_LevelI.asset");
        bodyArmorLevelII ??= LoadItem<ArmorItemDefinition>("Assets/Data/Items/Armor/Armor_Body_LevelII.asset");
        bodyArmorLevelIII ??= LoadItem<ArmorItemDefinition>("Assets/Data/Items/Armor/Armor_Body_LevelIII.asset");

        basicBackpack ??= LoadItem<ContainerItemDefinition>("Assets/Data/Items/Containers/Container_Backpack_Basic.asset");
        largeBackpack ??= LoadItem<ContainerItemDefinition>("Assets/Data/Items/Containers/Container_Backpack_4x4.asset");

        if (blueMiscLoot == null || blueMiscLoot.Length == 0)
            blueMiscLoot = FindLootByTier(ItemValueTier.Blue);
        if (goldMiscLoot == null || goldMiscLoot.Length == 0)
            goldMiscLoot = FindLootByTier(ItemValueTier.Gold);
        if (redMiscLoot == null || redMiscLoot.Length == 0)
            redMiscLoot = FindLootByTier(ItemValueTier.Red);
#endif
    }

#if UNITY_EDITOR
    private static T LoadItem<T>(string path) where T : ItemDefinition
    {
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    private static ItemDefinition[] FindLootByTier(ItemValueTier tier)
    {
        List<ItemDefinition> items = new List<ItemDefinition>();
        string[] guids = AssetDatabase.FindAssets("t:LootItemDefinition", new[] { "Assets/Data/Items/Loot" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            LootItemDefinition item = AssetDatabase.LoadAssetAtPath<LootItemDefinition>(path);
            if (item == null || item.isCurrency || item.valueTier != tier)
                continue;

            items.Add(item);
        }

        return items.ToArray();
    }
#endif
}
