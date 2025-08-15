using UnityEngine;

[DisallowMultipleComponent]
public class SpriteSpinner : MonoBehaviour
{
    [Tooltip("Degrees per second to rotate around Z-axis.")]
    public float spinSpeed = 90f;

    [Tooltip("Spin clockwise? If false, spins counterclockwise.")]
    public bool clockwise = true;

    void Update()
    {
        float direction = clockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, spinSpeed * direction * Time.deltaTime);
    }
}
