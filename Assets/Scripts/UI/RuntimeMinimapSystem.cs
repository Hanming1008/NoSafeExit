using JUTPS;
using UnityEngine;

[DisallowMultipleComponent]
public class RuntimeMinimapSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followTarget;

    [Header("Camera")]
    [SerializeField] private float cameraHeight = 60f;
    [SerializeField] private float minimapOrthographicSize = 20f;
    [SerializeField] private float fullMapOrthographicSize = 55f;
    [SerializeField] private Color clearColor = new Color(0.06f, 0.08f, 0.10f, 1f);
    [SerializeField] private LayerMask cullingMask = ~0;

    [Header("Render")]
    [SerializeField] private int textureSize = 1024;

    private Camera minimapCamera;
    private RenderTexture mapTexture;
    private bool fullMapActive;

    public Texture MapTexture => mapTexture;
    public bool HasValidFeed => mapTexture != null && minimapCamera != null;
    public float TargetYaw => followTarget != null ? followTarget.eulerAngles.y : 0f;

    void Awake()
    {
        ResolveReferences();
        EnsureCameraAndTexture();
    }

    void OnValidate()
    {
        ResolveReferences();

        if (cameraHeight < 5f)
            cameraHeight = 5f;

        if (minimapOrthographicSize < 1f)
            minimapOrthographicSize = 1f;

        if (fullMapOrthographicSize < minimapOrthographicSize)
            fullMapOrthographicSize = minimapOrthographicSize;

        if (textureSize < 128)
            textureSize = 128;
    }

    void LateUpdate()
    {
        ResolveReferences();
        EnsureCameraAndTexture();
        UpdateCameraTransform();
    }

    void OnDestroy()
    {
        if (mapTexture != null)
        {
            mapTexture.Release();
            Destroy(mapTexture);
        }

        if (minimapCamera != null)
            Destroy(minimapCamera.gameObject);
    }

    public void SetFullMapActive(bool active)
    {
        fullMapActive = active;
        ApplyCameraSettings();
    }

    private void ResolveReferences()
    {
        if (followTarget != null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            followTarget = player.transform;
    }

    private void EnsureCameraAndTexture()
    {
        if (mapTexture == null || mapTexture.width != textureSize || mapTexture.height != textureSize)
            RebuildRenderTexture();

        if (minimapCamera == null)
        {
            GameObject cameraObject = new GameObject("Runtime Minimap Camera");
            cameraObject.transform.SetParent(transform, false);
            minimapCamera = cameraObject.AddComponent<Camera>();
            minimapCamera.enabled = true;
            minimapCamera.orthographic = true;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = clearColor;
            minimapCamera.cullingMask = cullingMask;
            minimapCamera.nearClipPlane = 0.1f;
            minimapCamera.farClipPlane = cameraHeight + 200f;
            minimapCamera.targetTexture = mapTexture;
            minimapCamera.gameObject.hideFlags = HideFlags.None;
            ApplyCameraSettings();
        }
        else if (minimapCamera.targetTexture != mapTexture)
        {
            minimapCamera.targetTexture = mapTexture;
        }
    }

    private void RebuildRenderTexture()
    {
        if (mapTexture != null)
        {
            mapTexture.Release();
            Destroy(mapTexture);
        }

        mapTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
        mapTexture.name = "RuntimeMinimapTexture";
        mapTexture.Create();

        if (minimapCamera != null)
            minimapCamera.targetTexture = mapTexture;
    }

    private void UpdateCameraTransform()
    {
        if (minimapCamera == null || followTarget == null)
            return;

        Vector3 targetPosition = followTarget.position;
        minimapCamera.transform.position = new Vector3(targetPosition.x, targetPosition.y + cameraHeight, targetPosition.z);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ApplyCameraSettings();
    }

    private void ApplyCameraSettings()
    {
        if (minimapCamera == null)
            return;

        minimapCamera.orthographicSize = fullMapActive
            ? fullMapOrthographicSize
            : minimapOrthographicSize;
        minimapCamera.backgroundColor = clearColor;
        minimapCamera.cullingMask = cullingMask;
    }
}
