using UnityEngine;

public class PlayerFaceMouse : MonoBehaviour
{
    public LayerMask groundMask;
    public float turnSpeed = 30f;
    public float maxRayDistance = 1000f;
    public bool snapToMouse = true;

    void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 targetPoint;
        bool hasPoint = Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundMask);

        if (hasPoint)
        {
            targetPoint = hit.point;
        }
        else
        {
            // Fallback: always intersect a horizontal plane at player height.
            Plane aimPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (!aimPlane.Raycast(ray, out float enter))
                return;

            targetPoint = ray.GetPoint(enter);
        }

        Vector3 dir = targetPoint - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        if (snapToMouse)
        {
            transform.rotation = targetRot;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }
}
