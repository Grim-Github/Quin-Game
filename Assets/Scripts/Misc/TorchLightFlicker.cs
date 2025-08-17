using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class TorchLightFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    [SerializeField] private float flickerAmount = 0.2f; // +/- around current base
    [SerializeField] private float flickerSpeed = 3f;    // Hz-ish

    private Light2D light2D;
    private float baseIntensity;
    private float phase; // per-instance offset (cached)

    void Awake()
    {
        light2D = GetComponent<Light2D>();
        baseIntensity = light2D.intensity;      // use whatever it's set to
        phase = Random.value * 1000f;           // one-time, desync instances
    }

    void Update()
    {
        float t = Time.time * flickerSpeed + phase;
        float noise = Mathf.PerlinNoise(t, 0f) - 0.5f;   // smooth torchy flicker
        light2D.intensity = baseIntensity + noise * flickerAmount;
    }
}
