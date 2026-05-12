using JUTPS.WeaponSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponReferenceAutoBinder : MonoBehaviour
{
    [SerializeField] private Weapon weapon;
    [SerializeField] private bool bindOnAwake = true;
    [SerializeField] private string[] shootPointNames = { "Shoot_Position", "Shooting Position" };
    [SerializeField] private string[] oppositeHandNames = { "LeftHandIK", "Left Hand IK Position" };
    [SerializeField] private string gunSliderNameContains = "Slide";

    void Awake()
    {
        if (bindOnAwake)
            BindReferences();
    }

    void OnValidate()
    {
        ResolveWeapon();
        if (!Application.isPlaying)
            BindReferences();
    }

    [ContextMenu("Bind Weapon References")]
    public void BindReferences()
    {
        ResolveWeapon();
        if (weapon == null)
            return;

        weapon.Shoot_Position = ResolveAnchor(weapon.Shoot_Position, shootPointNames);
        weapon.OppositeHandPosition = ResolveAnchor(weapon.OppositeHandPosition, oppositeHandNames);
        weapon.GunSlider = ResolveContainingAnchor(weapon.GunSlider, gunSliderNameContains, true);

        if (weapon.GunSlider != null)
            weapon.SliderStartLocalPosition = weapon.GunSlider.localPosition;

        weapon.RefreshItemDependencies();
    }

    private void ResolveWeapon()
    {
        if (weapon == null)
            weapon = GetComponent<Weapon>();
    }

    private Transform ResolveAnchor(Transform current, string[] candidateNames)
    {
        if (IsValidChild(current))
            return current;

        if (candidateNames == null)
            return null;

        for (int i = 0; i < candidateNames.Length; i++)
        {
            Transform found = FindChildByName(candidateNames[i]);
            if (found != null)
                return found;
        }

        return current;
    }

    private Transform ResolveContainingAnchor(Transform current, string partialName, bool requireActive)
    {
        if (IsValidChild(current, requireActive))
            return current;

        return FindChildContaining(partialName, requireActive);
    }

    private bool IsValidChild(Transform target, bool requireActive = false)
    {
        return target != null &&
               target.IsChildOf(transform) &&
               (!requireActive || target.gameObject.activeInHierarchy);
    }

    private Transform FindChildByName(string childName)
    {
        if (string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private Transform FindChildContaining(string partialName, bool requireActive)
    {
        if (string.IsNullOrWhiteSpace(partialName))
            return null;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null &&
                (!requireActive || children[i].gameObject.activeInHierarchy) &&
                children[i].name.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return children[i];
        }

        return null;
    }
}
