using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyStats : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Death")]
    public bool hideOnDeath = true;
    public float hideDelay = 0f;

    public bool IsAlive => !isDead;

    private bool isDead;
    private EnemyAIController enemyAI;
    private NavMeshAgent navMeshAgent;
    private Collider[] colliders;

    void Awake()
    {
        if (maxHealth <= 0f) maxHealth = 1f;
        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);

        enemyAI = GetComponent<EnemyAIController>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public void TakeDamage(float amount, GameObject source)
    {
        if (isDead || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"Enemy took {amount:F1} damage. HP: {currentHealth:F1}/{maxHealth:F1}");

        if (currentHealth <= 0f)
            Die(source);
    }

    private void Die(GameObject source)
    {
        if (isDead) return;
        isDead = true;

        if (enemyAI != null) enemyAI.enabled = false;
        if (navMeshAgent != null) navMeshAgent.enabled = false;

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        string sourceName = source != null ? source.name : "Unknown";
        Debug.Log($"Enemy died. Killed by: {sourceName}");

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
