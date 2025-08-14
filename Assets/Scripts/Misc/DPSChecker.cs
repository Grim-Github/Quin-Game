using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DPSChecker : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("UI Text to display DPS.")]
    public TextMeshProUGUI dpsText;

    [Header("Settings")]
    [Tooltip("How far back to calculate DPS (seconds).")]
    public float dpsWindow = 3f;

    private struct DamageEvent
    {
        public float time;
        public int amount;
    }

    private readonly List<DamageEvent> damageHistory = new List<DamageEvent>();

    private void Update()
    {
        float now = Time.time;

        // Remove old events
        for (int i = damageHistory.Count - 1; i >= 0; i--)
        {
            if (now - damageHistory[i].time > dpsWindow)
                damageHistory.RemoveAt(i);
        }

        // Sum recent damage
        float totalDamage = 0f;
        foreach (var e in damageHistory)
            totalDamage += e.amount;

        // Calculate DPS for the last X seconds
        float dps = totalDamage / Mathf.Max(0.01f, dpsWindow);

        if (dpsText)
            dpsText.text = $"DPS: {dps:F1}";
    }

    /// <summary>
    /// Call this whenever damage is dealt.
    /// </summary>
    public void RegisterDamage(int amount)
    {
        if (amount <= 0) return;
        damageHistory.Add(new DamageEvent { time = Time.time, amount = amount });
    }
}
