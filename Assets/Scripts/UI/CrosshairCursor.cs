using UnityEngine;

public class CrosshairCursor : MonoBehaviour
{
    public static CrosshairCursor Instance { get; private set; }

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

    [Header("Hit Feedback")]
    public bool enableHitFeedback = true;
    [Range(0.02f, 0.4f)] public float hitFeedbackDuration = 0.1f;
    public Color normalHitColor = new Color(1f, 0.92f, 0.2f, 1f);
    public Color criticalHitColor = new Color(1f, 0.2f, 0.2f, 1f);
    [Range(3, 16)] public int hitMarkerLength = 8;
    [Range(1, 8)] public int hitMarkerThickness = 2;
    [Range(0, 8)] public int hitMarkerGap = 2;

    private Texture2D generatedTexture;
    private Texture2D normalHitTexture;
    private Texture2D criticalHitTexture;
    private float hitFeedbackTimer;

    private void OnEnable()
    {
        Instance = this;
        ApplyCursor();
    }

    private void Start()
    {
        ApplyCursor();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        Cursor.SetCursor(null, Vector2.zero, cursorMode);
    }

    private void OnDestroy()
    {
        if (generatedTexture != null)
            Destroy(generatedTexture);
        if (normalHitTexture != null)
            Destroy(normalHitTexture);
        if (criticalHitTexture != null)
            Destroy(criticalHitTexture);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            ApplyCursor();
    }
#endif

    private void Update()
    {
        if (hitFeedbackTimer <= 0f)
            return;

        hitFeedbackTimer -= Time.deltaTime;
        if (hitFeedbackTimer <= 0f)
        {
            hitFeedbackTimer = 0f;
            ApplyCursor();
        }
    }

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

    public void TriggerHitFeedback(bool criticalHit)
    {
        if (!enableHitFeedback)
            return;

        EnsureHitFeedbackTextures();

        Texture2D hitTexture = criticalHit ? criticalHitTexture : normalHitTexture;
        if (hitTexture == null)
            return;

        if (unlockCursor)
            Cursor.lockState = CursorLockMode.None;

        if (forceVisible)
            Cursor.visible = true;

        Cursor.SetCursor(hitTexture, ResolveHotspot(hitTexture), cursorMode);
        hitFeedbackTimer = hitFeedbackDuration;
    }

    public static void ShowHitFeedback(bool criticalHit)
    {
        if (Instance == null)
        {
            Instance = FindObjectOfType<CrosshairCursor>();
        }

        if (Instance != null)
        {
            Instance.TriggerHitFeedback(criticalHit);
        }
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

    private void EnsureHitFeedbackTextures()
    {
        int size = Mathf.Clamp(fallbackSize, 8, 128);

        if (normalHitTexture == null || normalHitTexture.width != size || normalHitTexture.height != size)
        {
            if (normalHitTexture != null)
                Destroy(normalHitTexture);

            normalHitTexture = BuildHitMarkerTexture(size, normalHitColor, "HitMarker_Normal");
        }

        if (criticalHitTexture == null || criticalHitTexture.width != size || criticalHitTexture.height != size)
        {
            if (criticalHitTexture != null)
                Destroy(criticalHitTexture);

            criticalHitTexture = BuildHitMarkerTexture(size, criticalHitColor, "HitMarker_Critical");
        }
    }

    private Texture2D BuildHitMarkerTexture(int size, Color markerColor, string textureName)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = textureName;
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0f, 0f, 0f, 0f);

        int center = size / 2;
        int length = Mathf.Clamp(hitMarkerLength, 3, size / 2);
        int gap = Mathf.Clamp(hitMarkerGap, 0, size / 4);
        int thickness = Mathf.Clamp(hitMarkerThickness, 1, 8);
        int halfThick = thickness / 2;

        for (int i = 0; i < length; i++)
        {
            PaintThickPixel(pixels, size, center - gap - i, center + gap + i, markerColor, halfThick);
            PaintThickPixel(pixels, size, center + gap + i, center + gap + i, markerColor, halfThick);
            PaintThickPixel(pixels, size, center - gap - i, center - gap - i, markerColor, halfThick);
            PaintThickPixel(pixels, size, center + gap + i, center - gap - i, markerColor, halfThick);
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static void PaintThickPixel(Color[] pixels, int size, int x, int y, Color color, int halfThick)
    {
        for (int oy = -halfThick; oy <= halfThick; oy++)
        {
            int py = y + oy;
            if (py < 0 || py >= size) continue;

            for (int ox = -halfThick; ox <= halfThick; ox++)
            {
                int px = x + ox;
                if (px < 0 || px >= size) continue;

                pixels[py * size + px] = color;
            }
        }
    }
}
