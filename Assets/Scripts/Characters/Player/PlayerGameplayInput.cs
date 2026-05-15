using JUTPS;
using JUTPS.InteractionSystem;
using JUTPS.JUInputSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerGameplayInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private JUPlayerCharacterInputAsset inputAsset;

    [Header("Menus")]
    [SerializeField] private KeyCode toggleInventoryKey = KeyCode.B;
    [SerializeField] private KeyCode toggleMapKey = KeyCode.M;
    [SerializeField] private KeyCode closeMenuKey = KeyCode.Escape;

    [Header("Weapons")]
    [SerializeField] private KeyCode primaryWeaponKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode secondaryWeaponKey = KeyCode.Alpha2;

    [Header("Quickbar")]
    [SerializeField] private KeyCode quickbarSlot1Key = KeyCode.Alpha3;
    [SerializeField] private KeyCode quickbarSlot2Key = KeyCode.Alpha4;
    [SerializeField] private KeyCode quickbarSlot3Key = KeyCode.Alpha5;
    [SerializeField] private KeyCode quickbarSlot4Key = KeyCode.Alpha6;
    [SerializeField] private KeyCode quickbarSlot5Key = KeyCode.Alpha7;
    [SerializeField] private KeyCode quickbarSlot6Key = KeyCode.Alpha8;

    [Header("Fallback")]
    [SerializeField] private KeyCode interactFallbackKey = KeyCode.F;

    public JUPlayerCharacterInputAsset InputAsset => inputAsset;

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        ResolveReferences();
    }

    public bool IsInventoryTogglePressed()
    {
        return Input.GetKeyDown(toggleInventoryKey);
    }

    public bool IsMapTogglePressed()
    {
        return Input.GetKeyDown(toggleMapKey);
    }

    public bool IsClosePressed()
    {
        return Input.GetKeyDown(closeMenuKey);
    }

    public bool IsInteractPressed()
    {
        if (inputAsset != null)
            return inputAsset.IsInteractTriggered;

        return Input.GetKeyDown(interactFallbackKey);
    }

    public bool IsPrimaryWeaponPressed()
    {
        return Input.GetKeyDown(primaryWeaponKey);
    }

    public bool IsSecondaryWeaponPressed()
    {
        return Input.GetKeyDown(secondaryWeaponKey);
    }

    public int GetTriggeredQuickbarIndex()
    {
        // Quick-use hotbar is disabled. Number keys remain free for future input mapping.
        return -1;
    }

    public string GetQuickbarLabel(int index)
    {
        return index switch
        {
            0 => "3",
            1 => "4",
            2 => "5",
            3 => "6",
            4 => "7",
            5 => "8",
            _ => string.Empty
        };
    }

    private void ResolveReferences()
    {
        if (inputAsset != null)
            return;

        JUCharacterController characterController = GetComponent<JUCharacterController>();
        if (characterController != null && characterController.Inputs != null)
        {
            inputAsset = characterController.Inputs;
            return;
        }

        JUInteractionSystem interactionSystem = GetComponent<JUInteractionSystem>();
        if (interactionSystem != null && interactionSystem.Inputs != null)
            inputAsset = interactionSystem.Inputs;
    }
}
