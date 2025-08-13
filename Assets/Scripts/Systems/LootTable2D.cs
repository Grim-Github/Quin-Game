using NaughtyAttributes;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[DisallowMultipleComponent]
public class LootTable2D : MonoBehaviour
{
    [System.Serializable]
    public class LootEntry
    {
        [Header("Loot")]
        [Tooltip("Prefab to spawn if this entry is selected.")]
        [ShowAssetPreview] public GameObject prefab;

        [Min(0f)]
        [Tooltip("Relative weight. 0 = never selected. Higher = more likely.")]
        public float weight = 1f;
    }

    [Header("Loot Table")]
    [SerializeField] private List<LootEntry> entries = new List<LootEntry>();

    [Header("Roll Settings")]
    [SerializeField] private bool rollOnAwake = true;
    [Tooltip("Optional fixed random seed for deterministic rolls. Leave 0 for Unity's RNG.")]
    [SerializeField] private int seed = 0;

    [Header("Spawn Settings")]
    [Tooltip("Where to spawn. If null, uses this transform.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Z position for spawned object. If 'Use Spawn Point Z' is true, this is ignored.")]
    [SerializeField] private float zPosition = 0f;
    [Tooltip("If true, keep the Z position of the spawn point (or this transform).")]
    [SerializeField] private bool useSpawnPointZ = true;
    [Tooltip("Parent for the spawned object. If null, none.")]
    [SerializeField] private Transform parent;

    [Header("Debug")]
    [ReadOnly][SerializeField] private int lastSelectedIndex = -1;
    [ReadOnly][SerializeField] private GameObject lastSpawned;

    private System.Random seededRng;

    private void Awake()
    {
        if (seed != 0)
            seededRng = new System.Random(seed);

        if (rollOnAwake)
            RollAndSpawn();
    }

    public GameObject RollAndSpawn()
    {
        int idx = WeightedPickIndex(entries);
        lastSelectedIndex = idx;

        if (idx < 0)
        {
            Debug.LogWarning($"[LootTable2D] No valid entries to roll on {name}. Nothing spawned.");
            return null;
        }

        var entry = entries[idx];
        var sp = spawnPoint != null ? spawnPoint : transform;

        Vector3 pos = sp.position;

        if (!useSpawnPointZ)
            pos.z = zPosition;

        lastSpawned = Instantiate(entry.prefab, pos, Quaternion.identity, parent);

        // ✅ Debug message when loot spawns
        // Debug.Log($"[LootTable2D] Spawned loot '{lastSpawned.name}' from {gameObject.name} at {pos}");

        return lastSpawned;
    }


    public int WeightedPickIndex(List<LootEntry> list)
    {
        float total = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.prefab != null && e.weight > 0f)
                total += e.weight;
        }

        if (total <= 0f) return -1;

        float r = NextFloat() * total;
        float cumulative = 0f;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.prefab == null || e.weight <= 0f) continue;

            cumulative += e.weight;
            if (r < cumulative)
                return i;
        }

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var e = list[i];
            if (e.prefab != null && e.weight > 0f)
                return i;
        }

        return -1;
    }

    [ContextMenu("DEBUG ▸ Roll (No Spawn)")]
    public void DebugRollOnly()
    {
        lastSelectedIndex = WeightedPickIndex(entries);
        Debug.Log($"[LootTable2D] Rolled index: {lastSelectedIndex} on {name}");
    }

    [ContextMenu("DEBUG ▸ Roll & Spawn")]
    public void DebugRollAndSpawn()
    {
        var go = RollAndSpawn();
        Debug.Log($"[LootTable2D] Spawned: {(go ? go.name : "null")} on {name}");
    }

    private float NextFloat()
    {
        if (seededRng == null) return Random.value;
        return (float)seededRng.NextDouble();
    }

#if UNITY_EDITOR
    private class ReadOnlyInspectorAttribute : PropertyAttribute { }
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyInspectorAttribute))]
    private class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => UnityEditor.EditorGUI.GetPropertyHeight(property, label, true);
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool prev = GUI.enabled; GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = prev;
        }
    }
#endif
}