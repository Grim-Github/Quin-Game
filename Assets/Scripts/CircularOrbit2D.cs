using UnityEngine;

/// <summary>
/// Makes the GameObject this script is attached to orbit around a target Transform in a 2D circular path.
/// </summary>
public class CircularOrbit2D : MonoBehaviour
{
    [Header("Orbit Settings")]
    [Tooltip("The object that this GameObject will orbit around.")]
    public Transform target;

    [Tooltip("The distance from the target object.")]
    public float distance = 3.0f;

    [Tooltip("The speed of the orbit. Can be negative for opposite direction.")]
    public float speed = 50.0f;

    // We use a private variable to store the current angle of the object in its orbit.
    // We don't expose it to the inspector because we calculate it continuously.
    private float angle = 0f;

    void Update()
    {
        // First, check if a target has been assigned. If not, the script will log an error
        // and stop executing in this frame to prevent further issues.
        if (target == null)
        {
            Debug.LogError("CircularOrbit2D: Target not set. Please assign a target Transform in the Inspector.");
            return;
        }

        // Increment the angle over time to create motion.
        // We multiply speed by Time.deltaTime to make the movement smooth and frame-rate independent.
        // We also convert the speed from degrees per second to radians per second.
        angle += speed * Time.deltaTime * Mathf.Deg2Rad;

        // Keep the angle within a 0 to 2*PI range (a full circle in radians).
        // This is not strictly necessary but can help prevent floating point inaccuracies over time.
        if (angle > (2.0f * Mathf.PI))
        {
            angle -= (2.0f * Mathf.PI);
        }

        // Calculate the new X and Y positions for the orbiting object.
        // We use cosine for the X coordinate and sine for the Y coordinate to get a point on a circle.
        // We then add the target's position to make the orbit centered around the target.
        float x = target.position.x + Mathf.Cos(angle) * distance;
        float y = target.position.y + Mathf.Sin(angle) * distance;

        // Create a new position vector. The Z position is kept the same as the current object's Z position.
        Vector3 newPosition = new Vector3(x, y, transform.position.z);

        // Apply the new position to this GameObject's transform.
        transform.position = newPosition;
    }
}
