// StatusEffects.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public enum StatusType
{
    Burning = 0,
    // Add more here (Slow, Stun, Poison, etc.)
}

[Serializable] public class UnityEventFloat : UnityEvent<float> { }

[Serializable]
public class StatusEffectEvents
{
    public StatusType type;
    public UnityEvent OnStatusEffectStart;
    public UnityEventFloat OnStatusEffectStay;
    public UnityEvent OnStatusEffectEnd;
}

internal sealed class ActiveEffect
{
    public StatusType Type;
    public float Remaining;
    public float Duration;
    public StatusEffectEvents Events;
}

public class StatusEffects : MonoBehaviour
{
    [Header("Status Effect Settings")]
    public List<StatusEffectEvents> effectEvents = new List<StatusEffectEvents>();

    [Header("UI")]
    [Tooltip("Optional TextMeshProUGUI to list all active effects and their remaining time.")]
    public TextMeshProUGUI statusEffectsText;

    [Header("Example Burning Tuning")]
    public float burningDamagePerSecond = 0f;

    private readonly Dictionary<StatusType, ActiveEffect> _active = new();
    private readonly Dictionary<StatusType, StatusEffectEvents> _eventMap = new();
    private List<ActiveEffect> _toProcess;

    private void Awake()
    {
        foreach (var e in effectEvents)
        {
            if (!_eventMap.ContainsKey(e.type))
                _eventMap.Add(e.type, e);
        }
    }

    private void Update()
    {
        if (_active.Count > 0)
        {
            if (_toProcess == null) _toProcess = new List<ActiveEffect>();
            _toProcess.Clear();
            foreach (var kv in _active) _toProcess.Add(kv.Value);

            float dt = Time.deltaTime;

            foreach (var ae in _toProcess)
            {
                ae.Events?.OnStatusEffectStay?.Invoke(dt);

                if (ae.Type == StatusType.Burning && burningDamagePerSecond > 0f)
                {
                    int dmg = Mathf.FloorToInt(burningDamagePerSecond * dt);
                    if (dmg > 0)
                        SendMessage("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);
                }

                ae.Remaining -= dt;
                if (ae.Remaining <= 0f)
                {
                    ae.Events?.OnStatusEffectEnd?.Invoke();
                    _active.Remove(ae.Type);
                }
            }
        }

        UpdateStatusEffectUI();
    }

    private void UpdateStatusEffectUI()
    {
        if (statusEffectsText == null) return;

        if (_active.Count == 0)
        {
            statusEffectsText.text = "No Status Effects";
            return;
        }

        System.Text.StringBuilder sb = new();
        foreach (var ae in _active.Values)
        {
            sb.AppendLine($"{ae.Type} ({ae.Remaining:F1}s)");
        }
        statusEffectsText.text = sb.ToString();
    }

    public void Apply(StatusType type, float durationSeconds)
    {
        if (durationSeconds <= 0f) return;

        if (_active.TryGetValue(type, out var existing))
        {
            existing.Duration = durationSeconds;
            existing.Remaining = durationSeconds;
            return;
        }

        var evt = _eventMap.TryGetValue(type, out var conf) ? conf : null;
        var ae = new ActiveEffect
        {
            Type = type,
            Duration = durationSeconds,
            Remaining = durationSeconds,
            Events = evt
        };

        _active[type] = ae;
        evt?.OnStatusEffectStart?.Invoke();
    }

    public void Remove(StatusType type)
    {
        if (_active.TryGetValue(type, out var ae))
        {
            ae.Events?.OnStatusEffectEnd?.Invoke();
            _active.Remove(type);
        }
    }

    public bool Has(StatusType type) => _active.ContainsKey(type);
    public float GetRemaining(StatusType type) =>
        _active.TryGetValue(type, out var ae) ? Mathf.Max(0f, ae.Remaining) : 0f;

    public void ClearAll()
    {
        foreach (var kv in _active)
            kv.Value.Events?.OnStatusEffectEnd?.Invoke();
        _active.Clear();
    }
}
