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

    [Header("Power")]
    public int minPower = 0; // Minimum power level for chatters to spawn
    public float chanceToUpgradeMinPower = 0.6f; // Chance to upgrade chatter power on interval

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

    private float elapsedSeconds = 0f;

    [Header("Debug / Runtime")]
    public List<GameObject> spawnedObjects = new(); // Now public for Inspector view
    private readonly HashSet<string> spawnedChatters = new();

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

        nextSpawnIncreaseTime = spawnIncreaseInterval > 0f ? spawnIncreaseInterval : 0f;

        IRC.Instance.OnChatMessage += OnChatMessage;
    }

    private void Update()
    {
        if (Time.timeScale > 0f)
        {
            elapsedSeconds += Time.deltaTime;

            if (spawnIncreaseInterval > 0f && elapsedSeconds >= nextSpawnIncreaseTime)
            {
                if (Random.value < chanceToUpgradeMinPower)
                    minPower++;

                maxSpawnCount += spawnIncreaseAmount;
                nextSpawnIncreaseTime = elapsedSeconds + spawnIncreaseInterval;
                Debug.Log($"[TwitchListener] Max spawn count increased to {maxSpawnCount} (minPower={minPower})");
            }
        }

        if (stopwatchText != null)
            stopwatchText.text = FormatTime(elapsedSeconds);

        MaintainSpawned();
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

        string display = chatter.tags.displayName;
        string nameKey = display.ToLowerInvariant();

        if (spawnedChatters.Contains(nameKey)) return;

        Vector3 spawnPos;
        if (!TryFindSafeSpawnPosition(out spawnPos))
        {
            Debug.LogWarning("[TwitchListener] Could not find safe spawn position for chatter.");
            return;
        }

        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
        go.name = display; // Set name to chatter display name

        var stats = go.GetComponent<ChatterStats>();
        if (stats != null)
        {
            stats.nameGUI.text = display;
            stats.nameGUI.color = chatter.GetNameColor();
            stats.power = chatter.tags.badges.Length + minPower;
        }

        spawnedChatters.Add(nameKey);
        spawnedObjects.Add(go);
        currentSpawnCount++;

        Debug.Log($"<color=#fef83e><b>[MESSAGE]</b></color> Spawned ({prefab.name}) for {display} at {spawnPos}");
    }

    private GameObject PickWeightedPrefab()
    {
        float now = elapsedSeconds;

        float total = 0f;
        foreach (var e in chatterPrefabs)
        {
            if (e != null && e.prefab != null && e.weight > 0f && now >= e.timeSpawn)
                total += e.weight;
        }
        if (total <= 0f) return null;

        float roll = Random.value * total;
        float acc = 0f;

        foreach (var e in chatterPrefabs)
        {
            if (e == null || e.prefab == null || e.weight <= 0f) continue;
            if (now < e.timeSpawn) continue;

            acc += e.weight;
            if (roll <= acc)
                return e.prefab;
        }

        foreach (var e in chatterPrefabs)
        {
            if (e?.prefab != null && now >= e.timeSpawn) return e.prefab;
        }
        return null;
    }

    public void RemoveChatter(string nameKey)
    {
        nameKey = (nameKey ?? "").ToLowerInvariant();

        if (spawnedChatters.Remove(nameKey))
            currentSpawnCount = Mathf.Max(0, currentSpawnCount - 1);
    }

    private void MaintainSpawned()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject go = spawnedObjects[i];
            if (go == null)
            {
                spawnedObjects.RemoveAt(i);
                continue;
            }

            if (player != null && maxDistanceFromPlayer > 0f)
            {
                float dist = Vector2.Distance(go.transform.position, player.position);
                if (dist > maxDistanceFromPlayer)
                {
                    Vector3 newPos = FindSafePositionNearPlayer();
                    go.transform.position = newPos;
                }
            }
        }
    }

    private bool TryFindSafeSpawnPosition(out Vector3 spawnPos)
    {
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

            if (Physics2D.OverlapCircle(spawnPos, spawnCheckRadius, avoidLayers) == null)
                return true;

        } while (safetyCounter < maxAttempts);

        spawnPos = player.position;
        return false;
    }

    private Vector3 FindSafePositionNearPlayer()
    {
        Vector3 pos;
        if (TryFindSafeSpawnPosition(out pos))
            return pos;

        return player.position + new Vector3(minSpawnDistance, 0f, zOffset);
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int mins = (int)(seconds / 60f);
        int secs = (int)(seconds % 60f);
        return $"{mins:00}:{secs:00}";
    }
}
