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

    [Header("Death")]
    public bool hideOnDeath = true;
    public float hideDelay = 0f;

    public bool IsAlive => !isDead;

    private bool isDead;
    private PlayerMove playerMove;
    private PlayerFaceMouse playerFaceMouse;
    private PlayerShoot playerShoot;
    private CharacterController characterController;

    void Awake()
    {
        if (maxHealth <= 0f) maxHealth = 1f;
        if (maxStamina <= 0f) maxStamina = 1f;

        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        currentStamina = Mathf.Clamp(currentStamina <= 0f ? maxStamina : currentStamina, 0f, maxStamina);

        playerMove = GetComponent<PlayerMove>();
        playerFaceMouse = GetComponent<PlayerFaceMouse>();
        playerShoot = GetComponent<PlayerShoot>();
        characterController = GetComponent<CharacterController>();
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

    public void TakeDamage(float amount, GameObject source)
    {
        if (isDead || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"Player took {amount:F1} damage. HP: {currentHealth:F1}/{maxHealth:F1}");

        if (currentHealth <= 0f)
            Die(source);
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
