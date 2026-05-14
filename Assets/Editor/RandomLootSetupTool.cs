using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RandomLootSetupTool
{
    private const string LootTableFolder = "Assets/Data/LootTables";
    private const string CommonTablePath = LootTableFolder + "/LootTable_CommonCrate.asset";
    private const string MilitaryTablePath = LootTableFolder + "/LootTable_MilitaryCrate.asset";
    private const string MedicalTablePath = LootTableFolder + "/LootTable_MedicalCrate.asset";

    private readonly struct EntrySpec
    {
        public readonly string assetPath;
        public readonly float weight;
        public readonly int minQuantity;
        public readonly int maxQuantity;

        public EntrySpec(string assetPath, float weight, int minQuantity = 1, int maxQuantity = 1)
        {
            this.assetPath = assetPath;
            this.weight = weight;
            this.minQuantity = minQuantity;
            this.maxQuantity = maxQuantity;
        }
    }

    [MenuItem("Tools/NoSafeExit/Setup Random Loot")]
    public static void SetupRandomLoot()
    {
        EnsureFolder(LootTableFolder);

        LootTable common = CreateOrResetTable(CommonTablePath, 2, 5, 18, CommonEntries());
        LootTable military = CreateOrResetTable(MilitaryTablePath, 2, 6, 20, MilitaryEntries());
        LootTable medical = CreateOrResetTable(MedicalTablePath, 2, 4, 16, MedicalEntries());

        int configuredContainers = ConfigureSceneContainers(common, military, medical);
        EnsureLootSpawnManager();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        Debug.Log($"RandomLootSetupTool: configured {configuredContainers} searchable container(s).");
    }

    private static LootTable CreateOrResetTable(
        string path,
        int minRolls,
        int maxRolls,
        int placementAttempts,
        IReadOnlyList<EntrySpec> specs)
    {
        LootTable table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<LootTable>();
            AssetDatabase.CreateAsset(table, path);
        }

        table.minRolls = minRolls;
        table.maxRolls = maxRolls;
        table.placementAttemptsPerRoll = placementAttempts;
        table.entries.Clear();

        for (int i = 0; i < specs.Count; i++)
        {
            EntrySpec spec = specs[i];
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(spec.assetPath);
            if (item == null)
            {
                Debug.LogWarning("RandomLootSetupTool: missing item asset " + spec.assetPath);
                continue;
            }

            table.entries.Add(new LootTable.Entry
            {
                item = item,
                weight = Mathf.Max(0f, spec.weight),
                minQuantity = Mathf.Max(1, spec.minQuantity),
                maxQuantity = Mathf.Max(spec.minQuantity, spec.maxQuantity),
                allowRotatedPlacement = false
            });
        }

        EditorUtility.SetDirty(table);
        return table;
    }

    private static int ConfigureSceneContainers(LootTable common, LootTable military, LootTable medical)
    {
        SearchableContainer[] containers = UnityEngine.Object.FindObjectsByType<SearchableContainer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int configured = 0;
        for (int i = 0; i < containers.Length; i++)
        {
            SearchableContainer container = containers[i];
            if (container == null)
                continue;

            LootTable table = SelectTableForContainer(container, common, military, medical);
            container.ConfigureRandomLoot(table, table != null, false, true);
            EditorUtility.SetDirty(container);
            configured++;
        }

        return configured;
    }

    private static LootTable SelectTableForContainer(SearchableContainer container, LootTable common, LootTable military, LootTable medical)
    {
        string name = (container.DisplayName + " " + container.name).ToLowerInvariant();
        if (name.Contains("medical") || name.Contains("med"))
            return medical;

        if (name.Contains("large") || name.Contains("military") || name.Contains("supply") || name.Contains("all items"))
            return military;

        return common;
    }

    private static void EnsureLootSpawnManager()
    {
        LootSpawnManager existing = UnityEngine.Object.FindFirstObjectByType<LootSpawnManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            EditorUtility.SetDirty(existing);
            return;
        }

        GameObject manager = new GameObject("LootSpawnManager");
        manager.AddComponent<LootSpawnManager>();
        Undo.RegisterCreatedObjectUndo(manager, "Create LootSpawnManager");
    }

    private static EntrySpec[] CommonEntries()
    {
        return new[]
        {
            new EntrySpec("Assets/Data/Items/Ammo/Ammo_556x45mm.asset", 8f, 15, 60),
            new EntrySpec("Assets/Data/Items/Ammo/Ammo_762x39mm.asset", 7f, 15, 60),
            new EntrySpec("Assets/Data/Items/Ammo/Ammo_9x19mm.asset", 10f, 15, 60),
            new EntrySpec("Assets/Data/Items/Debug/Debug_Medkit.asset", 3f, 1, 1),
            new EntrySpec("Assets/Data/Items/Medical/Medical_Bandage.asset", 8f, 1, 3),
            new EntrySpec("Assets/Data/Items/Consumables/Consumable_Water.asset", 8f, 1, 1),
            new EntrySpec("Assets/Data/Items/Consumables/Consumable_Food.asset", 8f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Currency_USDollars.asset", 10f, 100, 1200),
            new EntrySpec("Assets/Data/Items/Loot/Loot_LithiumBattery.asset", 6f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_Documents.asset", 6f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_Sunglasses.asset", 4f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_Tape.asset", 7f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_WetWipes.asset", 7f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_Toothpaste.asset", 5f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_ToothpasteSpoon.asset", 5f, 1, 1),
            new EntrySpec("Assets/Data/Items/Weapons/Weapon_Glock.asset", 2f, 1, 1),
            new EntrySpec("Assets/Data/Items/Armor/Armor_Helmet_LevelI.asset", 1.5f, 1, 1),
            new EntrySpec("Assets/Data/Items/Armor/Armor_Body_LevelI.asset", 1f, 1, 1),
            new EntrySpec("Assets/Data/Items/Containers/Container_Backpack_Basic.asset", 1.2f, 1, 1)
        };
    }

    private static EntrySpec[] MilitaryEntries()
    {
        List<EntrySpec> entries = new List<EntrySpec>(CommonEntries())
        {
            new EntrySpec("Assets/Data/Items/Weapons/Weapon_HK416.asset", 1.4f, 1, 1),
            new EntrySpec("Assets/Data/Items/Weapons/Weapon_AK47.asset", 2f, 1, 1),
            new EntrySpec("Assets/Data/Items/Weapons/Weapon_SVD.asset", 0.65f, 1, 1),
            new EntrySpec("Assets/Data/Items/Weapons/Weapon_Groza.asset", 0.45f, 1, 1),
            new EntrySpec("Assets/Data/Items/Weapons/Weapon_MK12.asset", 0.55f, 1, 1),
            new EntrySpec("Assets/Data/Items/Weapons/Weapon_MCX.asset", 1f, 1, 1),
            new EntrySpec("Assets/Data/Items/Armor/Armor_Helmet_Operator.asset", 1.2f, 1, 1),
            new EntrySpec("Assets/Data/Items/Armor/Armor_Helmet_LevelIII.asset", 0.35f, 1, 1),
            new EntrySpec("Assets/Data/Items/Armor/Armor_Body_LevelII.asset", 0.9f, 1, 1),
            new EntrySpec("Assets/Data/Items/Armor/Armor_Body_LevelIII.asset", 0.35f, 1, 1),
            new EntrySpec("Assets/Data/Items/Containers/Container_Backpack_4x4.asset", 0.8f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_GasCan.asset", 1.1f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_MortarShell.asset", 0.8f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_ThermalScope.asset", 0.25f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_NightVisionGoggles.asset", 0.35f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_MilitaryLaptop.asset", 0.25f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Loot_GoldenTeapot.asset", 0.35f, 1, 1)
        };

        return entries.ToArray();
    }

    private static EntrySpec[] MedicalEntries()
    {
        return new[]
        {
            new EntrySpec("Assets/Data/Items/Debug/Debug_Medkit.asset", 8f, 1, 2),
            new EntrySpec("Assets/Data/Items/Medical/Medical_Bandage.asset", 12f, 1, 3),
            new EntrySpec("Assets/Data/Items/Consumables/Consumable_Water.asset", 8f, 1, 2),
            new EntrySpec("Assets/Data/Items/Consumables/Consumable_Food.asset", 5f, 1, 2),
            new EntrySpec("Assets/Data/Items/Loot/Loot_WetWipes.asset", 5f, 1, 1),
            new EntrySpec("Assets/Data/Items/Loot/Currency_USDollars.asset", 2f, 50, 600)
        };
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(assetPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            throw new InvalidOperationException("Invalid folder path: " + assetPath);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
