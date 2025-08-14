using TMPro;
using UnityEngine;

[ExecuteAlways] // Updates in edit mode too
public class ParentToTMPSize : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tmpText;

    void Update()
    {
        if (!tmpText) return;

        // Get preferred size of the text
        Vector2 size = new Vector2(tmpText.preferredWidth, tmpText.preferredHeight);

        // Apply to parent RectTransform
        RectTransform parentRect = GetComponent<RectTransform>();
        parentRect.sizeDelta = size;
    }
}
