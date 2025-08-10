using UnityEngine;

public class FollowNearest2D : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Layer to search for targets")]
    public LayerMask targetLayer;
    [Tooltip("Movement speed towards the target")]
    public float moveSpeed = 5f;
    [Tooltip("Max distance to search for targets")]
    public float searchRadius = 10f;
    [Tooltip("How often to choose a new target (seconds)")]
    public float updateInterval = 1f;
    [Tooltip("Tag to find the player transform")]
    public string playerTag = "Player";

    [Header("Arrival Smoothing")]
    [Tooltip("Stop moving when within this distance of the target.")]
    public float stopDistance = 0.5f;
    [Tooltip("Start slowing down when within this distance of the target.")]
    public float decelerationRadius = 2f;
    [Tooltip("Use Rigidbody2D.MovePosition if available.")]
    public bool useRigidbodyMovement = true;

    private Transform target;
    private Transform playerTransform;
    private float nextUpdateTime = 0f;
    private Rigidbody2D rb2d;

    private void Start()
    {
        // Try to find player transform by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
            playerTransform = playerObj.transform;

        rb2d = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Pick a new target every updateInterval seconds
        if (Time.time >= nextUpdateTime)
        {
            FindNearestTarget();
            nextUpdateTime = Time.time + updateInterval;
        }

        // Move towards the current target only if it's in range
        if (target != null)
        {
            Vector2 toTarget = (Vector2)(target.position - transform.position);
            float distanceToTarget = toTarget.magnitude;

            // Not in search radius? don't move
            if (distanceToTarget > searchRadius) return;

            // Inside deadzone? stop perfectly -> no jitter
            if (distanceToTarget <= stopDistance) return;

            // Smooth arrival: scale speed as we get close (between stopDistance and decelerationRadius)
            float speed = moveSpeed;
            if (distanceToTarget < decelerationRadius)
            {
                float t = Mathf.InverseLerp(stopDistance, decelerationRadius, distanceToTarget);
                speed *= Mathf.Clamp01(t); // 0..1
            }

            // Compute step and clamp so we never overshoot past stopDistance
            Vector2 dir = toTarget / Mathf.Max(distanceToTarget, 0.0001f);
            float maxStep = (distanceToTarget - stopDistance);
            Vector2 step = dir * speed * Time.deltaTime;
            if (step.magnitude > maxStep) step = dir * maxStep;

            if (useRigidbodyMovement && rb2d != null)
                rb2d.MovePosition(rb2d.position + step);
            else
                transform.position += (Vector3)step;
        }
    }

    void FindNearestTarget()
    {
        Vector3 searchCenter = playerTransform != null ? playerTransform.position : transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(searchCenter, searchRadius, targetLayer);

        float closestDistance = Mathf.Infinity;
        Transform closestTarget = null;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            // skip self if we’re on the same layer/query
            if (hit.transform == transform) continue;

            float distance = Vector2.Distance(searchCenter, hit.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = hit.transform;
            }
        }

        target = closestTarget; // always reassign, even if same as before
    }

    // Draw the search radius in Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = (playerTransform != null) ? playerTransform.position : transform.position;
        Gizmos.DrawWireSphere(center, searchRadius);
    }
}
