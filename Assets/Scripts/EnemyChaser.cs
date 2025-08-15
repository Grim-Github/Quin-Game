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
        exploder.GetComponent<ExplosionDamage2D>().baseDamage = GetComponent<SimpleHealth>().maxHealth;
        exploder.GetComponent<ExplosionDamage2D>().DoExplosion();
    }

    private void FixedUpdate()
    {
        if (target == null) return;

        Vector2 currentPos = rb.position;
        Vector2 targetPos = target.position;
        Vector2 toTarget = targetPos - currentPos;
        float distance = toTarget.magnitude;

        if (distance <= stoppingDistance)
        {
            rb.linearVelocity = Vector2.zero;
            if (!hasReached)
            {
                hasReached = true;
                onReachDestination?.Invoke();
            }
            return;
        }

        Vector2 desiredDir = toTarget.normalized;
        if (TryGetComponent<StatusEffectSystem>(out StatusEffectSystem ses))
        {
            if (!ses.HasStatus(StatusEffectSystem.StatusType.Stun))
            {
                rb.linearVelocity = desiredDir * moveSpeed;
            }
            else
            {
                rb.linearVelocity = Vector2.zero; // Stop moving if stunned
            }
        }


        ResetReachedIfFar(distance);
    }

    private void ResetReachedIfFar(float distance)
    {
        if (hasReached && repeatEvent && distance > stoppingDistance + resetDistanceBuffer)
            hasReached = false;
    }
}
