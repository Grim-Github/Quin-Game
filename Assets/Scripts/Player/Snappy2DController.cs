using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class Snappy2DController : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private bool allowDiagonal = true;

    [Header("Feel")]
    [Tooltip("If true: movement is instant (snappy). If false: velocity eases toward target using acceleration. Higher = snappier.")]
    [SerializeField] private bool instant = true;
    [Tooltip("When not instant: how fast velocity moves toward target. Higher = snappier.")]
    [SerializeField] private float acceleration = 50f;

    [Header("UI")]
    [SerializeField] private Slider dashSlider;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.5f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("If true, flips the sprite in the opposite direction.")]
    [SerializeField] private bool invertSpriteFlip = false;
    [SerializeField] private AudioClip dashClip;

    private Rigidbody2D rb;
    private Vector2 input;
    private Vector2 targetVelocity;
    private Vector2 velocity; // used when smoothing

    private bool isDashing;
    private float dashEndTime;
    private float nextDashTime;
    private Vector2 dashDirection;
    private AudioSource playerSource;

    public float MoveSpeed => moveSpeed;
    public float DashSpeed => dashSpeed;
    public float DashDuration => dashDuration;
    public float DashCooldown => dashCooldown;

    private void Awake()

    {
        playerSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 0f;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        // Handle dash input
        if (!isDashing && Time.time >= nextDashTime && Input.GetKeyDown(KeyCode.Space))
        {
            if (input != Vector2.zero) // dash only if moving
            {
                if (playerSource != null)
                {
                    playerSource.PlayOneShot(dashClip);
                }
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

        // Flip sprite based on horizontal movement
        if (spriteRenderer != null && input.x != 0)
        {
            bool flip = input.x < 0;
            if (invertSpriteFlip) flip = !flip;
            spriteRenderer.flipX = flip;
        }
    }

    public void IncreaseMoveSpeed(float amount)
    {
        if (amount == 0f) return;
        moveSpeed = Mathf.Max(0f, moveSpeed + amount);
    }

    public void IncreaseDashSpeed(float amount)
    {
        if (amount == 0f) return;
        dashSpeed = Mathf.Max(0f, dashSpeed + amount);
    }

    public void IncreaseDashCooldown(float amount)
    {
        if (amount == 0f) return;
        dashCooldown = Mathf.Max(0f, dashCooldown + amount);
    }

    private void FixedUpdate()
    {
        if (dashSlider != null)
        {
            float remaining = Mathf.Max(0f, nextDashTime - Time.time);
            float fill = 1f - Mathf.Clamp01(remaining / dashCooldown); // 1 = ready

            if (fill >= 1f)
                dashSlider.value = 0f; // ready → empty
            else
                dashSlider.value = fill; // cooling down → fill growing
        }



        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashSpeed;
            if (Time.time >= dashEndTime)
                isDashing = false;
            return;
        }

        if (instant)
        {
            rb.linearVelocity = targetVelocity;
        }
        else
        {
            velocity = Vector2.MoveTowards(velocity, targetVelocity, acceleration * Time.fixedDeltaTime);

            if (TryGetComponent<StatusEffectSystem>(out StatusEffectSystem ses))
            {
                if (!ses.HasStatus(StatusEffectSystem.StatusType.Stun) || !ses.HasStatus(StatusEffectSystem.StatusType.Frozen))
                {
                    if (!ses.HasStatus(StatusEffectSystem.StatusType.Speed))
                    {
                        rb.linearVelocity = velocity;
                    }
                    else
                    {
                        rb.linearVelocity = velocity * 2;
                    }
                }
                else
                {
                    rb.linearVelocity = Vector2.zero; // Stop moving if stunned
                }
            }
        }
    }
}
