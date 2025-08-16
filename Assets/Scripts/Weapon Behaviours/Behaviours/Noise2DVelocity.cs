using UnityEngine;

/// <summary>
/// Applies smooth 2D noise-based velocity to a Rigidbody2D.
/// Great for wandering, jitter, or idle movement effects.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Noise2DVelocity : MonoBehaviour
{
    [Header("Noise Settings")]
    [Tooltip("Controls how fast the noise changes over time.")]
    [SerializeField] private float noiseFrequency = 1f;

    [Tooltip("Scales the output velocity.")]
    [SerializeField] private float noiseAmplitude = 3f;

    [Tooltip("Offset applied to separate X/Y noise.")]
    [SerializeField] private float noiseOffset = 100f;

    private Rigidbody2D rb;
    private float seed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        seed = Random.value * 1000f; // randomize seed so different objects don't sync
    }

    private void FixedUpdate()
    {
        float t = Time.time * noiseFrequency + seed;

        // Get smooth values from Perlin noise
        float noiseX = Mathf.PerlinNoise(t, 0f) * 2f - 1f; // -1..1
        float noiseY = Mathf.PerlinNoise(0f, t + noiseOffset) * 2f - 1f; // -1..1

        Vector2 velocity = new Vector2(noiseX, noiseY) * noiseAmplitude;

        rb.linearVelocity = velocity;
    }
}
