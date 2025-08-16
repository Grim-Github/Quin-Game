using UnityEngine;

[DisallowMultipleComponent]
public class UITabs : MonoBehaviour
{
    [Tooltip("Assign all tab GameObjects here in order.")]
    public GameObject[] tabs;

    [Tooltip("Currently selected tab index.")]
    [SerializeField] private int currentIndex = 0;

    private void Start()
    {
        // Ensure only the current tab is active on start
        SelectTab(currentIndex);
    }

    private void Update()
    {
        if (tabs == null || tabs.Length == 0) return;

        // Scroll wheel input
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll > 0f) // Scroll up
        {
            currentIndex = (currentIndex - 1 + tabs.Length) % tabs.Length;
            SelectTab(currentIndex);
        }
        else if (scroll < 0f) // Scroll down
        {
            currentIndex = (currentIndex + 1) % tabs.Length;
            SelectTab(currentIndex);
        }
    }

    /// <summary>
    /// Activates the tab at the given index and deactivates all others.
    /// </summary>
    public void SelectTab(int index)
    {
        if (tabs == null || tabs.Length == 0)
            return;

        currentIndex = Mathf.Clamp(index, 0, tabs.Length - 1);

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
                tabs[i].SetActive(i == currentIndex);
        }
    }
}
