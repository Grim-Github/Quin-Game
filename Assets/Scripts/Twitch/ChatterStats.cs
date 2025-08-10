using TMPro;
using UnityEngine;

public class ChatterStats : MonoBehaviour
{
    public TextMeshProUGUI nameGUI;
    public int power = 0;

    private void Start()
    {
        var health = GetComponent<SimpleHealth>();
        if (health != null)
        {
            int baseMaxHealth = health.maxHealth; // store original
            int bonusFromPercent = Mathf.RoundToInt(baseMaxHealth * 0.075f * power);
            int bonusFromFlat = 0;

            health.maxHealth += bonusFromPercent + bonusFromFlat;
            health.Heal(bonusFromPercent + bonusFromFlat);
            health.SyncSlider();
        }
    }
}
