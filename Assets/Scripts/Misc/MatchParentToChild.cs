using UnityEngine;

[ExecuteAlways]
public class MatchParentToChild : MonoBehaviour
{
    [SerializeField] private RectTransform child;

    void Update()
    {
        if (child == null) return;
        RectTransform parent = GetComponent<RectTransform>();
        parent.sizeDelta = child.sizeDelta; // copies width/height
    }
}
