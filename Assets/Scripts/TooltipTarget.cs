using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TooltipTarget : MonoBehaviour
{
    [TextArea] public string tooltipMessage;

    private void OnMouseEnter()
    {
        if (TooltipManager.Instance == null) return;

        string fullMessage = tooltipMessage;

        // Append SimpleHealth.extraTextField (where your rarity text goes)
        var health = GetComponent<SimpleHealth>();
        if (health != null)
        {
            var field = typeof(SimpleHealth).GetField(
                "extraTextField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            if (field != null)
            {
                string extra = field.GetValue(health) as string;
                if (!string.IsNullOrWhiteSpace(extra))
                {
                    if (!string.IsNullOrWhiteSpace(fullMessage))
                        fullMessage += "\n\n";
                    fullMessage += extra;
                }
            }
        }

        // Pass 'this' so the manager can verify if we get destroyed/disabled
        TooltipManager.Instance.ShowTooltip(fullMessage, this);
    }

    private void OnMouseExit()
    {
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.HideTooltip();
    }

    private void OnDisable()
    {
        // In case the object gets disabled while hovered
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.HideTooltip();
    }

    private void OnDestroy()
    {
        // In case the object is destroyed while hovered
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.HideTooltip();
    }
}
