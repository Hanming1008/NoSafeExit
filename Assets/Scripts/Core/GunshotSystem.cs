using System;
using UnityEngine;

public static class GunshotSystem
{
    // pos = 枪声位置, radius = 传播半径
    public static Action<Vector3, float> OnGunshot;

    public static void Emit(Vector3 pos, float radius)
    {
        OnGunshot?.Invoke(pos, radius);
    }
}