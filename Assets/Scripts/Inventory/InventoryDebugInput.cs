using JUTPS;
using UnityEngine;

[DisallowMultipleComponent]
public class InventoryDebugInput : MonoBehaviour
{
    private const string SmallCarryComboTestId = "debug_combo_small";

    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerItemDrop itemDrop;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private JUHealth juHealth;
    [SerializeField] private GameplayUIRoot gameplayUIRoot;

    [Header("Test Items")]
    [SerializeField] private ArmorItemDefinition debugHelmet;
    [SerializeField] private WeaponItemDefinition debugWeapon;
    [SerializeField] private WeaponItemDefinition debugAlternatePrimaryWeapon;
    [SerializeField] private WeaponItemDefinition debugSecondaryWeapon;
    [SerializeField] private AmmoItemDefinition debugAmmo;
    [SerializeField] private MedicalItemDefinition debugMedical;
    [SerializeField] private ArmorItemDefinition debugArmor;
    [SerializeField] private ContainerItemDefinition debugBackpack;

    [Header("Debug Keys")]
    public KeyCode equipHelmetKey = KeyCode.F3;
    public KeyCode equipBackpackKey = KeyCode.F4;
    public KeyCode addLoadoutKey = KeyCode.F5;
    public KeyCode equipWeaponKey = KeyCode.F6;
    public KeyCode equipArmorKey = KeyCode.F7;
    public KeyCode equipMedicalKey = KeyCode.F8;
    public KeyCode useMedicalKey = KeyCode.F9;
    public KeyCode logStateKey = KeyCode.F10;
    public KeyCode dropFirstBackpackItemKey = KeyCode.F11;
    public KeyCode equipSecondaryWeaponKey = KeyCode.F12;

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    void Update()
    {
        if (Input.GetKeyDown(equipHelmetKey))
            EquipDebugHelmet();

        if (Input.GetKeyDown(equipBackpackKey))
            EquipDebugBackpack();

        if (Input.GetKeyDown(addLoadoutKey))
            AddDebugLoadout();

        if (Input.GetKeyDown(equipWeaponKey))
            EquipDebugWeapon();

        if (Input.GetKeyDown(equipArmorKey))
            EquipDebugArmor();

        if (Input.GetKeyDown(equipMedicalKey))
            EquipDebugMedical();

        if (Input.GetKeyDown(useMedicalKey))
            UseQuickMedical();

        if (Input.GetKeyDown(logStateKey))
            LogInventoryState();

        if (Input.GetKeyDown(dropFirstBackpackItemKey))
            DropFirstBackpackItem();

        if (Input.GetKeyDown(equipSecondaryWeaponKey))
            EquipDebugSecondaryWeapon();
    }

    public void AssignTestItems(
        ArmorItemDefinition helmet,
        WeaponItemDefinition weapon,
        WeaponItemDefinition alternatePrimaryWeapon,
        WeaponItemDefinition secondaryWeapon,
        AmmoItemDefinition ammo,
        MedicalItemDefinition medical,
        ArmorItemDefinition armor,
        ContainerItemDefinition backpack)
    {
        debugHelmet = helmet;
        debugWeapon = weapon;
        debugAlternatePrimaryWeapon = alternatePrimaryWeapon;
        debugSecondaryWeapon = secondaryWeapon;
        debugAmmo = ammo;
        debugMedical = medical;
        debugArmor = armor;
        debugBackpack = backpack;
    }

