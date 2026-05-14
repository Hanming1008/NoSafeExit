using System;
using JUTPS;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerItemUse : MonoBehaviour
{
    private enum UseSourceType
    {
        None,
        InventorySlot,
        GridPlacement,
        Custom
    }

    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerGridInventory gridInventory;
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private JUHealth juHealth;
    [SerializeField] private JUCharacterController juCharacter;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private bool logDebugMessages = true;

    private UseSourceType activeSourceType = UseSourceType.None;
    private ItemDefinition activeItem;
    private int activeInventorySlotIndex = -1;
    private GridContainerState activeGridContainer;
    private GridItemPlacement activeGridPlacement;
    private string activeCustomDisplayName;
    private Sprite activeCustomIcon;
    private Action activeCustomCompleteAction;
    private float useElapsed;
    private float useDuration;

    public bool IsUsing => activeItem != null || activeSourceType == UseSourceType.Custom;
    public ItemDefinition ActiveItem => activeItem;
    public string ActiveUseDisplayName => activeItem != null ? activeItem.displayName : activeCustomDisplayName;
    public Sprite ActiveUseIcon => activeItem != null ? activeItem.GetGridInventorySpriteOrFallback() : activeCustomIcon;
    public MedicalItemDefinition ActiveMedical => activeItem as MedicalItemDefinition;
    public float UseProgressNormalized => useDuration > 0.01f ? Mathf.Clamp01(useElapsed / useDuration) : 0f;
    public float UseRemainingNormalized => 1f - UseProgressNormalized;
    public float UseRemainingSeconds => Mathf.Max(0f, useDuration - useElapsed);

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
        if (!IsUsing)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelActiveUse();
            return;
        }

        DisableMovementForUse();
        useElapsed += Time.deltaTime;
        if (useElapsed >= useDuration)
            CompleteActiveUse();
    }

    public bool CanUse(ItemDefinition item)
    {
        return item is MedicalItemDefinition || item is ConsumableItemDefinition;
    }

    public bool TryUseBackpackSlot(int slotIndex)
    {
        if (inventory == null)
            return false;

        InventorySlot slot = inventory.GetSlot(slotIndex);
        if (slot == null || slot.IsEmpty)
            return false;

        if (!CanUse(slot.Item))
        {
            Log("Use failed because " + slot.Item.displayName + " is not supported yet.");
            return false;
        }

        return TryStartItemUse(slot.Item, UseSourceType.InventorySlot, slotIndex, null, null);
    }

    public bool TryUseAssignedItem(ItemDefinition item)
    {
        if (item == null)
            return false;

        if (inventory != null)
        {
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                InventorySlot slot = inventory.GetSlot(i);
                if (slot == null || slot.IsEmpty || slot.Item != item)
                    continue;

                return TryUseBackpackSlot(i);
            }
        }

        if (gridInventory != null &&
            gridInventory.TryFindFirstPlacement(item, out GridContainerState container, out GridItemPlacement placement))
        {
            return TryUseGridPlacement(container, placement);
        }

        return false;
    }

    public bool TryUseGridPlacement(GridContainerState container, GridItemPlacement placement)
    {
        if (container == null || placement == null || placement.IsEmpty)
            return false;

        if (!CanUse(placement.Item))
        {
            Log("Use failed because " + placement.Item.displayName + " is not supported yet.");
            return false;
        }

        return TryStartItemUse(placement.Item, UseSourceType.GridPlacement, -1, container, placement);
    }

    public bool TryStartCustomUse(string displayName, Sprite icon, float duration, Action onComplete)
    {
        if (IsUsing || onComplete == null)
            return false;

        activeItem = null;
        activeSourceType = UseSourceType.Custom;
        activeInventorySlotIndex = -1;
        activeGridContainer = null;
        activeGridPlacement = null;
        activeCustomDisplayName = string.IsNullOrWhiteSpace(displayName) ? "Using" : displayName;
        activeCustomIcon = icon;
        activeCustomCompleteAction = onComplete;
        useElapsed = 0f;
        useDuration = Mathf.Max(0.01f, duration);

        DisableMovementForUse();
        Log("Started " + activeCustomDisplayName + ".");
        return true;
    }

    private bool TryStartItemUse(
        ItemDefinition item,
        UseSourceType sourceType,
        int inventorySlotIndex,
        GridContainerState gridContainer,
        GridItemPlacement gridPlacement)
    {
        if (IsUsing || item == null)
            return false;

        if (!CanApplyItem(item))
        {
            Log(item.displayName + " had no effect.");
            return false;
        }

        activeItem = item;
        activeSourceType = sourceType;
        activeInventorySlotIndex = inventorySlotIndex;
        activeGridContainer = gridContainer;
        activeGridPlacement = gridPlacement;
        useElapsed = 0f;
        useDuration = Mathf.Max(0.01f, GetUseDuration(item));

        DisableMovementForUse();
        Log("Started using " + item.displayName + ".");
        return true;
    }

    public bool CancelActiveUse()
    {
        if (!IsUsing)
            return false;

        string displayName = activeItem != null ? activeItem.displayName : activeCustomDisplayName;
        Log("Canceled using " + displayName + ".");
        ClearActiveUse();
        return true;
    }

    private void CompleteActiveUse()
    {
        if (activeSourceType == UseSourceType.Custom)
        {
            activeCustomCompleteAction?.Invoke();
            ClearActiveUse();
            return;
        }

        ItemDefinition item = activeItem;
        if (item == null)
        {
            ClearActiveUse();
            return;
        }

        if (!ConsumeActiveSourceItem())
        {
            Log(item.displayName + " could not be consumed.");
            ClearActiveUse();
            return;
        }

        ApplyItemEffects(item);
        ClearActiveUse();
    }

    private float GetUseDuration(ItemDefinition item)
    {
        return item switch
        {
            MedicalItemDefinition medical => medical.useDuration,
            ConsumableItemDefinition consumable => consumable.useDuration,
            _ => 0.01f
        };
    }

    private void ApplyItemEffects(ItemDefinition item)
    {
        if (item is MedicalItemDefinition medical)
        {
            ApplyMedicalEffects(medical);
            return;
        }

        if (item is ConsumableItemDefinition consumable)
            ApplyConsumableEffects(consumable);
    }

    private void ApplyMedicalEffects(MedicalItemDefinition medical)
    {
        float healedAmount = 0f;
        float restoredStamina = 0f;

        if (playerStats != null)
        {
            healedAmount = playerStats.Heal(medical.healAmount);
            restoredStamina = playerStats.RestoreStamina(medical.staminaRestoreAmount);
        }
        else if (juHealth != null && !juHealth.IsDead)
        {
            float previousHealth = juHealth.Health;
            juHealth.Health = Mathf.Min(juHealth.MaxHealth, juHealth.Health + medical.healAmount);
            healedAmount = juHealth.Health - previousHealth;
        }

        Log(
            "Used " + medical.displayName +
            ". Healed " + healedAmount.ToString("F1") +
            ", restored stamina " + restoredStamina.ToString("F1") + ".");
    }

    private void ApplyConsumableEffects(ConsumableItemDefinition consumable)
    {
        float restoredHydration = 0f;
        float restoredHunger = 0f;

        if (playerStats != null)
        {
            restoredHydration = playerStats.RestoreHydration(consumable.hydrationRestoreAmount);
            restoredHunger = playerStats.RestoreHunger(consumable.hungerRestoreAmount);
        }

        Log(
            "Used " + consumable.displayName +
            ". Restored hydration " + restoredHydration.ToString("F1") +
            ", restored hunger " + restoredHunger.ToString("F1") + ".");
    }

    private bool CanApplyItem(ItemDefinition item)
    {
        return item switch
        {
            MedicalItemDefinition medical => CanApplyMedical(medical),
            ConsumableItemDefinition consumable => CanApplyConsumable(consumable),
            _ => false
        };
    }

    private bool CanApplyMedical(MedicalItemDefinition medical)
    {
        if (medical == null)
            return false;

        if (playerStats != null)
        {
            bool canHeal = playerStats.IsAlive && playerStats.currentHealth < playerStats.maxHealth && medical.healAmount > 0f;
            bool canRestoreStamina = playerStats.IsAlive && playerStats.currentStamina < playerStats.maxStamina && medical.staminaRestoreAmount > 0f;
            return canHeal || canRestoreStamina;
        }

        return juHealth != null && !juHealth.IsDead && juHealth.Health < juHealth.MaxHealth && medical.healAmount > 0f;
    }

    private bool CanApplyConsumable(ConsumableItemDefinition consumable)
    {
        if (consumable == null || playerStats == null || !playerStats.IsAlive)
            return false;

        bool canRestoreHydration = playerStats.currentHydration < playerStats.maxHydration && consumable.hydrationRestoreAmount > 0f;
        bool canRestoreHunger = playerStats.currentHunger < playerStats.maxHunger && consumable.hungerRestoreAmount > 0f;
        return canRestoreHydration || canRestoreHunger;
    }

    private bool ConsumeActiveSourceItem()
    {
        switch (activeSourceType)
        {
            case UseSourceType.InventorySlot:
                return inventory != null
                    && activeInventorySlotIndex >= 0
                    && inventory.TryRemoveFromSlot(activeInventorySlotIndex, 1);

            case UseSourceType.GridPlacement:
                if (activeGridContainer == null || activeGridPlacement == null || activeGridPlacement.IsEmpty)
                    return false;

                if (activeGridPlacement.Item != activeItem)
                    return false;

                int removedAmount = activeGridPlacement.Remove(1);
                if (activeGridPlacement.IsEmpty)
                    activeGridContainer.TryRemovePlacement(activeGridPlacement);

                return removedAmount > 0;

            default:
                return false;
        }
    }

    private void ClearActiveUse()
    {
        activeItem = null;
        activeSourceType = UseSourceType.None;
        activeInventorySlotIndex = -1;
        activeGridContainer = null;
        activeGridPlacement = null;
        activeCustomDisplayName = null;
        activeCustomIcon = null;
        activeCustomCompleteAction = null;
        useElapsed = 0f;
        useDuration = 0f;

        if (juCharacter != null)
            juCharacter.enableMove();
    }

    private void DisableMovementForUse()
    {
        if (juCharacter != null)
            juCharacter.DisableLocomotion();

        if (playerRigidbody != null)
        {
            Vector3 velocity = playerRigidbody.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            playerRigidbody.linearVelocity = velocity;
        }
    }

    private void ResolveReferences()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (gridInventory == null)
            gridInventory = GetComponent<PlayerGridInventory>();

        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (juHealth == null)
            juHealth = GetComponent<JUHealth>();

        if (juCharacter == null)
            juCharacter = GetComponent<JUCharacterController>();

        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody>();
    }

    private void Log(string message)
    {
        if (!logDebugMessages)
            return;

        Debug.Log("PlayerItemUse: " + message, this);
    }
}
