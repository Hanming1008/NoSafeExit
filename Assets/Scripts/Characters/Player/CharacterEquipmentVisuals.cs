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
}