    [ContextMenu("Add Debug Loadout")]
    public void AddDebugLoadout()
    {
        if (inventory == null)
        {
            Debug.LogWarning("InventoryDebugInput: PlayerInventory reference is missing.", this);
            return;
        }

        if (gameplayUIRoot != null && gameplayUIRoot.SingleGridTestMode)
        {
            AddSingleGridTestItem();
            return;
        }

        int addedItems = 0;

        if (debugHelmet != null && inventory.TryAddItem(debugHelmet, 1))
            addedItems++;

        if (debugWeapon != null && inventory.TryAddItem(debugWeapon, 1))
            addedItems++;

        if (debugAlternatePrimaryWeapon != null && inventory.TryAddItem(debugAlternatePrimaryWeapon, 1))
            addedItems++;

        if (debugSecondaryWeapon != null && inventory.TryAddItem(debugSecondaryWeapon, 1))
            addedItems++;

        if (debugAmmo != null && inventory.AddItem(debugAmmo, 60) > 0)
            addedItems++;

        if (debugMedical != null && inventory.AddItem(debugMedical, 2) > 0)
            addedItems++;

        if (debugArmor != null && inventory.TryAddItem(debugArmor, 1))
            addedItems++;

        if (debugBackpack != null && inventory.TryAddItem(debugBackpack, 1))
            addedItems++;

        Debug.Log($"InventoryDebugInput: Added debug loadout entries = {addedItems}.", this);
    }

    private void AddSingleGridTestItem()
    {
        inventory.ClearAll();
        if (equipment != null)
            equipment.ClearAllEquipment();

        string targetItemId = gameplayUIRoot != null ? gameplayUIRoot.SingleGridTestItemId : string.Empty;
        if (string.IsNullOrWhiteSpace(targetItemId))
        {
            Debug.LogWarning("InventoryDebugInput: Single-grid test mode is enabled but no target item id is configured.", this);
            return;
        }

        if (targetItemId == SmallCarryComboTestId)
        {
            AddSmallCarryComboTestItems();
            return;
        }

        ItemDefinition targetItem = null;
        int targetQuantity = 0;

        if (debugWeapon != null && debugWeapon.itemId == targetItemId)
        {
            targetItem = debugWeapon;
            targetQuantity = 1;
        }
        else if (debugHelmet != null && debugHelmet.itemId == targetItemId)
        {
            targetItem = debugHelmet;
            targetQuantity = 1;
        }
        else if (debugAlternatePrimaryWeapon != null && debugAlternatePrimaryWeapon.itemId == targetItemId)
        {
            targetItem = debugAlternatePrimaryWeapon;
            targetQuantity = 1;
        }
        else if (debugSecondaryWeapon != null && debugSecondaryWeapon.itemId == targetItemId)
        {
            targetItem = debugSecondaryWeapon;
            targetQuantity = 1;
        }
        else if (debugAmmo != null && debugAmmo.itemId == targetItemId)
        {
            targetItem = debugAmmo;
            targetQuantity = 60;
        }
        else if (debugMedical != null && debugMedical.itemId == targetItemId)
        {
            targetItem = debugMedical;
            targetQuantity = 2;
        }
        else if (debugArmor != null && debugArmor.itemId == targetItemId)
        {
            targetItem = debugArmor;
            targetQuantity = 1;
        }
        else if (debugBackpack != null && debugBackpack.itemId == targetItemId)
        {
            targetItem = debugBackpack;
            targetQuantity = 1;
        }

        if (targetItem == null || targetQuantity <= 0)
        {
            Debug.LogWarning($"InventoryDebugInput: Could not find a matching debug item for single-grid target '{targetItemId}'.", this);
            return;
        }

        int addedQuantity = inventory.AddItem(targetItem, targetQuantity);
        Debug.Log($"InventoryDebugInput: Added single-grid test item '{targetItem.displayName}' x{addedQuantity}.", this);
    }

    private void AddSmallCarryComboTestItems()
    {
        int addedEntries = 0;

        if (debugSecondaryWeapon != null && inventory.AddItem(debugSecondaryWeapon, 1) > 0)
            addedEntries++;

        if (debugAmmo != null && inventory.AddItem(debugAmmo, 60) > 0)
            addedEntries++;

        if (debugMedical != null && inventory.AddItem(debugMedical, 2) > 0)
            addedEntries++;

        Debug.Log($"InventoryDebugInput: Added small carry combo test entries = {addedEntries}.", this);
    }

