using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float runSpeed = 8f;
    public float gravity = -20f;

    private CharacterController controller;
    private PlayerStats stats;
    private Vector3 velocity;
    private bool isSprinting;

    public float CurrentPlanarSpeed { get; private set; }
    public float CurrentNormalizedSpeed { get; private set; }
    public bool IsSprinting => isSprinting;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
    }

    void Update()
    {
        if (stats != null && !stats.IsAlive) return;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(x, 0f, z).normalized;
        bool hasMoveInput = move.sqrMagnitude > 0.0001f;
        bool sprintInput = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool wantsSprint = hasMoveInput && sprintInput;

        float speed = moveSpeed;

        if (stats != null)
        {
            if (wantsSprint)
                isSprinting = stats.TrySprint(Time.deltaTime, isSprinting);
            else
                isSprinting = false;

            if (!isSprinting)
                stats.RecoverStamina(Time.deltaTime);
        }
        else
        {
            isSprinting = wantsSprint;
        }

        if (isSprinting)
            speed = runSpeed;

        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;

        Vector3 motion = move * speed;
        motion.y = velocity.y;
        controller.Move(motion * Time.deltaTime);

        CurrentPlanarSpeed = hasMoveInput ? speed : 0f;
        CurrentNormalizedSpeed = Mathf.Clamp01(CurrentPlanarSpeed / Mathf.Max(0.01f, runSpeed));
    }
}
