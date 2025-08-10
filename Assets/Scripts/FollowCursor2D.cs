using UnityEngine;

public class FollowCursor2D : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Units per second")]
    public float moveSpeed = 5f;
    [Tooltip("Stop moving if within this distance to the cursor")]
    public float stopDistance = 0.05f;

    [Header("Options")]
    [Tooltip("If true, object rotates to face the cursor (Z-up 2D)")]
    public bool rotateTowardsCursor = false;

    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
        if (cam == null)
            Debug.LogWarning("FollowCursor2D: No Camera.main found. Assign a MainCamera tag.");
    }

    private void Update()
    {
        if (cam == null) return;

        // Cursor position in world (2D)
        Vector3 cursorWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        cursorWorld.z = transform.position.z; // keep current Z

        // Move towards cursor
        Vector3 toCursor = cursorWorld - transform.position;
        float dist = toCursor.magnitude;

        if (dist > stopDistance)
        {
            Vector3 step = toCursor.normalized * moveSpeed * Time.deltaTime;
            if (step.sqrMagnitude > toCursor.sqrMagnitude) step = toCursor; // prevent overshoot
            transform.position += step;
        }

        // Optional face cursor
        if (rotateTowardsCursor && dist > 0.0001f)
        {
            float angle = Mathf.Atan2(toCursor.y, toCursor.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
#endif
}
