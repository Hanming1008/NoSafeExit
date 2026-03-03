using JUTPS.WeaponSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Weapon))]
public class WeaponGunshotBridge : MonoBehaviour
{
    [Min(0f)]
    public float gunshotRadius = 35f;

    public bool useShootPosition = true;

    [SerializeField]
    private Weapon weapon;

    private void Awake()
    {
        CacheWeapon();
    }

    private void OnEnable()
    {
        CacheWeapon();
        if (weapon != null)
            weapon.OnShot.AddListener(HandleShot);
    }

    private void OnDisable()
    {
        if (weapon != null)
            weapon.OnShot.RemoveListener(HandleShot);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheWeapon();
    }
#endif

    private void CacheWeapon()
    {
        if (weapon == null)
            weapon = GetComponent<Weapon>();
    }

    private void HandleShot()
    {
        Vector3 emitPosition = transform.position;

        if (useShootPosition && weapon != null && weapon.Shoot_Position != null)
            emitPosition = weapon.Shoot_Position.position;

        GunshotSystem.Emit(emitPosition, gunshotRadius);
    }
}
