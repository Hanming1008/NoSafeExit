using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    public Transform muzzle;          // 枪口点
    public GameObject bulletPrefab;   // Bullet.prefab
    public float fireRate = 6f;       // 每秒几发

    // ✅ 枪声传播半径（建议 > 敌人的 chaseDistance）
    public float gunshotRadius = 25f;

    private float nextFireTime = 0f;

    void Update()
    {
        if (Input.GetMouseButton(0)) // 左键按住连发
        {
            TryFire();
        }
    }

    void TryFire()
    {
        if (Time.time < nextFireTime) return;
        if (muzzle == null || bulletPrefab == null) return;

        GameObject bullet = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
        BulletProjectile projectile = bullet.GetComponent<BulletProjectile>();
        if (projectile != null)
            projectile.Initialize(gameObject);

        // ✅ 发出枪声事件，让更远的敌人也能进入警戒
        GunshotSystem.Emit(muzzle.position, gunshotRadius);

        nextFireTime = Time.time + 1f / fireRate;
    }
}
