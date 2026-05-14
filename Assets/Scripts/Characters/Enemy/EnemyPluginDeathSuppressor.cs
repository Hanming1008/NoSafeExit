using JUTPS;
using JUTPS.InteractionSystem;
using JUTPS.InventorySystem;
using JUTPS.Utilities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPluginDeathSuppressor : MonoBehaviour
{
    private JUHealth health;
    private JUInventory inventory;

    private void Awake()
    {
        CacheReferences();
        DisablePluginDeathHelpers();
        DisablePluginInteraction();
        DisablePluginPickup();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (health != null)
        {
            health.OnDeath.RemoveListener(HandleDeath);
            health.OnDeath.AddListener(HandleDeath);
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath.RemoveListener(HandleDeath);
        }
    }

    private void LateUpdate()
    {
        if (health != null && health.IsDead)
        {
            SuppressPluginLootState();
        }
    }

    private void CacheReferences()
    {
        if (health == null)
        {
            health = GetComponent<JUHealth>();
        }

        if (inventory == null)
        {
            inventory = GetComponent<JUInventory>();
        }
    }

    private void DisablePluginDeathHelpers()
    {
        foreach (JUAutoDestroy autoDestroy in GetComponents<JUAutoDestroy>())
        {
            autoDestroy.enabled = false;
            Destroy(autoDestroy);
        }

        foreach (JUAutoInstantiate autoInstantiate in GetComponents<JUAutoInstantiate>())
        {
            autoInstantiate.enabled = false;
            Destroy(autoInstantiate);
        }
    }

    private void DisablePluginInteraction()
    {
        foreach (JUInteractionSystem interactionSystem in GetComponents<JUInteractionSystem>())
        {
            interactionSystem.UseDefaultInputs = false;
            interactionSystem.InteractionEnabled = false;
            interactionSystem.BlockInteractions = true;
            interactionSystem.enabled = false;
        }
    }

    private void DisablePluginPickup()
    {
        if (inventory == null)
        {
            return;
        }

        inventory.EnablePickup = false;
        inventory.UsePlayerInputs = false;
        inventory.UseDefaultInputToPickUp = false;
        inventory.AutoEquipPickedUpItems = false;
        inventory.CheckerRadius = 0f;
        inventory.ItemToPickUp = null;
    }

    private void HandleDeath()
    {
        DisablePluginDeathHelpers();
        DisablePluginInteraction();
        SuppressPluginLootState();
    }

    private void SuppressPluginLootState()
    {
        if (inventory == null)
        {
            return;
        }

        inventory.IsALoot = false;
        inventory.ItemToPickUp = null;
        inventory.enabled = false;
    }
}
