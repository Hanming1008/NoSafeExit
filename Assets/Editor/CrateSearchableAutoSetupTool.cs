using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CrateSearchableAutoSetupTool
{
    private const string SmallCrateModelName = "SM_Prop_Crate_Plastic_01";
    private const string LargeCrateModelName = "SM_Prop_Crate_Plastic_04";
    private const string CommonLootTablePath = "Assets/Data/LootTables/LootTable_CommonCrate.asset";
    private const string MilitaryLootTablePath = "Assets/Data/LootTables/LootTable_MilitaryCrate.asset";

    [MenuItem("Tools/NoSafeExit/Setup Searchable Plastic Crates")]
    public static void SetupSearchablePlasticCrates()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("CrateSearchableAutoSetupTool: no active scene.");
            return;
        }

        LootTable commonTable = AssetDatabase.LoadAssetAtPath<LootTable>(CommonLootTablePath);
        LootTable militaryTable = AssetDatabase.LoadAssetAtPath<LootTable>(MilitaryLootTablePath);
        MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Dictionary<GameObject, CrateSetupSpec> crateTargets = new Dictionary<GameObject, CrateSetupSpec>();

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.gameObject.scene != activeScene)
                continue;

            if (!TryGetCrateSpec(meshFilter, commonTable, militaryTable, out CrateSetupSpec spec))
                continue;

            GameObject target = meshFilter.gameObject;
            if (target == null || target.scene != activeScene)
                continue;

            crateTargets[target] = spec;
        }

        RemoveParentContainersThatOnlyRepresentChildCrates(activeScene, crateTargets);

        int configured = 0;
        foreach (KeyValuePair<GameObject, CrateSetupSpec> pair in crateTargets)
        {
            ConfigureCrate(pair.Key, pair.Value, configured + 1);
            configured++;
        }

        EnsureLootSpawnManager();

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"CrateSearchableAutoSetupTool: configured {configured} plastic crate(s). Small=4x4, Large=6x6.");
    }

    [MenuItem("Tools/NoSafeExit/Report Scene Crate Meshes")]
    public static void ReportSceneCrateMeshes()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("Scene crate mesh report");
        builder.AppendLine($"Active scene: {activeScene.name}");
        builder.AppendLine();

        int count = 0;
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.gameObject.scene != activeScene)
                continue;

            string objectName = meshFilter.gameObject.name;
            string meshName = meshFilter.sharedMesh.name;
            string meshAssetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
            if (!ContainsCrateToken(objectName, "crate")
                && !ContainsCrateToken(meshName, "crate")
                && !ContainsCrateToken(meshAssetPath, "crate"))
                continue;

            SearchableContainer container = meshFilter.GetComponentInParent<SearchableContainer>(true);
            count++;
            builder.AppendLine($"[{count:00}] {GetHierarchyPath(meshFilter.transform)}");
            builder.AppendLine($"     active: {meshFilter.gameObject.activeInHierarchy}");
            builder.AppendLine($"     mesh: {meshName}");
            builder.AppendLine($"     asset: {meshAssetPath}");
            builder.AppendLine($"     searchable: {(container != null ? container.gameObject.name : "no")}");
        }

        string reportPath = "Assets/Editor/CrateSearchableAutoSetupReport.txt";
        File.WriteAllText(reportPath, builder.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"CrateSearchableAutoSetupTool: wrote {count} crate mesh entries to {reportPath}.");
    }

    private static bool TryGetCrateSpec(MeshFilter meshFilter, LootTable commonTable, LootTable militaryTable, out CrateSetupSpec spec)
    {
        spec = default;
        if (meshFilter == null)
            return false;

        GameObject gameObject = meshFilter.gameObject;
        string objectName = gameObject.name;
        string meshName = string.Empty;
        string meshAssetPath = string.Empty;
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            meshName = meshFilter.sharedMesh.name;
            meshAssetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
        }

        if (ContainsCrateToken(objectName, "lid")
            || ContainsCrateToken(meshName, "lid")
            || ContainsCrateToken(meshAssetPath, "lid"))
            return false;

        if (ContainsCrateToken(objectName, SmallCrateModelName)
            || ContainsCrateToken(meshName, SmallCrateModelName)
            || ContainsCrateToken(meshAssetPath, SmallCrateModelName))
        {
            spec = new CrateSetupSpec(
                "Small Supply Crate",
                4,
                4,
                2.2f,
                commonTable);
            return true;
        }

        if (ContainsCrateToken(objectName, LargeCrateModelName)
            || ContainsCrateToken(meshName, LargeCrateModelName)
            || ContainsCrateToken(meshAssetPath, LargeCrateModelName))
        {
            spec = new CrateSetupSpec(
                "Large Supply Crate",
                6,
                6,
                2.8f,
                militaryTable);
            return true;
        }

        return false;
    }

    private static void RemoveParentContainersThatOnlyRepresentChildCrates(Scene activeScene, Dictionary<GameObject, CrateSetupSpec> crateTargets)
    {
        SearchableContainer[] containers = Object.FindObjectsByType<SearchableContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < containers.Length; i++)
        {
            SearchableContainer container = containers[i];
            if (container == null || container.gameObject.scene != activeScene)
                continue;

            if (crateTargets.ContainsKey(container.gameObject))
                continue;

            if (!HasConfiguredCrateChild(container.transform, crateTargets))
                continue;

            Undo.DestroyObjectImmediate(container);
        }
    }

    private static bool HasConfiguredCrateChild(Transform root, Dictionary<GameObject, CrateSetupSpec> crateTargets)
    {
        if (root == null)
            return false;

        foreach (GameObject target in crateTargets.Keys)
        {
            if (target == null || target.transform == root)
                continue;

            if (target.transform.IsChildOf(root))
                return true;
        }

        return false;
    }

    private static bool ContainsCrateToken(string value, string token)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        Stack<string> parts = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", parts);
    }

    private static GameObject ResolveSearchableRoot(GameObject visualObject)
    {
        if (visualObject == null)
            return null;

        SearchableContainer existingContainer = visualObject.GetComponentInParent<SearchableContainer>(true);
        if (existingContainer != null)
            return existingContainer.gameObject;

        Transform parent = visualObject.transform.parent;
        if (parent != null && LooksLikeCrateRoot(parent))
            return parent.gameObject;

        return visualObject;
    }

    private static bool LooksLikeCrateRoot(Transform transform)
    {
        if (transform == null)
            return false;

        if (transform.parent == null)
            return false;

        string name = transform.name.ToLowerInvariant();
        if (name.StartsWith("_map") || name.Contains("runtime") || name.Contains("environment"))
            return false;

        return transform.GetComponent<MeshFilter>() != null
            || transform.GetComponent<MeshCollider>() != null
            || name.Contains("crate")
            || name.Contains("prop");
    }

    private static void ConfigureCrate(GameObject target, CrateSetupSpec spec, int index)
    {
        SearchableContainer container = target.GetComponent<SearchableContainer>();
        if (container == null)
            container = Undo.AddComponent<SearchableContainer>(target);

        container.ContainerState.Clear();
        container.SetDimensions(spec.rows, spec.columns);
        container.SetDisplayName(spec.displayName + " " + index.ToString("00"));
        container.ConfigureRandomLoot(spec.lootTable, spec.lootTable != null, false, true);
        container.EnsureInitialized();

        SerializedObject serializedObject = new SerializedObject(container);
        SetSerializedString(serializedObject, "containerDisplayName", spec.displayName + " " + index.ToString("00"));
        SetSerializedInt(serializedObject, "rows", spec.rows);
        SetSerializedInt(serializedObject, "columns", spec.columns);
        SetSerializedFloat(serializedObject, "interactionRadius", spec.interactionRadius);
        SetSerializedBool(serializedObject, "seedOnAwake", false);
        SetSerializedBool(serializedObject, "clearExistingBeforeSeed", true);
        SetSerializedBool(serializedObject, "seeded", false);
        SerializedProperty initialItems = serializedObject.FindProperty("initialItems");
        if (initialItems != null)
            initialItems.ClearArray();

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(container);
        EditorUtility.SetDirty(target);
    }

    private static void EnsureLootSpawnManager()
    {
        LootSpawnManager existing = Object.FindFirstObjectByType<LootSpawnManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            EditorUtility.SetDirty(existing);
            return;
        }

        GameObject manager = new GameObject("LootSpawnManager");
        manager.AddComponent<LootSpawnManager>();
        Undo.RegisterCreatedObjectUndo(manager, "Create LootSpawnManager");
    }

    private static void SetSerializedString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void SetSerializedInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.intValue = value;
    }

    private static void SetSerializedFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetSerializedBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private readonly struct CrateSetupSpec
    {
        public readonly string displayName;
        public readonly int rows;
        public readonly int columns;
        public readonly float interactionRadius;
        public readonly LootTable lootTable;

        public CrateSetupSpec(string displayName, int rows, int columns, float interactionRadius, LootTable lootTable)
        {
            this.displayName = displayName;
            this.rows = rows;
            this.columns = columns;
            this.interactionRadius = interactionRadius;
            this.lootTable = lootTable;
        }
    }
}
