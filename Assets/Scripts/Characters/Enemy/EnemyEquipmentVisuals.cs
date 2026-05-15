using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyCorpseLoot))]
public class EnemyEquipmentVisuals : MonoBehaviour
{
    [SerializeField] private EnemyCorpseLoot corpseLoot;
    [SerializeField] private EnemyLoadoutGenerator loadoutGenerator;
    [SerializeField] private bool autoDiscoverProjectVisuals = true;
    [SerializeField]
    private string[] managedBodyVisualObjectNames =
    {
        "SM_Chr_Bombsuit_Male_01",
        "SM_Chr_Civilian_Female_01",
        "SM_Chr_Civilian_Female_02",
        "SM_Chr_Civilian_Male_01",
        "SM_Chr_Civilian_Male_02",
        "SM_Chr_Contractor_Female_01",
        "SM_Chr_Contractor_Male_01",
        "SM_Chr_Contractor_Male_02",
        "SM_Chr_Ghillie_Male_01",
        "SM_Chr_Insurgent_Female_01",
        "SM_Chr_Insurgent_Female_02",
        "SM_Chr_Insurgent_Male_01",
        "SM_Chr_Insurgent_Male_02",
        "SM_Chr_Insurgent_Male_03",
        "SM_Chr_Insurgent_Male_04",
        "SM_Chr_Insurgent_Male_05",
        "SM_Chr_Leader_Male_01",
        "SM_Chr_Pilot_Female_01",
        "SM_Chr_Pilot_Male_01",
        "SM_Chr_Soldier_Female_01",
        "SM_Chr_Soldier_Female_02",
        "SM_Chr_Soldier_Male_01",
        "SM_Chr_Soldier_Male_02"
    };
    [SerializeField]
    private string[] managedArchetypeAttachmentObjectNames =
    {
        "SM_Chr_Attach_Contractor_Scarf_01",
        "SM_Chr_Attach_Insurgent_Headpiece_03",
        "SM_Chr_Attach_Insurgent_Neck_03"
    };
    [SerializeField] private string[] militiaBodyVisualObjectNames = { "SM_Chr_Insurgent_Male_03" };
    [SerializeField] private string[] militiaAttachmentObjectNames =
    {
        "SM_Chr_Attach_Insurgent_Headpiece_03",
        "SM_Chr_Attach_Insurgent_Neck_03"
    };
    [SerializeField] private string[] mercenaryBodyVisualObjectNames = { "SM_Chr_Contractor_Male_01" };
    [SerializeField] private string[] mercenaryAttachmentObjectNames = { "SM_Chr_Attach_Contractor_Scarf_01" };
    [SerializeField]
    private string[] managedEquippedVisualObjectNames =
    {
        "SM_Chr_Attach_Helmet_01_Goggles_01",
        "SM_Chr_Attach_Helmet_01_Goggles_01_Glass",
        "SM_Chr_Attach_Helmet_02",
        "SM_Chr_Attach_NVG_03",
        "SM_Chr_Attach_Helmet_07",
        "SM_Chr_Attach_Helmet_09"
    };

    private readonly Dictionary<string, List<GameObject>> visualLookup = new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
    private ArmorItemDefinition lastHeadArmor;
    private ArmorItemDefinition lastChestArmor;
    private ContainerItemDefinition lastBackpack;
    private EnemyArchetype lastArchetype = (EnemyArchetype)(-1);
    private string[] lastHiddenNames = Array.Empty<string>();

    private void Awake()
    {
        ResolveReferences();
        AutoDiscoverVisualNamesIfNeeded();
        RebuildLookup();
        Refresh(true);
    }

    private void OnValidate()
    {
        ResolveReferences();
        AutoDiscoverVisualNamesIfNeeded();
    }

    private void Update()
    {
        Refresh(false);
    }

    public void ForceRefreshNow()
    {
        ResolveReferences();
        AutoDiscoverVisualNamesIfNeeded();
        RebuildLookup();
        Refresh(true);
    }

    private void ResolveReferences()
    {
        if (corpseLoot == null)
            corpseLoot = GetComponent<EnemyCorpseLoot>();

        if (loadoutGenerator == null)
            loadoutGenerator = GetComponent<EnemyLoadoutGenerator>();
    }

    private void RebuildLookup()
    {
        visualLookup.Clear();
        RegisterRecursive(transform);
    }

    private void RegisterRecursive(Transform node)
    {
        if (node == null)
            return;

        if (!visualLookup.TryGetValue(node.name, out List<GameObject> objects))
        {
            objects = new List<GameObject>();
            visualLookup.Add(node.name, objects);
        }

        objects.Add(node.gameObject);

        for (int i = 0; i < node.childCount; i++)
            RegisterRecursive(node.GetChild(i));
    }

