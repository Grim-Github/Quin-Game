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
        float interval = Mathf.Max(0f, listener.spawnIncreaseInterval);
        int cap = Mathf.Max(0, listener.minPower * Mathf.Max(1, listener.maxSpawnPerPowerRatio));

        // --- Current spawns vs global cap ---
        sb.AppendLine($"{h}<b>Spawns:</b></color> {v}{cur}</color> / {v}{cap}</color>");

        sb.AppendLine();

        // --- Min power and upgrade chance ---
        float chance = Mathf.Clamp01(listener.chanceToUpgradeMinPower);
        sb.AppendLine($"{h}<b>Power:</b></color> {v}{listener.minPower}</color>");
        if (interval > 0f)
            sb.AppendLine($"{n}Chance to +1 every {interval:0}s:</color> {v}{chance * 100f:0}%</color>");
        else
            sb.AppendLine($"{n}Power growth:</color> {v}disabled</color>");

        return sb.ToString();
    }

    private static string ColorTag(string hexNoHash) => $"<color=#{hexNoHash}>";
}
