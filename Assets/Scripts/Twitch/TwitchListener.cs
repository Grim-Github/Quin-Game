using Lexone.UnityTwitchChat;
using NaughtyAttributes;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class TwitchListener : MonoBehaviour
{
    [System.Serializable]
    public class ChatterSpawnEntry
    {
        [ShowAssetPreview] public GameObject prefab;

        [Min(0f)] public float weight = 1f;

        [Header("Spawn Gate")]
        [Tooltip("Seconds since start before this prefab can be selected to spawn.")]
        [MinMaxSlider(0, 600f)] public Vector2 timeSpawn = new Vector2(0, 600); // eligibility time
    }

    [Header("Spawn Setup")]
    [SerializeField] private List<ChatterSpawnEntry> chatterPrefabs = new();
    [SerializeField] private Transform player;
    [SerializeField] private float minSpawnDistance = 1.5f;
    [SerializeField] private float maxSpawnDistance = 3.5f;

    [Header("Limits")]
    [SerializeField, Min(1)] public int maxSpawnCount = 20;

    [Tooltip("How often to increase max spawn count (seconds)")]
    [SerializeField, Min(0f)] public float spawnIncreaseInterval = 60f;

    [Tooltip("How much to increase max spawn count each interval")]
    [SerializeField, Min(1)] public int spawnIncreaseAmount = 1;

    public int minPower = 0; // Minimum power level for chatters to spawn
    public float chanceToUpgradeMinPower = 0.6f; // Chance to upgrade chatter power on spawn

    // Track time for next increase
    private float nextSpawnIncreaseTime = 0f;

    [Header("Collision Check")]
    [Tooltip("Radius used for checking if spawn position is ON these layers (e.g., Ground).")]
    [SerializeField] private float spawnCheckRadius = 0.5f;

    [Tooltip("Layers the spawn position MUST overlap (e.g., Ground/Walkable).")]
    [SerializeField] private LayerMask spawnOnLayers;

    [Header("Repositioning")]
    [Tooltip("If a chatter drifts farther than this from the player, it will be teleported back near the player.")]
    [SerializeField] private float maxDistanceFromPlayer = 12f;

    [Header("UI")]
    [Tooltip("Optional: displays stopwatch time (MM:SS)")]
    [SerializeField] private TextMeshProUGUI stopwatchText;

    // Stopwatch time
    private float elapsedSeconds = 0f;
    // Track spawned chatters
    [SerializeField] public readonly List<GameObject> spawnedChatters = new();

    private void Start()
    {
        if (player == null) player = transform;
        if (minSpawnDistance > maxSpawnDistance)
        {
            float t = minSpawnDistance;
            minSpawnDistance = maxSpawnDistance;
            maxSpawnDistance = t;
        }

        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage += OnChatMessage;
    }

    private void Update()
    {

        for (int i = spawnedChatters.Count - 1; i >= 0; i--)
            if (spawnedChatters[i] == null)
                spawnedChatters.RemoveAt(i);


        // Only update stopwatch if the game isn't paused
        if (Time.timeScale > 0f)
        {
            elapsedSeconds += Time.deltaTime;

            // Check if it's time to increase spawn cap
            if (spawnIncreaseInterval > 0f && elapsedSeconds >= nextSpawnIncreaseTime)
            {
                if (Random.value < chanceToUpgradeMinPower)
                {
                    minPower++;
                }

                maxSpawnCount += spawnIncreaseAmount;
                nextSpawnIncreaseTime = elapsedSeconds + spawnIncreaseInterval;
                Debug.Log($"[TwitchListener] Max spawn count increased to {maxSpawnCount}");
            }


            // Reposition chatters if they drift too far
            for (int i = spawnedChatters.Count - 1; i >= 0; i--)
            {
                GameObject chatterObj = spawnedChatters[i];
                if (chatterObj == null || player != null) continue;

                float dist = Vector3.Distance(player.position, chatterObj.transform.position);
                if (dist > maxDistanceFromPlayer)
                {
                    Vector3? newPos = FindValidSpawnPosition();
                    if (newPos.HasValue)
                    {
                        // Teleport chatter safely
                        Rigidbody2D rb = chatterObj.GetComponent<Rigidbody2D>();
                        if (rb != null)
                            rb.position = newPos.Value; // physics-safe teleport
                        else
                            chatterObj.transform.position = newPos.Value;

                        Debug.Log($"[TwitchListener] Repositioned {chatterObj.name} to stay near player.");
                    }
                }
            }

        }

        // Update stopwatch UI
        if (stopwatchText != null)
            stopwatchText.text = FormatTime(elapsedSeconds);
    }

    private void OnDestroy()
    {
        if (IRC.Instance != null)
            IRC.Instance.OnChatMessage -= OnChatMessage;
    }


    private Vector2? FindValidSpawnPosition()
    {
        if (player == null) return null;

        Vector3 spawnPos = player.position;
        int safetyCounter = 0;
        const int maxAttempts = 20;

        while (safetyCounter < maxAttempts)
        {
            safetyCounter++;

            float angle = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Mathf.Lerp(minSpawnDistance * minSpawnDistance,
                                            maxSpawnDistance * maxSpawnDistance,
                                            Random.value));
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * r;
            spawnPos = player.position + offset;

            if (Physics2D.OverlapCircle(spawnPos, spawnCheckRadius, spawnOnLayers) != null)
                return spawnPos; // ✅ Found a valid position
        }

        Debug.LogWarning("[TwitchListener] Could not find valid spawn position.");
        return null; // ❌ Failed
    }



    private void OnChatMessage(Chatter chatter)
    {
        if (player == null) return;
        if (spawnedChatters.Count >= maxSpawnCount) return;

        var prefab = PickWeightedPrefab();
        if (prefab == null) return;

        // Use displayName (lowercased) as unique key
        string nameKey = (chatter?.tags?.displayName ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrEmpty(nameKey)) return;

        // --- NEW: Prevent duplicate chatter spawns ---
        if (spawnedChatters.Any(c => c != null &&
                                     c.name.Equals(chatter.tags.displayName,
                                                   System.StringComparison.OrdinalIgnoreCase)))
        {
            Debug.Log($"Chatter {chatter.tags.displayName} already spawned, skipping.");
            return;
        }
        // Find a valid spawn position
        Vector3? spawnPosNullable = FindValidSpawnPosition();
        if (!spawnPosNullable.HasValue)
            return;

        Vector3 spawnPos = spawnPosNullable.Value;

        GameObject instantiatedChatter = Instantiate(prefab, spawnPos, Quaternion.identity);
        instantiatedChatter.transform.name = chatter.tags.displayName;

        // --- NEW: Keep track ---
        spawnedChatters.Add(instantiatedChatter);

        var stats = instantiatedChatter.GetComponent<ChatterStats>();
        if (stats != null)
        {
            stats.nameGUI.text = chatter.tags.displayName;
            stats.nameGUI.color = chatter.GetNameColor();
            stats.power = chatter.tags.badges.Length + minPower;
        }

        var chatterMessage = instantiatedChatter.GetComponent<ChatterMessagePopups>();
        if (chatterMessage != null)
            chatterMessage.ShowMessage(chatter.message);

        Debug.Log($"<color=#fef83e><b>[MESSAGE]</b></color> Spawned ({prefab.name}) for {chatter.tags.displayName} at {spawnPos}");
    }


    private GameObject PickWeightedPrefab()
    {
        float now = elapsedSeconds; // stopwatch time

        // 1) Sum weights only for entries eligible in [min..max] window
        float total = 0f;
        foreach (var e in chatterPrefabs)
        {
            if (e != null && e.prefab != null && e.weight > 0f && IsEligibleByTime(now, e.timeSpawn))
                total += e.weight;
        }
        if (total <= 0f) return null; // nothing eligible at this time

        // 2) Weighted roll among only eligible entries
        float roll = Random.value * total;
        float acc = 0f;

        foreach (var e in chatterPrefabs)
        {
            if (e == null || e.prefab == null || e.weight <= 0f) continue;
            if (!IsEligibleByTime(now, e.timeSpawn)) continue;

            acc += e.weight;
            if (roll <= acc)
                return e.prefab;
        }

        // 3) Fallback (shouldn't happen if total>0, but safe)
        foreach (var e in chatterPrefabs)
        {
            if (e?.prefab != null && IsEligibleByTime(now, e.timeSpawn))
                return e.prefab;
        }
        return null;
    }

    private static bool IsEligibleByTime(float now, Vector2 window)
    {
        // window.x = earliest allowed time, window.y = latest allowed time
        return now >= window.x && now <= window.y;
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int mins = (int)(seconds / 60f);
        int secs = (int)(seconds % 60f);
        return $"{mins:00}:{secs:00}";
    }
}
