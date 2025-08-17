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

    [Header("Search Mode")]
    [Tooltip("If true, search is centered around the player. If false, around this object.")]
    public bool searchAroundPlayer = false;

    [Header("Arrival Smoothing")]
    [Tooltip("Stop moving when within this distance of the target.")]
    public float stopDistance = 0.5f;
    [Tooltip("Start slowing down when within this distance of the target.")]
    public float decelerationRadius = 2f;
    [Tooltip("Use Rigidbody2D.MovePosition if available.")]
    public bool useRigidbodyMovement = true;

    [Header("Coordination")]
    [Tooltip("Other followers to avoid duplicating targets with.")]
    public FollowNearest2D[] otherFollowers;

    [Header("Return Behavior")]
    [Tooltip("If true, the object will return to its starting point when no target is found.")]
    public bool returnToOriginWhenIdle = false;

    private Transform target;
    private Transform playerTransform;
    private float nextUpdateTime = 0f;
    private float nextPlayerRetryTime = 0f;
    private Rigidbody2D rb2d;

    // Cached local origin
    private Vector3 localOrigin;

    public Transform CurrentTarget => target;

    private void Start()
    {
        TryFindPlayer();
        rb2d = GetComponent<Rigidbody2D>();
        localOrigin = transform.localPosition; // cache starting point
    }

    private void Update()
    {
        // Re-try to find player every 1s if missing (handles late spawns / scene reloads)
        if (playerTransform == null && Time.time >= nextPlayerRetryTime)
        {
            TryFindPlayer();
            nextPlayerRetryTime = Time.time + 1f;
        }

        if (Time.time >= nextUpdateTime)
        {
            FindNearestTarget();
            nextUpdateTime = Time.time + updateInterval;
        }

        // If target drifted out of the search sphere, clear it now
        if (target != null)
        {
            Vector2 searchCenter = (searchAroundPlayer && playerTransform != null)
                ? (Vector2)playerTransform.position
                : (Vector2)transform.position;

            float distanceFromCenterToTarget = Vector2.Distance(searchCenter, target.position);
            if (distanceFromCenterToTarget > searchRadius)
                target = null;
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (target == null)
        {
            if (returnToOriginWhenIdle)
                ReturnToOrigin(dt);
            return;
        }

        MoveTowards(target.position, dt);
    }

    private void MoveTowards(Vector3 destination, float dt)
    {
        Vector2 toTarget = (Vector2)(destination - transform.position);
        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget <= stopDistance) return;

        float speed = moveSpeed;
        if (distanceToTarget < decelerationRadius)
        {
            float t = Mathf.InverseLerp(stopDistance, decelerationRadius, distanceToTarget);
            speed *= Mathf.Clamp01(t);
        }

        Vector2 dir = (distanceToTarget > 0.0001f) ? toTarget / distanceToTarget : Vector2.zero;
        float maxStep = Mathf.Max(0f, distanceToTarget - stopDistance);
        Vector2 step = dir * speed * dt;
        if (step.sqrMagnitude > (maxStep * maxStep)) step = dir * maxStep;

        if (useRigidbodyMovement && rb2d != null)
            rb2d.MovePosition(rb2d.position + step);
        else
            transform.position += (Vector3)step;
    }

    private void ReturnToOrigin(float dt)
    {
        Vector3 worldOrigin = transform.parent != null
            ? transform.parent.TransformPoint(localOrigin)
            : localOrigin;

        MoveTowards(worldOrigin, dt);
    }

    private void FindNearestTarget()
    {
        Vector3 searchCenter = (searchAroundPlayer && playerTransform != null)
            ? playerTransform.position
            : transform.position;

        var hits = Physics2D.OverlapCircleAll(searchCenter, searchRadius, targetLayer);

        var claimed = new System.Collections.Generic.HashSet<Transform>();
        if (otherFollowers != null)
        {
            foreach (var f in otherFollowers)
                if (f != null && f != this && f.CurrentTarget != null)
                    claimed.Add(f.CurrentTarget);
        }

        float closestDistance = Mathf.Infinity;
        Transform closestTarget = null;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            Transform ht = hit.transform;
            if (ht == transform) continue;
            if (claimed.Contains(ht)) continue;

            float d = Vector2.Distance(searchCenter, ht.position);
            if (d < closestDistance)
            {
                closestDistance = d;
                closestTarget = ht;
            }
        }

        target = closestTarget;
    }

    private void TryFindPlayer()
    {
        if (string.IsNullOrEmpty(playerTag)) return;
        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null) playerTransform = playerObj.transform;
    }
}
