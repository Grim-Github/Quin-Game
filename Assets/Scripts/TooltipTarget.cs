using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TooltipTarget : MonoBehaviour
{
    [TextArea] public string tooltipMessage;

    private void OnMouseEnter()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowTooltip(tooltipMessage);
        }
    }

    private void OnMouseExit()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }
}
