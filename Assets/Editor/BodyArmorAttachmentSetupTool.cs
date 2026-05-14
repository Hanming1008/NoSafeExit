using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BodyArmorAttachmentSetupTool
{
    private const string PlayerName = "SM_Chr_Soldier_Female_01_Alt_02";
    private const string LevelIISourceName = "SM_Chr_Soldier_Male_02_Alt_03";
    private const string LevelIIISourceName = "SM_Chr_Soldier_Male_01_Alt_01";

    [MenuItem("Tools/NoSafeExit/Setup Body Armor Attachment Visuals")]
    public static void SetupBodyArmorAttachmentVisuals()
    {
        GameObject player = FindSceneObject(PlayerName);
        GameObject levelIISource = FindSceneObject(LevelIISourceName);
        GameObject levelIIISource = FindSceneObject(LevelIIISourceName);

        if (player == null || levelIISource == null || levelIIISource == null)
        {
            Debug.LogError("BodyArmorAttachmentSetupTool: missing player or source mannequin.");
            return;
        }

        string[] levelIIVisuals = RebuildVisualSet(player.transform, levelIISource.transform, "LevelII");
        string[] levelIIIVisuals = RebuildVisualSet(player.transform, levelIIISource.transform, "LevelIII");

        ApplyVisualNames(
            "Assets/Data/Items/Armor/Armor_Body_LevelII.asset",
            "Assets/Data/Items/Containers/Container_Rig_LevelII.asset",
            levelIIVisuals);
        ApplyVisualNames(
            "Assets/Data/Items/Armor/Armor_Body_LevelIII.asset",
            "Assets/Data/Items/Containers/Container_Rig_LevelIII.asset",
            levelIIIVisuals);

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveScene(player.scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"BodyArmorAttachmentSetupTool: Level II visuals={levelIIVisuals.Length}, Level III visuals={levelIIIVisuals.Length}.");
    }

    private static string[] RebuildVisualSet(Transform playerRoot, Transform sourceRoot, string prefix)
    {
        RemoveGeneratedVisuals(playerRoot, prefix + "_");

        List<Transform> sourceRoots = CollectActiveArmorAttachmentRoots(sourceRoot);
        List<string> clonedNames = new List<string>(sourceRoots.Count);
        Dictionary<string, int> nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < sourceRoots.Count; i++)
        {
            Transform source = sourceRoots[i];
            string parentPath = GetRelativePath(sourceRoot, source.parent);
            Transform targetParent = string.IsNullOrEmpty(parentPath) ? playerRoot : playerRoot.Find(parentPath);
            if (targetParent == null)
                targetParent = playerRoot;

            GameObject clone = Object.Instantiate(source.gameObject, targetParent, false);
            string cloneName = BuildUniqueName(prefix, source.name, nameCounts);
            clone.name = cloneName;
            clone.SetActive(false);
            SetLayerRecursive(clone, playerRoot.gameObject.layer);
            clonedNames.Add(cloneName);
        }

        return clonedNames.ToArray();
    }

    private static List<Transform> CollectActiveArmorAttachmentRoots(Transform sourceRoot)
    {
        List<Transform> candidates = new List<Transform>();
        Renderer[] renderers = sourceRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Transform transform = renderer.transform;
            if (!transform.gameObject.activeSelf || !IsArmorAttachmentName(transform.name))
                continue;

            candidates.Add(transform);
        }

        candidates.Sort((a, b) => GetDepth(a).CompareTo(GetDepth(b)));

        List<Transform> roots = new List<Transform>();
        for (int i = 0; i < candidates.Count; i++)
        {
            Transform candidate = candidates[i];
            bool hasSelectedAncestor = false;
            for (int selectedIndex = 0; selectedIndex < roots.Count; selectedIndex++)
            {
                if (IsAncestorOf(roots[selectedIndex], candidate))
                {
                    hasSelectedAncestor = true;
                    break;
                }
            }

            if (!hasSelectedAncestor)
                roots.Add(candidate);
        }

        return roots;
    }

    private static bool IsArmorAttachmentName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        return objectName.StartsWith("SM_Chr_Attach_Padding", StringComparison.Ordinal)
            || objectName.Contains("Nameplate")
            || objectName.Contains("Pouch")
            || objectName.Contains("Grenade")
            || objectName.Contains("ShellStrip")
            || objectName.Contains("Tool")
            || objectName.Contains("Bomb_C4");
    }

    private static void ApplyVisualNames(string armorPath, string containerPath, string[] visualNames)
    {
        ArmorItemDefinition armor = AssetDatabase.LoadAssetAtPath<ArmorItemDefinition>(armorPath);
        ContainerItemDefinition container = AssetDatabase.LoadAssetAtPath<ContainerItemDefinition>(containerPath);

        if (armor == null || container == null)
            throw new InvalidOperationException("Missing armor/container asset for " + armorPath);

        armor.equippedVisualObjectNames = visualNames;
        armor.hiddenVisualObjectNames = Array.Empty<string>();
        container.equippedVisualObjectNames = visualNames;
        container.hiddenVisualObjectNames = Array.Empty<string>();
        EditorUtility.SetDirty(armor);
        EditorUtility.SetDirty(container);
    }

    private static void RemoveGeneratedVisuals(Transform root, string prefix)
    {
        List<GameObject> targets = new List<GameObject>();
        CollectGeneratedVisuals(root, prefix, targets);
        for (int i = 0; i < targets.Count; i++)
            Object.DestroyImmediate(targets[i]);
    }

    private static void CollectGeneratedVisuals(Transform node, string prefix, List<GameObject> targets)
    {
        if (node == null)
            return;

        for (int i = node.childCount - 1; i >= 0; i--)
        {
            Transform child = node.GetChild(i);
            if (child.name.StartsWith(prefix, StringComparison.Ordinal))
            {
                targets.Add(child.gameObject);
                continue;
            }

            CollectGeneratedVisuals(child, prefix, targets);
        }
    }

    private static string BuildUniqueName(string prefix, string baseName, Dictionary<string, int> nameCounts)
    {
        string stem = prefix + "_" + baseName;
        if (!nameCounts.TryGetValue(stem, out int count))
        {
            nameCounts[stem] = 1;
            return stem;
        }

        nameCounts[stem] = count + 1;
        return stem + "_" + count;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == null || target == null || target == root)
            return string.Empty;

        Stack<string> parts = new Stack<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            parts.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", parts);
    }

    private static int GetDepth(Transform transform)
    {
        int depth = 0;
        Transform current = transform;
        while (current != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private static bool IsAncestorOf(Transform ancestor, Transform descendant)
    {
        Transform current = descendant != null ? descendant.parent : null;
        while (current != null)
        {
            if (current == ancestor)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return null;

        GameObject[] roots = activeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindInChildren(roots[i].transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindInChildren(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root.gameObject;

        foreach (Transform child in root)
        {
            GameObject found = FindInChildren(child, objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
