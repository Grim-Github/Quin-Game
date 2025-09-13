using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomObjectRandomizer : MonoBehaviour
{
    [Header("Player & Rules")]
    [Tooltip("Player transform used to check spawn distance.")]
    public Transform player;

    [Min(0)]
    [Tooltip("Maximum simultaneous active spawned objects.")]
    public int maxActive = 5;

    [Min(0f)]
    [Tooltip("Only spawn at points farther than this distance from the player.")]
    public float minDistanceFromPlayer = 8f;

    [Header("Spawn Sources (Scene Objects)")]
    [Tooltip("Scene objects that define spawn points & templates. They will be disabled at runtime and used as templates for instantiation.")]
    public List<GameObject> sourceObjects = new List<GameObject>();

    [Header("Respawn")]
    [Tooltip("If true, when a spawned object is destroyed, the manager will try to spawn a new one.")]
    public bool autoRespawn = true;

    [Min(0f)]
    [Tooltip("If a spawn attempt fails (e.g., all points too close to player), retry after this delay.")]
    public float respawnRetryDelay = 0.5f;

    // --- Internals ---
    private readonly List<SpawnPoint> _points = new List<SpawnPoint>();
    private int _activeCount;

    [System.Serializable]
    private class SpawnPoint
    {
        public GameObject source;        // original scene object (kept disabled)
        public Vector3 position;
        public Quaternion rotation;

        public bool occupied;
        public GameObject instance;      // current live clone (if any)

        public SpawnPoint(GameObject src)
        {
            source = src;
            position = src.transform.position;
            rotation = src.transform.rotation;
            occupied = false;
            instance = null;
        }
    }

    private void Awake()
    {
        if (player == null)
        {
            // Try to auto-find a player by tag to be friendlier.
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }
    }

    private void Start()
    {
        BuildSpawnPoints();
        DeactivateSources();
        FillUpToMax();
    }

    private void BuildSpawnPoints()
    {
        _points.Clear();

        if (sourceObjects == null) return;

        foreach (var go in sourceObjects)
        {
            if (go == null) continue;
            _points.Add(new SpawnPoint(go));
        }
    }

    private void DeactivateSources()
    {
        // Requirement (4): “on runtime deactivate every gameobject and instantiate it”
        foreach (var p in _points)
        {
            if (p.source != null && p.source.activeSelf)
                p.source.SetActive(false);
        }
    }

    private void FillUpToMax()
    {
        // Try to spawn until we hit maxActive or we run out of eligible points
        while (_activeCount < maxActive)
        {
            // If we can’t place a new one now, break (we’ll retry on timer if autoRespawn)
            if (!TrySpawnOne())
                break;
        }
    }

    private bool TrySpawnOne()
    {
        if (player == null || _points.Count == 0) return false;

        // Collect eligible spawn indices
        List<int> candidates = null;
        var playerPos = player.position;

        // Lazy allocate
        for (int i = 0; i < _points.Count; i++)
        {
            var sp = _points[i];
            if (sp.occupied) continue;
            if (sp.source == null) continue; // skip missing
            if (Vector3.Distance(playerPos, sp.position) < minDistanceFromPlayer) continue;

            (candidates ??= new List<int>()).Add(i);
        }

        if (candidates == null || candidates.Count == 0) return false;

        // Pick a random candidate
        int chosenIndex = candidates[Random.Range(0, candidates.Count)];
        var chosen = _points[chosenIndex];

        // Instantiate a clone of the source (scene object acts like a template)
        var clone = Instantiate(chosen.source, chosen.position, chosen.rotation);
        clone.SetActive(true);

        // Attach a marker that reports destruction back to this manager
        var marker = clone.GetComponent<RandomizerSpawnMarker>();
        if (marker == null) marker = clone.AddComponent<RandomizerSpawnMarker>();
        marker.Configure(this, chosenIndex);

        chosen.instance = clone;
        chosen.occupied = true;

        _activeCount++;
        return true;
    }

    private IEnumerator RetryFillAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        FillUpToMax();
    }

    // Called by marker when its instance is destroyed
    internal void HandleInstanceDestroyed(int spawnPointIndex)
    {
        if (spawnPointIndex < 0 || spawnPointIndex >= _points.Count) return;

        var sp = _points[spawnPointIndex];
        sp.instance = null;
        if (sp.occupied)
        {
            sp.occupied = false;
            _activeCount = Mathf.Max(0, _activeCount - 1);
        }

        if (autoRespawn)
        {
            // Try immediately; if it fails (e.g., too close), schedule a retry
            if (!TrySpawnOne())
                StartCoroutine(RetryFillAfterDelay(respawnRetryDelay));
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxActive < 0) maxActive = 0;
        if (minDistanceFromPlayer < 0f) minDistanceFromPlayer = 0f;
        if (respawnRetryDelay < 0f) respawnRetryDelay = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize spawn points and player distance ring
        Gizmos.matrix = Matrix4x4.identity;
        if (player != null)
        {
            Gizmos.color = new Color(0f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireSphere(player.position, minDistanceFromPlayer);
        }

        if (sourceObjects != null)
        {
            foreach (var go in sourceObjects)
            {
                if (go == null) continue;
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(go.transform.position, Vector3.one * 0.5f);
            }
        }
    }
#endif
}

/// <summary>
/// Added to each spawned clone. Notifies the manager when the object is destroyed,
/// so the manager can free the slot and optionally respawn another.
/// </summary>
public class RandomizerSpawnMarker : MonoBehaviour
{
    private RoomObjectRandomizer _manager;
    private int _spawnPointIndex = -1;

    public void Configure(RoomObjectRandomizer manager, int spawnPointIndex)
    {
        _manager = manager;
        _spawnPointIndex = spawnPointIndex;
    }

    private void OnDestroy()
    {
        // If the scene is unloading, manager might be gone; guard with null checks
        if (_manager != null && gameObject.scene.isLoaded)
        {
            _manager.HandleInstanceDestroyed(_spawnPointIndex);
        }
    }
}
