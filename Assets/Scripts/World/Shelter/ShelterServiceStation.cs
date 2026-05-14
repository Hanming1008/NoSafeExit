using System.Collections.Generic;
using JUTPS;
using UnityEngine;

[DisallowMultipleComponent]
public class ShelterServiceStation : MonoBehaviour
{
    public enum ServiceKind
    {
        Needs,
        Medical
    }

    [Header("Service")]
    [SerializeField] private ServiceKind serviceKind = ServiceKind.Needs;
    [SerializeField] private string promptText = "Restore Needs";
    [SerializeField] private string progressDisplayName = "Restoring Needs";
    [SerializeField] private Sprite progressIcon;
    [SerializeField] private float holdDuration = 3f;

    [Header("Interaction")]
    [SerializeField] private float interactionRadius = 2.4f;
    [SerializeField] private Vector3 playerCheckOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Highlight")]
    [SerializeField] private bool includeNearbyRenderers = true;
    [SerializeField] private float nearbyHighlightRadius = 1.9f;

    private readonly List<GameObject> highlightRoots = new List<GameObject>();
    private PlayerItemUse itemUse;
    private PlayerStats playerStats;
    private JUHealth juHealth;
    private Transform player;
    private bool playerNear;
    private bool stationUseStarted;

    public bool IsPlayerNear => playerNear;
    public string PromptText => promptText;
    public GameObject BoundsRoot => gameObject;
    public Transform FallbackTransform => transform;

    void Awake()
    {
        ResolveReferences();
        RebuildHighlightRoots();
    }

    void OnValidate()
    {
        holdDuration = Mathf.Max(0.1f, holdDuration);
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
        nearbyHighlightRadius = Mathf.Max(0.1f, nearbyHighlightRadius);
    }

    void Update()
    {
        ResolveReferences();
        playerNear = IsPlayerWithinRange();

        if (!playerNear)
        {
            CancelStationUseIfActive();
            return;
        }

        if (stationUseStarted)
        {
            if (itemUse == null || !itemUse.IsUsing)
            {
                stationUseStarted = false;
                return;
            }

            if (!Input.GetKey(KeyCode.F))
            {
                itemUse.CancelActiveUse();
                stationUseStarted = false;
            }

            return;
        }

        if (!Input.GetKeyDown(KeyCode.F) || itemUse == null || !CanApplyService())
            return;

        stationUseStarted = itemUse.TryStartCustomUse(progressDisplayName, progressIcon, holdDuration, ApplyService);
    }

    public void GetHighlightRoots(List<GameObject> results)
    {
        if (results == null)
            return;

        if (highlightRoots.Count == 0)
            RebuildHighlightRoots();

        for (int i = 0; i < highlightRoots.Count; i++)
        {
            GameObject root = highlightRoots[i];
            if (root != null && !results.Contains(root))
                results.Add(root);
        }
    }

    [ContextMenu("Rebuild Highlight Roots")]
    public void RebuildHighlightRoots()
    {
        highlightRoots.Clear();
        AddHighlightRoot(gameObject);

        if (!includeNearbyRenderers)
            return;

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float radiusSqr = nearbyHighlightRadius * nearbyHighlightRadius;
        Vector3 center = transform.position;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if ((renderer.bounds.center - center).sqrMagnitude > radiusSqr)
                continue;

            AddHighlightRoot(renderer.gameObject);
        }
    }

    private void AddHighlightRoot(GameObject root)
    {
        if (root == null || highlightRoots.Contains(root))
            return;

        highlightRoots.Add(root);
    }

    private bool CanApplyService()
    {
        if (serviceKind == ServiceKind.Needs)
        {
            return playerStats != null
                && playerStats.IsAlive
                && (playerStats.currentHydration < playerStats.maxHydration
                    || playerStats.currentHunger < playerStats.maxHunger);
        }

        bool canHealPlayerStats = playerStats != null
            && playerStats.IsAlive
            && playerStats.currentHealth < playerStats.maxHealth;
        bool canHealJuHealth = juHealth != null
            && !juHealth.IsDead
            && juHealth.Health < juHealth.MaxHealth;
        return canHealPlayerStats || canHealJuHealth;
    }

    private void ApplyService()
    {
        if (serviceKind == ServiceKind.Needs)
        {
            if (playerStats != null)
            {
                playerStats.RestoreHydration(playerStats.maxHydration);
                playerStats.RestoreHunger(playerStats.maxHunger);
            }

            stationUseStarted = false;
            return;
        }

        if (playerStats != null)
            playerStats.Heal(playerStats.maxHealth);

        if (juHealth != null && !juHealth.IsDead)
            juHealth.Health = juHealth.MaxHealth;

        stationUseStarted = false;
    }

    private void CancelStationUseIfActive()
    {
        if (!stationUseStarted)
            return;

        if (itemUse != null && itemUse.IsUsing)
            itemUse.CancelActiveUse();

        stationUseStarted = false;
    }

    private bool IsPlayerWithinRange()
    {
        if (player == null)
            return false;

        Vector3 center = player.TransformPoint(playerCheckOffset);
        float distanceSqr = (transform.position - center).sqrMagnitude;
        return distanceSqr <= interactionRadius * interactionRadius;
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                player = taggedPlayer.transform;
        }

        if (player == null)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Exclude);
            if (stats != null)
                player = stats.transform;
        }

        if (player == null)
            return;

        if (itemUse == null)
            itemUse = player.GetComponent<PlayerItemUse>();

        if (playerStats == null)
            playerStats = player.GetComponent<PlayerStats>();

        if (juHealth == null)
            juHealth = player.GetComponent<JUHealth>();
    }
}
