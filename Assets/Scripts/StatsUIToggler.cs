using UnityEngine;
using UnityEngine.Rendering;

public class StatsUIToggler : MonoBehaviour
{
    [Tooltip("UI object to show when holding the key.")]
    [SerializeField] private GameObject statsUI;

    [Tooltip("Key to hold to show stats.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Tooltip("Time scale when holding the key (1 = normal, 0.5 = half speed, 0 = paused).")]
    [Range(0f, 1f)]
    [SerializeField] private float slowTimeScale = 0.5f;

    [SerializeField] private Volume slowMoVolume;

    private float defaultTimeScale;

    private void Start()
    {
        if (statsUI != null)
            statsUI.SetActive(false);
        if (slowMoVolume) slowMoVolume.weight = 0f;

        defaultTimeScale = Time.timeScale;
    }

    private void Update()
    {
        // Block toggler when ShowSelection (or any full pause) is active.
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            if (statsUI && statsUI.activeSelf) statsUI.SetActive(false);
            if (slowMoVolume) slowMoVolume.weight = 0f;
            return;
        }

        if (Input.GetKey(toggleKey))
        {
            if (statsUI && !statsUI.activeSelf)
            {
                statsUI.SetActive(true);
                Time.timeScale = slowTimeScale;
            }
            if (slowMoVolume) slowMoVolume.weight = 1f;
        }
        else
        {
            if (statsUI && statsUI.activeSelf)
            {
                statsUI.SetActive(false);
                Time.timeScale = defaultTimeScale;
            }
            if (slowMoVolume) slowMoVolume.weight = 0f;
        }
    }
}
