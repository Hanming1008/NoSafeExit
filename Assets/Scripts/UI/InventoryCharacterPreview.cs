using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InventoryCharacterPreview : MonoBehaviour
{
    [Header("Preview")]
    [SerializeField] private int previewLayer = 30;
    [SerializeField] private Vector3 cameraLocalOffset = new Vector3(-0.18f, 1.16f, 6.8f);
    [SerializeField] private Vector3 lookAtLocalOffset = new Vector3(0f, 0.9f, 0f);
    [SerializeField] private Vector2Int textureSize = new Vector2Int(768, 1024);

    private Transform sourceRoot;
    private RawImage targetImage;
    private Text fallbackText;
    private RenderTexture previewTexture;
    private Camera previewCamera;
    private Light keyLight;
    private Light fillLight;
    private Camera mainCamera;
    private int originalMainCameraMask;
    private bool mainCameraMaskCaptured;
    private bool previewActive;

    private readonly Dictionary<GameObject, int> originalRendererLayers = new Dictionary<GameObject, int>();

    public void Configure(Transform source, RawImage image, Text fallback)
    {
        sourceRoot = source;
        targetImage = image;
        fallbackText = fallback;

        EnsurePreviewResources();
        UpdateTargetImageState();
    }

    public void SetPreviewActive(bool active)
    {
        if (previewActive == active)
        {
            UpdateTargetImageState();
            return;
        }

        previewActive = active;

        if (previewActive)
        {
            EnsurePreviewResources();
            CacheSourceRendererLayers();
            ApplyPreviewLayerToSourceRenderers();
            EnsureMainCameraMaskIncludesPreviewLayer();
        }
        else
        {
            RestoreSourceRendererLayers();
            RestoreMainCameraMask();
        }

        if (previewCamera != null)
            previewCamera.enabled = previewActive;

        if (keyLight != null)
            keyLight.enabled = previewActive;

        if (fillLight != null)
            fillLight.enabled = previewActive;

        UpdateTargetImageState();
    }

    void LateUpdate()
    {
        if (!previewActive || sourceRoot == null || targetImage == null)
            return;

        EnsurePreviewResources();
        CacheSourceRendererLayers();
        ApplyPreviewLayerToSourceRenderers();
        EnsureMainCameraMaskIncludesPreviewLayer();
        UpdatePreviewCamera();
        UpdateTargetImageState();
    }

    void OnDestroy()
    {
        RestoreSourceRendererLayers();
        RestoreMainCameraMask();
        CleanupPreviewResources();
    }

    private void EnsurePreviewResources()
    {
        if (previewTexture == null)
        {
            previewTexture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2,
                name = "InventoryCharacterPreviewRT"
            };
            previewTexture.Create();
        }

        if (previewCamera == null)
        {
            GameObject cameraObject = new GameObject("InventoryPreviewCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.cullingMask = 1 << previewLayer;
            previewCamera.fieldOfView = 18f;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 50f;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;
            previewCamera.targetTexture = previewTexture;
            previewCamera.enabled = previewActive;
        }

        if (keyLight == null)
            keyLight = CreatePreviewLight("InventoryPreviewKeyLight", new Vector3(42f, 142f, 0f), 0.9f, new Color(0.86f, 0.88f, 0.94f));

        if (fillLight == null)
            fillLight = CreatePreviewLight("InventoryPreviewFillLight", new Vector3(320f, 300f, 0f), 0.22f, new Color(0.54f, 0.58f, 0.66f));

        if (targetImage != null)
            targetImage.texture = previewTexture;
    }

    private void CacheSourceRendererLayers()
    {
        if (sourceRoot == null)
            return;

        Renderer[] renderers = sourceRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            GameObject rendererObject = renderer.gameObject;
            if (!originalRendererLayers.ContainsKey(rendererObject))
                originalRendererLayers.Add(rendererObject, rendererObject.layer);
        }
    }

    private void ApplyPreviewLayerToSourceRenderers()
    {
        foreach (KeyValuePair<GameObject, int> entry in originalRendererLayers)
        {
            if (entry.Key == null)
                continue;

            if (entry.Key.layer != previewLayer)
                entry.Key.layer = previewLayer;
        }
    }

    private void RestoreSourceRendererLayers()
    {
        foreach (KeyValuePair<GameObject, int> entry in originalRendererLayers)
        {
            if (entry.Key == null)
                continue;

            entry.Key.layer = entry.Value;
        }

        originalRendererLayers.Clear();
    }

    private void EnsureMainCameraMaskIncludesPreviewLayer()
    {
        Camera currentMainCamera = Camera.main;
        if (currentMainCamera == null)
            return;

        if (mainCamera != currentMainCamera)
        {
            RestoreMainCameraMask();
            mainCamera = currentMainCamera;
        }

        if (!mainCameraMaskCaptured)
        {
            originalMainCameraMask = mainCamera.cullingMask;
            mainCameraMaskCaptured = true;
        }

        int previewMask = 1 << previewLayer;
        if ((mainCamera.cullingMask & previewMask) == 0)
            mainCamera.cullingMask |= previewMask;
    }

    private void RestoreMainCameraMask()
    {
        if (!mainCameraMaskCaptured || mainCamera == null)
            return;

        mainCamera.cullingMask = originalMainCameraMask;
        mainCameraMaskCaptured = false;
        mainCamera = null;
    }

    private void UpdatePreviewCamera()
    {
        if (previewCamera == null || sourceRoot == null)
            return;

        Vector3 focusPoint = sourceRoot.TransformPoint(lookAtLocalOffset);
        previewCamera.transform.position = sourceRoot.TransformPoint(cameraLocalOffset);
        previewCamera.transform.LookAt(focusPoint);

        if (keyLight != null)
            keyLight.transform.position = focusPoint;

        if (fillLight != null)
            fillLight.transform.position = focusPoint;
    }

    private void UpdateTargetImageState()
    {
        if (targetImage != null)
        {
            targetImage.enabled = previewActive && previewTexture != null;
            targetImage.color = previewActive ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (fallbackText != null)
            fallbackText.enabled = !previewActive || previewTexture == null;
    }

    private void CleanupPreviewResources()
    {
        if (previewCamera != null)
            DestroyImmediate(previewCamera.gameObject);
        if (keyLight != null)
            DestroyImmediate(keyLight.gameObject);
        if (fillLight != null)
            DestroyImmediate(fillLight.gameObject);

        if (previewTexture != null)
        {
            previewTexture.Release();
            DestroyImmediate(previewTexture);
        }

        previewCamera = null;
        keyLight = null;
        fillLight = null;
        previewTexture = null;
    }

    private Light CreatePreviewLight(string lightName, Vector3 eulerAngles, float intensity, Color color)
    {
        GameObject lightObject = new GameObject(lightName);
        lightObject.hideFlags = HideFlags.HideAndDontSave;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << previewLayer;
        light.enabled = previewActive;
        lightObject.transform.rotation = Quaternion.Euler(eulerAngles);
        return light;
    }
}
