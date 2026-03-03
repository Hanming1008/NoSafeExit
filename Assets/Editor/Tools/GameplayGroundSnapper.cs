using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;using JUTPS.ItemSystem;


public static class GameplayGroundSnapper
{
    private const float RayStartHeight = 120f;
    private const float RayDistance = 500f;
    private const float SurfaceEpsilon = 0.02f;

    [MenuItem("Tools/NoSafeExit/Snap Gameplay Objects To Ground")]
    public static void SnapGameplayObjectsToGround()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("[GroundSnap] No active scene.");
            return;
        }

        var targets = new List<TargetEntry>();
        var seen = new HashSet<int>();

        AddTargets(PlayerStatsTargets(), targets, seen, "Player");
        AddTargets(LootTargets(), targets, seen, "Loot");
        AddTargets(ExtractionTargets(), targets, seen, "Extraction");

        int moved = 0;
        int failed = 0;

        for (int i = 0; i < targets.Count; i++)
        {
            TargetEntry entry = targets[i];
            if (entry.Transform == null) continue;

            if (TrySnap(entry.Transform, entry.FixedOffset, out float oldY, out float newY))
            {
                moved++;
                Debug.Log($"[GroundSnap] {entry.Label}: {entry.Transform.name} y {oldY:F3} -> {newY:F3}");
            }
            else
            {
                failed++;
                Debug.LogWarning($"[GroundSnap] Could not find ground under: {entry.Transform.name}");
            }
        }

        if (moved > 0)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        Debug.Log($"[GroundSnap] Done. moved={moved}, failed={failed}, total={targets.Count}");
    }

    private static IEnumerable<Transform> PlayerStatsTargets()
    {
        PlayerStats[] players = UnityEngine.Object.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
                yield return players[i].transform;
        }
    }

private static IEnumerable<Transform> LootTargets()
    {
        JUItem[] loots = UnityEngine.Object.FindObjectsByType<JUItem>(FindObjectsSortMode.None);
        for (int i = 0; i < loots.Length; i++)
        {
            if (loots[i] != null)
                yield return loots[i].transform;
        }
    }

    private static IEnumerable<Transform> ExtractionTargets()
    {
        ExtractionZoneTimer[] zones = UnityEngine.Object.FindObjectsByType<ExtractionZoneTimer>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null)
                yield return zones[i].transform;
        }
    }

    private static void AddTargets(IEnumerable<Transform> source, List<TargetEntry> targets, HashSet<int> seen, string label)
    {
        foreach (Transform t in source)
        {
            if (t == null) continue;
            if (!seen.Add(t.gameObject.GetInstanceID())) continue;

            float fixedOffset = ResolveOffset(t, label);
            targets.Add(new TargetEntry(t, label, fixedOffset));
        }
    }

    private static float ResolveOffset(Transform t, string label)
    {
        if (label == "Extraction")
            return SurfaceEpsilon;

        if (TryGetPivotToBottom(t, out float pivotToBottom))
            return Mathf.Max(SurfaceEpsilon, pivotToBottom + SurfaceEpsilon);

        return SurfaceEpsilon;
    }

    private static bool TryGetPivotToBottom(Transform t, out float pivotToBottom)
    {
        if (t.TryGetComponent<CharacterController>(out var controller))
        {
            pivotToBottom = t.position.y - controller.bounds.min.y;
            return true;
        }

        Collider col = t.GetComponent<Collider>();
        if (col == null)
            col = t.GetComponentInChildren<Collider>(true);

        if (col != null)
        {
            pivotToBottom = t.position.y - col.bounds.min.y;
            return true;
        }

        Renderer rend = t.GetComponentInChildren<Renderer>(true);
        if (rend != null)
        {
            pivotToBottom = t.position.y - rend.bounds.min.y;
            return true;
        }

        pivotToBottom = 0f;
        return false;
    }

    private static bool TrySnap(Transform t, float fixedOffset, out float oldY, out float newY)
    {
        oldY = t.position.y;
        newY = oldY;

        Vector3 origin = t.position + Vector3.up * RayStartHeight;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, RayDistance, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var ownColliders = new HashSet<Collider>(t.GetComponentsInChildren<Collider>(true));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null) continue;
            if (ownColliders.Contains(hitCollider)) continue;
            if (hitCollider.transform.IsChildOf(t)) continue;

            Vector3 pos = t.position;
            float snappedY = hits[i].point.y + fixedOffset;

            Undo.RecordObject(t, "Snap Gameplay Object To Ground");
            t.position = new Vector3(pos.x, snappedY, pos.z);

            newY = snappedY;
            return true;
        }

        return false;
    }

    private readonly struct TargetEntry
    {
        public readonly Transform Transform;
        public readonly string Label;
        public readonly float FixedOffset;

        public TargetEntry(Transform transform, string label, float fixedOffset)
        {
            Transform = transform;
            Label = label;
            FixedOffset = fixedOffset;
        }
    }
}
