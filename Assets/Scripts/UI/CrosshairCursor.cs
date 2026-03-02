using UnityEngine;

public class CrosshairCursor : MonoBehaviour
{
    [Header("Cursor")]
    public Texture2D crosshairTexture;
    public Vector2 hotspot = new Vector2(-1f, -1f); // (-1, -1) means texture center.
    public CursorMode cursorMode = CursorMode.Auto;
    public bool forceVisible = true;
    public bool unlockCursor = true;

    [Header("Generated Fallback")]
    public bool useGeneratedFallback = true;
    [Range(8, 128)] public int fallbackSize = 32;
    [Range(1, 8)] public int lineThickness = 2;
    public Color fallbackColor = new Color(1f, 1f, 1f, 0.95f);

    private Texture2D generatedTexture;

    private void OnEnable()
    {
        ApplyCursor();
    }

    private void Start()
    {
        ApplyCursor();
    }

    private void OnDisable()
    {
        Cursor.SetCursor(null, Vector2.zero, cursorMode);
    }

    private void OnDestroy()
    {
        if (generatedTexture != null)
            Destroy(generatedTexture);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            ApplyCursor();
    }
#endif

    [ContextMenu("Apply Cursor")]
    public void ApplyCursor()
    {
        Texture2D textureToUse = crosshairTexture;

        if (textureToUse == null && useGeneratedFallback)
        {
            EnsureGeneratedTexture();
            textureToUse = generatedTexture;
        }

        if (unlockCursor)
            Cursor.lockState = CursorLockMode.None;

        if (forceVisible)
            Cursor.visible = true;

        Cursor.SetCursor(textureToUse, ResolveHotspot(textureToUse), cursorMode);
    }

    private Vector2 ResolveHotspot(Texture2D tex)
    {
        if (tex == null)
            return Vector2.zero;

        if (hotspot.x < 0f || hotspot.y < 0f)
            return new Vector2(tex.width * 0.5f, tex.height * 0.5f);

        return hotspot;
    }

    private void EnsureGeneratedTexture()
    {
        int size = Mathf.Clamp(fallbackSize, 8, 128);

        if (generatedTexture != null && generatedTexture.width == size && generatedTexture.height == size)
            return;

        if (generatedTexture != null)
            Destroy(generatedTexture);

        generatedTexture = BuildGeneratedCrosshair(size);
    }

    private Texture2D BuildGeneratedCrosshair(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "GeneratedCrosshair";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0f, 0f, 0f, 0f);

        int center = size / 2;
        int thickness = Mathf.Clamp(lineThickness, 1, 8);
        int halfThick = thickness / 2;
        int gap = Mathf.Max(2, size / 10);

        for (int y = 0; y < size; y++)
        {
            if (Mathf.Abs(y - center) <= gap)
                continue;

            for (int t = -halfThick; t <= halfThick; t++)
            {
                int x = center + t;
                if (x >= 0 && x < size)
                    pixels[y * size + x] = fallbackColor;
            }
        }

        for (int x = 0; x < size; x++)
        {
            if (Mathf.Abs(x - center) <= gap)
                continue;

            for (int t = -halfThick; t <= halfThick; t++)
            {
                int y = center + t;
                if (y >= 0 && y < size)
                    pixels[y * size + x] = fallbackColor;
            }
        }

        for (int y = -halfThick; y <= halfThick; y++)
        {
            for (int x = -halfThick; x <= halfThick; x++)
            {
                int px = center + x;
                int py = center + y;
                if (px >= 0 && px < size && py >= 0 && py < size)
                    pixels[py * size + px] = fallbackColor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }
}
