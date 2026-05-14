using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SearchableContainer : MonoBehaviour
{
    [Serializable]
    public sealed class InitialItemEntry
    {
        public ItemDefinition item;
        [Min(1)] public int quantity = 1;
    }

    [Header("Container")]
    [SerializeField] private string containerDisplayName = "Search Container";
    [Min(1)] [SerializeField] private int rows = 4;
    [Min(1)] [SerializeField] private int columns = 4;
    [Min(0.25f)] [SerializeField] private float interactionRadius = 2f;

    [Header("Initial Contents")]
    [SerializeField] private bool seedOnAwake = true;
    [SerializeField] private bool clearExistingBeforeSeed = true;
    [SerializeField] private List<InitialItemEntry> initialItems = new List<InitialItemEntry>();

    [Header("Random Loot")]
    [SerializeField] private bool useRandomLoot;
    [SerializeField] private LootTable lootTable;

    [Header("Runtime State")]
    [SerializeReference] private GridContainerState containerState = new GridContainerState();
    [SerializeField] private bool seeded;

    public string DisplayName => string.IsNullOrWhiteSpace(containerDisplayName) ? gameObject.name : containerDisplayName;
    public float InteractionRadius => interactionRadius;
    public GridContainerState ContainerState => containerState;
    public bool UsesRandomLoot => useRandomLoot && lootTable != null;
    public LootTable AssignedLootTable => lootTable;

    void Awake()
    {
        EnsureInitialized();
        if (seedOnAwake && !seeded)
            SeedConfiguredLoot();
    }

    void OnValidate()
    {
        rows = Mathf.Max(1, rows);
        columns = Mathf.Max(1, columns);
        interactionRadius = Mathf.Max(0.25f, interactionRadius);
        EnsureInitialized();
    }

    [ContextMenu("Ensure Initialized")]
    public void EnsureInitialized()
    {
        containerState ??= new GridContainerState();
        containerState.Configure(GridContainerKind.External, rows, columns);
    }

    [ContextMenu("Seed Initial Items")]
    public void SeedInitialItems()
    {
        EnsureInitialized();

        if (clearExistingBeforeSeed)
            containerState.Clear();

        for (int i = 0; i < initialItems.Count; i++)
        {
            InitialItemEntry entry = initialItems[i];
            if (entry == null || entry.item == null || entry.quantity <= 0)
                continue;

            ItemRuntimeData runtimeData = entry.quantity == 1
                ? ItemRuntimeData.CreateFor(entry.item)
                : null;

            containerState.TryPlaceNewItem(entry.item, entry.quantity, runtimeData, out _);
        }

        seeded = true;
    }

    [ContextMenu("Seed Configured Loot")]
    public void SeedConfiguredLoot()
    {
        if (UsesRandomLoot)
        {
            SeedRandomLoot();
            return;
        }

        SeedInitialItems();
    }

    [ContextMenu("Seed Random Loot")]
    public void SeedRandomLoot()
    {
        SeedRandomLoot(Environment.TickCount ^ GetInstanceID());
    }

    public void SeedRandomLoot(int seed)
    {
        EnsureInitialized();

        if (clearExistingBeforeSeed)
            containerState.Clear();

        if (lootTable == null)
        {
            seeded = true;
            return;
        }

        System.Random random = new System.Random(seed);
        int rollCount = lootTable.RollCount(random);
        for (int i = 0; i < rollCount; i++)
        {
            LootTable.Entry entry = lootTable.PickEntry(random);
            if (entry?.item == null)
                continue;

            int quantity = lootTable.RollQuantity(entry, random);
            TryPlaceRandomLootEntry(entry, quantity);
        }

        seeded = true;
    }

    public void SetDisplayName(string value)
    {
        containerDisplayName = value;
    }

    public void SetDimensions(int targetRows, int targetColumns)
    {
        rows = Mathf.Max(1, targetRows);
        columns = Mathf.Max(1, targetColumns);
        EnsureInitialized();
    }

    public void ConfigureRandomLoot(LootTable table, bool enabled, bool seedWhenAwake, bool clearExisting)
    {
        lootTable = table;
        useRandomLoot = enabled && table != null;
        seedOnAwake = seedWhenAwake;
        clearExistingBeforeSeed = clearExisting;
        seeded = false;
    }

    private bool TryPlaceRandomLootEntry(LootTable.Entry entry, int quantity)
    {
        if (entry?.item == null || quantity <= 0)
            return false;

        // Random loot should stay tidy in containers: original orientation, first open slot.
        ItemRuntimeData runtimeData = entry.item.canStack ? null : ItemRuntimeData.CreateFor(entry.item);
        return containerState.TryPlaceNewItem(entry.item, quantity, runtimeData, out _, false);
    }
}
