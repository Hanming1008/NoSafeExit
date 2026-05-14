using UnityEngine;

public class ExtractionZoneTimer : MonoBehaviour
{
    public float requiredTime = 5f;
    public float currentTime = 0f;
    public bool lockPlayerMovementOnExtract = false;

    public ZoneFlasher flasher;

    private bool playerInside;
    private bool extracted;
    private float nextLogAt;
    private Collider playerColliderInside;
    private Transform playerTransformInside;

    void Awake()
    {
        if (flasher == null)
            flasher = GetComponentInChildren<ZoneFlasher>();
    }

    void Update()
    {
        if (extracted || !playerInside)
            return;

        currentTime += Time.deltaTime;

        if (currentTime >= nextLogAt)
        {
            Debug.Log($"Extracting... {currentTime:F1}/{requiredTime:F1}s");
            nextLogAt = currentTime + 0.5f;
        }

        if (currentTime >= requiredTime)
        {
            extracted = true;
            Debug.Log("Extraction success.");
            if (flasher != null)
                flasher.SetFlashing(false);

            playerInside = false;

            if (RaidFlowController.Instance != null)
                RaidFlowController.Instance.CompleteExtraction(playerTransformInside);
            else if (lockPlayerMovementOnExtract)
                LockPlayerMovement(playerColliderInside);

            ResetTimerState();
            return;
        }

        if (RaidFlowController.Instance != null)
            RaidFlowController.Instance.SetExtractionProgress(true, currentTime, requiredTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = true;
        playerColliderInside = other;
        playerTransformInside = ResolvePlayerTransform(other);
        Debug.Log("Entered extraction zone. Stay to extract.");

        if (flasher != null)
            flasher.SetFlashing(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = false;
        if (playerColliderInside == other)
            playerColliderInside = null;

        Transform exitingPlayer = ResolvePlayerTransform(other);
        if (playerTransformInside == exitingPlayer)
            playerTransformInside = null;

        ResetTimerState();
        Debug.Log("Left extraction zone. Timer reset.");

        if (flasher != null)
            flasher.SetFlashing(false);
    }

    private void ResetTimerState()
    {
        currentTime = 0f;
        nextLogAt = 0f;
        extracted = false;

        if (RaidFlowController.Instance != null)
            RaidFlowController.Instance.SetExtractionProgress(false, 0f, requiredTime);
    }

    private void LockPlayerMovement(Collider playerCollider)
    {
        if (playerCollider == null)
            return;

        PlayerMove playerMove = playerCollider.GetComponent<PlayerMove>();
        if (playerMove == null)
            playerMove = playerCollider.GetComponentInParent<PlayerMove>();

        if (playerMove != null)
            playerMove.enabled = false;
    }

    private Transform ResolvePlayerTransform(Collider playerCollider)
    {
        if (playerCollider == null)
            return null;

        PlayerMove playerMove = playerCollider.GetComponent<PlayerMove>();
        if (playerMove == null)
            playerMove = playerCollider.GetComponentInParent<PlayerMove>();
        if (playerMove != null)
            return playerMove.transform;

        PlayerStats playerStats = playerCollider.GetComponent<PlayerStats>();
        if (playerStats == null)
            playerStats = playerCollider.GetComponentInParent<PlayerStats>();
        if (playerStats != null)
            return playerStats.transform;

        return playerCollider.CompareTag("Player") ? playerCollider.transform : null;
    }
}
