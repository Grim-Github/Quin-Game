using System.Collections;
using TMPro;
using UnityEngine;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;

    [Header("UI Elements")]
    [SerializeField] private GameObject tooltipPanel; // Parent panel for tooltip
    [SerializeField] private TextMeshProUGUI tooltipText;
    [SerializeField] private Vector2 padding = new Vector2(20f, 10f); // Extra space around text

    [Header("Hover Polling")]
    [Tooltip("Layers to consider for TooltipTarget when polling the mouse position.")]
    [SerializeField] private LayerMask tooltipLayerMask = ~0;
    [Tooltip("If true, the tooltip auto-hides when no valid TooltipTarget is under the cursor.")]
    [SerializeField] private bool autoHideWhenNoTarget = true;

    [Header("Hide Delay")]
    [Tooltip("Time (seconds, unscaled) to wait before hiding after losing target.")]
    [SerializeField] private float hideDelay = 1.5f;

    private RectTransform panelRect;
    private Canvas canvas;

    // Track the current target so we can detect when it gets destroyed/disabled.
    private TooltipTarget currentTarget;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        panelRect = tooltipPanel.GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        HideTooltipImmediate();
    }

    private void Update()
    {
        if (!tooltipPanel.activeSelf) return;

        // Follow mouse
        Vector2 mousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            Input.mousePosition,
            canvas.worldCamera,
            out mousePos
        );
        panelRect.localPosition = mousePos + new Vector2(10f, 10f);

        if (!autoHideWhenNoTarget) return;

        // If our known target is gone/disabled, schedule a hide (once).
        bool targetAlive = currentTarget != null &&
                           currentTarget.isActiveAndEnabled &&
                           currentTarget.gameObject.activeInHierarchy;

        if (!targetAlive)
        {
            ScheduleHideIfNeeded();   // <-- don't restart each frame
            currentTarget = null;
            return;
        }

        // Otherwise, check if we're still over ANY TooltipTarget in world space.
        var world = Camera.main != null
            ? (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition)
            : (Vector2)Input.mousePosition; // fallback

        var hits = Physics2D.OverlapPointAll(world, tooltipLayerMask);
        bool anyTargetUnderMouse = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            var tt = hits[i].GetComponent<TooltipTarget>();
            if (tt != null && tt.isActiveAndEnabled)
            {
                anyTargetUnderMouse = true;
                break;
            }
        }

        if (!anyTargetUnderMouse)
        {
            ScheduleHideIfNeeded();   // <-- don't restart each frame
            currentTarget = null;
        }
    }

    // Overload lets TooltipTarget pass itself in so we can track it
    public void ShowTooltip(string message, TooltipTarget source = null)
    {
        currentTarget = source;

        // Cancel any scheduled hide immediately (we're showing again)
        CancelScheduledHide();

        if (!tooltipPanel.activeSelf)
            tooltipPanel.SetActive(true);

        tooltipText.text = message;

        // Resize to content
        tooltipText.ForceMeshUpdate();
        Vector2 newSize = new Vector2(
            tooltipText.preferredWidth + padding.x,
            tooltipText.preferredHeight + padding.y
        );
        panelRect.sizeDelta = newSize;
    }

    public void HideTooltip()
    {
        ScheduleHideIfNeeded();
    }

    private void HideTooltipImmediate()
    {
        CancelScheduledHide();
        tooltipPanel.SetActive(false);
        tooltipText.text = "";
    }

    // ---- KEY CHANGE: schedule ONCE, don't reset every frame ----
    private void ScheduleHideIfNeeded()
    {
        if (!tooltipPanel.activeSelf) return; // already hidden
        if (hideCoroutine != null) return;    // already scheduled
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private void CancelScheduledHide()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    private IEnumerator HideAfterDelay()
    {
        // Use REALTIME so pauses (timeScale = 0) don't freeze the hide.
        float t = 0f;
        while (t < hideDelay)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        HideTooltipImmediate();
        hideCoroutine = null;
    }
}
