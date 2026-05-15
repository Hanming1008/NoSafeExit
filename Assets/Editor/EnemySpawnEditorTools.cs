#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class EnemySpawnEditorTools
{
    [MenuItem("Tools/NoSafeExit/Enemy Spawning/Spawn Raid Enemies")]
    private static void SpawnRaidEnemies()
    {
        EnemySpawnManager manager = Object.FindFirstObjectByType<EnemySpawnManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogWarning("EnemySpawnEditorTools: No EnemySpawnManager found in the scene.");
            return;
        }

        manager.SpawnRaidEnemies();
    }

    [MenuItem("Tools/NoSafeExit/Enemy Spawning/Clear Spawned Enemies")]
    private static void ClearSpawnedEnemies()
    {
        EnemySpawnManager manager = Object.FindFirstObjectByType<EnemySpawnManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogWarning("EnemySpawnEditorTools: No EnemySpawnManager found in the scene.");
            return;
        }

        manager.ClearSpawnedEnemies();
    }
}
#endif
