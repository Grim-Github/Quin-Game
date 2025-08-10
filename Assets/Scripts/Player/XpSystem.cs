using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class XpSystem : MonoBehaviour
{
    public enum CurveType { Linear, Quadratic, Exponential, CustomPerLevel }

    [Header("Level State")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int currentXpInLevel = 0;
    [SerializeField] private int maxLevel = 100;

    [Header("Curve Settings")]
    [SerializeField] private CurveType curve = CurveType.Exponential;
    [SerializeField] private int baseNextLevelXp = 100;
    [SerializeField] private int linearStep = 25;
    [SerializeField] private int quadA = 15;
    [SerializeField] private float expGrowth = 1.25f;
    [SerializeField] private AnimationCurve customNextLevelXp = AnimationCurve.Linear(1, 100, 100, 1000);

    [Header("UI Display")]
    [Tooltip("Optional: slider showing current XP progress in this level.")]
    [SerializeField] private Slider xpSliderUI;
    [SerializeField] private TextMeshProUGUI levelText;

    [Serializable] public class LevelUpEvent : UnityEvent<int> { }
    public LevelUpEvent OnLevelUp;

    private GameObject playerObject;
    private PowerUpSelectionUI PUSUI;

    // Selection queue machinery
    private int pendingSelections = 0;
    private Coroutine selectionRunner;

    public int CurrentLevel => currentLevel;
    public int CurrentXpInLevel => currentXpInLevel;
    public int XpNeededThisLevel => GetXpRequiredForNextLevel(currentLevel);
    public float Progress01 => XpNeededThisLevel > 0 ? (float)currentXpInLevel / XpNeededThisLevel : 1f;
    public bool IsMaxLevel => currentLevel >= maxLevel;

    private void Awake()
    {
        var gc = GameObject.FindGameObjectWithTag("GameController");
        if (gc != null) PUSUI = gc.GetComponent<PowerUpSelectionUI>();

        playerObject = GameObject.FindGameObjectWithTag("Player");
    }

    private void Update()
    {
        UpdateXpUI();
    }

    /// <summary>
    /// Your per-level reward. Still called once per level gained.
    /// </summary>
    public void LevelUp()
    {
        if (playerObject != null && playerObject.TryGetComponent(out SimpleHealth hp))
        {
            hp.maxHealth += 10;
            hp.Heal(10);
            hp.SyncSlider();
        }
    }

    /// <summary>
    /// Adds XP, handles multi-level gains, and queues one selection per level.
    /// Returns how many levels were gained.
    /// </summary>
    public int AddExperience(int amount)
    {
        if (amount <= 0 || IsMaxLevel) return 0;

        int levelsGained = 0;
        int safety = 1000;

        while (amount > 0 && !IsMaxLevel && safety-- > 0)
        {
            int needed = GetXpRequiredForNextLevel(currentLevel);
            int remaining = needed - currentXpInLevel;

            if (amount < remaining)
            {
                currentXpInLevel += amount;
                amount = 0;
                break;
            }

            // consume enough XP to finish this level
            amount -= remaining;

            // level up
            currentLevel = Mathf.Min(currentLevel + 1, maxLevel);
            currentXpInLevel = 0;

            levelsGained++;
            OnLevelUp?.Invoke(currentLevel);
            LevelUp();
        }

        if (IsMaxLevel) currentXpInLevel = 0;

        // Queue exactly one selection per level gained
        if (levelsGained > 0)
        {
            pendingSelections += levelsGained;
            if (selectionRunner == null)
                selectionRunner = StartCoroutine(RunSelectionQueue());
        }

        UpdateXpUI();
        return levelsGained;
    }

    public void SetLevel(int level, bool resetXpInLevel = true)
    {
        currentLevel = Mathf.Clamp(level, 1, maxLevel);
        if (resetXpInLevel)
            currentXpInLevel = 0;
        else
            currentXpInLevel = Mathf.Clamp(currentXpInLevel, 0, GetXpRequiredForNextLevel(currentLevel));

        UpdateXpUI();
    }

    public int GetXpRequiredForNextLevel(int level)
    {
        level = Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));

        switch (curve)
        {
            case CurveType.Linear:
                return Mathf.Max(1, baseNextLevelXp + linearStep * (level - 1));

            case CurveType.Quadratic:
                return Mathf.Max(1, baseNextLevelXp + quadA * (level - 1) * (level - 1));

            case CurveType.Exponential:
                return Mathf.Max(1, Mathf.RoundToInt(baseNextLevelXp * Mathf.Pow(expGrowth, level - 1)));

            case CurveType.CustomPerLevel:
                return Mathf.Max(1, Mathf.RoundToInt(customNextLevelXp.Evaluate(level)));

            default:
                return baseNextLevelXp;
        }
    }

    public int GetTotalXpToReachLevel(int targetLevel)
    {
        targetLevel = Mathf.Clamp(targetLevel, 1, maxLevel);
        int sum = 0;
        for (int L = 1; L < targetLevel; L++)
            sum += GetXpRequiredForNextLevel(L);
        return sum;
    }

    private void UpdateXpUI()
    {
        if (levelText != null)
            levelText.text = "Level " + currentLevel;

        if (xpSliderUI != null)
        {
            xpSliderUI.maxValue = XpNeededThisLevel;
            xpSliderUI.value = currentXpInLevel;
        }
    }

    /// <summary>
    /// Sequentially opens PowerUp selection exactly once per pending level-up.
    /// Waits for the panel to close (Time.timeScale restored) between opens.
    /// </summary>
    private IEnumerator RunSelectionQueue()
    {
        // If selection is already open (paused), wait until it closes
        while (Time.timeScale == 0f) yield return null;

        while (pendingSelections > 0)
        {
            if (PUSUI != null)
            {
                // Open selection (ShowSelection pauses timeScale = 0)
                PUSUI.ShowSelection();

                // Wait while open (paused)
                while (Time.timeScale == 0f)
                    yield return null;

                // Slight delay to avoid same-frame reopen issues
                yield return null;
            }

            pendingSelections--;
        }

        selectionRunner = null;
    }
}
