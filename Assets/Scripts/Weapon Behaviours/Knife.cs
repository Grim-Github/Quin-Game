using TMPro;
using UnityEngine;
using UnityEngine.UI; // For Image

public class Knife : MonoBehaviour
{
    [Header("AOE Damage")]
    [SerializeField, Tooltip("Main hit radius for selecting enemies.")]
    public float radius = 1f;
    [SerializeField, Tooltip("Base damage dealt to main target.")]
    public int damage = 10;
    [SerializeField] public SimpleHealth.DamageType damageType;
    [SerializeField, Tooltip("Which layers are considered valid targets.")]
    private LayerMask targetMask = ~0;
    [SerializeField, Tooltip("Maximum number of targets per tick (0 = unlimited).")]
    public int maxTargetsPerTick = 0;
    [Header("Hit Origins")]
    [Tooltip("Optional extra origins to check. If empty, uses this transform.")]
    [SerializeField] private Transform[] hitOrigins;



    [Header("AOE Splash Damage")]
    [SerializeField, Tooltip("Radius around the main target for splash damage. 0 disables splash.")]
    public float splashRadius = 0;
    [SerializeField, Tooltip("Damage dealt to enemies inside splashRadius (percentage of main damage).")]
    [Range(0f, 1f)] public float splashDamagePercent = 0.5f;

    [Header("On Hit Effects")]
    public bool applyStatusEffectOnHit = false;
    [Range(0f, 1f)] public float statusApplyChance = 1f;    // optional: chance to apply on hit (0..1)
    public StatusEffectSystem.StatusType statusEffectOnHit = StatusEffectSystem.StatusType.Bleeding;
    public float statusEffectDuration = 3f; // Duration in seconds for the status effect

