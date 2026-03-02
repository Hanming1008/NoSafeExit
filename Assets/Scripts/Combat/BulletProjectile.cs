using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BulletProjectile : MonoBehaviour
{
    [Header("Ballistics")]
    public float speed = 120f;
    public float lifeTime = 3f;
    public float damage = 20f;

    [Header("VFX")]
    public GameObject projectileVfxPrefab;
    public GameObject impactVfxPrefab;
    public float impactVfxLifeTime = 2f;
    public float impactSpawnNormalOffset = 0.01f;
    public float projectileVfxScale = 3.5f;
    public float projectileVfxWidthMultiplier = 3.0f;
    public bool hidePhysicalBulletMesh = true;
    public bool disableProjectileVfxScripts = true;
    public bool forceProjectileVfxLocalSimulation = true;

    [Header("Tracer")]
    public bool enableTracer = false;
    public float tracerTime = 0.08f;
    public float tracerStartWidth = 0.12f;
    public float tracerEndWidth = 0.01f;
    public Color tracerStartColor = new Color(1f, 0.95f, 0.75f, 1f);
    public Color tracerEndColor = new Color(1f, 0.45f, 0.1f, 0f);
    public float tracerMinVertexDistance = 0.01f;

    private Rigidbody rb;
    private Collider bulletCollider;
    private GameObject owner;
    private GameObject projectileVfxOverride;
    private GameObject impactVfxOverride;
    private Renderer[] bulletRenderers;
    private TrailRenderer tracer;
    private static Material tracerMaterial;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();
        bulletRenderers = GetComponentsInChildren<Renderer>(true);
        SetupTracer();
    }

    void Start()
    {
        rb.linearVelocity = transform.forward * speed;
        SpawnProjectileVfx();
        Destroy(gameObject, lifeTime);
    }

    public void Initialize(GameObject ownerObject)
    {
        Initialize(ownerObject, null, null);
    }

    public void Initialize(GameObject ownerObject, GameObject projectileVfxPrefabOverride, GameObject impactVfxPrefabOverride)
    {
        owner = ownerObject;
        projectileVfxOverride = projectileVfxPrefabOverride;
        impactVfxOverride = impactVfxPrefabOverride;
        RefreshPhysicalBulletVisibility();

        if (owner == null || bulletCollider == null) return;

        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            Collider c = ownerColliders[i];
            if (c != null)
                Physics.IgnoreCollision(bulletCollider, c, true);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
            damageable.TakeDamage(damage, owner);

        SpawnImpactVfx(collision);
        Destroy(gameObject);
    }

    private void SpawnProjectileVfx()
    {
        GameObject vfxPrefab = projectileVfxOverride != null ? projectileVfxOverride : projectileVfxPrefab;
        if (vfxPrefab == null)
        {
            RefreshPhysicalBulletVisibility();
            return;
        }

        GameObject projectileVfx = Instantiate(vfxPrefab, transform.position, transform.rotation, transform);
        if (projectileVfxScale > 0f && !Mathf.Approximately(projectileVfxScale, 1f))
            projectileVfx.transform.localScale *= projectileVfxScale;

        PrepareProjectileVfx(projectileVfx);
        RefreshPhysicalBulletVisibility();
    }

    private void SpawnImpactVfx(Collision collision)
    {
        GameObject vfxPrefab = impactVfxOverride != null ? impactVfxOverride : impactVfxPrefab;
        if (vfxPrefab == null) return;

        Vector3 hitPoint = transform.position;
        Vector3 hitNormal = -transform.forward;

        if (collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            hitPoint = contact.point;
            hitNormal = contact.normal;
        }

        if (hitNormal.sqrMagnitude < 0.0001f)
            hitNormal = Vector3.up;

        Quaternion vfxRotation = Quaternion.LookRotation(hitNormal);
        GameObject impactVfx = Instantiate(vfxPrefab, hitPoint + hitNormal * impactSpawnNormalOffset, vfxRotation);

        if (impactVfxLifeTime > 0f)
            Destroy(impactVfx, impactVfxLifeTime);
    }

    private void RefreshPhysicalBulletVisibility()
    {
        if (bulletRenderers == null) return;

        bool hasVisualProjectile = projectileVfxOverride != null || projectileVfxPrefab != null;
        bool showPhysicalMesh = !(hidePhysicalBulletMesh && hasVisualProjectile);

        for (int i = 0; i < bulletRenderers.Length; i++)
        {
            if (bulletRenderers[i] != null)
                bulletRenderers[i].enabled = showPhysicalMesh;
        }
    }

    private void PrepareProjectileVfx(GameObject vfxRoot)
    {
        if (vfxRoot == null) return;

        Rigidbody[] rigidbodies = vfxRoot.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (body == null) continue;

            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
        }

        Collider[] colliders = vfxRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Renderer[] renderers = vfxRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        if (disableProjectileVfxScripts)
        {
            MonoBehaviour[] scripts = vfxRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < scripts.Length; i++)
            {
                if (scripts[i] != null)
                    scripts[i].enabled = false;
            }
        }

        if (forceProjectileVfxLocalSimulation)
        {
            ParticleSystem[] systems = vfxRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                if (!Mathf.Approximately(projectileVfxWidthMultiplier, 1f))
                    main.startSizeMultiplier *= projectileVfxWidthMultiplier;
            }
        }

        if (!Mathf.Approximately(projectileVfxWidthMultiplier, 1f))
        {
            TrailRenderer[] trailRenderers = vfxRoot.GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trailRenderers.Length; i++)
            {
                TrailRenderer trail = trailRenderers[i];
                if (trail == null) continue;

                trail.startWidth *= projectileVfxWidthMultiplier;
                trail.endWidth *= projectileVfxWidthMultiplier;
                trail.widthMultiplier *= projectileVfxWidthMultiplier;
            }

            LineRenderer[] lineRenderers = vfxRoot.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lineRenderers.Length; i++)
            {
                LineRenderer line = lineRenderers[i];
                if (line == null) continue;

                line.startWidth *= projectileVfxWidthMultiplier;
                line.endWidth *= projectileVfxWidthMultiplier;
                line.widthMultiplier *= projectileVfxWidthMultiplier;
            }
        }
    }

    private void SetupTracer()
    {
        tracer = GetComponent<TrailRenderer>();
        if (!enableTracer)
        {
            if (tracer != null)
                tracer.enabled = false;
            return;
        }

        if (tracer == null)
            tracer = gameObject.AddComponent<TrailRenderer>();

        tracer.enabled = true;
        tracer.time = Mathf.Max(0.01f, tracerTime);
        tracer.startWidth = Mathf.Max(0.001f, tracerStartWidth);
        tracer.endWidth = Mathf.Max(0f, tracerEndWidth);
        tracer.minVertexDistance = Mathf.Max(0.001f, tracerMinVertexDistance);
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.receiveShadows = false;
        tracer.alignment = LineAlignment.View;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(tracerStartColor, 0f),
                new GradientColorKey(tracerEndColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(tracerStartColor.a, 0f),
                new GradientAlphaKey(tracerEndColor.a, 1f)
            });
        tracer.colorGradient = gradient;

        if (tracerMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                tracerMaterial = new Material(shader);
                tracerMaterial.name = "BulletTracerRuntimeMaterial";
            }
        }

        if (tracerMaterial != null)
            tracer.sharedMaterial = tracerMaterial;
    }
}
