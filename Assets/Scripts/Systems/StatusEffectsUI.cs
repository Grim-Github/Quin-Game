using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StatusEffectsUI : MonoBehaviour
{
    [Serializable]
    public class IconMapping
    {
        public StatusEffectSystem.StatusType type;
        public Sprite sprite;
    }

    [Header("References")]
    [SerializeField] private StatusEffectSystem target;     // If null, auto-find on this GameObject
    [SerializeField] private Transform iconsParent;         // Parent under a Canvas/Panel
    [SerializeField] private Image iconPrefab;              // Prefab with Image (Type=Filled)

    [Header("Sprites for statuses to show")]
    [SerializeField] private List<IconMapping> icons = new();

    private readonly Dictionary<StatusEffectSystem.StatusType, Image> activeIcons = new();
    private readonly Dictionary<StatusEffectSystem.StatusType, float> startDurations = new();
    private Dictionary<StatusEffectSystem.StatusType, Sprite> spriteByType;

    private void Awake()
    {
        if (!target) target = GetComponent<StatusEffectSystem>();
        if (!target)
        {
            Debug.LogWarning("[StatusEffectsUI] No StatusEffectSystem target found.");
            enabled = false;
            return;
        }

        spriteByType = new Dictionary<StatusEffectSystem.StatusType, Sprite>(icons.Count);
        foreach (var m in icons)
        {
            if (!spriteByType.ContainsKey(m.type) && m.sprite != null)
                spriteByType.Add(m.type, m.sprite);
        }
    }

    private void OnEnable()
    {
        target.OnStart += HandleStart;
        target.OnEnd += HandleEnd;

        // Show already active effects
        foreach (StatusEffectSystem.StatusType t in Enum.GetValues(typeof(StatusEffectSystem.StatusType)))
        {
            if (target.HasStatus(t))
                CreateOrRefreshIcon(t);
        }
    }

    private void OnDisable()
    {
        target.OnStart -= HandleStart;
        target.OnEnd -= HandleEnd;
    }

    private void Update()
    {
        foreach (var kv in activeIcons)
        {
            var type = kv.Key;
            var img = kv.Value;

            float remaining = target.GetRemainingTime(type);
            if (!startDurations.TryGetValue(type, out float startDur))
                startDur = Mathf.Max(remaining, 0.0001f);

            float fill = (startDur <= 0.0001f) ? 0f : Mathf.Clamp01(remaining / startDur);
            img.fillAmount = fill;
        }
    }

    private void HandleStart(StatusEffectSystem.StatusType type) => CreateOrRefreshIcon(type);

    private void HandleEnd(StatusEffectSystem.StatusType type)
    {
        if (activeIcons.TryGetValue(type, out var img))
        {
            Destroy(img.gameObject);
            activeIcons.Remove(type);
            startDurations.Remove(type);
        }
    }

    private void CreateOrRefreshIcon(StatusEffectSystem.StatusType type)
    {
        if (!spriteByType.TryGetValue(type, out var sprite)) return;

        if (activeIcons.TryGetValue(type, out var existing))
        {
            startDurations[type] = Mathf.Max(target.GetRemainingTime(type), 0.0001f);
            existing.sprite = sprite;
            return;
        }

        var newIcon = Instantiate(iconPrefab, iconsParent ? iconsParent : transform);
        newIcon.name = $"StatusIcon_{type}";
        newIcon.sprite = sprite;
        newIcon.fillAmount = 1f;

        activeIcons[type] = newIcon;
        startDurations[type] = Mathf.Max(target.GetRemainingTime(type), 0.0001f);
    }
}
