using UnityEngine;

public class ExtractionZoneTimer : MonoBehaviour
{
    public float requiredTime = 5f;
    public float currentTime = 0f;

    public ZoneFlasher flasher;

    private bool playerInside = false;
    private bool extracted = false;
    private float nextLogAt = 0f;

    void Awake()
    {
        if (flasher == null)
            flasher = GetComponentInChildren<ZoneFlasher>();
    }

    void Update()
    {
        if (extracted) return;

        if (playerInside)
        {
            currentTime += Time.deltaTime;

            if (currentTime >= nextLogAt)
            {
                Debug.Log($"Extracting... {currentTime:F1}/{requiredTime:F1}s");
                nextLogAt = currentTime + 0.5f;
            }

            if (currentTime >= requiredTime)
            {
                extracted = true;
                Debug.Log("✅ Extraction SUCCESS!");
                if (flasher != null) flasher.SetFlashing(false);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = true;
        Debug.Log("Entered extraction zone. Stay to extract.");
        if (flasher != null) flasher.SetFlashing(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = false;

        // 出圈清零
        currentTime = 0f;
        nextLogAt = 0f;

        Debug.Log("Left extraction zone. Timer reset.");
        if (flasher != null) flasher.SetFlashing(false);
    }
}