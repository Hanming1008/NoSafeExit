using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAnimatorBridge : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;
    public string speedParam = "Speed";
    public string movingParam = "IsMoving";
    public string sprintingParam = "IsSprinting";
    public string deadParam = "IsDead";
    public string shootTriggerParam = "Shoot";
    public float speedLerp = 12f;

    private CharacterController controller;
    private PlayerMove playerMove;
    private PlayerStats playerStats;

    private int speedHash;
    private int movingHash;
    private int sprintingHash;
    private int deadHash;
    private int shootTriggerHash;

    private bool hasSpeed;
    private bool hasMoving;
    private bool hasSprinting;
    private bool hasDead;
    private bool hasShootTrigger;

    private float speedValue;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerMove = GetComponent<PlayerMove>();
        playerStats = GetComponent<PlayerStats>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        CacheParameterAvailability();
    }

    void OnValidate()
    {
        speedLerp = Mathf.Max(0.01f, speedLerp);
    }

    void Update()
    {
        if (animator == null || controller == null)
            return;

        bool isDead = playerStats != null && !playerStats.IsAlive;

        float targetNormalizedSpeed;
        bool isMoving;
        bool isSprinting;

        if (playerMove != null)
        {
            targetNormalizedSpeed = isDead ? 0f : playerMove.CurrentNormalizedSpeed;
            isMoving = !isDead && playerMove.CurrentPlanarSpeed > 0.01f;
            isSprinting = !isDead && isMoving && playerMove.IsSprinting;
        }
        else
        {
            Vector3 planarVelocity = controller.velocity;
            planarVelocity.y = 0f;

            float moveSpeed = planarVelocity.magnitude;
            float refRunSpeed = 1f;
            targetNormalizedSpeed = isDead ? 0f : Mathf.Clamp01(moveSpeed / refRunSpeed);
            isMoving = !isDead && planarVelocity.sqrMagnitude > 0.01f;
            isSprinting = !isDead && isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        }

        speedValue = Mathf.Lerp(speedValue, targetNormalizedSpeed, speedLerp * Time.deltaTime);

        if (hasSpeed)
            animator.SetFloat(speedHash, speedValue);

        if (hasMoving)
            animator.SetBool(movingHash, isMoving);

        if (hasSprinting)
            animator.SetBool(sprintingHash, isSprinting);

        if (hasDead)
            animator.SetBool(deadHash, isDead);
    }

    public void TriggerShoot()
    {
        if (animator == null || !hasShootTrigger)
            return;

        if (playerStats != null && !playerStats.IsAlive)
            return;

        animator.SetTrigger(shootTriggerHash);
    }

    private void CacheParameterAvailability()
    {
        hasSpeed = false;
        hasMoving = false;
        hasSprinting = false;
        hasDead = false;
        hasShootTrigger = false;

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        speedHash = Animator.StringToHash(speedParam);
        movingHash = Animator.StringToHash(movingParam);
        sprintingHash = Animator.StringToHash(sprintingParam);
        deadHash = Animator.StringToHash(deadParam);
        shootTriggerHash = Animator.StringToHash(shootTriggerParam);

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == speedHash)
                hasSpeed = true;
            else if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == movingHash)
                hasMoving = true;
            else if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == sprintingHash)
                hasSprinting = true;
            else if (parameter.type == AnimatorControllerParameterType.Bool && parameter.nameHash == deadHash)
                hasDead = true;
            else if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == shootTriggerHash)
                hasShootTrigger = true;
        }
    }
}
