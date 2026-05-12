using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerEquipment : MonoBehaviour
{
    [Serializable]
    public class EquipmentEntry
    {
        public EquipmentSlotType slotType;
        public InventorySlot slot = new InventorySlot();

        public EquipmentEntry(EquipmentSlotType slotType)
        {
            this.slotType = slotType;
            slot = new InventorySlot();
        }
    }

    [Header("References")]
    [SerializeField] private PlayerInventory inventory;

    [Header("Equipment")]
    [SerializeField] private List<EquipmentEntry> equippedSlots = new List<EquipmentEntry>();

    public PlayerInventory Inventory => inventory;
    public IReadOnlyList<EquipmentEntry> EquippedSlots => equippedSlots;

    void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        EnsureEquipmentSlots();
    }

    void OnValidate()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        EnsureEquipmentSlots();
    }

    [ContextMenu("Ensure Equipment Slots")]
    public void EnsureEquipmentSlots()
    {
        if (equippedSlots == null)
            equippedSlots = new List<EquipmentEntry>();

        Array slotTypes = Enum.GetValues(typeof(EquipmentSlotType));
        for (int i = 0; i < slotTypes.Length; i++)
        {
            EquipmentSlotType slotType = (EquipmentSlotType)slotTypes.GetValue(i);
            EquipmentEntry entry = GetEntry(slotType);
            if (entry == null)
            {
                equippedSlots.Add(new EquipmentEntry(slotType));
            }
            else if (entry.slot == null)
            {
                entry.slot = new InventorySlot();
            }
        }

        equippedSlots.Sort((left, right) => left.slotType.CompareTo(right.slotType));
    }

    public InventorySlot GetSlot(EquipmentSlotType slotType)
    {
        EquipmentEntry entry = GetEntry(slotType);
        return entry != null ? entry.slot : null;
    }

    public WeaponItemDefinition GetEquippedWeaponDefinition(EquipmentSlotType slotType)
    {
        InventorySlot slot = GetSlot(slotType);
        if (slot == null || slot.IsEmpty)
            return null;

        return slot.Item as WeaponItemDefinition;
    }

    public bool CanEquip(EquipmentSlotType slotType, ItemDefinition item)
    {
        if (item == null)
            return false;

        switch (slotType)
        {
            case EquipmentSlotType.PrimaryWeapon:
            case EquipmentSlotType.SecondaryWeapon:
                return item is WeaponItemDefinition;

            case EquipmentSlotType.QuickUseMedical:
                return item is MedicalItemDefinition;

            case EquipmentSlotType.Backpack:
                return item is ContainerItemDefinition container && container.containerKind == GridContainerKind.Backpack;

            case EquipmentSlotType.HeadArmor:
                return item is ArmorItemDefinition headArmor && headArmor.armorSlot == ArmorSlotType.Head;

            case EquipmentSlotType.ChestArmor:
                return item is ArmorItemDefinition chestArmor && chestArmor.armorSlot == ArmorSlotType.Chest;

            case EquipmentSlotType.LegsArmor:
                return item is ArmorItemDefinition legsArmor && legsArmor.armorSlot == ArmorSlotType.Legs;

            case EquipmentSlotType.FeetArmor:
                return item is ArmorItemDefinition feetArmor && feetArmor.armorSlot == ArmorSlotType.Feet;

            default:
                return false;
        }
    }

    public bool TryAssignEquippedItem(EquipmentSlotType slotType, ItemDefinition item, int quantity = 1, bool replaceExisting = true)
    {
        if (!CanEquip(slotType, item))
            return false;

        InventorySlot slot = GetSlot(slotType);
        if (slot == null)
            return false;

        if (!replaceExisting && !slot.IsEmpty)
            return false;

        return slot.TrySet(item, NormalizeQuantityForSlot(slotType, item, quantity));
    }

    public bool TryEquipFromInventory(int inventorySlotIndex, EquipmentSlotType slotType)
    {
        if (inventory == null)
            return false;

        return TryEquipFromInventory(inventory, inventorySlotIndex, slotType);
    }

    public bool TryEquipWeaponFromInventory(int inventorySlotIndex)
    {
        if (inventory == null)
            return false;

        return TryEquipWeaponFromInventory(inventory, inventorySlotIndex);
    }

    public bool TryEquipWeaponFromInventory(PlayerInventory sourceInventory, int inventorySlotIndex)
    {
        if (sourceInventory == null)
            return false;

        InventorySlot sourceSlot = sourceInventory.GetSlot(inventorySlotIndex);
        if (sourceSlot == null || sourceSlot.IsEmpty || sourceSlot.Item is not WeaponItemDefinition weaponDefinition)
            return false;

        EquipmentSlotType targetSlot = GetPreferredWeaponSlot(weaponDefinition);
        return TryEquipFromInventory(sourceInventory, inventorySlotIndex, targetSlot);
    }

    public bool TryEquipFromInventory(PlayerInventory sourceInventory, int inventorySlotIndex, EquipmentSlotType slotType)
    {
        if (sourceInventory == null)
            return false;

        InventorySlot sourceSlot = sourceInventory.GetSlot(inventorySlotIndex);
        if (sourceSlot == null || sourceSlot.IsEmpty)
            return false;

        ItemDefinition sourceItem = sourceSlot.Item;
        if (!CanEquip(slotType, sourceItem))
            return false;

        int quantityToMove = NormalizeQuantityForSlot(slotType, sourceItem, sourceSlot.Quantity);
        if (quantityToMove <= 0)
            return false;

        InventorySlot equipmentSlot = GetSlot(slotType);
        if (equipmentSlot == null)
            return false;

        ItemDefinition previousItem = equipmentSlot.Item;
        int previousQuantity = equipmentSlot.Quantity;
        ItemRuntimeData sourceRuntimeData = sourceSlot.GetRuntimeDataForTransfer(quantityToMove);
        ItemRuntimeData previousRuntimeData = equipmentSlot.GetRuntimeDataForTransfer(previousQuantity);

        if (!sourceInventory.TryRemoveFromSlot(inventorySlotIndex, quantityToMove))
            return false;

        equipmentSlot.Clear();
        if (!equipmentSlot.TrySet(sourceItem, quantityToMove, sourceRuntimeData))
        {
            sourceInventory.TryAddItem(sourceItem, quantityToMove, sourceRuntimeData);
            if (previousItem != null)
                equipmentSlot.TrySet(previousItem, previousQuantity, previousRuntimeData);
            return false;
        }

        if (previousItem != null && previousQuantity > 0 && !sourceInventory.TryAddItem(previousItem, previousQuantity, previousRuntimeData))
        {
            equipmentSlot.Clear();
            equipmentSlot.TrySet(previousItem, previousQuantity, previousRuntimeData);
            sourceInventory.TryAddItem(sourceItem, quantityToMove, sourceRuntimeData);
            return false;
        }

        return true;
    }

    public bool TryUnequipToInventory(EquipmentSlotType slotType)
    {
        if (inventory == null)
            return false;

        return TryUnequipToInventory(slotType, inventory);
    }

    public bool TryUnequipToInventory(EquipmentSlotType slotType, PlayerInventory targetInventory)
    {
        if (targetInventory == null)
            return false;

        InventorySlot equipmentSlot = GetSlot(slotType);
        if (equipmentSlot == null || equipmentSlot.IsEmpty)
            return false;

        ItemRuntimeData runtimeData = equipmentSlot.GetRuntimeDataForTransfer(equipmentSlot.Quantity);
        if (!targetInventory.TryAddItem(equipmentSlot.Item, equipmentSlot.Quantity, runtimeData))
            return false;

        equipmentSlot.Clear();
        return true;
    }

    public bool TryUnequipToContainer(
        EquipmentSlotType slotType,
        GridContainerState primaryContainer,
        GridContainerState secondaryContainer = null,
        GridContainerState tertiaryContainer = null)
    {
        InventorySlot equipmentSlot = GetSlot(slotType);
        if (equipmentSlot == null || equipmentSlot.IsEmpty)
            return false;

        ItemDefinition item = equipmentSlot.Item;
        int quantity = equipmentSlot.Quantity;
        ItemRuntimeData runtimeData = equipmentSlot.GetRuntimeDataForTransfer(quantity);

        if (TryPlaceInContainer(primaryContainer, item, quantity, runtimeData)
            || TryPlaceInContainer(secondaryContainer, item, quantity, runtimeData)
            || TryPlaceInContainer(tertiaryContainer, item, quantity, runtimeData))
        {
            equipmentSlot.Clear();
            return true;
        }

        return false;
    }

    public void ClearAllEquipment()
    {
        for (int i = 0; i < equippedSlots.Count; i++)
        {
            if (equippedSlots[i] != null && equippedSlots[i].slot != null)
                equippedSlots[i].slot.Clear();
        }
    }

    private EquipmentEntry GetEntry(EquipmentSlotType slotType)
    {
        for (int i = 0; i < equippedSlots.Count; i++)
        {
            EquipmentEntry entry = equippedSlots[i];
            if (entry != null && entry.slotType == slotType)
                return entry;
        }

        return null;
    }

    private static bool TryPlaceInContainer(GridContainerState container, ItemDefinition item, int quantity, ItemRuntimeData runtimeData)
    {
        if (container == null || item == null || quantity <= 0)
            return false;

        return container.TryPlaceNewItem(item, quantity, runtimeData, out _);
    }

    private EquipmentSlotType GetPreferredWeaponSlot(WeaponItemDefinition weaponDefinition)
    {
        if (weaponDefinition != null && weaponDefinition.weaponCategory == WeaponCategory.Pistol)
            return EquipmentSlotType.SecondaryWeapon;

        return EquipmentSlotType.PrimaryWeapon;
    }

    private int NormalizeQuantityForSlot(EquipmentSlotType slotType, ItemDefinition item, int requestedQuantity)
    {
        if (item == null || requestedQuantity <= 0)
            return 0;

        if (slotType == EquipmentSlotType.QuickUseMedical && item is MedicalItemDefinition)
            return item.canStack ? Mathf.Min(requestedQuantity, item.maxStackSize) : 1;

        return 1;
    }
}
