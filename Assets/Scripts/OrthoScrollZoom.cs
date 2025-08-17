using System.Collections;
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

    // Perlin original values to restore
    private float _origAmplitude;
    private float _origFrequency;
    private Coroutine _shakeRoutine;

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

        // Old input system scroll
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

        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(duration, intensity));
    }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        // Set shake values
        float prevAmp = perlin.AmplitudeGain;
        float prevFreq = perlin.FrequencyGain;

        perlin.AmplitudeGain = intensity;
        perlin.FrequencyGain = shakeFrequency;

        // Wait for duration (scaled or unscaled)
        float t = 0f;
        while (t < duration)
        {
            t = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        // Restore previous values
        perlin.AmplitudeGain = prevAmp;
        perlin.FrequencyGain = prevFreq;

        _shakeRoutine = null;
    }
}