    [Header("Lifesteal")]
    [Range(0f, 1f)][SerializeField] public float lifestealPercent = 0.25f;

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
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] public GameObject statsTextPrefab;
    [SerializeField] private Transform uiParent;
    [TextArea][SerializeField] public string extraTextField;
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] public Sprite weaponSprite;

    [Header("Range Visual")]
    [Tooltip("Child SpriteRenderer that should visually match the AOE radius.")]
    [SerializeField] private SpriteRenderer rangeRenderer;
    [Tooltip("Extra world-units padding added to the visual radius (optional).")]
    [SerializeField] private float visualPadding = 0f;
    [Tooltip("If true, auto-scales the rangeRenderer to match 'radius'.")]
    [SerializeField] private bool autoScaleRangeVisual = true;


    [HideInInspector] public TextMeshProUGUI statsTextInstance;
    private GameObject statsGameobjectInstance;
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
            // Instantiate the prefab root
            var go = Instantiate(statsTextPrefab, uiParent);
            // Find the TMP text anywhere under it
            statsTextInstance = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (statsTextInstance != null) statsTextInstance.text = "";
            statsGameobjectInstance = go;
            // Find Image in children and assign sprite
            // Find child GameObject named "Icon" and get its Image
            var iconObj = go.transform.Find("Icon");
            if (iconObj != null)
                iconImage = iconObj.GetComponent<Image>();

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

    public void UpdateStatsText()
    {
        if (statsTextInstance != null)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            const string numColor = "#8888FF";

            sb.AppendLine($"<b>{transform.name} Stats</b>");
            sb.AppendLine($"Radius: <color={numColor}>{radius:F2}</color>");
            sb.AppendLine($"Damage: <color={numColor}>{damage}</color>");
            sb.AppendLine($"Splash: <color={numColor}>{splashRadius:F2}</color> (<color={numColor}>{splashDamagePercent * 100f:F0}</color>% dmg)");

            if (wt != null)
                sb.AppendLine($"Attack Delay: <color={numColor}>{wt.interval:F1}</color>s");

            sb.AppendLine($"Lifesteal: <color={numColor}>{(lifestealPercent * 100f):F0}</color>%");
            sb.AppendLine($"Crit: <color={numColor}>{(critChance * 100f):F0}</color>% x<color={numColor}>{critMultiplier:F2}</color>");
            sb.AppendLine($"Max Targets: <color={numColor}>{maxTargetsPerTick}</color>");

            if (applyStatusEffectOnHit)
            {
                sb.AppendLine($"Status Effect Chance: <color={numColor}>{statusApplyChance * 100f:F0}</color>%");
                sb.AppendLine($"On Hit: {statusEffectOnHit} (<color={numColor}>{statusEffectDuration:F1}</color>s)");
            }


            if (!string.IsNullOrWhiteSpace(extraTextField))
                sb.AppendLine(extraTextField);

            statsTextInstance.text = sb.ToString();
        }
    }

    public void RemoveStatsText()
    {
        if (statsTextInstance != null)
        {
            Destroy(statsTextInstance.gameObject.transform.root.gameObject);
            statsTextInstance = null;
        }
    }
    private void OnDisable()
    {
        Destroy(statsGameobjectInstance);
    }
    public void OnKnifeTick()
    {


        if (selfSfxObject != null)
            Instantiate(selfSfxObject, transform.position, Quaternion.identity);

        // choose origins (fallback to self)
        Transform[] origins = (hitOrigins != null && hitOrigins.Length > 0) ? hitOrigins : new Transform[] { transform };

        if (shootClip != null) shootSource?.PlayOneShot(shootClip);

        bool anyHit = false;
        int targetsHit = 0;

        for (int oi = 0; oi < origins.Length; oi++)
        {
            var origin = origins[oi];
            if (origin == null) continue;

            Collider2D[] hits = Physics2D.OverlapCircleAll(origin.position, radius, targetMask);

            if (hits.Length > 0 && !anyHit)
            {
                anyHit = true;
                if (stabClip != null) shootSource?.PlayOneShot(stabClip);
            }

            for (int hi = 0; hi < hits.Length; hi++)
            {
                var col = hits[hi];
                if (col == null || col.gameObject == gameObject) continue;

                if (slashEffect != null)
                    Instantiate(slashEffect, col.transform.position, Quaternion.identity);

                SimpleHealth health = col.GetComponent<SimpleHealth>();
                StatusEffectSystem splashStatus = col.GetComponent<StatusEffectSystem>();

                // status on hit (safe-guard health != null)
                if (splashStatus != null && health != null && health.IsAlive && !health.IsInvulnerable)
                {
                    if (applyStatusEffectOnHit && Random.Range(0f, 1f) <= statusApplyChance)
                    {
                        splashStatus.AddStatus(statusEffectOnHit, statusEffectDuration, 1f);
                    }
                }

                if (health != null && health.IsAlive && !health.IsInvulnerable)
                {
                    // main hit
                    bool isCrit = Random.value < Mathf.Clamp01(critChance);
                    float mult = isCrit ? Mathf.Max(1f, critMultiplier) : 1f;
                    int dealt = Mathf.RoundToInt(damage * mult);

                    health.TakeDamage(dealt, damageType);

                    // lifesteal
                    if (lifestealPercent > 0f && parentHealth != null && parentHealth.IsAlive)
                    {
                        int healAmount = Mathf.RoundToInt(dealt * lifestealPercent);
                        parentHealth.Heal(healAmount);
                    }

                    // splash
                    if (splashRadius > 0f && splashDamagePercent > 0f)
                    {
                        Collider2D[] splashHits = Physics2D.OverlapCircleAll(col.transform.position, splashRadius, targetMask);
                        for (int si = 0; si < splashHits.Length; si++)
                        {
                            var splashCol = splashHits[si];
                            if (splashCol == null || splashCol == col || splashCol.gameObject == gameObject) continue;

                            SimpleHealth splashHealth = splashCol.GetComponent<SimpleHealth>();
                            if (splashHealth != null && splashHealth.IsAlive && !splashHealth.IsInvulnerable)
                            {
                                int splashDamage = Mathf.RoundToInt(dealt * splashDamagePercent);
                                splashHealth.TakeDamage(splashDamage, SimpleHealth.DamageType.Physical);
                            }
                        }
                    }

                    targetsHit++;
                    if (maxTargetsPerTick > 0 && targetsHit >= maxTargetsPerTick)
                        return; // stop after cap reached
                }
            }
        }

        // no targets anywhere → fling a slash VFX near first origin (or self)
        if (!anyHit && slashEffect != null)
        {
            Transform baseOrigin = (hitOrigins != null && hitOrigins.Length > 0 && hitOrigins[0] != null) ? hitOrigins[0] : transform;
            Vector3 fxPos = baseOrigin.position + (Vector3)(Random.insideUnitCircle * 1f);
            Instantiate(slashEffect, fxPos, Quaternion.identity);
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

    public void EnableOnHitEffect(StatusEffectSystem.StatusType effectType)
    {
        applyStatusEffectOnHit = true;
        statusEffectOnHit = effectType;
    }

    public void SetOnHitEffectDuration(float duration)
    {
        statusEffectDuration = duration;
    }
    public void EnableOnHitEffectByIndex(int effectIndex)
    {
        applyStatusEffectOnHit = true;
        statusEffectOnHit = (StatusEffectSystem.StatusType)effectIndex;
    }


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
