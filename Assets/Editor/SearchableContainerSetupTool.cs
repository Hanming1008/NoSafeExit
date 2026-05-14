using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SearchableContainerSetupTool
{
    private const string TargetObjectName = "SM_Prop_EmergencyDrop_Crate_01 (1)";
    private const string LargeCrateObjectName = "SM_Prop_Crate_01";
    private const string AllItemsCratePrefix = "All Items Crate";
    private const int AllItemsCrateRows = 7;
    private const int AllItemsCrateColumns = 7;

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

    [MenuItem("Tools/NoSafeExit/Apply Armor Test Crate Contents")]
    public static void ApplyArmorTestCrateContents()
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

        displayName.stringValue = "Armor Test Crate";
        rows.intValue = 7;
        columns.intValue = 7;
        interactionRadius.floatValue = 2.5f;
        seedOnAwake.boolValue = true;
        clearExisting.boolValue = true;
        initialItems.ClearArray();

        AddEntry(initialItems, "Assets/Data/Items/Armor/Armor_Body_LevelI.asset", 1);
        AddEntry(initialItems, "Assets/Data/Items/Armor/Armor_Body_LevelII.asset", 1);
        AddEntry(initialItems, "Assets/Data/Items/Armor/Armor_Body_LevelIII.asset", 1);

        seeded.boolValue = false;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        container.EnsureInitialized();
        EditorUtility.SetDirty(container);
        EditorSceneManager.MarkSceneDirty(container.gameObject.scene);
        EditorSceneManager.SaveScene(container.gameObject.scene);

        Debug.Log("Applied armor test crate contents to " + LargeCrateObjectName);
    }

    [MenuItem("Tools/NoSafeExit/Populate All Items Around Player")]
    public static void PopulateAllItemsAroundPlayer()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("No active scene found.");
            return;
        }

        Transform player = FindPlayerTransform();
        if (player == null)
        {
            Debug.LogError("Could not find player by PlayerInventory component or Player tag.");
            return;
        }

        GameObject template = FindTargetObject(LargeCrateObjectName);
        if (template == null)
        {
            SearchableContainer fallbackContainer = Object.FindFirstObjectByType<SearchableContainer>(FindObjectsInactive.Include);
            template = fallbackContainer != null ? fallbackContainer.gameObject : null;
        }

        if (template == null)
        {
            Debug.LogError("Could not find a searchable crate template.");
            return;
        }

        RemoveGeneratedAllItemsCrates(activeScene);
        int removedPickups = RemoveNearbyLooseEquipmentPickups(player.position, 8f);

        List<SearchableContainer> containers = GetBaseSearchableContainers(player.position);
        List<ItemDefinition> items = LoadNonDebugItemDefinitions();
        items.Sort(CompareItemsForPacking);

        int itemIndex = 0;
        int containerIndex = 0;
        while (itemIndex < items.Count)
        {
            SearchableContainer container = EnsureAllItemsContainer(containers, template, player, containerIndex);
            ConfigureAllItemsContainer(container, containerIndex + 1);

            int placedInContainer = FillContainer(container, items, itemIndex, out int nextItemIndex);
            if (placedInContainer <= 0)
            {
                Debug.LogError($"Failed to place item '{items[itemIndex].displayName}' into a {AllItemsCrateColumns}x{AllItemsCrateRows} crate. Check its inventory size.");
                break;
            }

            itemIndex = nextItemIndex;
            containerIndex++;
        }

        for (int i = 0; i < containers.Count; i++)
        {
            PositionAllItemsContainer(containers[i], player, i);
            EditorUtility.SetDirty(containers[i]);
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"Populated {itemIndex}/{items.Count} item definitions into {containerIndex} crate(s). Removed {removedPickups} nearby loose helmet/backpack pickup(s).");
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

    private static Transform FindPlayerTransform()
    {
        PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (inventory != null)
            return inventory.transform;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        return taggedPlayer != null ? taggedPlayer.transform : null;
    }

    private static List<ItemDefinition> LoadNonDebugItemDefinitions()
    {
        List<ItemDefinition> items = new List<ItemDefinition>();
        string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Data/Items" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(path) || path.Contains("/Debug/"))
                continue;

            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (ShouldIncludeInAllItemsCrate(item) && !items.Contains(item))
                items.Add(item);
        }

        return items;
    }

    private static bool ShouldIncludeInAllItemsCrate(ItemDefinition item)
    {
        if (item == null)
            return false;

        // Armor-provided rig containers are implementation details. The actual lootable items
        // are the ArmorItemDefinition assets that own damage reduction, durability, and weight.
        if (item is ContainerItemDefinition container && container.containerKind == GridContainerKind.Rig)
            return false;

        return true;
    }

    private static int CompareItemsForPacking(ItemDefinition a, ItemDefinition b)
    {
        int areaCompare = GetItemArea(b).CompareTo(GetItemArea(a));
        if (areaCompare != 0)
            return areaCompare;

        int typeCompare = a.Type.CompareTo(b.Type);
        if (typeCompare != 0)
            return typeCompare;

        return string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase);
    }

    private static int GetItemArea(ItemDefinition item)
    {
        if (item == null)
            return 0;

        return Mathf.Max(1, item.inventoryRows) * Mathf.Max(1, item.inventoryColumns);
    }

    private static List<SearchableContainer> GetBaseSearchableContainers(Vector3 playerPosition)
    {
        SearchableContainer[] foundContainers = Object.FindObjectsByType<SearchableContainer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<SearchableContainer> containers = new List<SearchableContainer>();
        for (int i = 0; i < foundContainers.Length; i++)
        {
            SearchableContainer container = foundContainers[i];
            if (container == null || container.name.StartsWith(AllItemsCratePrefix))
                continue;

            containers.Add(container);
        }

        containers.Sort((a, b) =>
            Vector3.SqrMagnitude(a.transform.position - playerPosition)
                .CompareTo(Vector3.SqrMagnitude(b.transform.position - playerPosition)));
        return containers;
    }

    private static SearchableContainer EnsureAllItemsContainer(
        List<SearchableContainer> containers,
        GameObject template,
        Transform player,
        int index)
    {
        if (index < containers.Count && containers[index] != null)
            return containers[index];

        Transform parent = template.transform.parent;
        GameObject crate = Object.Instantiate(template, parent);
        crate.name = $"{AllItemsCratePrefix} {index + 1:00}";
        PositionAllItemsContainer(crate.transform, player, index);

        SearchableContainer container = crate.GetComponent<SearchableContainer>();
        if (container == null)
            container = crate.AddComponent<SearchableContainer>();

        containers.Add(container);
        return container;
    }

    private static void ConfigureAllItemsContainer(SearchableContainer container, int displayIndex)
    {
        if (container == null)
            return;

        container.SetDisplayName($"{AllItemsCratePrefix} {displayIndex:00}");
        container.SetDimensions(AllItemsCrateRows, AllItemsCrateColumns);
        container.EnsureInitialized();
        container.ContainerState.Clear();

        SerializedObject serializedObject = new SerializedObject(container);
        SetSerializedString(serializedObject, "containerDisplayName", $"{AllItemsCratePrefix} {displayIndex:00}");
        SetSerializedInt(serializedObject, "rows", AllItemsCrateRows);
        SetSerializedInt(serializedObject, "columns", AllItemsCrateColumns);
        SetSerializedFloat(serializedObject, "interactionRadius", 2.8f);
        SetSerializedBool(serializedObject, "seedOnAwake", false);
        SetSerializedBool(serializedObject, "clearExistingBeforeSeed", false);
        SetSerializedBool(serializedObject, "seeded", true);

        SerializedProperty initialItems = serializedObject.FindProperty("initialItems");
        if (initialItems != null)
            initialItems.ClearArray();

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        container.EnsureInitialized();
    }

    private static int FillContainer(
        SearchableContainer container,
        List<ItemDefinition> items,
        int startIndex,
        out int nextItemIndex)
    {
        nextItemIndex = startIndex;
        if (container == null || items == null)
            return 0;

        int placed = 0;
        for (int i = startIndex; i < items.Count; i++)
        {
            ItemDefinition item = items[i];
            int quantity = GetTestQuantity(item);
            if (!TryPlaceItem(container.ContainerState, item, quantity))
                break;

            placed++;
            nextItemIndex = i + 1;
        }

        return placed;
    }

    private static bool TryPlaceItem(GridContainerState container, ItemDefinition item, int quantity)
    {
        if (container == null || item == null)
            return false;

        ItemRuntimeData runtimeData = item.canStack ? null : ItemRuntimeData.CreateFor(item);
        if (container.TryPlaceNewItem(item, quantity, runtimeData, out _, false))
            return true;

        runtimeData = item.canStack ? null : ItemRuntimeData.CreateFor(item);
        return GridItemPlacement.CanRotate(item)
            && container.TryPlaceNewItem(item, quantity, runtimeData, out _, true);
    }

    private static int GetTestQuantity(ItemDefinition item)
    {
        if (item == null)
            return 1;

        return item.canStack ? Mathf.Max(1, item.maxStackSize) : 1;
    }

    private static void PositionAllItemsContainer(SearchableContainer container, Transform player, int index)
    {
        if (container != null)
            PositionAllItemsContainer(container.transform, player, index);
    }

    private static void PositionAllItemsContainer(Transform crate, Transform player, int index)
    {
        if (crate == null || player == null)
            return;

        const float spacing = 2.45f;
        int column = index % 3;
        int row = index / 3;
        Vector3 origin = player.position + new Vector3(-2.6f, 0f, -2.5f);
        Vector3 targetPosition = origin + new Vector3(column * spacing, 0f, -row * spacing);
        targetPosition.y = player.position.y - 0.06f;
        crate.position = targetPosition;
        crate.rotation = Quaternion.Euler(0f, 165f + (index * 11f), 0f);
    }

    private static int RemoveNearbyLooseEquipmentPickups(Vector3 playerPosition, float radius)
    {
        int removed = 0;
        float radiusSqr = radius * radius;
        WorldItemPickup[] pickups = Object.FindObjectsByType<WorldItemPickup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = pickups.Length - 1; i >= 0; i--)
        {
            WorldItemPickup pickup = pickups[i];
            if (pickup == null || pickup.ItemDefinition == null)
                continue;

            if ((pickup.transform.position - playerPosition).sqrMagnitude > radiusSqr)
                continue;

            bool shouldRemove = pickup.ItemDefinition is ArmorItemDefinition armor && armor.armorSlot == ArmorSlotType.Head;
            shouldRemove |= pickup.ItemDefinition is ContainerItemDefinition container
                && container.containerKind == GridContainerKind.Backpack;

            if (!shouldRemove)
                continue;

            Object.DestroyImmediate(pickup.gameObject);
            removed++;
        }

        return removed;
    }

    private static void RemoveGeneratedAllItemsCrates(Scene activeScene)
    {
        SearchableContainer[] containers = Object.FindObjectsByType<SearchableContainer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = containers.Length - 1; i >= 0; i--)
        {
            SearchableContainer container = containers[i];
            if (container == null || !container.name.StartsWith(AllItemsCratePrefix))
                continue;

            Object.DestroyImmediate(container.gameObject);
        }
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
