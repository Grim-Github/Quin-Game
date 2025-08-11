using TMPro;
using UnityEngine;
using UnityEngine.UI; // Added for Image

public class Knife : MonoBehaviour
{
    [Header("AOE Damage")]
    [SerializeField, Tooltip("Main hit radius for selecting enemies.")]
    private float radius = 1f;
    [SerializeField, Tooltip("Base damage dealt to main target.")]
    public int damage = 10;
    [SerializeField, Tooltip("Which layers are considered valid targets.")]
    private LayerMask targetMask = ~0;
    [SerializeField, Tooltip("Maximum number of targets per tick (0 = unlimited).")]
    private int maxTargetsPerTick = 0;

    [Header("AOE Splash Damage")]
    [SerializeField, Tooltip("Radius around the main target for splash damage. 0 disables splash.")]
    private float splashRadius = 0;
    [SerializeField, Tooltip("Damage dealt to enemies inside splashRadius (percentage of main damage).")]
    [Range(0f, 1f)] private float splashDamagePercent = 0.5f;

    [Header("Lifesteal")]
    [Range(0f, 1f)][SerializeField] private float lifestealPercent = 0.25f;

    [Header("Criticals")]
    [Range(0f, 1f)] public float critChance = 0f;
    [Min(1f)] public float critMultiplier = 2f;

    [Header("Upgrades")]
    public WeaponUpgrades nextUpgrade;

