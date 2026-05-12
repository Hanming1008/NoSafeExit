using JUTPS.ItemSystem;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponGrip_", menuName = "NoSafeExit/Weapons/Grip Profile")]
public class WeaponGripProfile : ScriptableObject
{
    [Header("Grip Pose")]
    [Min(0)]
    public int itemWieldPositionID = 1;
    public JUHoldableItem.ItemHoldingPose holdPose = JUHoldableItem.ItemHoldingPose.Rifle;

    [Header("Weapon Root")]
    public Vector3 weaponLocalPosition;
    public Vector3 weaponLocalEulerAngles;
    public Vector3 weaponLocalScale = Vector3.one;

    [Header("Opposite Hand IK")]
    public bool useOppositeHandIK = true;
    public bool createOppositeHandIKIfMissing = true;
    public string oppositeHandAnchorName = "LeftHandIK";
    public Vector3 oppositeHandLocalPosition;
    public Vector3 oppositeHandLocalEulerAngles;
    [Range(0f, 1f)]
    public float leftElbowAdjustWeight = 0f;
    public bool overrideLeftElbowHint = false;
    public Vector3 leftElbowHintOffset = new Vector3(-2f, -3f, 1f);

    [Header("Shoot Point")]
    public bool createShootPointIfMissing = true;
    public string shootPointName = "Shoot_Position";
    public Vector3 shootPointLocalPosition = new Vector3(0f, 0.1f, 0.86f);
    public Vector3 shootPointLocalEulerAngles;

    [Header("Slider Reference")]
    public bool resolveGunSliderReference = true;
    public bool clearGunSliderWhenMissing = false;
    public string gunSliderNameContains = "Slide";

    void OnValidate()
    {
        if (weaponLocalScale == Vector3.zero)
            weaponLocalScale = Vector3.one;

        if (string.IsNullOrWhiteSpace(oppositeHandAnchorName))
            oppositeHandAnchorName = "LeftHandIK";

        if (string.IsNullOrWhiteSpace(shootPointName))
            shootPointName = "Shoot_Position";

        if (string.IsNullOrWhiteSpace(gunSliderNameContains))
            gunSliderNameContains = "Slide";
    }
}
