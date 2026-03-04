using UnityEngine;

public class WorldSpaceBillboard : MonoBehaviour
{
    [SerializeField] private bool lockX = false;
    [SerializeField] private bool lockY = false;
    [SerializeField] private bool lockZ = false;

    private Camera cachedCamera;

    private void LateUpdate()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main;
            if (cachedCamera == null)
            {
                return;
            }
        }

        Vector3 toCamera = cachedCamera.transform.position - transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        Vector3 euler = targetRotation.eulerAngles;

        Vector3 currentEuler = transform.eulerAngles;
        if (lockX) euler.x = currentEuler.x;
        if (lockY) euler.y = currentEuler.y;
        if (lockZ) euler.z = currentEuler.z;

        transform.rotation = Quaternion.Euler(euler);
    }
}
