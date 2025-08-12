using Lexone.UnityTwitchChat;
using NaughtyAttributes;
using System.Collections.Generic;
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
        [Min(0f)] public float timeSpawn = 0f; // eligibility time
    }

    [Header("Spawn Setup")]
    [SerializeField] private List<ChatterSpawnEntry> chatterPrefabs = new();
    [SerializeField] private Transform player;
    [SerializeField] private float minSpawnDistance = 1.5f;
    [SerializeField] private float maxSpawnDistance = 3.5f;
    [SerializeField] private float zOffset = 0f;

    [Header("Limits")]
    [SerializeField, Min(1)] private int maxSpawnCount = 20;

    [Tooltip("How often to increase max spawn count (seconds)")]
    [SerializeField, Min(0f)] private float spawnIncreaseInterval = 60f;

    [Tooltip("How much to increase max spawn count each interval")]
    [SerializeField, Min(1)] private int spawnIncreaseAmount = 1;

    public int minPower = 0; // Minimum power level for chatters to spawn
    public float chanceToUpgradeMinPower = 0.6f; // Chance to upgrade chatter power on spawn

    // Track time for next increase
    private float nextSpawnIncreaseTime = 0f;


    [Header("Collision Check")]
    [Tooltip("Radius used for checking if spawn position overlaps colliders")]
    [SerializeField] private float spawnCheckRadius = 0.5f;
    [Tooltip("Layers to avoid when spawning")]
    [SerializeField] private LayerMask avoidLayers;

    [Header("Repositioning")]
    [Tooltip("If a chatter drifts farther than this from the player, it will be teleported back near the player.")]
    [SerializeField] private float maxDistanceFromPlayer = 12f;

    [Header("UI")]
    [Tooltip("Optional: displays stopwatch time (MM:SS)")]
    [SerializeField] private TextMeshProUGUI stopwatchText;

    // Stopwatch time
    private float elapsedSeconds = 0f;

    private HashSet<string> spawnedChatters = new HashSet<string>();
    private int currentSpawnCount = 0;

    private void Start()
    {
        if (player == null) player = transform;
        if (minSpawnDistance > maxSpawnDistance)
        {
            float t = minSpawnDistance;
            minSpawnDistance = maxSpawnDistance;
            maxSpawnDistance = t;
        }

        IRC.Instance.OnChatMessage += OnChatMessage;
    }

    private void Update()
    {
        // Only update stopwatch if the game isn't paused
        if (Time.timeScale > 0f)
        {
            elapsedSeconds += Time.deltaTime;

            // Check if it's time to increase spawn cap
            if (spawnIncreaseInterval > 0f && elapsedSeconds >= nextSpawnIncreaseTime)
            {
                if (Random.Range(0, 1) < chanceToUpgradeMinPower)
                {
                    minPower++;
                }


                maxSpawnCount += spawnIncreaseAmount;
                nextSpawnIncreaseTime = elapsedSeconds + spawnIncreaseInterval;
                Debug.Log($"[TwitchListener] Max spawn count increased to {maxSpawnCount}");
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

    private void OnChatMessage(Chatter chatter)
    {
        if (player == null) return;
        if (currentSpawnCount >= maxSpawnCount) return;

        var prefab = PickWeightedPrefab();
        if (prefab == null) return;

        string nameKey = chatter.tags.displayName.ToLowerInvariant();
        if (spawnedChatters.Contains(nameKey)) return;

        Vector3 spawnPos;
        int safetyCounter = 0;
        const int maxAttempts = 20;
        do
        {
            safetyCounter++;
            float angle = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Mathf.Lerp(minSpawnDistance * minSpawnDistance,
                                            maxSpawnDistance * maxSpawnDistance,
                                            Random.value));
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * r;
            spawnPos = player.position + offset;
            spawnPos.z += zOffset;

        } while (Physics2D.OverlapCircle(spawnPos, spawnCheckRadius, avoidLayers) != null
                 && safetyCounter < maxAttempts);

        if (safetyCounter >= maxAttempts)
        {
            Debug.LogWarning("Could not find safe spawn position for chatter.");
            return;
        }

        spawnedChatters.Add(nameKey);
        currentSpawnCount++;

        GameObject instantiatedChatter = Instantiate(prefab, spawnPos, Quaternion.identity);

        var tracker = instantiatedChatter.AddComponent<ChatterTracker>();
        tracker.Init(
            listener: this,
            nameKey: nameKey,
            player: player,
            minSpawnDistance: minSpawnDistance,
            maxSpawnDistance: maxSpawnDistance,
            zOffset: zOffset,
            avoidLayers: avoidLayers,
            spawnCheckRadius: spawnCheckRadius,
            maxDistanceFromPlayer: maxDistanceFromPlayer
        );

        var stats = instantiatedChatter.GetComponent<ChatterStats>();
        if (stats != null)
        {
            stats.nameGUI.text = chatter.tags.displayName;
            stats.nameGUI.color = chatter.GetNameColor();
            stats.power = chatter.tags.badges.Length + minPower;
        }

        Debug.Log($"<color=#fef83e><b>[MESSAGE]</b></color> Spawned ({prefab.name}) for {chatter.tags.displayName} at {spawnPos}");
    }

    private GameObject PickWeightedPrefab()
    {
        float now = elapsedSeconds; // use stopwatch time for gating

        // Compute total weight only among eligible entries
        float total = 0f;
        foreach (var e in chatterPrefabs)
        {
            if (e != null && e.prefab != null && e.weight > 0f && now >= e.timeSpawn)
                total += e.weight;
        }
        if (total <= 0f) return null; // nothing eligible yet

        float roll = Random.value * total;
        float acc = 0f;

        foreach (var e in chatterPrefabs)
        {
            if (e == null || e.prefab == null || e.weight <= 0f) continue;
            if (now < e.timeSpawn) continue; // gate by time

            acc += e.weight;
            if (roll <= acc)
                return e.prefab;
        }

        // Fallback (eligible first)
        foreach (var e in chatterPrefabs)
        {
            if (e?.prefab != null && now >= e.timeSpawn) return e.prefab;
        }
        return null;
    }

    public void RemoveChatter(string nameKey)
    {
        if (spawnedChatters.Remove(nameKey))
            currentSpawnCount = Mathf.Max(0, currentSpawnCount - 1);
    }

    private class ChatterTracker : MonoBehaviour
    {
        private TwitchListener listener;
        private string nameKey;

        private Transform player;
        private float minSpawnDistance;
        private float maxSpawnDistance;
        private float zOffset;
        private LayerMask avoidLayers;
        private float spawnCheckRadius;
        private float maxDistanceFromPlayer;

        public void Init(
            TwitchListener listener,
            string nameKey,
            Transform player,
            float minSpawnDistance,
            float maxSpawnDistance,
            float zOffset,
            LayerMask avoidLayers,
            float spawnCheckRadius,
            float maxDistanceFromPlayer)
        {
            this.listener = listener;
            this.nameKey = nameKey;

            this.player = player;
            this.minSpawnDistance = minSpawnDistance;
            this.maxSpawnDistance = maxSpawnDistance;
            this.zOffset = zOffset;
            this.avoidLayers = avoidLayers;
            this.spawnCheckRadius = spawnCheckRadius;
            this.maxDistanceFromPlayer = maxDistanceFromPlayer;
        }

        private void Update()
        {
            if (player == null) return;

            float dist = Vector2.Distance(transform.position, player.position);
            if (dist > maxDistanceFromPlayer)
            {
                // Teleport back near player using the same safe spawn logic
                Vector3 newPos = FindSafePositionNearPlayer();
                transform.position = newPos;
            }
        }

        private Vector3 FindSafePositionNearPlayer()
        {
            Vector3 spawnPos = player.position;
            int safetyCounter = 0;
            const int maxAttempts = 20;

            do
            {
                safetyCounter++;
                float angle = Random.value * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Mathf.Lerp(minSpawnDistance * minSpawnDistance,
                                                maxSpawnDistance * maxSpawnDistance,
                                                Random.value));
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * r;
                spawnPos = player.position + offset;
                spawnPos.z += zOffset;

            } while (Physics2D.OverlapCircle(spawnPos, spawnCheckRadius, avoidLayers) != null
                     && safetyCounter < maxAttempts);

            return spawnPos;
        }

        private void OnDestroy()
        {
            listener?.RemoveChatter(nameKey);
        }
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int mins = (int)(seconds / 60f);
        int secs = (int)(seconds % 60f);
        return $"{mins:00}:{secs:00}";
    }
}