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
    [Tooltip("Frequency while shaking.")]
    [SerializeField] private float shakeFrequency = 2f;
    [Tooltip("Use unscaled time so shake still runs during slow-mo/pause.")]
    [SerializeField] private bool useUnscaledTime = true;

    private float _targetSize;
    private float _currentSize;

    // Perlin original values to restore (from Awake)
    private float _origAmplitude;
    private float _origFrequency;

    // --- Shake state (no coroutine) ---
    private bool _isShaking = false;
    private float _shakeTimer = 0f;
    private float _shakeDuration = 0f;
    private float _shakeIntensity = 0f;
    private float _prevAmp = 0f;
    private float _prevFreq = 0f;

    private void Awake()
    {
        if (cmCamera == null) cmCamera = GetComponent<CinemachineCamera>();
        if (cmCamera == null)
        {
            Debug.LogError("[OrthoScrollZoom] No CinemachineCamera found.");
            enabled = false;
            return;
        }

        // Ensure a Perlin noise component exists
        if (perlin == null && !cmCamera.TryGetComponent(out perlin))
            perlin = cmCamera.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();

        // Cache originals
        _origAmplitude = perlin != null ? perlin.AmplitudeGain : 0f;
        _origFrequency = perlin != null ? perlin.FrequencyGain : 0f;

        _targetSize = Mathf.Clamp(GetSize(), minSize, maxSize);
        _currentSize = _targetSize;
        SetSize(_currentSize);
    }

    private void Update()
    {
        // Block zoom if UITab is active (only if assigned)
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

        // --- Shake tick (no coroutine) ---
        if (_isShaking && perlin != null)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _shakeTimer += dt;

            float half = Mathf.Max(0.0001f, _shakeDuration * 0.5f);
            float amp;

            if (_shakeTimer < half)
            {
                // ease-in: 0 -> intensity
                float u = _shakeTimer / half;
                amp = Mathf.Lerp(0f, _shakeIntensity, u);
            }
            else if (_shakeTimer < _shakeDuration)
            {
                // ease-out: intensity -> 0
                float u = (_shakeTimer - half) / half;
                amp = Mathf.Lerp(_shakeIntensity, 0f, u);
            }
            else
            {
                // done
                perlin.AmplitudeGain = _prevAmp;
                perlin.FrequencyGain = _prevFreq;
                _isShaking = false;
                return;
            }

            perlin.AmplitudeGain = amp;
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
    /// Simple Perlin-based camera shake.
    /// duration: how long the shake lasts (seconds)
    /// intensity: amplitude gain during shake
    /// </summary>
    public void CameraShake(float duration, float intensity)
    {
        if (perlin == null)
        {
            Debug.LogWarning("[OrthoScrollZoom] No CinemachineBasicMultiChannelPerlin on camera.");
            return;
        }

        // Save current values (so we restore whatever was there)
        _prevAmp = perlin.AmplitudeGain;
        _prevFreq = perlin.FrequencyGain;

        // Configure shake
        perlin.FrequencyGain = shakeFrequency;

        // Start/Restart shake
        _shakeDuration = Mathf.Max(0f, duration);
        _shakeIntensity = Mathf.Max(0f, intensity);
        _shakeTimer = 0f;
        _isShaking = _shakeDuration > 0f;
    }
}
