using UnityEngine;

public enum EnemySpawnArchetypeMode
{
    Random,
    Militia,
    Mercenary
}

[DisallowMultipleComponent]
public sealed class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private EnemySpawnArchetypeMode archetypeMode = EnemySpawnArchetypeMode.Random;
    [SerializeField, Min(0f)] private float weight = 1f;
    [SerializeField] private bool enabledForRaid = true;

    public EnemySpawnArchetypeMode ArchetypeMode => archetypeMode;
    public float Weight => Mathf.Max(0f, weight);
    public bool EnabledForRaid => enabledForRaid && Weight > 0f;
}
