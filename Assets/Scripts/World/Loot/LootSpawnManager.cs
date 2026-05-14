using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LootSpawnManager : MonoBehaviour
{
    [Header("Raid Loot")]
    [SerializeField] private bool seedOnStart = true;
    [SerializeField] private bool includeInactiveContainers;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int fixedSeed = 1008;

    void Start()
    {
        if (seedOnStart)
            RerollAllContainers();
    }

    [ContextMenu("Reroll All Containers")]
    public void RerollAllContainers()
    {
        SearchableContainer[] containers = FindContainers();
        int baseSeed = useRandomSeed
            ? Environment.TickCount ^ DateTime.UtcNow.Millisecond
            : fixedSeed;

        System.Random random = new System.Random(baseSeed);
        int rerolled = 0;
        for (int i = 0; i < containers.Length; i++)
        {
            SearchableContainer container = containers[i];
            if (container == null || !container.UsesRandomLoot)
                continue;

            container.SeedRandomLoot(random.Next());
            rerolled++;
        }

        Debug.Log($"LootSpawnManager: rerolled {rerolled} searchable container(s). Seed={baseSeed}.", this);
    }

    private SearchableContainer[] FindContainers()
    {
        return UnityEngine.Object.FindObjectsByType<SearchableContainer>(
            includeInactiveContainers ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
    }
}
