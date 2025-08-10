using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerUpSelectionUI : MonoBehaviour
{
    [Header("UI Setup")]
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private TMP_Text[] nameTexts;
    [SerializeField] private TMP_Text[] descriptionTexts;
    [SerializeField] private Button[] selectButtons;
    [SerializeField] private Image[] iconImages;

    [Header("References")]
    [SerializeField] private PowerUpChooser powerUpChooser;

    [Header("Defaults")]
    [Tooltip("Default icon to use when a power-up has no icon.")]
    [SerializeField] public Sprite defaultIcon;

    private int[] shownIndices;
    private bool warnedNoDefault;

    private void Awake()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);

        // Wire up buttons safely
        for (int i = 0; i < selectButtons.Length; i++)
        {
            if (selectButtons[i] == null) continue;
            int idx = i; // capture
            selectButtons[i].onClick.RemoveAllListeners();
            selectButtons[i].onClick.AddListener(() => SelectPowerUp(idx));
        }
    }

    public void ShowSelection()
    {
        if (powerUpChooser == null || powerUpChooser.powerUps == null || powerUpChooser.powerUps.Count == 0)
        {
            Debug.LogWarning("[PowerUpSelectionUI] No power-ups available!");
            return;
        }

        // Build eligible candidates (only by caps; avoid over-filtering)
        List<int> candidates = new List<int>();
        for (int i = 0; i < powerUpChooser.powerUps.Count; i++)
        {
            var pu = powerUpChooser.powerUps[i];

            bool alreadyActive = pu.powerUpObject != null &&
                                 pu.powerUpObject.scene.IsValid() &&
                                 pu.powerUpObject.activeInHierarchy;

            if (!alreadyActive && powerUpChooser.CanSelectByIndex(i))
                candidates.Add(i);
        }


        if (candidates.Count == 0)
        {
            Debug.Log("[PowerUpSelectionUI] No eligible power-ups to show (type caps reached).");
            ClosePanel();
            return;
        }

        Time.timeScale = 0f;
        if (selectionPanel != null) selectionPanel.SetActive(true);

        int slotCount = Mathf.Min(3, selectButtons.Length, candidates.Count);
        shownIndices = PickRandomUnique(candidates, slotCount);

        if (defaultIcon == null && !warnedNoDefault)
        {
            warnedNoDefault = true;
            Debug.LogWarning("[PowerUpSelectionUI] defaultIcon is not assigned. Consider assigning one to guarantee a sprite.");
        }

        // Fill visible slots
        for (int i = 0; i < selectButtons.Length; i++)
        {
            bool has = i < shownIndices.Length;

            if (has)
            {
                var pu = powerUpChooser.powerUps[shownIndices[i]];

                if (nameTexts != null && i < nameTexts.Length && nameTexts[i] != null)
                    nameTexts[i].text = pu.powerUpName;

                if (descriptionTexts != null && i < descriptionTexts.Length && descriptionTexts[i] != null)
                    descriptionTexts[i].text = pu.powerUpDescription;

                // ICONS: per-powerup icon -> defaultIcon (guaranteed fallback)
                if (iconImages != null && i < iconImages.Length && iconImages[i] != null)
                {
                    var img = iconImages[i];
                    Sprite spriteToUse = pu.powerUpIcon != null ? pu.powerUpIcon : defaultIcon;

                    // Assign and ensure visible
                    img.sprite = spriteToUse;
                    img.enabled = true;
                    img.gameObject.SetActive(true);
                }

                if (selectButtons[i] != null)
                {
                    selectButtons[i].interactable = true;
                    selectButtons[i].gameObject.SetActive(true);
                }
            }
            else
            {
                // Hide unused slots entirely
                if (nameTexts != null && i < nameTexts.Length && nameTexts[i] != null)
                    nameTexts[i].text = string.Empty;

                if (descriptionTexts != null && i < descriptionTexts.Length && descriptionTexts[i] != null)
                    descriptionTexts[i].text = string.Empty;

                if (iconImages != null && i < iconImages.Length && iconImages[i] != null)
                {
                    iconImages[i].sprite = null;
                    iconImages[i].enabled = false;
                    iconImages[i].gameObject.SetActive(false);
                }

                if (selectButtons[i] != null)
                {
                    selectButtons[i].interactable = false;
                    selectButtons[i].gameObject.SetActive(false);
                }
            }
        }
    }

    private void SelectPowerUp(int buttonSlot)
    {
        if (shownIndices == null || buttonSlot < 0 || buttonSlot >= shownIndices.Length) return;

        int powerUpIndex = shownIndices[buttonSlot];
        if (powerUpChooser == null || powerUpChooser.powerUps == null ||
            powerUpIndex < 0 || powerUpIndex >= powerUpChooser.powerUps.Count)
        {
            Debug.LogWarning("[PowerUpSelectionUI] Selected power-up index is no longer valid.");
            ClosePanel();
            return;
        }

        powerUpChooser.TryChoosePowerUp(powerUpIndex);
        ClosePanel();
    }

    private void ClosePanel()
    {
        if (selectionPanel != null) selectionPanel.SetActive(false);
        Time.timeScale = 1f;
        shownIndices = null;
    }
    private int[] PickRandomUnique(List<int> source, int count)
    {
        List<int> result = new List<int>(count);
        List<int> available = new List<int>(source);

        for (int picks = 0; picks < count && available.Count > 0; picks++)
        {
            // Calculate total weight
            float totalWeight = 0f;
            foreach (var idx in available)
                totalWeight += Mathf.Max(0f, powerUpChooser.powerUps[idx].weight);

            // Roll
            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            int chosenIndex = available[0];

            foreach (var idx in available)
            {
                cumulative += Mathf.Max(0f, powerUpChooser.powerUps[idx].weight);
                if (roll <= cumulative)
                {
                    chosenIndex = idx;
                    break;
                }
            }

            result.Add(chosenIndex);
            available.Remove(chosenIndex);
        }

        return result.ToArray();
    }

}
