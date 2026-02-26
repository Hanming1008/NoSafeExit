using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BulletProjectile : MonoBehaviour
{
    public float speed = 25f;
    public float lifeTime = 3f;
    public float damage = 20f;

    private Rigidbody rb;
    private Collider bulletCollider;
    private GameObject owner;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bulletCollider = GetComponent<Collider>();
    }

    void Start()
    {
        rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifeTime);
    }

    public void Initialize(GameObject ownerObject)
    {
        owner = ownerObject;
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

        Destroy(gameObject);
    }
}
