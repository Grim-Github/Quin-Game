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

    private Transform target;
    private Transform playerTransform;
    private float nextUpdateTime = 0f;

    private void Start()
    {
        // Try to find player transform by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
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
            float distanceToTarget = Vector2.Distance(transform.position, target.position);
            if (distanceToTarget <= searchRadius)
            {
                Vector2 direction = (target.position - transform.position).normalized;
                transform.position += (Vector3)(direction * moveSpeed * Time.deltaTime);
            }
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
        Vector3 center = playerTransform != null ? playerTransform.position : transform.position;
        Gizmos.DrawWireSphere(center, searchRadius);
    }
}
