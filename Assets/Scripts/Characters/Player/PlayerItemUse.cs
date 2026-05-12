using JUTPS;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerItemUse : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private JUHealth juHealth;
    [SerializeField] private bool logDebugMessages = true;

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    public bool CanUse(ItemDefinition item)
    {
        return item is MedicalItemDefinition;
    }

    public bool TryUseBackpackSlot(int slotIndex)
    {
        if (inventory == null)
            return false;

        InventorySlot slot = inventory.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
            return false;

        return TryUseItemInSlot(slotIndex, slot.Item);
    }

    public bool TryUseAssignedItem(ItemDefinition item)
    {
        if (inventory == null || item == null)
            return false;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (slot == null || slot.IsEmpty || slot.Item != item)
                continue;

            return TryUseItemInSlot(i, item);
        }

        return false;
    }

    private bool TryUseItemInSlot(int slotIndex, ItemDefinition item)
    {
        if (item is MedicalItemDefinition medical)
            return TryUseMedical(slotIndex, medical);

        Log("Use failed because " + item.displayName + " is not supported yet.");
        return false;
    }

    private bool TryUseMedical(int slotIndex, MedicalItemDefinition medical)
    {
        float healedAmount = 0f;
        float restoredStamina = 0f;
        bool usedSuccessfully = false;

        if (playerStats != null)
        {
            healedAmount = playerStats.Heal(medical.healAmount);
            restoredStamina = playerStats.RestoreStamina(medical.staminaRestoreAmount);
            usedSuccessfully = healedAmount > 0f || restoredStamina > 0f;
        }
        else if (juHealth != null && !juHealth.IsDead)
        {
            float previousHealth = juHealth.Health;
            juHealth.Health = Mathf.Min(juHealth.MaxHealth, juHealth.Health + medical.healAmount);
            healedAmount = juHealth.Health - previousHealth;
            usedSuccessfully = healedAmount > 0f;
        }

        if (!usedSuccessfully)
        {
            Log(medical.displayName + " had no effect.");
            return false;
        }

        if (!inventory.TryRemoveFromSlot(slotIndex, 1))
        {
            Log(medical.displayName + " was used but could not be removed from the inventory.");
            return false;
        }

        Log(
            "Used " + medical.displayName +
            ". Healed " + healedAmount.ToString("F1") +
            ", restored stamina " + restoredStamina.ToString("F1") + ".");
        return true;
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (juHealth == null)
            juHealth = GetComponent<JUHealth>();
    }

    private void Log(string message)
    {
        if (!logDebugMessages)
            return;

        Debug.Log("PlayerItemUse: " + message, this);
    }
}
