using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    public Transform muzzle;          // 枪口点
    public GameObject bulletPrefab;   // Bullet.prefab
    public float fireRate = 6f;       // 每秒几发

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

        Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
        nextFireTime = Time.time + 1f / fireRate;
    }
}