    [Header("SFX")]
    [SerializeField] private AudioClip shootClip;
    [SerializeField] private AudioClip stabClip;
    [SerializeField] private GameObject slashEffect;
    [Tooltip("Extra SFX GameObject spawned directly on top of the Knife.")]
    [SerializeField] private GameObject selfSfxObject;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statsTextPrefab;
    [SerializeField] private Transform uiParent;
    [TextArea][SerializeField] private string extraTextField;
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] private Sprite weaponSprite;

    [Header("Range Visual")]
    [Tooltip("Child SpriteRenderer that should visually match the AOE radius.")]
    [SerializeField] private SpriteRenderer rangeRenderer;
    [Tooltip("Extra world-units padding added to the visual radius (optional).")]
    [SerializeField] private float visualPadding = 0f;
    [Tooltip("If true, auto-scales the rangeRenderer to match 'radius'.")]
    [SerializeField] private bool autoScaleRangeVisual = true;

    private TextMeshProUGUI statsTextInstance;
    private Image iconImage;
    private AudioSource shootSource;
    private SimpleHealth parentHealth;
    private WeaponTick wt;
    private PowerUpChooser powerUpChooser;

    private void Awake()
    {
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();
        if (nextUpgrade != null && powerUpChooser != null)
        {
            powerUpChooser.powerUps.Add(nextUpgrade.Upgrade);
        }

        shootSource = GetComponent<AudioSource>();

        if (transform.parent != null && transform.parent.parent != null)
            parentHealth = transform.parent.parent.GetComponent<SimpleHealth>();
        else
            parentHealth = GetComponentInParent<SimpleHealth>();

        if (statsTextPrefab != null && uiParent != null)
        {
            statsTextInstance = Instantiate(statsTextPrefab, uiParent);
            statsTextInstance.text = "";

            iconImage = statsTextInstance.GetComponentInChildren<Image>(true);
            if (iconImage != null && weaponSprite != null)
                iconImage.sprite = weaponSprite;
        }

        wt = GetComponent<WeaponTick>();
        UpdateStatsText();
        UpdateRangeVisual();
    }

    private void Update()
    {
        UpdateStatsText();

        if (autoScaleRangeVisual)
            UpdateRangeVisual();
    }

    private void UpdateStatsText()
    {
        if (statsTextInstance != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.AppendLine($"<b>{transform.name} Stats</b>");

            if (radius > 0f)
                sb.AppendLine($"Radius: {radius:F2}");

            if (damage > 0)
                sb.AppendLine($"Damage: {damage}");

            if (splashRadius > 0f && splashDamagePercent > 0f)
                sb.AppendLine($"Splash: {splashRadius:F2} ({splashDamagePercent * 100f:F0}% dmg)");

            if (wt != null && wt.interval > 0f)
                sb.AppendLine($"Attack Delay: {wt.interval:F1}s");

            if (lifestealPercent > 0f)
                sb.AppendLine($"Lifesteal: {(lifestealPercent * 100f):F0}%");

            if (critChance > 0f)
                sb.AppendLine($"Crit: {(critChance * 100f):F0}% x{critMultiplier:F2}");

            if (maxTargetsPerTick > 0)
                sb.AppendLine($"Max Targets: {maxTargetsPerTick}");

            if (!string.IsNullOrWhiteSpace(extraTextField))
                sb.AppendLine(extraTextField);

            statsTextInstance.text = sb.ToString();
        }
    }


    public void OnKnifeTick()
    {
        if (selfSfxObject != null)
            Instantiate(selfSfxObject, transform.position, Quaternion.identity);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, targetMask);
        if (shootClip != null) shootSource?.PlayOneShot(shootClip);

        if (hits.Length == 0)
        {
            if (slashEffect != null)
            {
                Vector3 fxPos = transform.position + (Vector3)(Random.insideUnitCircle * 1f);
                Instantiate(slashEffect, fxPos, Quaternion.identity);
            }
            return;
        }

        int targetsHit = 0;
        foreach (var col in hits)
        {
            if (col == null || col.gameObject == gameObject) continue;

            if (stabClip != null) shootSource?.PlayOneShot(stabClip);

            if (slashEffect != null)
                Instantiate(slashEffect, col.transform.position, Quaternion.identity);

            SimpleHealth health = col.GetComponent<SimpleHealth>();
            if (health != null && health.IsAlive && !health.IsInvulnerable)
            {
                // Main hit damage
                bool isCrit = Random.value < Mathf.Clamp01(critChance);
                float mult = isCrit ? Mathf.Max(1f, critMultiplier) : 1f;
                int dealt = Mathf.RoundToInt(damage * mult);
                health.TakeDamage(dealt);

                // Lifesteal from main hit
                if (lifestealPercent > 0f && parentHealth != null && parentHealth.IsAlive)
                {
                    int healAmount = Mathf.RoundToInt(dealt * lifestealPercent);
                    parentHealth.Heal(healAmount);
                }

                // Splash damage
                if (splashRadius > 0f && splashDamagePercent > 0f)
                {
                    Collider2D[] splashHits = Physics2D.OverlapCircleAll(col.transform.position, splashRadius, targetMask);
                    foreach (var splashCol in splashHits)
                    {
                        if (splashCol == null || splashCol == col || splashCol.gameObject == gameObject) continue;

                        SimpleHealth splashHealth = splashCol.GetComponent<SimpleHealth>();
                        if (splashHealth != null && splashHealth.IsAlive && !splashHealth.IsInvulnerable)
                        {
                            int splashDamage = Mathf.RoundToInt(dealt * splashDamagePercent);
                            splashHealth.TakeDamage(splashDamage);
                        }
                    }
                }

                targetsHit++;
                if (maxTargetsPerTick > 0 && targetsHit >= maxTargetsPerTick)
                    break;
            }
        }
    }

    [ContextMenu("Sync Range Visual Now")]
    private void UpdateRangeVisual()
    {
        if (!autoScaleRangeVisual || rangeRenderer == null || rangeRenderer.sprite == null)
            return;

        float desiredDiameter = Mathf.Max(0f, 2f * (radius + visualPadding));
        var sprite = rangeRenderer.sprite;
        Vector2 spriteSizeWorld = sprite.bounds.size;

        float parentScaleX = rangeRenderer.transform.parent ? rangeRenderer.transform.parent.lossyScale.x : 1f;
        float parentScaleY = rangeRenderer.transform.parent ? rangeRenderer.transform.parent.lossyScale.y : 1f;

        float baseW = Mathf.Max(0.0001f, spriteSizeWorld.x * parentScaleX);
        float baseH = Mathf.Max(0.0001f, spriteSizeWorld.y * parentScaleY);

        float scaleX = desiredDiameter / baseW;
        float scaleY = desiredDiameter / baseH;

        rangeRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        rangeRenderer.enabled = desiredDiameter > 0.0001f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isEditor && !Application.isPlaying)
            UpdateRangeVisual();
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);

        if (splashRadius > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, splashRadius);
        }
    }
}
