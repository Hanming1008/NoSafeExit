using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterEquipmentVisuals : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerGridInventory gridInventory;

    [Header("Startup Loadout")]
    [SerializeField] private bool syncStartupSceneLoadout = true;
    [SerializeField] private ArmorItemDefinition startupHeadArmor;
    [SerializeField] private ArmorItemDefinition startupChestArmor;
    [SerializeField] private ContainerItemDefinition startupBackpack;
    [SerializeField] private ContainerItemDefinition startupRigContainer;

    [Header("Behavior")]
    [SerializeField] private bool hideManagedVisualsWhenUnequipped;
    [SerializeField] private bool disablePrototypeFallbacksOnStart;
    [SerializeField]
    private string[] managedHeadVisualObjectNames =
    {
        "SM_Chr_Attach_Helmet_01_Goggles_01",
        "SM_Chr_Attach_Helmet_01_Goggles_01_Glass",
        "SM_Chr_Attach_Helmet_02",
        "SM_Chr_Attach_NVG_03",
        "SM_Chr_Attach_Helmet_07",
        "SM_Chr_Attach_Helmet_09"
    };

    private readonly Dictionary<string, List<GameObject>> visualLookup = new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
    private string[] lastHeadEnabled = Array.Empty<string>();
    private string[] lastHeadHidden = Array.Empty<string>();
    private string[] lastChestEnabled = Array.Empty<string>();
    private string[] lastChestHidden = Array.Empty<string>();
    private string[] lastBackpackEnabled = Array.Empty<string>();
    private string[] lastBackpackHidden = Array.Empty<string>();
    private ArmorItemDefinition lastHeadArmor;
    private ArmorItemDefinition lastChestArmor;
    private ContainerItemDefinition lastBackpack;
    private bool rigDriven;
    private bool backpackDriven;

    void Awake()
    {
        ResolveReferences();
        if (equipment != null)
            equipment.EnsureEquipmentSlots();
        if (gridInventory != null)
            gridInventory.EnsureContainers();
        RebuildLookup();

        if (disablePrototypeFallbacksOnStart && gridInventory != null)
            gridInventory.SetPrototypeFallbacks(false, false);

        if (syncStartupSceneLoadout)
            ApplyStartupLoadoutFromActiveVisuals();

        Refresh(force: true);
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    void Update()
    {
        Refresh(force: false);
    }

    public void ForceRefreshNow()
    {
        Refresh(force: true);
    }

    public bool TryBuildWorldPickupVisual(ItemDefinition definition, Transform parent)
    {
        if (definition == null || parent == null)
            return false;

        string[] visualNames = GetEquippedVisualNames(definition);
        if (visualNames == null || visualNames.Length == 0)
            return false;

        bool createdAnyVisual = false;
        HashSet<int> clonedSourceIds = new HashSet<int>();
        for (int i = 0; i < visualNames.Length; i++)
        {
            string visualName = visualNames[i];
            if (string.IsNullOrWhiteSpace(visualName))
                continue;

            if (!visualLookup.TryGetValue(visualName, out List<GameObject> visualObjects) || visualObjects == null)
                continue;

            for (int objectIndex = 0; objectIndex < visualObjects.Count; objectIndex++)
            {
                GameObject source = visualObjects[objectIndex];
                if (source == null || !clonedSourceIds.Add(source.GetInstanceID()))
                    continue;

                GameObject clone = Instantiate(source, source.transform.position, source.transform.rotation);
                clone.name = source.name;
                clone.transform.SetParent(parent, true);
                SetLayerRecursive(clone, 0);
                SetActiveRecursive(clone, true);
                createdAnyVisual = true;
            }
        }

        if (createdAnyVisual)
            CenterClonedVisuals(parent);

        return createdAnyVisual;
    }

    private void ResolveReferences()
    {
        if (equipment == null)
            equipment = GetComponent<PlayerEquipment>();

        if (gridInventory == null)
            gridInventory = GetComponent<PlayerGridInventory>();
    }

    private void RebuildLookup()
    {
        visualLookup.Clear();
        RegisterRecursive(transform);
    }

    private void RegisterRecursive(Transform node)
    {
        if (node == null)
            return;

        if (!visualLookup.TryGetValue(node.name, out List<GameObject> objects))
        {
            objects = new List<GameObject>();
            visualLookup.Add(node.name, objects);
        }

        objects.Add(node.gameObject);

        for (int i = 0; i < node.childCount; i++)
            RegisterRecursive(node.GetChild(i));
    }

    private void Refresh(bool force)
    {
        if (equipment == null)
            return;

        InventorySlot headSlot = equipment.GetSlot(EquipmentSlotType.HeadArmor);
        InventorySlot chestSlot = equipment.GetSlot(EquipmentSlotType.ChestArmor);
        InventorySlot backpackSlot = equipment.GetSlot(EquipmentSlotType.Backpack);

        ArmorItemDefinition currentHeadArmor = headSlot?.Item as ArmorItemDefinition;
        ArmorItemDefinition currentChestArmor = chestSlot?.Item as ArmorItemDefinition;
        ContainerItemDefinition currentBackpack = backpackSlot?.Item as ContainerItemDefinition;

        SyncContainerState(currentChestArmor, chestSlot?.RuntimeData, currentBackpack, backpackSlot?.RuntimeData);

        if (force || currentHeadArmor != lastHeadArmor)
        {
            SetActiveForNames(managedHeadVisualObjectNames, false);
            ApplyVisualTransition(
                ref lastHeadEnabled,
                ref lastHeadHidden,
                currentHeadArmor != null ? currentHeadArmor.equippedVisualObjectNames : Array.Empty<string>(),
                currentHeadArmor != null ? currentHeadArmor.hiddenVisualObjectNames : Array.Empty<string>());
            lastHeadArmor = currentHeadArmor;
        }

        if (force || currentChestArmor != lastChestArmor)
        {
            ApplyVisualTransition(
                ref lastChestEnabled,
                ref lastChestHidden,
                currentChestArmor != null ? currentChestArmor.equippedVisualObjectNames : Array.Empty<string>(),
                currentChestArmor != null ? currentChestArmor.hiddenVisualObjectNames : Array.Empty<string>());

            lastChestArmor = currentChestArmor;
        }

        if (force || currentBackpack != lastBackpack)
        {
            ApplyVisualTransition(
                ref lastBackpackEnabled,
                ref lastBackpackHidden,
                currentBackpack != null ? currentBackpack.equippedVisualObjectNames : Array.Empty<string>(),
                currentBackpack != null ? currentBackpack.hiddenVisualObjectNames : Array.Empty<string>());

            lastBackpack = currentBackpack;
        }
    }

    private void SyncContainerState(
        ArmorItemDefinition currentChestArmor,
        ItemRuntimeData currentChestRuntime,
        ContainerItemDefinition currentBackpack,
        ItemRuntimeData currentBackpackRuntime)
    {
        if (gridInventory == null)
            return;

        if (disablePrototypeFallbacksOnStart
            && (gridInventory.UsePrototypeRigWhenUnassigned || gridInventory.UsePrototypeBackpackWhenUnassigned))
        {
            gridInventory.SetPrototypeFallbacks(false, false);
        }

        ContainerItemDefinition targetRig = currentChestArmor != null ? currentChestArmor.providedRigContainer : null;
        if (targetRig == null && currentChestArmor != null && startupChestArmor != null && currentChestArmor.itemId == startupChestArmor.itemId)
            targetRig = startupRigContainer;
        if (targetRig != null)
        {
            if (gridInventory.EquippedRig != targetRig || gridInventory.EquippedRigRuntime != currentChestRuntime)
                gridInventory.EquipRig(targetRig, currentChestRuntime);

            rigDriven = true;
        }
        else if (rigDriven || gridInventory.EquippedRig != null)
        {
            gridInventory.UnequipRig();
            rigDriven = false;
        }

        if (currentBackpack != null)
        {
            if (gridInventory.EquippedBackpack != currentBackpack || gridInventory.EquippedBackpackRuntime != currentBackpackRuntime)
                gridInventory.EquipBackpack(currentBackpack, currentBackpackRuntime);

            backpackDriven = true;
        }
        else if (backpackDriven || gridInventory.EquippedBackpack != null)
        {
            gridInventory.UnequipBackpack();
            backpackDriven = false;
        }
    }

    private void ApplyStartupLoadoutFromActiveVisuals()
    {
        if (equipment == null)
            return;

        TryAssignStartupArmor(EquipmentSlotType.HeadArmor, startupHeadArmor);
        TryAssignStartupArmor(EquipmentSlotType.ChestArmor, startupChestArmor);
        TryAssignStartupBackpack();
    }

    private void TryAssignStartupArmor(EquipmentSlotType slotType, ArmorItemDefinition armorDefinition)
    {
        if (armorDefinition == null)
            return;

        InventorySlot slot = equipment.GetSlot(slotType);
        if (slot != null && !slot.IsEmpty)
            return;

        if (!HasAnyActiveVisual(armorDefinition.equippedVisualObjectNames))
            return;

        equipment.TryAssignEquippedItem(slotType, armorDefinition, 1, true);
    }

    private void TryAssignStartupBackpack()
    {
        if (startupBackpack == null)
            return;

        InventorySlot slot = equipment.GetSlot(EquipmentSlotType.Backpack);
        if (slot != null && !slot.IsEmpty)
            return;

        if (!HasAnyActiveVisual(startupBackpack.equippedVisualObjectNames))
            return;

        equipment.TryAssignEquippedItem(EquipmentSlotType.Backpack, startupBackpack, 1, true);
    }

    private bool HasAnyActiveVisual(string[] names)
    {
        if (names == null)
            return false;

        for (int i = 0; i < names.Length; i++)
        {
            string visualName = names[i];
            if (string.IsNullOrWhiteSpace(visualName))
                continue;

            if (!visualLookup.TryGetValue(visualName, out List<GameObject> visualObjects) || visualObjects == null || visualObjects.Count == 0)
                continue;

            for (int objectIndex = 0; objectIndex < visualObjects.Count; objectIndex++)
            {
                GameObject visualObject = visualObjects[objectIndex];
                if (visualObject != null && visualObject.activeSelf)
                    return true;
            }
        }

        return false;
    }

    private void ApplyVisualTransition(
        ref string[] lastEnabledNames,
        ref string[] lastHiddenNames,
        string[] currentEnabledNames,
        string[] currentHiddenNames)
    {
        SetActiveForNames(lastHiddenNames, true);
        SetActiveForNames(lastEnabledNames, false);

        if (currentEnabledNames != null && currentEnabledNames.Length > 0)
            SetActiveForNames(currentEnabledNames, true);
        else if (!hideManagedVisualsWhenUnequipped)
            SetActiveForNames(lastEnabledNames, true);

        SetActiveForNames(currentHiddenNames, false);

        lastEnabledNames = CloneOrEmpty(currentEnabledNames);
        lastHiddenNames = CloneOrEmpty(currentHiddenNames);
    }

    private void SetActiveForNames(string[] names, bool activeState)
    {
        if (names == null)
            return;

        for (int i = 0; i < names.Length; i++)
        {
            string visualName = names[i];
            if (string.IsNullOrWhiteSpace(visualName))
                continue;

            if (!visualLookup.TryGetValue(visualName, out List<GameObject> visualObjects) || visualObjects == null)
                continue;

            for (int objectIndex = 0; objectIndex < visualObjects.Count; objectIndex++)
            {
                GameObject visualObject = visualObjects[objectIndex];
                if (visualObject != null)
                    visualObject.SetActive(activeState);
            }
        }
    }

    private static string[] CloneOrEmpty(string[] names)
    {
        if (names == null || names.Length == 0)
            return Array.Empty<string>();

        string[] clone = new string[names.Length];
        Array.Copy(names, clone, names.Length);
        return clone;
    }

    private static string[] GetEquippedVisualNames(ItemDefinition definition)
    {
        if (definition is ArmorItemDefinition armor)
            return armor.equippedVisualObjectNames;

        if (definition is ContainerItemDefinition container)
            return container.equippedVisualObjectNames;

        return Array.Empty<string>();
    }

    private static void SetActiveRecursive(GameObject root, bool active)
    {
        if (root == null)
            return;

        root.SetActive(active);
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
            SetActiveRecursive(rootTransform.GetChild(i).gameObject, active);
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
            SetLayerRecursive(rootTransform.GetChild(i).gameObject, layer);
    }

    private static void CenterClonedVisuals(Transform parent)
    {
        if (!TryGetLocalRendererBounds(parent, out Bounds localBounds))
            return;

        Vector3 horizontalOffset = new Vector3(localBounds.center.x, 0f, localBounds.center.z);
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            child.localPosition -= horizontalOffset;
        }
    }

    private static bool TryGetLocalRendererBounds(Transform parent, out Bounds localBounds)
    {
        localBounds = default;
        if (parent == null)
            return false;

        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                Vector3 localCorner = parent.InverseTransformPoint(corners[cornerIndex]);
                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        return hasBounds;
    }
}
