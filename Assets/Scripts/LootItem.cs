using UnityEngine;

public class LootItem : MonoBehaviour
{
    public int value = 1;

    private bool playerInRange = false;

    void Update()
    {
        if (!playerInRange) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (GameManager.Instance != null)
                GameManager.Instance.AddLoot(value);
            else
                Debug.LogWarning("No GameManager found!");

            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
        Debug.Log("Press F to pick up: " + gameObject.name);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }
}