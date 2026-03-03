using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class RebuildNavMeshAndSnapAgents
{
    private const float DefaultSnapRadius = 80f;

    [MenuItem("Tools/NoSafeExit/Rebuild NavMesh And Snap Agents")]
    public static void RebuildAndSnap()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("[NavMeshFix] No active scene.");
            return;
        }

        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        if (surfaces == null || surfaces.Length == 0)
        {
            Debug.LogWarning("[NavMeshFix] No NavMeshSurface found in active scene.");
            return;
        }

        int rebuiltCount = 0;
        for (int i = 0; i < surfaces.Length; i++)
        {
            NavMeshSurface surface = surfaces[i];
            if (surface == null || !surface.isActiveAndEnabled) continue;

            Undo.RecordObject(surface, "Rebuild NavMesh Surface");
            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
            rebuiltCount++;
        }

        NavMeshAgent[] agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        int snappedCount = 0;
        int failedCount = 0;

        for (int i = 0; i < agents.Length; i++)
        {
            NavMeshAgent agent = agents[i];
            if (agent == null) continue;

            Transform t = agent.transform;
            int areaMask = agent.areaMask;
            if (areaMask == 0) areaMask = NavMesh.AllAreas;

            if (NavMesh.SamplePosition(t.position, out NavMeshHit hit, DefaultSnapRadius, areaMask))
            {
                Undo.RecordObject(t, "Snap Agent To NavMesh");
                t.position = hit.position;
                EditorUtility.SetDirty(t);
                snappedCount++;
            }
            else
            {
                failedCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[NavMeshFix] Rebuilt surfaces={rebuiltCount}, snapped agents={snappedCount}, failed agents={failedCount}.");
    }
}