    [ContextMenu("Equip Debug Weapon")]
    public void EquipDebugWeapon()
    {
        if (debugWeapon == null)
        {
            Debug.LogWarning("InventoryDebugInput: Debug weapon is not assigned.", this);
            return;
        }

        TryEquipItem(debugWeapon, EquipmentSlotType.PrimaryWeapon);
    }

    [ContextMenu("Equip Debug Secondary Weapon")]
    public void EquipDebugSecondaryWeapon()
    {
        if (debugSecondaryWeapon == null)
        {
            Debug.LogWarning("InventoryDebugInput: Debug secondary weapon is not assigned.", this);
            return;
        }

        TryEquipItem(debugSecondaryWeapon, EquipmentSlotType.SecondaryWeapon);
    }

    [ContextMenu("Equip Debug Armor")]
    public void EquipDebugArmor()
    {
        if (debugArmor == null)
        {
            Debug.LogWarning("InventoryDebugInput: Debug armor is not assigned.", this);
            return;
        }

        TryEquipItem(debugArmor, GetArmorEquipmentSlot(debugArmor.armorSlot));
    }

    [ContextMenu("Equip Debug Helmet")]
    public void EquipDebugHelmet()
    {
        if (debugHelmet == null)
        {
            Debug.LogWarning("InventoryDebugInput: Debug helmet is not assigned.", this);
            return;
        }

        TryEquipItem(debugHelmet, GetArmorEquipmentSlot(debugHelmet.armorSlot));
    }

    [ContextMenu("Equip Debug Backpack")]
    public void EquipDebugBackpack()
    {
        if (debugBackpack == null)
        {
            Debug.LogWarning("InventoryDebugInput: Debug backpack is not assigned.", this);
            return;
        }

        TryEquipItem(debugBackpack, EquipmentSlotType.Backpack);
    }

    [ContextMenu("Equip Debug Medical")]
    public void EquipDebugMedical()
    {
        if (debugMedical == null)
        {
            Debug.LogWarning("InventoryDebugInput: Debug medical item is not assigned.", this);
            return;
        }

        TryEquipItem(debugMedical, EquipmentSlotType.QuickUseMedical);
    }

    [ContextMenu("Use Quick Medical")]
    public void UseQuickMedical()
    {
        if (equipment == null)
        {
            Debug.LogWarning("InventoryDebugInput: PlayerEquipment reference is missing.", this);
            return;
        }

        InventorySlot medicalSlot = equipment.GetSlot(EquipmentSlotType.QuickUseMedical);
        if (medicalSlot == null || medicalSlot.IsEmpty || medicalSlot.Item is not MedicalItemDefinition medical)
        {
            Debug.LogWarning("InventoryDebugInput: No medical item equipped in quick-use slot.", this);
            return;
        }

        bool usedSuccessfully = false;
        float healedAmount = 0f;
        float restoredStamina = 0f;

        if (playerStats != null)
        {
            healedAmount = playerStats.Heal(medical.healAmount);
            restoredStamina = playerStats.RestoreStamina(medical.staminaRestoreAmount);
            usedSuccessfully = healedAmount > 0f || restoredStamina > 0f;
        }
        else if (juHealth != null && !juHealth.IsDead)
        {
            float oldHealth = juHealth.Health;
            juHealth.Health = Mathf.Min(juHealth.MaxHealth, juHealth.Health + medical.healAmount);
            healedAmount = juHealth.Health - oldHealth;
            usedSuccessfully = healedAmount > 0f;
        }

        if (!usedSuccessfully)
        {
            Debug.Log("InventoryDebugInput: Medical use had no effect.", this);
            return;
        }

        medicalSlot.Remove(1);
        Debug.Log(
            $"InventoryDebugInput: Used {medical.displayName}. Healed {healedAmount:F1}, restored stamina {restoredStamina:F1}.",
            this);
    }

