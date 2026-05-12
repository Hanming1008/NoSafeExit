using JUTPS.WeaponSystem;
using UnityEngine;

public static class WeaponGripProfileApplier
{
    public static bool ApplyToWeapon(Weapon weapon, WeaponGripProfile profile)
    {
        if (weapon == null || profile == null)
            return false;

        Transform weaponTransform = weapon.transform;
        weaponTransform.localPosition = profile.weaponLocalPosition;
        weaponTransform.localRotation = Quaternion.Euler(profile.weaponLocalEulerAngles);
        weaponTransform.localScale = profile.weaponLocalScale;

        weapon.ItemWieldPositionID = Mathf.Max(0, profile.itemWieldPositionID);
        weapon.HoldPose = profile.holdPose;

        if (profile.useOppositeHandIK)
        {
            Transform oppositeHand = ResolveAnchor(
                weaponTransform,
                weapon.OppositeHandPosition,
                profile.oppositeHandAnchorName,
                profile.createOppositeHandIKIfMissing);

            if (oppositeHand != null)
            {
                oppositeHand.localPosition = profile.oppositeHandLocalPosition;
                oppositeHand.localRotation = Quaternion.Euler(profile.oppositeHandLocalEulerAngles);
                weapon.OppositeHandPosition = oppositeHand;
            }
        }
        else
        {
            weapon.OppositeHandPosition = null;
        }

        Transform shootPoint = ResolveAnchor(
            weaponTransform,
            weapon.Shoot_Position,
            profile.shootPointName,
            profile.createShootPointIfMissing);
        if (shootPoint != null)
        {
            shootPoint.localPosition = profile.shootPointLocalPosition;
            shootPoint.localRotation = Quaternion.Euler(profile.shootPointLocalEulerAngles);
            weapon.Shoot_Position = shootPoint;
        }

        if (profile.resolveGunSliderReference)
        {
            Transform gunSlider = FindChildContaining(weaponTransform, profile.gunSliderNameContains);
            if (gunSlider != null || profile.clearGunSliderWhenMissing)
                weapon.GunSlider = gunSlider;

            if (weapon.GunSlider != null)
                weapon.SliderStartLocalPosition = weapon.GunSlider.localPosition;
        }

        weapon.RefreshItemDependencies();
        return true;
    }

    private static Transform ResolveAnchor(
        Transform root,
        Transform existingAnchor,
        string anchorName,
        bool createIfMissing)
    {
        if (existingAnchor != null)
            return existingAnchor;

        Transform found = FindChildByName(root, anchorName);
        if (found != null || !createIfMissing)
            return found;

        GameObject anchorObject = new GameObject(anchorName);
        Transform anchorTransform = anchorObject.transform;
        anchorTransform.SetParent(root, false);
        return anchorTransform;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private static Transform FindChildContaining(Transform root, string partialName)
    {
        if (root == null || string.IsNullOrWhiteSpace(partialName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null &&
                children[i].name.IndexOf(partialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return children[i];
        }

        return null;
    }
}
