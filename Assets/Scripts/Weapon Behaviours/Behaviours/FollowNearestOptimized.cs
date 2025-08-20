using UnityEngine;

[AddComponentMenu("AI/Follow Nearest 2D (Ultra Simple)")]
public class FollowNearestOptimized : MonoBehaviour
{
    [Header("Find Targets")]
    public string targetTag = "Target";
    public float searchRadius = 10f;

    [Header("Movement")]
    public float moveSpeed = 5f;

    Transform tr;
    Transform target;

    void Awake() => tr = transform;

    void Update()
    {
        if (target == null || ((Vector2)target.position - (Vector2)tr.position).sqrMagnitude > searchRadius * searchRadius)
            FindNearest();

        if (target != null)
            tr.position = Vector2.MoveTowards(tr.position, target.position, moveSpeed * Time.deltaTime);
    }

    void FindNearest()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag(targetTag);
        float bestD2 = float.PositiveInfinity;
        Transform best = null;

        foreach (var o in objs)
        {
            if (!o) continue;
            var t = o.transform;
            if (t == tr) continue;

            float d2 = ((Vector2)t.position - (Vector2)tr.position).sqrMagnitude;
            if (d2 < bestD2 && d2 <= searchRadius * searchRadius)
            {
                bestD2 = d2;
                best = t;
            }
        }
        target = best;
    }
}
