using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro; // optional, only if you show labels with TMP
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class WeaponRerollUIHelper : MonoBehaviour
{
    [Header("Selection UI (Required)")]
    public Button prevButton;
    public Button nextButton;

    [Header("Action Buttons (Optional, in order)")]
    [Tooltip("0=Reroll Rarity+Stats, 1=Reroll Stats, 2=Reroll Random Stat, 3=Reroll Into Another, 4=Upgrade Random Tier, 5=Upgrade Rarity (keep stats, add unique), 6=Remove Random Upgrade, 7=Add Random Upgrade")]
    public Button[] actionButtons;

    [Header("Labels & Icon (Optional)")]
    public TMP_Text selectedNameLabel;
    public TMP_Text selectedExtraLabel;
    public Image selectedIcon; // will display weaponSprite if available

    // Cached active controllers
    private readonly List<WeaponRarityController> controllers = new List<WeaponRarityController>();
    private int index = -1;

    // Track CTRL display state to avoid unnecessary UI churn
    private bool lastCtrlHeld = false;

    private void Awake()
    {
        if (prevButton != null)
        {
            prevButton.onClick.RemoveAllListeners();
            prevButton.onClick.AddListener(() =>
            {
                RefreshControllers();
                SelectPrev();
            });
        }
        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() =>
            {
                RefreshControllers();
                SelectNext();
            });
        }

        WireActions();

        if (controllers.Count > 0)
            index = 0;

        UpdateSelectionUI();

        Debug.Log($"[WeaponRerollUIHelper] Cached {controllers.Count} active WeaponRarityController(s).");
        for (int i = 0; i < controllers.Count; i++)
        {
            var c = controllers[i];
            Debug.Log($" - #{i}: {c.name} | Extra: \"{GetExtraText(c)}\"");
        }
    }

    private void Update()
    {
        // Detect CTRL state (either left or right)
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl != lastCtrlHeld)
        {
            lastCtrlHeld = ctrl;
            // Rebuild the selection UI text according to the new CTRL state
            UpdateSelectionUI();
        }
    }

    [ContextMenu("Refresh Controllers")]
    public void RefreshControllers()
    {
        controllers.Clear();

        var found = FindObjectsByType<WeaponRarityController>(FindObjectsSortMode.None)
            .Where(c => c != null && c.isActiveAndEnabled);

        controllers.AddRange(found);

        if (controllers.Count == 0)
        {
            index = -1;
        }
        else
        {
            if (index < 0 || index >= controllers.Count)
                index = 0; // Auto-select first if none
        }
    }

    public WeaponRarityController CurrentTarget()
    {
        if (index < 0 || index >= controllers.Count) return null;
        return controllers[index];
    }


    private void OnEnable()
    {
        RefreshControllers();

        // Auto-select if nothing selected but we have weapons
        if (index < 0 && controllers.Count > 0)
            index = 0;

        UpdateSelectionUI();

    }

    public void SelectPrev()
    {
        if (controllers.Count == 0) return;
        index = (index - 1 + controllers.Count) % controllers.Count;
        UpdateSelectionUI();
    }

    public void SelectNext()
    {
        if (controllers.Count == 0) return;
        index = (index + 1) % controllers.Count;
        UpdateSelectionUI();
    }

    private void UpdateSelectionUI()
    {
        var target = CurrentTarget();
        bool ctrl = lastCtrlHeld;

        // --- Labels ---
        if (selectedNameLabel != null)
            selectedNameLabel.text = target ? target.name : "<none>";

        if (selectedExtraLabel != null)
        {
            if (!target)
            {
                selectedExtraLabel.text = "";
            }
            else
            {
                // CTRL held => rarity + ranges overlay; otherwise show the weapon's own extra text
                selectedExtraLabel.text = ctrl ? GetCtrlOverlayText(target) : GetExtraText(target);
            }
        }

        // --- Icon ---
        if (selectedIcon != null)
        {
            if (GetWeaponSprite(target) != null)
            {
                selectedIcon.sprite = GetWeaponSprite(target);
            }
        }

        // --- Buttons ---
        bool hasTarget = target != null;


        if (actionButtons != null)
        {
            foreach (var b in actionButtons)
                if (b != null) b.enabled = hasTarget;
        }
    }


    private void WireActions()
    {
        if (actionButtons == null || actionButtons.Length == 0) return;

        UnityAction[] actions =
        {
            () => { RefreshControllers(); CurrentTarget()?.RerollRarityAndStats(); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.RerollStats(); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.RerollRandomStat(); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.RerollRandomStatIntoAnother(); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.RandomizeRandomTier(true); UpdateSelectionUI(); },
            () => { RefreshControllers(); CurrentTarget()?.UpgradeRarityKeepStats(); UpdateSelectionUI(); },

            // NEW 6: Remove a random applied upgrade
            () => { RefreshControllers(); CurrentTarget()?.RemoveRandomUpgrade(); UpdateSelectionUI(); },

            // NEW 7: Add a random applicable upgrade
            () => { RefreshControllers(); CurrentTarget()?.AddRandomUpgrade(); UpdateSelectionUI(); },
        };

        for (int i = 0; i < actionButtons.Length; i++)
        {
            var btn = actionButtons[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            if (i < actions.Length) btn.onClick.AddListener(actions[i]);
            // No automatic text setting; you control button visuals in the Inspector
        }
    }

    private string GetCtrlOverlayText(WeaponRarityController controller)
    {
        // Rarity + ranges (ranges come from controller.GetRollableRangesSummary()).
        // Rarity is fetched by reflection (controller keeps it private).
        string rarity = GetRarityText(controller);
        string ranges = SafeGetRangesSummary(controller);

        if (!string.IsNullOrEmpty(rarity) && !string.IsNullOrEmpty(ranges))
            return $"<b>Rarity:</b> {rarity}\n{ranges}";
        if (!string.IsNullOrEmpty(rarity))
            return $"<b>Rarity:</b> {rarity}";
        return ranges ?? "";
    }

    private string SafeGetRangesSummary(WeaponRarityController controller)
    {
        if (!controller) return "";
        try
        {
            // Requires the public API you added on the controller
            return controller.GetRollableRangesSummary();
        }
        catch
        {
            // If not present, fall back to nothing
            return "";
        }
    }

    private string GetRarityText(WeaponRarityController controller)
    {
        if (!controller) return "";
        try
        {
            var field = typeof(WeaponRarityController)
                .GetField("current", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                var val = field.GetValue(controller);
                if (val != null)
                {
                    // Use your existing rarity formatter so colors match rarity type
                    var method = typeof(WeaponContext)
                        .GetMethod("FormatRarity", BindingFlags.Public | BindingFlags.Static);
                    if (method != null)
                    {
                        return (string)method.Invoke(null, new object[] { val }) ?? "";
                    }
                    else
                    {
                        // Fallback: just return the enum name
                        return val.ToString();
                    }
                }
            }
        }
        catch { }
        return "";
    }




    private string GetExtraText(WeaponRarityController controller)
    {
        if (!controller) return "";

        var shooter = controller.GetComponent<SimpleShooter>();
        if (shooter != null) return shooter.extraTextField;

        var knife = controller.GetComponent<Knife>();
        if (knife != null) return knife.extraTextField;

        return "";
    }

    private Sprite GetWeaponSprite(WeaponRarityController controller)
    {
        if (!controller) return null;

        var shooter = controller.GetComponent<SimpleShooter>();
        if (shooter != null) return shooter.weaponSprite;

        var knife = controller.GetComponent<Knife>();
        if (knife != null) return knife.weaponSprite;

        return null;
    }
}
