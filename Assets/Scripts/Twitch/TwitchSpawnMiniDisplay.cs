using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TwitchSpawnMiniDisplay : MonoBehaviour
{
    [Header("Sources")]
    [Tooltip("Drag your TwitchListener here.")]
    public TwitchListener listener;

    [Tooltip("TMP text target where the info will be rendered.")]
    public TextMeshProUGUI targetText;

    [Header("Update")]
    [Min(0.05f)] public float refreshInterval = 0.25f;

    [Header("Style (hex codes without #)")]
    public string headingHex = "FFD166"; // yellow
    public string valueHex = "00AEEF"; // cyan
    public string noteHex = "9E9E9E"; // gray

    private float nextRefresh;

    private void Reset()
    {
        if (!targetText) targetText = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void Update()
    {
        if (!listener || !targetText) return;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;

        targetText.richText = true;
        targetText.text = BuildText();
    }

    private string BuildText()
    {
        var sb = new StringBuilder(256);

        string h = ColorTag(headingHex);
        string v = ColorTag(valueHex);
        string n = ColorTag(noteHex);

        int cur = Mathf.Max(0, listener.spawnedChatters.Count);
        int cap = Mathf.Max(0, listener.maxSpawnCount);
        float interval = Mathf.Max(0f, listener.spawnIncreaseInterval);
        int inc = Mathf.Max(0, listener.spawnIncreaseAmount);

        // --- Current spawns vs cap + growth rules ---
        sb.AppendLine($"{h}<b>Spawns:</b></color> {v}{cur}</color> / {v}{cap}</color>");
        if (interval > 0f && inc > 0)
            sb.AppendLine($"{n}Grows by</color> {v}{inc}</color> {n}every</color> {v}{interval:0}s</color>");
        else
            sb.AppendLine($"{n}Spawn cap growth:</color> {v}disabled</color>");

        sb.AppendLine();

        // --- Min power and upgrade chance ---
        float chance = Mathf.Clamp01(listener.chanceToUpgradeMinPower);
        sb.AppendLine($"{h}<b>Power:</b></color> {v}{listener.minPower}</color>");
        sb.AppendLine($"{n}Chance to +1 each growth:</color> {v}{chance * 100f:0}%</color>");

        return sb.ToString();
    }

    private static string ColorTag(string hexNoHash) => $"<color=#{hexNoHash}>";
}
