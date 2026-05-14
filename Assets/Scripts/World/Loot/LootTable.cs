using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LootTable_", menuName = "NoSafeExit/Loot/Loot Table")]
public class LootTable : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public ItemDefinition item;
        [Min(0f)] public float weight = 1f;
        [Min(1)] public int minQuantity = 1;
        [Min(1)] public int maxQuantity = 1;
        public bool allowRotatedPlacement;
    }

    [Header("Roll Count")]
    [Min(0)] public int minRolls = 2;
    [Min(0)] public int maxRolls = 5;

    [Header("Placement")]
    [Min(1)] public int placementAttemptsPerRoll = 12;

    [Header("Entries")]
    public List<Entry> entries = new List<Entry>();

    public int RollCount(System.Random random)
    {
        if (random == null)
            random = new System.Random();

        int min = Mathf.Max(0, minRolls);
        int max = Mathf.Max(min, maxRolls);
        return random.Next(min, max + 1);
    }

    public Entry PickEntry(System.Random random)
    {
        if (random == null)
            random = new System.Random();

        float totalWeight = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry?.item == null || entry.weight <= 0f)
                continue;

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
            return null;

        double roll = random.NextDouble() * totalWeight;
        float accumulated = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry?.item == null || entry.weight <= 0f)
                continue;

            accumulated += entry.weight;
            if (roll <= accumulated)
                return entry;
        }

        return null;
    }

    public int RollQuantity(Entry entry, System.Random random)
    {
        if (entry?.item == null)
            return 0;

        if (random == null)
            random = new System.Random();

        int min = Mathf.Max(1, entry.minQuantity);
        int max = Mathf.Max(min, entry.maxQuantity);
        int quantity = random.Next(min, max + 1);

        return entry.item.canStack
            ? Mathf.Clamp(quantity, 1, Mathf.Max(1, entry.item.maxStackSize))
            : 1;
    }

    void OnValidate()
    {
        if (maxRolls < minRolls)
            maxRolls = minRolls;

        if (placementAttemptsPerRoll < 1)
            placementAttemptsPerRoll = 1;

        if (entries == null)
            entries = new List<Entry>();

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null)
                continue;

            if (entry.weight < 0f)
                entry.weight = 0f;

            if (entry.minQuantity < 1)
                entry.minQuantity = 1;

            if (entry.maxQuantity < entry.minQuantity)
                entry.maxQuantity = entry.minQuantity;
        }
    }
}
