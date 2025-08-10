// WeaponTick.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class WeaponTick : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds between tick cycles (or between burst starts).")]
    [SerializeField] public float interval = 1f;
    [Tooltip("If true, starts ticking automatically on Awake.")]
    [SerializeField] private bool startOnAwake = true;
    [Tooltip("If true, the tick repeats. If false, fires only once.")]
    [SerializeField] private bool repeat = true;
    [Tooltip("If true, uses unscaled time (ignores Time.timeScale).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Burst")]
    [Tooltip("Enable to fire multiple ticks per cycle.")]
    [SerializeField] private bool burstEnabled = false;
    [Tooltip("How many ticks to fire per burst (>= 1).")]
    [SerializeField] private int burstCount = 3;
    [Tooltip("Seconds between ticks inside the burst.")]
    [SerializeField] private float burstSpacing = 0.1f;

    [Header("Event")]
    public UnityEvent onTick;

    private Coroutine tickCoroutine;

    private void Awake()
    {
        if (startOnAwake)
            StartTick();
    }

    /// <summary>Begins the tick timer. If already running, restarts it.</summary>
    public void StartTick()
    {
        StopTick();
        tickCoroutine = StartCoroutine(TickRoutine());
    }

    /// <summary>Stops the timer if it's running.</summary>
    public void StopTick()
    {
        if (tickCoroutine != null)
        {
            StopCoroutine(tickCoroutine);
            tickCoroutine = null;
        }
    }

    /// <summary>Immediately invokes a single tick (does not affect coroutine schedule).</summary>
    public void TriggerNow()
    {
        onTick?.Invoke();
    }

    /// <summary>Stops and restarts the timer from scratch.</summary>
    public void ResetAndStart()
    {
        StartTick();
    }

    /// <summary>Immediately fires a full burst now (respects burst settings).</summary>
    public void TriggerBurstNow()
    {
        if (!burstEnabled || burstCount <= 1)
        {
            onTick?.Invoke();
            return;
        }
        StartCoroutine(DoBurstOnce());
    }

    private IEnumerator TickRoutine()
    {
        // Basic input sanitation
        float safeInterval = Mathf.Max(0f, interval);
        float safeBurstSpacing = Mathf.Max(0f, burstSpacing);
        int safeBurstCount = Mathf.Max(1, burstCount);

        while (true)
        {
            // Wait until the next cycle/burst start
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(safeInterval);
            else
                yield return new WaitForSeconds(safeInterval);

            if (burstEnabled && safeBurstCount > 1)
            {
                // Fire a burst
                for (int i = 0; i < safeBurstCount; i++)
                {
                    onTick?.Invoke();

                    // Spacing between ticks inside the burst (skip after the last one)
                    if (i < safeBurstCount - 1)
                    {
                        if (useUnscaledTime)
                            yield return new WaitForSecondsRealtime(safeBurstSpacing);
                        else
                            yield return new WaitForSeconds(safeBurstSpacing);
                    }
                }
            }
            else
            {
                // Single tick mode
                onTick?.Invoke();
            }

            if (!repeat) break;
        }

        tickCoroutine = null;
    }

    private IEnumerator DoBurstOnce()
    {
        float safeBurstSpacing = Mathf.Max(0f, burstSpacing);
        int safeBurstCount = Mathf.Max(1, burstCount);

        if (!burstEnabled || safeBurstCount <= 1)
        {
            onTick?.Invoke();
            yield break;
        }

        for (int i = 0; i < safeBurstCount; i++)
        {
            onTick?.Invoke();
            if (i < safeBurstCount - 1)
            {
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(safeBurstSpacing);
                else
                    yield return new WaitForSeconds(safeBurstSpacing);
            }
        }
    }

    private void OnDisable()
    {
        StopTick();
    }
}
