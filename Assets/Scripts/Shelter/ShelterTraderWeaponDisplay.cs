using JUTPS.ItemSystem;
using UnityEngine;

public class ShelterTraderWeaponDisplay : MonoBehaviour
{
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private GameObject heldWeaponPrefab;
    [SerializeField] private WeaponGripProfile gripProfile;
    [SerializeField] private string heldWeaponName = "TraderHeldWeapon_HK416";
    [SerializeField] private bool combatReadyPose = true;
    [SerializeField, Range(0f, 1f)] private float leftHandIKWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float leftElbowHintWeight = 0.7f;

    private Transform rightHand;
    private Transform oppositeHandTarget;
    private GameObject heldWeaponInstance;
    private RuntimeAnimatorController appliedController;

    void Reset()
    {
        targetAnimator = GetComponent<Animator>();
    }

    void Awake()
    {
        EnsureDisplay();
    }

    void OnEnable()
    {
        EnsureDisplay();
    }

    void LateUpdate()
    {
        ApplyRifleIdleParameters();
    }

    void OnAnimatorIK(int layerIndex)
    {
        ApplyLeftHandIK();
    }

    public void EnsureDisplay()
    {
        if (targetAnimator == null)
            targetAnimator = GetComponent<Animator>();

        if (targetAnimator != null)
        {
            if (animatorController != null && targetAnimator.runtimeAnimatorController != animatorController)
            {
                targetAnimator.runtimeAnimatorController = animatorController;
                appliedController = animatorController;
                targetAnimator.Rebind();
            }
            else if (targetAnimator.runtimeAnimatorController != null && appliedController != targetAnimator.runtimeAnimatorController)
            {
                appliedController = targetAnimator.runtimeAnimatorController;
                targetAnimator.Rebind();
            }

            targetAnimator.applyRootMotion = false;
        }

        EnsureWeaponInstance();
        ApplyRifleIdleParameters();
    }

    private void EnsureWeaponInstance()
    {
        if (heldWeaponPrefab == null || targetAnimator == null)
            return;

        rightHand = targetAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand == null)
            rightHand = FindChildByName(transform, "Hand_R");
        if (rightHand == null)
            rightHand = FindChildByName(transform, "RightHand");
        if (rightHand == null)
            return;

        Transform existing = FindDirectChildByName(rightHand, heldWeaponName);
        heldWeaponInstance = existing != null ? existing.gameObject : Instantiate(heldWeaponPrefab, rightHand);
        heldWeaponInstance.name = heldWeaponName;
        heldWeaponInstance.SetActive(true);

        Transform weaponTransform = heldWeaponInstance.transform;
        weaponTransform.SetParent(rightHand, false);

        if (gripProfile != null)
        {
            weaponTransform.localPosition = gripProfile.weaponLocalPosition;
            weaponTransform.localRotation = Quaternion.Euler(gripProfile.weaponLocalEulerAngles);
            weaponTransform.localScale = gripProfile.weaponLocalScale;
            oppositeHandTarget = EnsureAnchor(weaponTransform, gripProfile.oppositeHandAnchorName, gripProfile.oppositeHandLocalPosition, gripProfile.oppositeHandLocalEulerAngles);
            EnsureAnchor(weaponTransform, gripProfile.shootPointName, gripProfile.shootPointLocalPosition, gripProfile.shootPointLocalEulerAngles);
        }
        else
        {
            weaponTransform.localPosition = new Vector3(0.165f, 0.027f, -0.04f);
            weaponTransform.localRotation = Quaternion.Euler(347.88754f, 98.85006f, 275.54523f);
            weaponTransform.localScale = Vector3.one;
            oppositeHandTarget = FindChildByName(weaponTransform, "LeftHandIK");
        }
    }

    private void ApplyRifleIdleParameters()
    {
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null)
            return;

        SetBoolIfExists("Moving", false);
        SetBoolIfExists("Running", false);
        SetBoolIfExists("Grounded", true);
        SetBoolIfExists("ItemEquiped", true);
        SetBoolIfExists("FireMode", combatReadyPose);
        SetFloatIfExists("Speed", 0f);
        SetFloatIfExists("Horizontal", 0f);
        SetFloatIfExists("Vertical", 0f);
        SetFloatIfExists("ItemWieldingRightHandPoseID", (int)JUHoldableItem.ItemHoldingPose.Rifle);
        SetIntegerIfExists("ItemsWieldingIdentifier", 1);
        ApplyUpperBodyLayerWeights();
    }

    private void ApplyUpperBodyLayerWeights()
    {
        if (targetAnimator == null || targetAnimator.layerCount <= 1)
            return;

        // Keep the trader's lower body neutral; only the upper body should be in the weapon pose.
        SetLayerWeightIfExists(1, 0f);
        SetLayerWeightIfExists(2, 0.8f);
        SetLayerWeightIfExists(3, 0f);
        SetLayerWeightIfExists(4, 1f);
        SetLayerWeightIfExists(5, 0f);
    }

    private void ApplyLeftHandIK()
    {
        if (targetAnimator == null || !targetAnimator.isHuman || oppositeHandTarget == null || !combatReadyPose)
            return;

        targetAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandIKWeight);
        targetAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftHandIKWeight);
        targetAnimator.SetIKPosition(AvatarIKGoal.LeftHand, oppositeHandTarget.position);
        targetAnimator.SetIKRotation(AvatarIKGoal.LeftHand, oppositeHandTarget.rotation);

        if (leftElbowHintWeight > 0f)
        {
            Vector3 hintOffset = gripProfile != null && gripProfile.overrideLeftElbowHint
                ? gripProfile.leftElbowHintOffset
                : new Vector3(-0.35f, -0.35f, 0.18f);
            Vector3 hintPosition = oppositeHandTarget.position + transform.TransformDirection(hintOffset);
            targetAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, leftElbowHintWeight);
            targetAnimator.SetIKHintPosition(AvatarIKHint.LeftElbow, hintPosition);
        }
    }

    private void SetLayerWeightIfExists(int layerIndex, float weight)
    {
        if (targetAnimator != null && layerIndex >= 0 && layerIndex < targetAnimator.layerCount)
            targetAnimator.SetLayerWeight(layerIndex, weight);
    }

    private void SetBoolIfExists(string parameterName, bool value)
    {
        if (HasParameter(parameterName, AnimatorControllerParameterType.Bool))
            targetAnimator.SetBool(parameterName, value);
    }

    private void SetFloatIfExists(string parameterName, float value)
    {
        if (HasParameter(parameterName, AnimatorControllerParameterType.Float))
            targetAnimator.SetFloat(parameterName, value);
    }

    private void SetIntegerIfExists(string parameterName, int value)
    {
        if (HasParameter(parameterName, AnimatorControllerParameterType.Int))
            targetAnimator.SetInteger(parameterName, value);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.name == parameterName && parameter.type == parameterType)
                return true;
        }

        return false;
    }

    private static Transform EnsureAnchor(Transform root, string anchorName, Vector3 localPosition, Vector3 localEulerAngles)
    {
        if (root == null || string.IsNullOrWhiteSpace(anchorName))
            return null;

        Transform anchor = FindChildByName(root, anchorName);
        if (anchor == null)
        {
            GameObject anchorObject = new GameObject(anchorName);
            anchor = anchorObject.transform;
            anchor.SetParent(root, false);
        }

        anchor.localPosition = localPosition;
        anchor.localRotation = Quaternion.Euler(localEulerAngles);
        return anchor;
    }

    private static Transform FindDirectChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.name == childName)
                return child;
        }

        return null;
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
}