    private void Refresh(bool force)
    {
        if (corpseLoot == null)
            return;

        ArmorItemDefinition headArmor = corpseLoot.GetSlot(EquipmentSlotType.HeadArmor)?.Item as ArmorItemDefinition;
        ArmorItemDefinition chestArmor = corpseLoot.GetSlot(EquipmentSlotType.ChestArmor)?.Item as ArmorItemDefinition;
        ContainerItemDefinition backpack = corpseLoot.GetSlot(EquipmentSlotType.Backpack)?.Item as ContainerItemDefinition;
        EnemyArchetype archetype = loadoutGenerator != null ? loadoutGenerator.Archetype : EnemyArchetype.Militia;

        if (!force && headArmor == lastHeadArmor && chestArmor == lastChestArmor && backpack == lastBackpack && archetype == lastArchetype)
            return;

        SetActiveForNames(lastHiddenNames, true);
        SetActiveForNames(managedEquippedVisualObjectNames, false);
        ApplyArchetypeVisuals(archetype);

        ApplyEquippedVisuals(headArmor);
        ApplyEquippedVisuals(chestArmor);
        ApplyEquippedVisuals(backpack);

        lastHiddenNames = CollectHiddenNames(headArmor, chestArmor, backpack);
        SetActiveForNames(lastHiddenNames, false);

        lastHeadArmor = headArmor;
        lastChestArmor = chestArmor;
        lastBackpack = backpack;
        lastArchetype = archetype;
    }

    private void ApplyArchetypeVisuals(EnemyArchetype archetype)
    {
        SetActiveForRendererNames(managedBodyVisualObjectNames, false);
        SetActiveForNames(managedArchetypeAttachmentObjectNames, false);

        if (archetype == EnemyArchetype.Mercenary)
        {
            SetActiveForRendererNames(mercenaryBodyVisualObjectNames, true);
            SetActiveForNames(mercenaryAttachmentObjectNames, true);
            return;
        }

        SetActiveForRendererNames(militiaBodyVisualObjectNames, true);
        SetActiveForNames(militiaAttachmentObjectNames, true);
    }

    private void ApplyEquippedVisuals(ItemDefinition item)
    {
        SetActiveForNames(GetEquippedVisualNames(item), true);
    }

    private static string[] CollectHiddenNames(params ItemDefinition[] items)
    {
        List<string> hiddenNames = new List<string>();
        for (int i = 0; i < items.Length; i++)
        {
            string[] names = GetHiddenVisualNames(items[i]);
            if (names == null)
                continue;

            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                string visualName = names[nameIndex];
                if (!string.IsNullOrWhiteSpace(visualName) && !hiddenNames.Contains(visualName))
                    hiddenNames.Add(visualName);
            }
        }

