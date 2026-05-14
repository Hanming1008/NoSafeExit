using UnityEngine;

public enum ItemValueTier
{
    Blue,
    Gold,
    Red
}

public abstract class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemId;
    public string displayName;

    [TextArea(2, 5)]
    public string description;

    [Header("Presentation")]
    public Sprite icon;
    public Sprite gridInventorySprite;
    public GameObject worldPrefab;

    [Header("Value")]
    public ItemValueTier valueTier = ItemValueTier.Blue;
    [Min(0f)]
    public float moneyValue;

    [Header("Inventory")]
    [Min(0f)]
    public float weight = 0.1f;
    public bool canStack;
    [Min(1)]
    public int maxStackSize = 1;
    [Min(1)]
    public int inventoryRows = 1;
    [Min(1)]
    public int inventoryColumns = 1;
    public bool canRotateInGrid;

    public abstract ItemType Type { get; }

    public float GetTotalMoneyValue(int quantity)
    {
        return moneyValue * Mathf.Max(0, quantity);
    }

    public Sprite GetGridInventorySpriteOrFallback()
    {
        if (this is WeaponItemDefinition weapon)
        {
            if (weapon.UsesLongGunDisplaySprite && weapon.equipmentSlotIcon != null)
                return weapon.equipmentSlotIcon;

            if (gridInventorySprite != null)
                return gridInventorySprite;

            if (weapon.equipmentSlotIcon != null)
                return weapon.equipmentSlotIcon;
        }
        else if (gridInventorySprite != null)
        {
            return gridInventorySprite;
        }

        return icon;
    }

    public bool ShouldFlipGridDisplaySprite()
    {
        return this is WeaponItemDefinition weapon && weapon.UsesLongGunDisplaySprite;
    }

    public float GetWorldSpriteTargetSize()
    {
        return this is WeaponItemDefinition weapon && weapon.UsesLongGunDisplaySprite
            ? 1.74f
            : 0.72f;
    }

    protected virtual void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId))
            itemId = name;

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = name;

        if (weight < 0f)
            weight = 0f;

        if (moneyValue < 0f)
            moneyValue = 0f;

        if (!canStack)
        {
            maxStackSize = 1;
        }
        else if (maxStackSize < 1)
        {
            maxStackSize = 1;
        }

        if (inventoryRows < 1)
            inventoryRows = 1;

        if (inventoryColumns < 1)
            inventoryColumns = 1;
    }
}