    [ContextMenu("Log Inventory State")]
    public void LogInventoryState()
    {
        if (inventory == null || equipment == null)
        {
            Debug.LogWarning("InventoryDebugInput: Missing inventory or equipment reference.", this);
            return;
        }

        Debug.Log(BuildInventoryReport(), this);
    }

    [ContextMenu("Drop First Backpack Item")]
    public void DropFirstBackpackItem()
    {
        if (itemDrop == null)
        {
            Debug.LogWarning("InventoryDebugInput: PlayerItemDrop reference is missing.", this);
            return;
        }

        if (!itemDrop.TryDropFirstOccupiedSlot())
            Debug.LogWarning("InventoryDebugInput: Failed to drop the first backpack item.", this);
    }

    private void TryEquipItem(ItemDefinition item, EquipmentSlotType slotType)
    {
        if (inventory == null || equipment == null)
        {
            Debug.LogWarning("InventoryDebugInput: Missing inventory or equipment reference.", this);
            return;
        }

        int inventorySlotIndex = FindInventorySlot(item);
        if (inventorySlotIndex < 0)
        {
            bool assignedDirectly = equipment.TryAssignEquippedItem(slotType, item, 1, true);
            Debug.Log(
                assignedDirectly
                    ? $"InventoryDebugInput: Directly assigned {item.displayName} to {slotType}."
                    : $"InventoryDebugInput: Item '{item.displayName}' is not in inventory.",
                this);
            return;
        }

        bool equippedSuccessfully = equipment.TryEquipFromInventory(inventorySlotIndex, slotType);
        Debug.Log(
            equippedSuccessfully
                ? $"InventoryDebugInput: Equipped {item.displayName} to {slotType}."
                : $"InventoryDebugInput: Failed to equip {item.displayName} to {slotType}.",
            this);
    }

    private int FindInventorySlot(ItemDefinition item)
    {
        if (inventory == null || item == null)
            return -1;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (slot != null && slot.Contains(item))
                return i;
        }

        return -1;
    }

    private EquipmentSlotType GetArmorEquipmentSlot(ArmorSlotType armorSlot)
    {
        switch (armorSlot)
        {
            case ArmorSlotType.Head:
                return EquipmentSlotType.HeadArmor;
            case ArmorSlotType.Chest:
                return EquipmentSlotType.ChestArmor;
            case ArmorSlotType.Legs:
                return EquipmentSlotType.LegsArmor;
            case ArmorSlotType.Feet:
                return EquipmentSlotType.FeetArmor;
            default:
                return EquipmentSlotType.ChestArmor;
        }
    }

    private string BuildInventoryReport()
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("InventoryDebugInput Report");
        builder.AppendLine("Backpack:");

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (slot == null || slot.IsEmpty)
            {
                builder.AppendLine($"- Slot {i}: Empty");
                continue;
            }

            builder.AppendLine($"- Slot {i}: {slot.Item.displayName} x{slot.Quantity}");
        }

        builder.AppendLine("Equipment:");
        for (int i = 0; i < equipment.EquippedSlots.Count; i++)
        {
            PlayerEquipment.EquipmentEntry entry = equipment.EquippedSlots[i];
            if (entry == null || entry.slot == null || entry.slot.IsEmpty)
            {
                builder.AppendLine($"- {entry?.slotType}: Empty");
                continue;
            }

            builder.AppendLine($"- {entry.slotType}: {entry.slot.Item.displayName} x{entry.slot.Quantity}");
        }

        return builder.ToString();
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (equipment == null)
            equipment = GetComponent<PlayerEquipment>();

        if (itemDrop == null)
            itemDrop = GetComponent<PlayerItemDrop>();

        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (juHealth == null)
            juHealth = GetComponent<JUHealth>();

        if (gameplayUIRoot == null)
            gameplayUIRoot = FindFirstObjectByType<GameplayUIRoot>();
    }
}
