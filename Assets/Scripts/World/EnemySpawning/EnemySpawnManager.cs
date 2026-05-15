using System;
using System.Collections.Generic;
using JUTPS;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class EnemySpawnManager : MonoBehaviour
{
    [Serializable]
    private sealed class SpawnTemplate
    {
        public EnemyArchetype archetype;
        public GameObject template;
    }

    [Header("Templates")]
    [SerializeField] private bool autoUseSceneEnemiesAsTemplates = true;
    [SerializeField] private bool hideSceneTemplates = true;
    [SerializeField] private List<SpawnTemplate> templates = new List<SpawnTemplate>();

    [Header("Spawn Count")]
    [SerializeField, Min(0)] private int minEnemiesPerRaid = 8;
    [SerializeField, Min(0)] private int maxEnemiesPerRaid = 14;
    [SerializeField, Range(0f, 1f)] private float mercenaryChance = 0.25f;

    [Header("Placement")]
    [SerializeField] private bool useExplicitSpawnPoints = true;
    [SerializeField] private bool useLootContainersAsFallbackAnchors = true;
    [SerializeField] private bool includeShelterContainers;
    [SerializeField, Min(0f)] private float minDistanceFromPlayer = 18f;
    [SerializeField, Min(0f)] private float minDistanceBetweenEnemies = 14f;
    [SerializeField, Min(0f)] private float minAnchorOffset = 4f;
    [SerializeField, Min(0f)] private float maxAnchorOffset = 16f;
    [SerializeField, Min(0f)] private float navMeshSampleRadius = 6f;
    [SerializeField, Min(1)] private int maxPlacementAttempts = 160;

    [Header("Lifecycle")]
    [SerializeField] private bool spawnOnRaidStart = true;
    [SerializeField] private bool clearOnRaidEnd = true;
    [SerializeField] private bool clearExistingSpawnedEnemiesBeforeSpawn = true;
    [SerializeField] private Transform spawnedEnemyRoot;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private readonly List<GameObject> sceneTemplates = new List<GameObject>();
    private bool wasRaidActive;
    private bool hasSpawnedForCurrentRaid;

    private void Awake()
    {
        EnsureSpawnRoot();
        if (autoUseSceneEnemiesAsTemplates)
            CollectSceneEnemyTemplates();
    }

    private void OnValidate()
    {
        maxEnemiesPerRaid = Mathf.Max(minEnemiesPerRaid, maxEnemiesPerRaid);
        maxAnchorOffset = Mathf.Max(minAnchorOffset, maxAnchorOffset);
    }

    private void Start()
    {
        wasRaidActive = RaidFlowController.IsRaidActive;
        if (spawnOnRaidStart && wasRaidActive)
            SpawnRaidEnemies();
    }

    private void Update()
    {
        bool raidActive = RaidFlowController.IsRaidActive;
        if (raidActive && !wasRaidActive)
        {
            if (spawnOnRaidStart)
                SpawnRaidEnemies();
        }
        else if (!raidActive && wasRaidActive)
        {
            hasSpawnedForCurrentRaid = false;
            if (clearOnRaidEnd)
                ClearSpawnedEnemies();
        }

        wasRaidActive = raidActive;
    }

    [ContextMenu("Spawn Raid Enemies")]
    public void SpawnRaidEnemies()
    {
        EnsureSpawnRoot();
        if (clearExistingSpawnedEnemiesBeforeSpawn)
            ClearSpawnedEnemies();

        if (hasSpawnedForCurrentRaid && !clearExistingSpawnedEnemiesBeforeSpawn)
            return;

        int targetCount = UnityEngine.Random.Range(minEnemiesPerRaid, maxEnemiesPerRaid + 1);
        Transform player = FindPlayerTransform();
        List<Vector3> occupiedPositions = new List<Vector3>();
        int spawned = 0;

        for (int i = 0; i < targetCount; i++)
        {
            EnemyArchetype archetype = RollArchetype();
            if (!TryFindSpawnPosition(player, occupiedPositions, out Vector3 position, out Quaternion rotation, out EnemySpawnPoint point))
                break;

            if (point != null)
                archetype = ResolvePointArchetype(point, archetype);

            GameObject spawnedEnemy = SpawnEnemy(archetype, position, rotation);
            if (spawnedEnemy == null)
                continue;

            spawnedEnemies.Add(spawnedEnemy);
            occupiedPositions.Add(position);
            spawned++;
        }

        hasSpawnedForCurrentRaid = true;
        Debug.Log($"EnemySpawnManager: spawned {spawned}/{targetCount} enemy(s).", this);
    }

    [ContextMenu("Clear Spawned Enemies")]
    public void ClearSpawnedEnemies()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = spawnedEnemies[i];
            if (enemy == null)
                continue;

            DestroyRuntimeObject(enemy);
        }

        spawnedEnemies.Clear();
    }

    private void CollectSceneEnemyTemplates()
    {
        EnemyLoadoutGenerator[] generators = FindObjectsByType<EnemyLoadoutGenerator>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < generators.Length; i++)
        {
            EnemyLoadoutGenerator generator = generators[i];
            if (generator == null)
                continue;

            GameObject root = generator.gameObject;
            if (root == gameObject || IsUnderSpawnRoot(root.transform) || sceneTemplates.Contains(root))
                continue;

            if (!HasTemplate(generator.Archetype))
                templates.Add(new SpawnTemplate { archetype = generator.Archetype, template = root });

            sceneTemplates.Add(root);
            if (hideSceneTemplates)
                root.SetActive(false);
        }
    }

    private bool HasTemplate(EnemyArchetype archetype)
    {
        for (int i = 0; i < templates.Count; i++)
        {
            SpawnTemplate entry = templates[i];
            if (entry != null && entry.archetype == archetype && entry.template != null)
                return true;
        }

        return false;
    }

    private GameObject SpawnEnemy(EnemyArchetype archetype, Vector3 position, Quaternion rotation)
    {
        GameObject template = GetTemplate(archetype);
        if (template == null)
            return null;

        GameObject enemy = Instantiate(template, position, rotation, spawnedEnemyRoot);
        enemy.name = archetype + "_Enemy_Spawned";

        EnemyLoadoutGenerator generator = enemy.GetComponent<EnemyLoadoutGenerator>();
        if (generator != null)
            generator.SetArchetype(archetype, false);

        enemy.SetActive(true);
        ResetSpawnedEnemy(enemy, archetype);
        return enemy;
    }

    private GameObject GetTemplate(EnemyArchetype archetype)
    {
        for (int i = 0; i < templates.Count; i++)
        {
            SpawnTemplate entry = templates[i];
            if (entry != null && entry.archetype == archetype && entry.template != null)
                return entry.template;
        }

        for (int i = 0; i < templates.Count; i++)
        {
            SpawnTemplate entry = templates[i];
            if (entry != null && entry.template != null)
                return entry.template;
        }

        return null;
    }

    private void ResetSpawnedEnemy(GameObject enemy, EnemyArchetype archetype)
    {
        if (enemy == null)
            return;

        enemy.tag = "Enemy";

        JUHealth health = enemy.GetComponent<JUHealth>();
        if (health != null)
            health.Health = health.MaxHealth;

        Rigidbody body = enemy.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        EnemyLoadoutGenerator generator = enemy.GetComponent<EnemyLoadoutGenerator>();
        if (generator != null)
        {
            generator.SetArchetype(archetype, false);
            generator.Generate();
        }

        EnemyPluginDeathSuppressor suppressor = enemy.GetComponent<EnemyPluginDeathSuppressor>();
        if (suppressor == null)
            enemy.AddComponent<EnemyPluginDeathSuppressor>();
    }

    private bool TryFindSpawnPosition(Transform player, List<Vector3> occupiedPositions, out Vector3 position, out Quaternion rotation, out EnemySpawnPoint point)
    {
        point = null;
        position = Vector3.zero;
        rotation = Quaternion.identity;

        List<EnemySpawnPoint> explicitPoints = GetExplicitSpawnPoints();
        SearchableContainer[] containers = useLootContainersAsFallbackAnchors
            ? FindObjectsByType<SearchableContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            : Array.Empty<SearchableContainer>();

        bool preferExplicitPoints = useExplicitSpawnPoints && explicitPoints.Count > 0;
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector3 candidate;
            EnemySpawnPoint selectedPoint = null;
            if (preferExplicitPoints && (attempt < explicitPoints.Count * 2 || containers.Length == 0))
            {
                selectedPoint = PickWeightedPoint(explicitPoints);
                if (selectedPoint == null)
                    continue;

                candidate = selectedPoint.transform.position;
            }
            else if (containers.Length > 0)
            {
                SearchableContainer container = containers[UnityEngine.Random.Range(0, containers.Length)];
                if (!IsValidContainerAnchor(container))
                    continue;

                Vector2 circle = UnityEngine.Random.insideUnitCircle;
                if (circle.sqrMagnitude < 0.0001f)
                    circle = Vector2.up;

                float distance = UnityEngine.Random.Range(minAnchorOffset, maxAnchorOffset);
                Vector3 offset = new Vector3(circle.normalized.x, 0f, circle.normalized.y) * distance;
                candidate = container.transform.position + offset;
            }
            else
            {
                return false;
            }

            if (!TryProjectToNavMesh(candidate, out Vector3 navPosition))
                continue;

            if (!PassesDistanceFilters(navPosition, player, occupiedPositions))
                continue;

            position = navPosition;
            rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            point = selectedPoint;
            return true;
        }

        return false;
    }

    private List<EnemySpawnPoint> GetExplicitSpawnPoints()
    {
        List<EnemySpawnPoint> points = new List<EnemySpawnPoint>();
        if (!useExplicitSpawnPoints)
            return points;

        EnemySpawnPoint[] allPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < allPoints.Length; i++)
        {
            if (allPoints[i] != null && allPoints[i].EnabledForRaid)
                points.Add(allPoints[i]);
        }

        return points;
    }

    private EnemySpawnPoint PickWeightedPoint(List<EnemySpawnPoint> points)
    {
        float totalWeight = 0f;
        for (int i = 0; i < points.Count; i++)
            totalWeight += points[i] != null ? points[i].Weight : 0f;

        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < points.Count; i++)
        {
            EnemySpawnPoint point = points[i];
            if (point == null)
                continue;

            roll -= point.Weight;
            if (roll <= 0f)
                return point;
        }

        return points[points.Count - 1];
    }

    private bool IsValidContainerAnchor(SearchableContainer container)
    {
        if (container == null)
            return false;

        if (includeShelterContainers)
            return true;

        if (container.GetComponentInParent<ShelterStashStation>() != null)
            return false;

        string displayName = container.DisplayName;
        return string.IsNullOrWhiteSpace(displayName)
            || displayName.IndexOf("stash", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private bool TryProjectToNavMesh(Vector3 candidate, out Vector3 navPosition)
    {
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            navPosition = hit.position;
            return true;
        }

        navPosition = default;
        return false;
    }

    private bool PassesDistanceFilters(Vector3 candidate, Transform player, List<Vector3> occupiedPositions)
    {
        Vector3 flatCandidate = Flatten(candidate);
        if (player != null && Vector3.Distance(flatCandidate, Flatten(player.position)) < minDistanceFromPlayer)
            return false;

        for (int i = 0; i < occupiedPositions.Count; i++)
        {
            if (Vector3.Distance(flatCandidate, Flatten(occupiedPositions[i])) < minDistanceBetweenEnemies)
                return false;
        }

        return true;
    }

    private EnemyArchetype RollArchetype()
    {
        return UnityEngine.Random.value < mercenaryChance
            ? EnemyArchetype.Mercenary
            : EnemyArchetype.Militia;
    }

    private EnemyArchetype ResolvePointArchetype(EnemySpawnPoint point, EnemyArchetype fallback)
    {
        if (point == null)
            return fallback;

        return point.ArchetypeMode switch
        {
            EnemySpawnArchetypeMode.Militia => EnemyArchetype.Militia,
            EnemySpawnArchetypeMode.Mercenary => EnemyArchetype.Mercenary,
            _ => fallback
        };
    }

    private Transform FindPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void EnsureSpawnRoot()
    {
        if (spawnedEnemyRoot != null)
            return;

        GameObject root = new GameObject("RuntimeSpawnedEnemies");
        root.transform.SetParent(transform, false);
        spawnedEnemyRoot = root.transform;
    }

    private bool IsUnderSpawnRoot(Transform target)
    {
        return spawnedEnemyRoot != null && target != null && target.IsChildOf(spawnedEnemyRoot);
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
