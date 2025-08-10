using UnityEngine;

[ExecuteAlways]
public class AccessoriesUpgrades : MonoBehaviour
{
    [Header("Power-Up")]
    public PowerUp Upgrade;

    [Tooltip("Autofilled: next sibling with AccessoriesUpgrades in the same parent.")]
    public AccessoriesUpgrades nextUpgrade;

    private PowerUpChooser powerUpChooser;

    // ---------------------- Lifecycle ----------------------

    private void Awake()
    {
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();

        AutoAssignNextUpgrade();   // keep next wired
        EnqueueNextUpgradeOnce();  // push next's Upgrade into chooser once
    }

    private void OnEnable()
    {
        // Editor recompile / domain reload safety
        AutoAssignNextUpgrade();
        EnqueueNextUpgradeOnce();
    }

    private void OnValidate()
    {
        // Auto-set icon from parent's Accessory if available
        if (transform.parent != null && transform.parent.TryGetComponent(out Accessory accessory))
        {
            if (accessory.icon != null && Upgrade != null)
            {
                Upgrade.powerUpIcon = accessory.icon;
            }
        }

        // Keep next wired live in the editor as you reorder children
        AutoAssignNextUpgrade();

    }

    private void OnTransformParentChanged()
    {
        AutoAssignNextUpgrade();
    }

    private void OnTransformChildrenChanged()
    {
        AutoAssignNextUpgrade();
    }

    // ---------------------- Auto-wire NEXT ----------------------

    /// <summary>
    /// Automatically sets nextUpgrade to the next sibling under the same parent
    /// that has an AccessoriesUpgrades component. If the immediate next child
    /// doesn't have it, scans forward until it finds one. Clears if none found.
    /// </summary>
    private void AutoAssignNextUpgrade()
    {
        var old = nextUpgrade;
        nextUpgrade = null;

        if (transform.parent == null) return;

        int myIndex = transform.GetSiblingIndex();
        var parent = transform.parent;

        for (int i = myIndex + 1; i < parent.childCount; i++)
        {
            var candidate = parent.GetChild(i).GetComponent<AccessoriesUpgrades>();
            if (candidate != null)
            {
                nextUpgrade = candidate;
                break;
            }
        }

#if UNITY_EDITOR
        if (old != nextUpgrade)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Adds the nextUpgrade's PowerUp asset to the chooser list once.
    /// Requires PowerUpChooser.powerUps to be a List&lt;PowerUp&gt;.
    /// Safely avoids duplicates and nulls.
    /// </summary>
    private void EnqueueNextUpgradeOnce()
    {
        if (powerUpChooser == null) return;
        if (nextUpgrade == null || nextUpgrade.Upgrade == null) return;

        try
        {
            var list = powerUpChooser.powerUps;
            if (list != null && !list.Contains(nextUpgrade.Upgrade))
            {
                list.Add(nextUpgrade.Upgrade);
            }
        }
        catch (System.Exception)
        {
            // If PowerUpChooser isn't backed by a List<PowerUp>, ignore silently.
        }
    }

#if UNITY_EDITOR
    // ---------- Minimal custom Inspector: show read-only next + refresh ----------
    [UnityEditor.CustomEditor(typeof(AccessoriesUpgrades))]
    private class AccessoriesUpgradesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var au = (AccessoriesUpgrades)target;
            var so = serializedObject;

            so.Update();

            UnityEditor.EditorGUILayout.PropertyField(so.FindProperty("Upgrade"));

            using (new UnityEditor.EditorGUI.DisabledScope(true))
            {
                UnityEditor.EditorGUILayout.ObjectField("Next Upgrade (auto)", au.nextUpgrade, typeof(AccessoriesUpgrades), true);
            }
            if (UnityEngine.GUILayout.Button("Refresh Next"))
            {
                au.AutoAssignNextUpgrade();
                UnityEditor.EditorUtility.SetDirty(au);
            }

            so.ApplyModifiedProperties();
        }
    }
#endif
}