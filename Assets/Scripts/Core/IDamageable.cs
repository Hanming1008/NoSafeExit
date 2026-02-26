using UnityEngine;

public interface IDamageable
{
    bool IsAlive { get; }
    void TakeDamage(float amount, GameObject source);
}
