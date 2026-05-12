using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SearchableContainerSetupTool
{
    private const string TargetObjectName = "SM_Prop_EmergencyDrop_Crate_01 (1)";
    private const string LargeCrateObjectName = "SM_Prop_Crate_01";

    [MenuItem("Tools/NoSafeExit/Apply Test Crate Contents")]
    public static void ApplyTestCrateContents()
    {
        SearchableContainer container = FindTargetContainer();
        if (container == null)
        {
            Debug.LogError("Could not find SearchableContainer on " + TargetObjectName);
            return;
        }

        SerializedObject serializedObject = new SerializedObject(container);
        SerializedProperty initialItems = serializedObject.FindProperty("initialItems");
        SerializedProperty seeded = serializedObject.FindProperty("seeded");

        if (initialItems == null || seeded == null)
        {
            Debug.LogError("SearchableContainer serialized fields not found.");
            return;
        }

        initialItems.ClearArray();

        AddEntry(initialItems, "Assets/Data/Items/Weapons/Weapon_Glock.asset", 1);
        AddEntry(initialItems, "Assets/Data/Items/Ammo/Ammo_556x45mm.asset", 60);
        AddEntry(initialItems, "Assets/Data/Items/Ammo/Ammo_9x19mm.asset", 60);
        AddEntry(initialItems, "Assets/Data/Items/Debug/Debug_Medkit.asset", 2);
        AddEntry(initialItems, "Assets/Data/Items/Consumables/Consumable_Water.asset", 1);
        AddEntry(initialItems, "Assets/Data/Items/Consumables/Consumable_Food.asset", 1);

        seeded.boolValue = false;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(container);
        EditorSceneManager.MarkSceneDirty(container.gameObject.scene);
        EditorSceneManager.SaveScene(container.gameObject.scene);

        Debug.Log("Applied test crate contents to " + TargetObjectName);
    }

    [MenuItem("Tools/NoSafeExit/Setup Large Searchable Crate")]
    public static void SetupLargeSearchableCrate()
    {
        GameObject target = FindTargetObject(LargeCrateObjectName);
        if (target == null)
        {
            Debug.LogError("Could not find target object " + LargeCrateObjectName);
            return;
        }

        SearchableContainer container = target.GetComponent<SearchableContainer>();
        if (container == null)
            container = Undo.AddComponent<SearchableContainer>(target);

        SerializedObject serializedObject = new SerializedObject(container);
        SerializedProperty displayName = serializedObject.FindProperty("containerDisplayName");
        SerializedProperty rows = serializedObject.FindProperty("rows");
        SerializedProperty columns = serializedObject.FindProperty("columns");
        SerializedProperty interactionRadius = serializedObject.FindProperty("interactionRadius");
        SerializedProperty seedOnAwake = serializedObject.FindProperty("seedOnAwake");
        SerializedProperty clearExisting = serializedObject.FindProperty("clearExistingBeforeSeed");
        SerializedProperty initialItems = serializedObject.FindProperty("initialItems");
        SerializedProperty seeded = serializedObject.FindProperty("seeded");

        if (displayName == null || rows == null || columns == null || interactionRadius == null
            || seedOnAwake == null || clearExisting == null || initialItems == null || seeded == null)
        {
            Debug.LogError("SearchableContainer serialized fields not found.");
            return;
        }

        displayName.stringValue = "Large Supply Crate";
        rows.intValue = 7;
        columns.intValue = 7;
        interactionRadius.floatValue = 2.5f;
        seedOnAwake.boolValue = true;
        clearExisting.boolValue = true;
        initialItems.ClearArray();

        AddEntry(initialItems, "Assets/Data/Items/Weapons/Weapon_HK416.asset", 1);
        AddEntry(initialItems, "Assets/Data/Items/Weapons/Weapon_AK47.asset", 1);
        AddEntry(initialItems, "Assets/Data/Items/Weapons/Weapon_Glock.asset", 1);
        AddEntry(initialItems, "Assets/Data/Items/Ammo/Ammo_556x45mm.asset", 60);
        AddEntry(initialItems, "Assets/Data/Items/Ammo/Ammo_9x19mm.asset", 60);
        AddEntry(initialItems, "Assets/Data/Items/Debug/Debug_Medkit.asset", 2);
        AddEntry(initialItems, "Assets/Data/Items/Consumables/Consumable_Water.asset", 1);
        AddEntry(initialItems, "Assets/Data/Items/Consumables/Consumable_Food.asset", 1);

        seeded.boolValue = false;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        container.EnsureInitialized();
        EditorUtility.SetDirty(container);
        EditorSceneManager.MarkSceneDirty(container.gameObject.scene);
        EditorSceneManager.SaveScene(container.gameObject.scene);

        Debug.Log("Configured large searchable crate on " + LargeCrateObjectName);
    }

    private static void AddEntry(SerializedProperty initialItems, string assetPath, int quantity)
    {
        ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
        if (item == null)
            throw new System.InvalidOperationException("Missing item asset at " + assetPath);

        int index = initialItems.arraySize;
        initialItems.InsertArrayElementAtIndex(index);

        SerializedProperty entry = initialItems.GetArrayElementAtIndex(index);
        SerializedProperty itemProp = entry.FindPropertyRelative("item");
        SerializedProperty quantityProp = entry.FindPropertyRelative("quantity");

        itemProp.objectReferenceValue = item;
        quantityProp.intValue = Mathf.Max(1, quantity);
    }

    private static SearchableContainer FindTargetContainer()
    {
        GameObject target = FindTargetObject(TargetObjectName);
        return target != null ? target.GetComponent<SearchableContainer>() : null;
    }

    private static GameObject FindTargetObject(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return null;

        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindInChildren(roots[i].transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindInChildren(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root.gameObject;

        foreach (Transform child in root)
        {
            GameObject found = FindInChildren(child, objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
