// EnemyChaser.cs
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyChaser : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("If left empty, will try to find GameObject with tag 'Player' on Awake.")]
    [SerializeField] private Transform target;

    [Header("Movement")]
    [Tooltip("Speed in units per second.")]
    [SerializeField] public float moveSpeed = 3f;
    [Tooltip("How close to the target before stopping.")]
    [SerializeField] private float stoppingDistance = 0.5f;

    [Header("Flee Behavior")]
    [Tooltip("If enabled, enemy flees when inside stoppingDistance, chases when outside.")]
    [SerializeField] private bool enableFlee = false;
    [Tooltip("Dead zone half-width around stoppingDistance where velocity is set to 0 to avoid flip-flopping between chase and flee (only used if flee enabled).")]
    [SerializeField] private float fleeBuffer = 0.25f;

    [Header("Reach Event")]
    [Tooltip("If true, allows the reach event to fire again after the target moves away far enough.")]
    [SerializeField] private bool repeatEvent = false;
    [Tooltip("Extra distance beyond stoppingDistance the target must exceed to reset the reached state (only used if repeatEvent is true).")]
    [SerializeField] private float resetDistanceBuffer = 1f;
    public UnityEvent onReachDestination;

    private Rigidbody2D rb;
    private bool hasReached;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }

        if (target == null)
        {
            Debug.LogWarning($"{name}: No target assigned and no GameObject with tag 'Player' was found.");
        }
    }

    public void InstantiateExplosion(GameObject explosion)
    {
        GameObject exploder = Instantiate(explosion, transform.position, Quaternion.identity);
        exploder.GetComponent<ExplosionDamage2D>().baseDamage = GetComponent<SimpleHealth>().maxHealth / 3;
        exploder.GetComponent<ExplosionDamage2D>().DoExplosion();
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        Vector2 currentPos = rb.position;
        Vector2 targetPos = target.position;
        Vector2 toTarget = targetPos - currentPos;
        float distance = toTarget.magnitude;

        // Decide desired direction (flee/approach with deadband)
        Vector2 desiredDir;
        if (enableFlee)
        {
            float buffer = Mathf.Max(0f, fleeBuffer);
            if (distance < stoppingDistance - buffer) desiredDir = (-toTarget).normalized;  // flee
            else if (distance > stoppingDistance + buffer) desiredDir = toTarget.normalized;     // chase
            else desiredDir = Vector2.zero;            // hold
        }
        else
        {
            desiredDir = distance > stoppingDistance ? toTarget.normalized : Vector2.zero;
        }

        // Fire reach event on first entry
        if (distance <= stoppingDistance && !hasReached)
        {
            hasReached = true;
            onReachDestination?.Invoke();
        }

        // Movement multiplier from status effects
        StatusEffectSystem ses = null;
        TryGetComponent(out ses);
        float mult = GetMoveMultiplier(ses); // 0 if Stun/Frozen, 2 if Speed, else 1

        // Apply velocity
        float speed = moveSpeed * mult;
        rb.linearVelocity = desiredDir * speed;

        ResetReachedIfFar(distance);
    }

    private void ResetReachedIfFar(float distance)
    {
        if (hasReached && repeatEvent && distance > stoppingDistance + resetDistanceBuffer)
            hasReached = false;
    }

    private float GetMoveMultiplier(StatusEffectSystem ses)
    {
        // If there's no StatusEffectSystem on this GameObject, treat it as having no statuses.
        if (ses == null)
            return 1f;

        if (ses.HasStatus(StatusEffectSystem.StatusType.Stun) || ses.HasStatus(StatusEffectSystem.StatusType.Frozen))
            return 0f;

        float m = 1f;
        if (ses.HasStatus(StatusEffectSystem.StatusType.Speed)) m *= 2f;   // tweak in inspector later
        return m;
    }

}
