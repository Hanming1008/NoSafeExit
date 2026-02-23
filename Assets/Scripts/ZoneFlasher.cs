using UnityEngine;

public class ZoneFlasher : MonoBehaviour
{
    public Renderer zoneRenderer;   // ZoneVisual 的 Renderer
    public float flashSpeed = 4f;   // 闪烁速度
    public float minAlpha = 0.15f;  // 最暗透明度
    public float maxAlpha = 0.55f;  // 最亮透明度

    private bool flashing = false;
    private Material runtimeMat;
    private Color baseColor;

    void Awake()
    {
        if (zoneRenderer == null)
            zoneRenderer = GetComponent<Renderer>();

        // 用实例材质（避免改到共享材质）
        runtimeMat = zoneRenderer.material;
        baseColor = runtimeMat.color;

        // 默认不闪：先把透明度设为 maxAlpha
        SetFlashing(false);
    }

    void Update()
    {
        if (!flashing) return;

        float t = (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f; // 0~1
        float a = Mathf.Lerp(minAlpha, maxAlpha, t);

        Color c = baseColor;
        c.a = a;
        runtimeMat.color = c;
    }

    public void SetFlashing(bool on)
    {
        flashing = on;

        // 不闪时固定显示（maxAlpha）
        Color c = baseColor;
        c.a = maxAlpha;
        runtimeMat.color = c;
    }
}