using UnityEngine;

public class FollowWASDOffset2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Units per second")]
    public float moveSpeed = 5f;
    [Tooltip("Distance from original position when fully pressed")]
    public float offsetDistance = 1f;
    [Tooltip("Stop moving if within this distance to the target position")]
    public float stopDistance = 0.05f;

    [Header("Options")]
    [Tooltip("If true, object rotates to face the move direction")]
    public bool rotateTowardsDirection = false;
    [Tooltip("If true, object will not return to start position when no input is pressed")]
    public bool stayOnNoInput = false;

    private Vector3 startLocalPos;

    private void Awake()
    {
        // Store local position relative to the parent
        startLocalPos = transform.localPosition;
    }

    private void Update()
    {
        // Get WASD or Arrow Keys
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector3 targetLocalPos;

        if (x == 0 && y == 0 && stayOnNoInput)
        {
            // No movement if no input and stay mode is enabled
            return;
        }
        else
        {
            // Target position in local space
            targetLocalPos = startLocalPos + new Vector3(x, y, 0f) * offsetDistance;
        }

        // Convert to world space for movement
        Vector3 targetWorldPos = transform.parent != null
            ? transform.parent.TransformPoint(targetLocalPos)
            : targetLocalPos;

        // Move towards target
        Vector3 toTarget = targetWorldPos - transform.position;
        float dist = toTarget.magnitude;

        if (dist > stopDistance)
        {
            Vector3 step = toTarget.normalized * moveSpeed * Time.deltaTime;
            if (step.sqrMagnitude > toTarget.sqrMagnitude) step = toTarget; // prevent overshoot
            transform.position += step;
        }

        // Optional face move direction
        if (rotateTowardsDirection && toTarget.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 center = transform.parent != null
            ? transform.parent.TransformPoint(startLocalPos != Vector3.zero ? startLocalPos : transform.localPosition)
            : (startLocalPos != Vector3.zero ? startLocalPos : transform.position);
        Gizmos.DrawWireSphere(center, offsetDistance);
    }
#endif
}
