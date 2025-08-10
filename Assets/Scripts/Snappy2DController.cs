using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Snappy2DController : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private bool allowDiagonal = true;

    [Header("Feel")]
    [Tooltip("If true: movement is instant (snappy). If false: velocity eases toward target using acceleration.")]
    [SerializeField] private bool instant = true;
    [Tooltip("When not instant: how fast velocity moves toward target. Higher = snappier.")]
    [SerializeField] private float acceleration = 50f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.5f;

    private Rigidbody2D rb;
    private Vector2 input;
    private Vector2 targetVelocity;
    private Vector2 velocity; // used when smoothing

    private bool isDashing;
    private float dashEndTime;
    private float nextDashTime;
    private Vector2 dashDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 0f;
    }

    private void Update()
    {
        // Handle dash input
        if (!isDashing && Time.time >= nextDashTime && Input.GetKeyDown(KeyCode.Space))
        {
            if (input != Vector2.zero) // dash only if moving
            {
                isDashing = true;
                dashDirection = input.normalized;
                dashEndTime = Time.time + dashDuration;
                nextDashTime = Time.time + dashCooldown;
            }
        }

        // Movement input
        if (!isDashing)
        {
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");

            if (!allowDiagonal)
            {
                if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                    input.y = 0f;
                else
                    input.x = 0f;
            }

            input = input.normalized;
            targetVelocity = input * moveSpeed;
        }
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashSpeed;

            if (Time.time >= dashEndTime)
            {
                isDashing = false;
            }
            return;
        }

        if (instant)
        {
            rb.linearVelocity = targetVelocity;
        }
        else
        {
            velocity = Vector2.MoveTowards(velocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            rb.linearVelocity = velocity;
        }
    }
}
