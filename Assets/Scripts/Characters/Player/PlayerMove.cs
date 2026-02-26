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

        controller.Move(move * speed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
