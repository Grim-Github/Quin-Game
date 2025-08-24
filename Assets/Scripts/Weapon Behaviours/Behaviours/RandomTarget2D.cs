using UnityEngine;

public class RandomTarget2D : MonoBehaviour
{
    [SerializeField] private float radius = 5f;
    [Tooltip("Optional custom origin. If empty, uses this transform.")]
    [SerializeField] private Transform centerPoint;

    /// <summary>
    /// Moves this transform to a random position inside the circle radius.
    /// </summary>
    public void MoveToRandomLocation()
    {
        Vector3 origin = centerPoint != null ? centerPoint.position : transform.position;

        // Pick random point inside a circle
        Vector2 randomOffset = Random.insideUnitCircle * radius;
        transform.position = origin + (Vector3)randomOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = centerPoint != null ? centerPoint.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, radius);
    }
}
