using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Stamina")]
    public float maxStamina = 150f;
    public float currentStamina = 150f;
    public float sprintDrainPerSecond = 25f;
    public float staminaRegenPerSecond = 18f;
    [Range(0.05f, 1f)]
    public float minStaminaToStartSprintRatio = 0.33333334f;

    [Header("Needs")]
    public float maxHydration = 100f;
    public float currentHydration = 100f;
    public float hydrationDrainPerSecond = 0.05f;
    public float maxHunger = 100f;
    public float currentHunger = 100f;
    public float hungerDrainPerSecond = 0.025f;

    [Header("Damage Zones")]
    public float headDamageMultiplier = 4f;
    public float chestDamageMultiplier = 1f;
    public float limbDamageMultiplier = 0.75f;
    [Range(0.5f, 0.98f)]
    public float headHeightThreshold = 0.78f;
    [Range(0.15f, 0.75f)]
    public float chestHeightThreshold = 0.38f;

    [Header("Armor")]
    [Tooltip("Durability lost per point of damage absorbed by armor.")]
    public float armorDurabilityDamageMultiplier = 1f;

    [Header("Debug")]
    public bool logArmorDamageDebug = true;

    [Header("Death")]
    public bool hideOnDeath = true;
    public float hideDelay = 0f;

    public bool IsAlive => !isDead;

    private bool isDead;
    private PlayerMove playerMove;
    private PlayerFaceMouse playerFaceMouse;
    private PlayerShoot playerShoot;
    private PlayerEquipment equipment;
    private CharacterController characterController;

    private enum HitRegion
    {
        Head,
        Chest,
        Limb
    }

    void Awake()
    {
        if (maxHealth <= 0f) maxHealth = 1f;
        if (maxStamina <= 0f) maxStamina = 1f;
        if (maxHydration <= 0f) maxHydration = 1f;
        if (maxHunger <= 0f) maxHunger = 1f;

        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        currentStamina = Mathf.Clamp(currentStamina <= 0f ? maxStamina : currentStamina, 0f, maxStamina);
        currentHydration = Mathf.Clamp(currentHydration <= 0f ? maxHydration : currentHydration, 0f, maxHydration);
        currentHunger = Mathf.Clamp(currentHunger <= 0f ? maxHunger : currentHunger, 0f, maxHunger);

        playerMove = GetComponent<PlayerMove>();
        playerFaceMouse = GetComponent<PlayerFaceMouse>();
        playerShoot = GetComponent<PlayerShoot>();
        EnsureEquipmentReference();
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (RaidFlowController.IsRaidActive)
            TickNeeds(Time.deltaTime);
    }

    public void TickNeeds(float deltaTime)
    {
        if (isDead || deltaTime <= 0f)
            return;

        currentHydration = Mathf.Max(0f, currentHydration - Mathf.Max(0f, hydrationDrainPerSecond) * deltaTime);
        currentHunger = Mathf.Max(0f, currentHunger - Mathf.Max(0f, hungerDrainPerSecond) * deltaTime);
    }

    public bool TrySprint(float deltaTime, bool wasSprinting)
    {
        if (isDead) return false;

        if (wasSprinting)
        {
            if (currentStamina <= 0f) return false;
        }
        else
        {
            float minStaminaToStartSprint = maxStamina * minStaminaToStartSprintRatio;
            if (currentStamina < minStaminaToStartSprint) return false;
        }

        currentStamina = Mathf.Max(0f, currentStamina - sprintDrainPerSecond * deltaTime);
        return true;
    }

    public void RecoverStamina(float deltaTime)
    {
        if (isDead) return;
        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * deltaTime);
    }

    public float Heal(float amount)
    {
        if (isDead || amount <= 0f)
            return 0f;

        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        return currentHealth - previousHealth;
    }

    public float RestoreStamina(float amount)
    {
        if (isDead || amount <= 0f)
            return 0f;

        float previousStamina = currentStamina;
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        return currentStamina - previousStamina;
    }

    public float RestoreHydration(float amount)
    {
        if (isDead || amount <= 0f)
            return 0f;

        float previousHydration = currentHydration;
        currentHydration = Mathf.Min(maxHydration, currentHydration + amount);
        return currentHydration - previousHydration;
    }

    public float RestoreHunger(float amount)
    {
        if (isDead || amount <= 0f)
            return 0f;

        float previousHunger = currentHunger;
        currentHunger = Mathf.Min(maxHunger, currentHunger + amount);
        return currentHunger - previousHunger;
    }

    public void TakeDamage(float amount, GameObject source)
    {
        TakeDamage(amount, source, null, transform.position + Vector3.up);
    }

    public void TakeDamage(float amount, GameObject source, Collider hitCollider, Vector3 hitPoint)
    {
        if (isDead || amount <= 0f) return;

        HitRegion hitRegion = ResolveHitRegion(hitCollider, hitPoint);
        float multipliedDamage = amount * GetHitRegionMultiplier(hitRegion);
        float finalDamage = ApplyEquippedArmor(hitRegion, multipliedDamage, out string armorLog);

        currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        Debug.Log($"Player took {finalDamage:F1} damage ({hitRegion}, raw {amount:F1}, scaled {multipliedDamage:F1}{armorLog}). HP: {currentHealth:F1}/{maxHealth:F1}");

        if (currentHealth <= 0f)
            Die(source);
    }

    public float ApplyArmorToIncomingPluginDamage(float damage, Vector3 hitPoint)
    {
        if (damage <= 0f)
            return 0f;

        HitRegion hitRegion = ResolveHitRegion(null, hitPoint);
        float finalDamage = ApplyEquippedArmor(hitRegion, damage, out string armorLog);
        if (logArmorDamageDebug)
            Debug.Log($"Player armor adjusted plugin damage ({hitRegion}, incoming {damage:F1}{armorLog}) => {finalDamage:F1}");

        return finalDamage;
    }

    private HitRegion ResolveHitRegion(Collider hitCollider, Vector3 hitPoint)
    {
        if (hitCollider != null)
        {
            string colliderPath = GetLowercaseTransformPath(hitCollider.transform);
            if (ContainsAny(colliderPath, "head", "helmet", "face", "neck", "goggle", "visor"))
                return HitRegion.Head;

            if (ContainsAny(colliderPath, "chest", "torso", "spine", "body", "upper", "vest", "armor", "rig", "pelvis", "hips"))
                return HitRegion.Chest;
        }

        if (hitCollider == null && hitPoint == Vector3.zero)
            hitPoint = transform.position + Vector3.up;

        Bounds bounds = hitCollider != null
            ? hitCollider.bounds
            : new Bounds(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));

        float height = Mathf.Max(0.01f, bounds.size.y);
        float normalizedHeight = Mathf.Clamp01((hitPoint.y - bounds.min.y) / height);
        if (normalizedHeight >= headHeightThreshold)
            return HitRegion.Head;
        if (normalizedHeight >= chestHeightThreshold)
            return HitRegion.Chest;

        return HitRegion.Limb;
    }

    private float GetHitRegionMultiplier(HitRegion hitRegion)
    {
        switch (hitRegion)
        {
            case HitRegion.Head:
                return Mathf.Max(0f, headDamageMultiplier);
            case HitRegion.Chest:
                return Mathf.Max(0f, chestDamageMultiplier);
            default:
                return Mathf.Max(0f, limbDamageMultiplier);
        }
    }

    private float ApplyEquippedArmor(HitRegion hitRegion, float damage, out string armorLog)
    {
        armorLog = string.Empty;
        if (damage <= 0f)
            return damage;

        EnsureEquipmentReference();
        if (equipment == null)
        {
            armorLog = ", no PlayerEquipment";
            return damage;
        }

        EquipmentSlotType slotType;
        switch (hitRegion)
        {
            case HitRegion.Head:
                slotType = EquipmentSlotType.HeadArmor;
                break;
            case HitRegion.Chest:
            case HitRegion.Limb:
                slotType = EquipmentSlotType.ChestArmor;
                break;
            default:
                return damage;
        }

        InventorySlot armorSlot = equipment.GetSlot(slotType);
        if (armorSlot == null)
        {
            armorLog = $", missing {slotType} slot";
            return damage;
        }

        if (armorSlot.IsEmpty)
        {
            armorLog = $", {slotType} empty";
            return damage;
        }

        if (armorSlot.Item is not ArmorItemDefinition armor)
        {
            string itemName = armorSlot.Item != null ? armorSlot.Item.displayName : "null";
            armorLog = $", {slotType} contains non-armor {itemName}";
            return damage;
        }

        ItemRuntimeData runtimeData = armorSlot.RuntimeData;
        if (runtimeData == null)
        {
            runtimeData = ItemRuntimeData.CreateFor(armor);
            armorSlot.TrySet(armor, armorSlot.Quantity, runtimeData);
            runtimeData = armorSlot.RuntimeData;
        }

        if (runtimeData == null)
        {
            armorLog = $", {armor.displayName} has no runtime durability";
            return damage;
        }

        float currentDurability = runtimeData.EnsureArmorDurability(armor);
        float maxDurability = Mathf.Max(1f, armor.maxDurability);
        if (currentDurability <= 0f)
        {
            armorLog = ", armor broken";
            return damage;
        }

        float durabilityRatio = Mathf.Clamp01(currentDurability / maxDurability);
        float effectiveReduction = Mathf.Clamp01(armor.damageReduction * durabilityRatio);
        float reducedDamage = damage * (1f - effectiveReduction);
        float absorbedDamage = damage - reducedDamage;
        float durabilityLoss = absorbedDamage * Mathf.Max(0f, armorDurabilityDamageMultiplier);
        float lostDurability = runtimeData.DamageArmorDurability(armor, durabilityLoss);

        armorLog = $", {armor.displayName} reduced {effectiveReduction * 100f:F1}%, durability -{lostDurability:F1} ({runtimeData.ArmorDurability:F1}/{maxDurability:F1})";
        return reducedDamage;
    }

    private void EnsureEquipmentReference()
    {
        if (equipment != null)
            return;

        equipment = GetComponent<PlayerEquipment>();
        if (equipment == null)
            equipment = GetComponentInParent<PlayerEquipment>();
        if (equipment == null)
            equipment = GetComponentInChildren<PlayerEquipment>(true);
    }

    private string GetLowercaseTransformPath(Transform target)
    {
        if (target == null)
            return string.Empty;

        string path = target.name;
        Transform current = target.parent;
        while (current != null && current != transform)
        {
            path += "/" + current.name;
            current = current.parent;
        }

        return path.ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        if (string.IsNullOrEmpty(text) || tokens == null)
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            if (!string.IsNullOrEmpty(tokens[i]) && text.Contains(tokens[i]))
                return true;
        }

        return false;
    }

    private void Die(GameObject source)
    {
        if (isDead) return;
        isDead = true;
        currentStamina = 0f;

        if (playerMove != null) playerMove.enabled = false;
        if (playerFaceMouse != null) playerFaceMouse.enabled = false;
        if (playerShoot != null) playerShoot.enabled = false;
        if (characterController != null) characterController.enabled = false;

        string sourceName = source != null ? source.name : "Unknown";
        Debug.Log($"Player died. Killed by: {sourceName}");

        if (hideOnDeath)
        {
            if (hideDelay <= 0f)
                gameObject.SetActive(false);
            else
                Invoke(nameof(HideSelf), hideDelay);
        }
    }

    private void HideSelf()
    {
        gameObject.SetActive(false);
    }
}
