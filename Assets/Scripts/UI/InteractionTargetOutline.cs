using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class InteractionTargetOutline : MonoBehaviour
{
    private const string OutlineContainerName = "__InteractionOutline";
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [SerializeField] private Color outlineColor = new Color(1f, 0.82f, 0.18f, 0.86f);
    [SerializeField] private float outlineWidth = 0.035f;

    private readonly List<GameObject> outlineObjects = new List<GameObject>();
    private Material outlineMaterial;
    private Transform outlineContainer;
    private bool highlighted;

    public void SetHighlighted(bool shouldHighlight)
    {
        if (highlighted == shouldHighlight)
            return;

        highlighted = shouldHighlight;

        if (highlighted)
            RebuildOutline();
        else
            ClearOutline();
    }

    void OnDisable()
    {
        highlighted = false;
        ClearOutline();
    }

    void OnDestroy()
    {
        ClearOutline();

        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }

    private void RebuildOutline()
    {
        ClearOutline();
        EnsureMaterial();

        if (outlineMaterial == null)
            return;

        outlineContainer = new GameObject(OutlineContainerName).transform;
        outlineContainer.SetParent(transform, false);

        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshRenderers.Length; i++)
            AddMeshRendererOutline(meshRenderers[i]);

        SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedRenderers.Length; i++)
            AddSkinnedRendererOutline(skinnedRenderers[i]);
    }

    private void EnsureMaterial()
    {
        if (outlineMaterial != null)
            return;

        Shader outlineShader = Shader.Find("NoSafeExit/InteractionOutline");
        if (outlineShader == null)
        {
            Debug.LogWarning("InteractionTargetOutline: Outline shader was not found.", this);
            return;
        }

        outlineMaterial = new Material(outlineShader)
        {
            name = "Runtime Interaction Outline"
        };
        outlineMaterial.SetColor(OutlineColorId, outlineColor);
        outlineMaterial.SetFloat(OutlineWidthId, outlineWidth);
    }

    private void AddMeshRendererOutline(MeshRenderer sourceRenderer)
    {
        if (!IsValidSourceRenderer(sourceRenderer))
            return;

        MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            return;

        GameObject outlineObject = new GameObject($"{sourceRenderer.name}_Outline");
        outlineObject.transform.SetParent(sourceRenderer.transform, false);
        outlineObject.layer = sourceRenderer.gameObject.layer;

        MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
        outlineFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.allowOcclusionWhenDynamic = false;
        outlineRenderer.sharedMaterials = BuildMaterialArray(sourceRenderer.sharedMaterials.Length);

        outlineObjects.Add(outlineObject);
    }

    private void AddSkinnedRendererOutline(SkinnedMeshRenderer sourceRenderer)
    {
        if (!IsValidSourceRenderer(sourceRenderer) || sourceRenderer.sharedMesh == null)
            return;

        GameObject outlineObject = new GameObject($"{sourceRenderer.name}_Outline");
        outlineObject.transform.SetParent(sourceRenderer.transform, false);
        outlineObject.layer = sourceRenderer.gameObject.layer;

        SkinnedMeshRenderer outlineRenderer = outlineObject.AddComponent<SkinnedMeshRenderer>();
        outlineRenderer.sharedMesh = sourceRenderer.sharedMesh;
        outlineRenderer.bones = sourceRenderer.bones;
        outlineRenderer.rootBone = sourceRenderer.rootBone;
        outlineRenderer.localBounds = sourceRenderer.localBounds;
        outlineRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.allowOcclusionWhenDynamic = false;
        outlineRenderer.sharedMaterials = BuildMaterialArray(sourceRenderer.sharedMaterials.Length);

        outlineObjects.Add(outlineObject);
    }

    private bool IsValidSourceRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled)
            return false;

        if (renderer.GetComponentInParent<InteractionTargetOutline>() != this)
            return false;

        Transform current = renderer.transform;
        while (current != null)
        {
            if (current.name == OutlineContainerName || outlineObjects.Exists(item => item != null && item.transform == current))
                return false;

            current = current.parent;
        }

        return true;
    }

    private Material[] BuildMaterialArray(int count)
    {
        int materialCount = Mathf.Max(1, count);
        Material[] materials = new Material[materialCount];
        for (int i = 0; i < materialCount; i++)
            materials[i] = outlineMaterial;

        return materials;
    }

    private void ClearOutline()
    {
        for (int i = outlineObjects.Count - 1; i >= 0; i--)
        {
            if (outlineObjects[i] != null)
                Destroy(outlineObjects[i]);
        }

        outlineObjects.Clear();

        if (outlineContainer != null)
            Destroy(outlineContainer.gameObject);

        outlineContainer = null;
    }
}
