using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    public Transform muzzle;
    public GameObject bulletPrefab;
    public float fireRate = 6f;

    [Header("VFX")]
    public GameObject muzzleFlashVfxPrefab;
    public float muzzleFlashLifeTime = 1.5f;
    public bool useProjectileVfx;
    public GameObject projectileVfxPrefab;
    public GameObject impactVfxPrefab;

    [Header("Shell Ejection")]
    public GameObject shellPrefab;
    public Transform shellEjectPoint;
    public float shellEjectForce = 2.6f;
    public float shellEjectUpForce = 1.6f;
    public float shellTorque = 8.0f;
    public float shellLifeTime = 2.5f;
    public float shellScale = 1.2f;
    public float shellDirectionJitter = 0.2f;
    public float shellSpawnRightOffset = 0.02f;
    public float shellSpawnUpOffset = 0.025f;
    public float shellSpawnBackOffset = 0.34f;

    public float gunshotRadius = 25f;

    private float nextFireTime;
    private PlayerAnimatorBridge animatorBridge;

    private void Awake()
    {
        animatorBridge = GetComponent<PlayerAnimatorBridge>();
    }

    private void Update()
    {
        if (Input.GetMouseButton(0))
            TryFire();
    }

    private void TryFire()
    {
        if (Time.time < nextFireTime) return;
        if (muzzle == null || bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
        BulletProjectile projectile = bullet.GetComponent<BulletProjectile>();
        if (projectile != null)
            projectile.Initialize(gameObject, useProjectileVfx ? projectileVfxPrefab : null, impactVfxPrefab);

        SpawnMuzzleFlash();
        SpawnShell();
        GunshotSystem.Emit(muzzle.position, gunshotRadius);
        if (animatorBridge != null)
            animatorBridge.TriggerShoot();

        nextFireTime = Time.time + 1f / fireRate;
    }

    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashVfxPrefab == null || muzzle == null) return;

        GameObject muzzleFx = Instantiate(muzzleFlashVfxPrefab, muzzle.position, muzzle.rotation);
        if (muzzleFlashLifeTime > 0f)
            Destroy(muzzleFx, muzzleFlashLifeTime);
    }

    private void SpawnShell()
    {
        if (shellPrefab == null || muzzle == null) return;

        Vector3 spawnPos;
        Quaternion spawnRot;
        Vector3 ejectDir;

        if (shellEjectPoint != null)
        {
            spawnPos = shellEjectPoint.position;
            spawnRot = shellEjectPoint.rotation;
            ejectDir = shellEjectPoint.right;
        }
        else
        {
            spawnPos = muzzle.position
                + muzzle.right * shellSpawnRightOffset
                + muzzle.up * shellSpawnUpOffset
                - muzzle.forward * shellSpawnBackOffset;
            spawnRot = muzzle.rotation * Quaternion.Euler(0f, 90f, 0f);
            ejectDir = muzzle.right;
        }

        GameObject shell = Instantiate(shellPrefab, spawnPos, spawnRot);
        if (shellScale > 0f)
            shell.transform.localScale *= shellScale;

        Rigidbody shellRb = EnsureShellPhysics(shell);
        IgnoreShellOwnerCollisions(shell);

        Vector3 randomizedDir = (ejectDir + Random.insideUnitSphere * shellDirectionJitter).normalized;
        Vector3 impulse = randomizedDir * shellEjectForce + Vector3.up * shellEjectUpForce - muzzle.forward * (shellEjectForce * 0.2f);
        shellRb.linearVelocity = Vector3.zero;
        shellRb.angularVelocity = Vector3.zero;
        shellRb.AddForce(impulse, ForceMode.VelocityChange);
        shellRb.AddTorque(Random.onUnitSphere * shellTorque, ForceMode.VelocityChange);

        if (shellLifeTime > 0f)
            Destroy(shell, shellLifeTime);
    }

    private Rigidbody EnsureShellPhysics(GameObject shell)
    {
        Rigidbody shellRb = shell.GetComponent<Rigidbody>();
        if (shellRb == null)
            shellRb = shell.AddComponent<Rigidbody>();

        if (shell.GetComponent<Collider>() == null)
        {
            CapsuleCollider collider = shell.AddComponent<CapsuleCollider>();
            collider.radius = 0.035f;
            collider.height = 0.12f;
            collider.direction = 2;
        }

        shellRb.isKinematic = false;
        shellRb.useGravity = true;
        shellRb.mass = 0.08f;
        shellRb.linearDamping = 0.05f;
        shellRb.angularDamping = 0.05f;
        shellRb.interpolation = RigidbodyInterpolation.Interpolate;
        shellRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        shellRb.constraints = RigidbodyConstraints.None;
        return shellRb;
    }

    private void IgnoreShellOwnerCollisions(GameObject shell)
    {
        Collider[] shellColliders = shell.GetComponentsInChildren<Collider>(true);
        if (shellColliders == null || shellColliders.Length == 0) return;

        Collider[] ownerColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < shellColliders.Length; i++)
        {
            Collider shellCollider = shellColliders[i];
            if (shellCollider == null) continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];
                if (ownerCollider == null) continue;
                Physics.IgnoreCollision(shellCollider, ownerCollider, true);
            }
        }
    }
}
