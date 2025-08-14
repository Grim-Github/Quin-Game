using TMPro; // optional, only used if your button has a TMP label
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class WeaponRerollUIHelper : MonoBehaviour
{
    [Header("References")]
    [Tooltip("UI Button prefab (should contain a Text or TMP_Text for the label).")]
    public Button buttonPrefab;

    [Tooltip("Parent transform for created buttons (e.g., a VerticalLayoutGroup).")]
    public Transform buttonParent;

    [Tooltip("Target WeaponRarityController to control.")]
    public WeaponRarityController target;

    private void Start()
    {
        if (!buttonPrefab || !target)
        {
            Debug.LogError("[WeaponRerollUIHelper] Missing references: Button Prefab and Target are required.");
            return;
        }

        if (!buttonParent) buttonParent = transform;

        // void methods – can be passed directly
        CreateButton("Reroll Rarity + Stats", target.RerollRarityAndStats);
        CreateButton("Reroll Stats", target.RerollStats);

        // bool-returning methods – wrap in lambdas to match UnityAction (void)
        CreateButton("Reroll Random Stat", () => { target.RerollRandomStat(); });
        CreateButton("Reroll Into Another", () => { target.RerollRandomStatIntoAnother(); });
        CreateButton("Upgrade Random Tier", () => { target.UpgradeRandomTier(1, true); });
    }

    private void CreateButton(string label, UnityAction onClick)
    {
        var btn = Instantiate(buttonPrefab, buttonParent);
        btn.onClick.AddListener(onClick);

        // Try TMP first, then legacy Text
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp) tmp.text = label;
        else
        {
            var txt = btn.GetComponentInChildren<Text>();
            if (txt) txt.text = label;
        }

        btn.name = $"Btn_{label.Replace(" ", "")}";
    }
}
