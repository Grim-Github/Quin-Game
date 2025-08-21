using Unity.Cinemachine;
using UnityEngine;

[AddComponentMenu("Camera/Cinemachine Ortho Scroll Zoom (Smooth + Perlin Shake)")]
public class OrthoScrollZoom : MonoBehaviour
{
    [Header("Target (CM3)")]
    [SerializeField] private CinemachineCamera cmCamera;

    [Header("Block Zoom When This UI Is Active")]
    [Tooltip("If assigned, zoom is disabled while this object is active in the hierarchy (e.g., your UITabs root panel).")]
    [SerializeField] private GameObject UITab;

    [Header("Zoom Settings")]
    [SerializeField] private float scrollSensitivity = 2.0f;
    [SerializeField] private float minSize = 2f;
    [SerializeField] private float maxSize = 7f;
    [SerializeField] private float zoomSmoothSpeed = 8f;
    [Range(0, 3)][SerializeField] private int roundDecimals = 2;

    [Header("Shake (Perlin)")]
    [Tooltip("If null, the script will add/find a CinemachineBasicMultiChannelPerlin on the same camera.")]
    [SerializeField] private CinemachineBasicMultiChannelPerlin perlin;
    [Tooltip("Frequency used while shaking.")]
    [SerializeField] private float shakeFrequency = 2f;
    [Tooltip("The maximum duration the camera shake can accumulate to.")]
    [SerializeField] private float maxShakeDuration = 1;

    private float _targetSize;
    private float _currentSize;

    // --- SUPER SIMPLE SHAKE STATE ---
    private float _shakeDuration = 0f;
    private float _shakeIntensity = 0f;

    private void Awake()
    {
        if (cmCamera == null) cmCamera = GetComponent<CinemachineCamera>();
        if (cmCamera == null)
        {
            Debug.LogError("[OrthoScrollZoom] No CinemachineCamera found.");
            enabled = false;
            return;
        }

        if (perlin == null && !cmCamera.TryGetComponent(out perlin))
            perlin = cmCamera.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();

        _targetSize = Mathf.Clamp(GetSize(), minSize, maxSize);
        _currentSize = _targetSize;
        SetSize(_currentSize);
    }

    private void Update()
    {
        // Block zoom if UITab is active
        if (UITab != null && UITab.activeInHierarchy)
            return;

        // --- Zoom ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > Mathf.Epsilon)
        {
            _targetSize -= scroll * scrollSensitivity;
            _targetSize = Mathf.Clamp(_targetSize, minSize, maxSize);

            if (roundDecimals > 0)
            {
                float factor = Mathf.Pow(10f, roundDecimals);
                _targetSize = Mathf.Round(_targetSize * factor) / factor;
            }
        }

        _currentSize = Mathf.Lerp(_currentSize, _targetSize, zoomSmoothSpeed * Time.unscaledDeltaTime);
        SetSize(_currentSize);

        // --- SUPER SIMPLE SHAKE TICK ---
        if (perlin != null)
        {
            if (_shakeDuration > 0f || _shakeIntensity > 0f)
            {
                // Apply shake
                perlin.FrequencyGain = shakeFrequency;
                perlin.AmplitudeGain = Mathf.Max(0f, _shakeIntensity);

                // Decay both by Time.deltaTime
                float dt = Time.deltaTime;
                _shakeDuration = Mathf.Max(0f, _shakeDuration - dt);
                _shakeIntensity = Mathf.Max(0f, _shakeIntensity - dt);
            }
        }
    }

    private float GetSize() => cmCamera.Lens.OrthographicSize;

    private void SetSize(float newSize)
    {
        var lens = cmCamera.Lens;
        lens.OrthographicSize = newSize;
        cmCamera.Lens = lens;
    }

    /// <summary>
    /// Adds to current shake. Both duration and intensity are accumulated.
    /// </summary>
    public void CameraShake(float duration, float intensity)
    {
        if (perlin == null)
        {
            Debug.LogWarning("[OrthoScrollZoom] No CinemachineBasicMultiChannelPerlin on camera.");
            return;
        }

        if (duration > 0f) _shakeDuration = Mathf.Min(_shakeDuration + duration, maxShakeDuration);
        if (intensity > 0f) _shakeIntensity += intensity;
    }
}
