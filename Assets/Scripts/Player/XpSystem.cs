using System;
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
    [Tooltip("Optional: TextMeshProUGUI element to display XP progress as 'current/needed'.")]
    [SerializeField] private Slider xpSliderUI;
    [SerializeField]
    private TextMeshProUGUI levelText
        ;


    [Serializable] public class LevelUpEvent : UnityEvent<int> { }
    public LevelUpEvent OnLevelUp;

    private GameObject playerObject;
    private PowerUpSelectionUI PUSUI;

    public int CurrentLevel => currentLevel;
    public int CurrentXpInLevel => currentXpInLevel;
    public int XpNeededThisLevel => GetXpRequiredForNextLevel(currentLevel);
    public float Progress01 => XpNeededThisLevel > 0 ? (float)currentXpInLevel / XpNeededThisLevel : 1f;
    public bool IsMaxLevel => currentLevel >= maxLevel;

    private void Awake()
    {
        PUSUI = GameObject.FindGameObjectWithTag("GameController").GetComponent<PowerUpSelectionUI>();
        playerObject = GameObject.FindGameObjectWithTag("Player");
    }

    private void Update()
    {
        UpdateXpUI();
    }

    public void LevelUp()
    {
        playerObject.GetComponent<SimpleHealth>().maxHealth += 10;
        playerObject.GetComponent<SimpleHealth>().Heal(10);
    }

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

            amount -= remaining;
            currentLevel = Mathf.Min(currentLevel + 1, maxLevel);
            currentXpInLevel = 0;
            PUSUI.ShowSelection();
            levelsGained++;
            OnLevelUp?.Invoke(currentLevel); LevelUp();
        }

        if (IsMaxLevel) currentXpInLevel = 0;

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
        levelText.text = "Level " + currentLevel;

        if (xpSliderUI != null)
        {
            xpSliderUI.maxValue = XpNeededThisLevel;
            xpSliderUI.value = currentXpInLevel;
        }
    }
}
