using JUTPS.CameraSystems;
using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class ShelterIndoorCameraZone : MonoBehaviour
{
    [Header("Activation")]
    [Min(0.5f)] [SerializeField] private float activationRadius = 9f;
    [SerializeField] private Vector3 playerCheckOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Indoor Camera State")]
    [SerializeField] private float distance = 4.2f;
    [SerializeField] private float movementSpeed = 28f;
    [SerializeField] private float fieldOfView = 62f;
    [SerializeField] private float pivotHeight = 1.35f;
    [SerializeField] private float cameraHeightOffset = 0.45f;
    [SerializeField] private float cameraPitch = 17f;
    [SerializeField] private bool useFixedZoneYaw = true;
    [SerializeField] private float fixedYawOffsetDegrees;
    [SerializeField] private float yawOffsetDegrees = 0f;
    [SerializeField] private float stateBlendSpeed = 14f;

    private readonly CameraState indoorCameraState = new CameraState("Shelter Indoor Camera");

    private Transform player;
    private TDCameraController topDownCamera;
    private bool playerInside;
    private bool wasInside;
    private bool savedCameraState;
    private Quaternion savedCameraRotation;
    private float savedRotX;
    private float savedRotY;
    private float savedRotXTarget;
    private float savedRotYTarget;

    public bool PlayerInside => playerInside;

    void OnValidate()
    {
        activationRadius = Mathf.Max(0.5f, activationRadius);
        distance = Mathf.Max(0.5f, distance);
        movementSpeed = Mathf.Max(0.1f, movementSpeed);
        fieldOfView = Mathf.Clamp(fieldOfView, 20f, 100f);
        pivotHeight = Mathf.Max(0f, pivotHeight);
        stateBlendSpeed = Mathf.Max(0.01f, stateBlendSpeed);
    }

    void Update()
    {
        ResolveReferences();
        playerInside = IsPlayerWithinRange();

        if (playerInside && topDownCamera != null && player != null)
        {
            SaveCameraRotationIfNeeded();
            ApplyIndoorCameraState();
        }
        else if (wasInside && topDownCamera != null)
        {
            RestoreOutdoorCameraState();
        }

        wasInside = playerInside;
    }

    void OnDisable()
    {
        if (topDownCamera != null)
            RestoreOutdoorCameraState();

        wasInside = false;
        playerInside = false;
    }

    private void ApplyIndoorCameraState()
    {
        ConfigureIndoorState();
        topDownCamera.IsTransitioningToCustomState = true;
        topDownCamera.SetCameraStateTransition(topDownCamera.GetCurrentCameraState, indoorCameraState, stateBlendSpeed);

        float targetYaw = useFixedZoneYaw
            ? transform.eulerAngles.y + fixedYawOffsetDegrees
            : player.eulerAngles.y + yawOffsetDegrees;
        Quaternion targetRotation = Quaternion.Euler(cameraPitch, targetYaw, 0f);
        topDownCamera.transform.rotation = targetRotation;
        topDownCamera.SetCameraRotation(cameraPitch, targetYaw, false);
    }

    private void SaveCameraRotationIfNeeded()
    {
        if (savedCameraState || topDownCamera == null)
            return;

        savedCameraRotation = topDownCamera.transform.rotation;
        savedRotX = topDownCamera.rotX;
        savedRotY = topDownCamera.rotY;
        savedRotXTarget = topDownCamera.rotxtarget;
        savedRotYTarget = topDownCamera.rotytarget;
        savedCameraState = true;
    }

    private void RestoreOutdoorCameraState()
    {
        if (topDownCamera == null)
            return;

        topDownCamera.DisableCustomStateTransitioningState();

        if (savedCameraState)
        {
            topDownCamera.transform.rotation = savedCameraRotation;
            topDownCamera.rotX = savedRotX;
            topDownCamera.rotY = savedRotY;
            topDownCamera.rotxtarget = savedRotXTarget;
            topDownCamera.rotytarget = savedRotYTarget;
            savedCameraState = false;
        }
    }

    private void ConfigureIndoorState()
    {
        indoorCameraState.Distance = distance;
        indoorCameraState.MovementSpeed = movementSpeed;
        indoorCameraState.CameraFieldOfView = fieldOfView;
        indoorCameraState.UpTargetOffset = pivotHeight;
        indoorCameraState.RightTargetOffset = 0f;
        indoorCameraState.ForwardTargetOffset = 0f;
        indoorCameraState.RightCameraOffset = 0f;
        indoorCameraState.UpCameraOffset = cameraHeightOffset;
        indoorCameraState.ForwardCameraOffset = 0f;
        indoorCameraState.RotationSensibility = 0f;
        indoorCameraState.VerticalRotationSensibility = 0f;
        indoorCameraState.MinRotation = cameraPitch;
        indoorCameraState.MaxRotation = cameraPitch;
    }

    private bool IsPlayerWithinRange()
    {
        if (player == null)
            return false;

        Vector3 center = player.TransformPoint(playerCheckOffset);
        float distanceSqr = (transform.position - center).sqrMagnitude;
        return distanceSqr <= activationRadius * activationRadius;
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                player = taggedPlayer.transform;
        }

        if (player == null)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Exclude);
            if (stats != null)
                player = stats.transform;
        }

        if (topDownCamera == null)
            topDownCamera = FindFirstObjectByType<TDCameraController>(FindObjectsInactive.Exclude);
    }
}
