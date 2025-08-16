using UnityEngine;

/// <summary>
/// Orbits either THIS object or a list of objects (multiple) around a target in 2D.
/// - target = center point
/// - multiple = objects that float; if empty, this object floats
/// </summary>
public class CircularOrbit2D : MonoBehaviour
{
    [Header("Orbit Settings")]
    [Tooltip("Center to orbit around.")]
    public Transform target;

    [Tooltip("If set, all of these objects will orbit around the target. If empty, this object orbits.")]
    public Transform[] multiple;

    [Tooltip("Orbit radius (same for all).")]
    public float distance = 3f;

    [Tooltip("Degrees per second. Negative = opposite direction.")]
    public float speed = 50f;

    [Tooltip("Starting angle offset in degrees.")]
    public float startAngleDegrees = 0f;

    private float baseAngleRad = 0f;

    void Update()
    {
        if (target == null)
        {
            Debug.LogError("CircularOrbit2D: No target assigned.");
            return;
        }

        // Advance base angle
        baseAngleRad += speed * Mathf.Deg2Rad * Time.deltaTime;
        if (baseAngleRad > Mathf.PI * 2f) baseAngleRad -= Mathf.PI * 2f;
        if (baseAngleRad < -Mathf.PI * 2f) baseAngleRad += Mathf.PI * 2f;

        float startRad = startAngleDegrees * Mathf.Deg2Rad;

        // Count valid entries in multiple
        int count = 0;
        if (multiple != null)
        {
            for (int i = 0; i < multiple.Length; i++)
                if (multiple[i] != null) count++;
        }

        if (count <= 0)
        {
            // Orbit THIS object
            float a = baseAngleRad + startRad;
            float x = target.position.x + Mathf.Cos(a) * distance;
            float y = target.position.y + Mathf.Sin(a) * distance;
            transform.position = new Vector3(x, y, transform.position.z);
            return;
        }

        // Orbit all objects in 'multiple', evenly spaced around the circle
        float step = (Mathf.PI * 2f) / count;
        int idx = 0;

        for (int i = 0; i < multiple.Length; i++)
        {
            var t = multiple[i];
            if (t == null) continue;

            float a = baseAngleRad + startRad + step * idx;
            float x = target.position.x + Mathf.Cos(a) * distance;
            float y = target.position.y + Mathf.Sin(a) * distance;

            t.position = new Vector3(x, y, t.position.z);
            idx++;
        }
    }
}