        return hiddenNames.ToArray();
    }

    private void SetActiveForNames(string[] names, bool activeState)
    {
        if (names == null)
            return;

        for (int i = 0; i < names.Length; i++)
        {
            string visualName = names[i];
            if (string.IsNullOrWhiteSpace(visualName))
                continue;

            if (!visualLookup.TryGetValue(visualName, out List<GameObject> visualObjects) || visualObjects == null)
            {
                if (!activeState || !TryCreateVisualInstanceFromSceneTemplate(visualName, out visualObjects))
                    continue;
            }

            for (int objectIndex = 0; objectIndex < visualObjects.Count; objectIndex++)
            {
                GameObject visualObject = visualObjects[objectIndex];
                if (visualObject != null)
                    visualObject.SetActive(activeState);
            }
        }
    }

    private bool TryCreateVisualInstanceFromSceneTemplate(string visualName, out List<GameObject> visualObjects)
    {
        visualObjects = null;

        Transform template = FindSceneTemplate(visualName);
        if (template == null)
            return false;

        Transform targetParent = IsHeadMountedVisual(visualName)
            ? FindBestLocalBone("Head")
            : FindMatchingLocalParent(template.parent);
        if (targetParent == null)
            targetParent = transform;

        GameObject clone = Instantiate(template.gameObject, targetParent);
        clone.name = visualName;
        clone.transform.localPosition = template.localPosition;
        clone.transform.localRotation = template.localRotation;
        clone.transform.localScale = ResolveLocalScaleForParent(template, targetParent);
        clone.SetActive(false);

        StripNonVisualComponents(clone);
        RegisterRecursive(clone.transform);

        return visualLookup.TryGetValue(visualName, out visualObjects) && visualObjects != null;
    }

    private static bool IsHeadMountedVisual(string visualName)
    {
        if (string.IsNullOrWhiteSpace(visualName))
            return false;

        return visualName.IndexOf("Helmet", StringComparison.OrdinalIgnoreCase) >= 0
            || visualName.IndexOf("NVG", StringComparison.OrdinalIgnoreCase) >= 0
            || visualName.IndexOf("Goggles", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Transform FindBestLocalBone(string boneName)
    {
        Transform[] localTransforms = GetComponentsInChildren<Transform>(true);
        Transform fallback = null;
        for (int i = 0; i < localTransforms.Length; i++)
        {
            Transform localTransform = localTransforms[i];
            if (localTransform == null || localTransform.name != boneName)
                continue;

            if (fallback == null)
                fallback = localTransform;

            // Prefer the visible character skeleton. The plugin also keeps an Armature/RootBone
            // hierarchy around, but attaching helmets there leaves them near the neck and detached
            // from the rendered ragdoll.
            string path = BuildTransformPath(localTransform);
            if (path.IndexOf("/Root/Hips/", StringComparison.Ordinal) >= 0
                && path.IndexOf("/Armature/RootBone/", StringComparison.Ordinal) < 0)
            {
                return localTransform;
            }
        }

        return fallback;
    }

    private Transform FindSceneTemplate(string visualName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform candidate = allTransforms[i];
            if (candidate == null || candidate.name != visualName)
                continue;

            if (candidate.IsChildOf(transform))
                continue;

            GameObject candidateObject = candidate.gameObject;
            if (candidateObject == null || !candidateObject.scene.IsValid() || !candidateObject.scene.isLoaded)
                continue;

            return candidate;
        }

        return null;
    }

    private Transform FindMatchingLocalParent(Transform templateParent)
    {
        if (templateParent == null || string.IsNullOrWhiteSpace(templateParent.name))
            return null;

        Transform[] localTransforms = GetComponentsInChildren<Transform>(true);
        Transform fallback = null;
        for (int i = 0; i < localTransforms.Length; i++)
        {
            Transform localTransform = localTransforms[i];
            if (localTransform == null || localTransform.name != templateParent.name)
                continue;

            if (fallback == null)
                fallback = localTransform;

            string path = BuildTransformPath(localTransform);
            if (path.IndexOf("/Root/Hips/", StringComparison.Ordinal) >= 0
                && path.IndexOf("/Armature/RootBone/", StringComparison.Ordinal) < 0)
            {
                return localTransform;
            }
        }

        return fallback;
    }

    private static string BuildTransformPath(Transform node)
    {
        if (node == null)
            return string.Empty;

        string path = node.name;
        Transform current = node.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static Vector3 ResolveLocalScaleForParent(Transform template, Transform targetParent)
    {
        if (template == null || targetParent == null)
            return template != null ? template.localScale : Vector3.one;

        Vector3 parentScale = targetParent.lossyScale;
        Vector3 templateWorldScale = template.lossyScale;
        return new Vector3(
            SafeScale(templateWorldScale.x, parentScale.x, template.localScale.x),
            SafeScale(templateWorldScale.y, parentScale.y, template.localScale.y),
            SafeScale(templateWorldScale.z, parentScale.z, template.localScale.z));
    }

    private static float SafeScale(float worldScale, float parentScale, float fallback)
    {
        return Mathf.Abs(parentScale) > 0.0001f ? worldScale / parentScale : fallback;
    }

    private static void StripNonVisualComponents(GameObject root)
    {
        if (root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            DestroyRuntimeObject(colliders[i]);

        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            DestroyRuntimeObject(rigidbodies[i]);

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            DestroyRuntimeObject(behaviours[i]);

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            DestroyRuntimeObject(animators[i]);
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

    private void SetActiveForRendererNames(string[] names, bool activeState)
    {
        if (names == null)
            return;

        HashSet<string> targetNames = new HashSet<string>(names, StringComparer.Ordinal);
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer != null && renderer.gameObject != null && targetNames.Contains(renderer.gameObject.name))
                renderer.gameObject.SetActive(activeState);
        }
    }

    private static string[] GetEquippedVisualNames(ItemDefinition item)
    {
        if (item is ArmorItemDefinition armor)
            return armor.equippedVisualObjectNames;

        if (item is ContainerItemDefinition container)
            return container.equippedVisualObjectNames;

        return Array.Empty<string>();
    }

    private static string[] GetHiddenVisualNames(ItemDefinition item)
    {
        if (item is ArmorItemDefinition armor)
            return armor.hiddenVisualObjectNames;

        if (item is ContainerItemDefinition container)
            return container.hiddenVisualObjectNames;

        return Array.Empty<string>();
    }

    private void AutoDiscoverVisualNamesIfNeeded()
    {
#if UNITY_EDITOR
        if (!autoDiscoverProjectVisuals)
            return;

        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        if (managedEquippedVisualObjectNames != null)
        {
            for (int i = 0; i < managedEquippedVisualObjectNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(managedEquippedVisualObjectNames[i]))
                    names.Add(managedEquippedVisualObjectNames[i]);
            }
        }

        AddEquippedVisualNamesFromAssets("t:ArmorItemDefinition", names);
        AddEquippedVisualNamesFromAssets("t:ContainerItemDefinition", names);

        managedEquippedVisualObjectNames = new string[names.Count];
        names.CopyTo(managedEquippedVisualObjectNames);
#endif
    }

#if UNITY_EDITOR
    private static void AddEquippedVisualNamesFromAssets(string filter, HashSet<string> names)
    {
        string[] guids = AssetDatabase.FindAssets(filter, new[] { "Assets/Data/Items" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            string[] equippedNames = GetEquippedVisualNames(item);
            if (equippedNames == null)
                continue;

            for (int nameIndex = 0; nameIndex < equippedNames.Length; nameIndex++)
            {
                if (!string.IsNullOrWhiteSpace(equippedNames[nameIndex]))
                    names.Add(equippedNames[nameIndex]);
            }
        }
    }
#endif
}
