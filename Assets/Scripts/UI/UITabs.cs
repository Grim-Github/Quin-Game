using UnityEngine;

public class UITabs : MonoBehaviour
{
    [Tooltip("Assign all tab GameObjects here in order.")]
    public GameObject[] tabs;

    /// <summary>
    /// Activates the tab at the given index and deactivates all others.
    /// </summary>
    public void SelectTab(int index)
    {
        if (tabs == null || tabs.Length == 0)
            return;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
                tabs[i].SetActive(i == index);
        }
    }
}
