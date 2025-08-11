using TMPro;
using UnityEngine;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    [Header("UI Elements")]
    [SerializeField] private GameObject tooltipPanel; // Parent panel for tooltip
    [SerializeField] private TextMeshProUGUI tooltipText;

    private RectTransform panelRect;
    private Canvas canvas;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        panelRect = tooltipPanel.GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        HideTooltip();
    }

    private void Update()
    {
        if (tooltipPanel.activeSelf)
        {
            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                Input.mousePosition,
                canvas.worldCamera,
                out mousePos
            );

            panelRect.localPosition = mousePos + new Vector2(10f, 10f); // Small offset from cursor
        }
    }

    public void ShowTooltip(string message)
    {
        tooltipPanel.SetActive(true);
        tooltipText.text = message;
    }

    public void HideTooltip()
    {
        tooltipPanel.SetActive(false);
        tooltipText.text = "";
    }
}
