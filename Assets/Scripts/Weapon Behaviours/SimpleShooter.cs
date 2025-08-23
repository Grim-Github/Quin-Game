using TMPro;
using UnityEngine;
using UnityEngine.UI; // For Image

public class SimpleShooter : MonoBehaviour
{
    [Header("Projectile Settings")]
    public GameObject bulletPrefab;
    public Transform[] shootTransforms; // Where to spawn bullets; if empty, uses this.transform

    public float shootForce = 10f;
    public int damage = 15;
    [SerializeField] public SimpleHealth.DamageType damageType;
    public float bulletLifetime = 5f;

    [Header("Criticals")]
    [Range(0f, 1f)] public float critChance = 0f;
    [Min(1f)] public float critMultiplier = 2f;

    [Header("On Hit Effects")]
    public bool applyStatusEffectOnHit = false;
    public float statusApplyChance = 1f;    // optional: chance to apply on hit (0..1)
    public StatusEffectSystem.StatusType statusEffectOnHit = StatusEffectSystem.StatusType.Bleeding;
    [Tooltip("Duration in seconds for the applied status effect.")]
    public float statusEffectDuration = 3f;

    [Header("Shot Pattern")]
    public int projectileCount = 1;
    [Tooltip("Total cone in degrees. Each projectile gets a random angle within [-spread/2, +spread/2].")]
    public float spreadAngle = 0f;

    [Header("SFX")]
    [SerializeField] private AudioClip shootClip;

    [Header("UI")]
    [Tooltip("Prefab root GameObject that contains a TextMeshProUGUI somewhere in its children.")]
    [SerializeField] public GameObject statsTextPrefab;
    [SerializeField] private Transform uiParent;
    [TextArea][SerializeField] public string extraTextField = " ";
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] public Sprite weaponSprite;

    private WeaponTick wt;
    [HideInInspector] public TextMeshProUGUI statsTextInstance;
    private Image iconImage;
    private AudioSource shootSource;
    private GameObject statsGameobjectInstance;
    public WeaponUpgrades nextUpgrade;
    PowerUpChooser powerUpChooser;

    private void Awake()
    {
        shootSource = GetComponent<AudioSource>();
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();

        // Enqueue once, safely
        if (nextUpgrade != null && nextUpgrade.Upgrade != null && powerUpChooser != null && powerUpChooser.powerUps != null)
        {
            if (!powerUpChooser.powerUps.Contains(nextUpgrade.Upgrade))
                powerUpChooser.powerUps.Add(nextUpgrade.Upgrade);
        }

        if (statsTextPrefab != null && uiParent != null)
        {
            // Instantiate the prefab root
            var go = Instantiate(statsTextPrefab, uiParent);
            statsGameobjectInstance = go;
            // Find the TMP text anywhere under it
            statsTextInstance = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (statsTextInstance != null) statsTextInstance.text = "";

            // Find child GameObject named "Icon" and get its Image
            var iconObj = go.transform.Find("Icon");
            if (iconObj != null)
                iconImage = iconObj.GetComponent<Image>();

            if (iconImage != null && weaponSprite != null)
                iconImage.sprite = weaponSprite;

        }

        wt = GetComponent<WeaponTick>();
        UpdateStatsText();
    }

    public void ChangeBullet(GameObject newBullet)
    {
        bulletPrefab = newBullet;
        if (statsTextInstance != null)
        {
            UpdateStatsText();
        }

    }

    private void Update()
    {
        UpdateStatsText();
    }

    public void RemoveStatsText()
    {
        if (statsTextInstance != null)
        {
            Destroy(statsTextInstance.gameObject.transform.root.gameObject);
            statsTextInstance = null;
        }
    }
    public void EnableOnHitEffect(StatusEffectSystem.StatusType effectType)
    {
        applyStatusEffectOnHit = true;
        statusEffectOnHit = effectType;
    }

    public void SetOnHitEffectDuration(float duration)
    {
        statusEffectDuration = duration;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="effectIndex"></param>
    public void EnableOnHitEffectByIndex(int effectIndex)
    {
        applyStatusEffectOnHit = true;
        statusEffectOnHit = (StatusEffectSystem.StatusType)effectIndex;
    }


    public void UpdateStatsText()
    {
        if (statsTextInstance == null) return;

        // Compute dynamic fields
        const string numColor = "#8888FF";
        string delay = wt != null ? $"<color={numColor}>{wt.interval:F1}</color>s" : "N/A";

        string penetrationInfo = "N/A";
        if (bulletPrefab != null)
        {
            if (bulletPrefab.TryGetComponent<BulletDamageTrigger>(out var bullet))
            {
                penetrationInfo = $"<color={numColor}>{bullet.penetration}</color>";
            }
            else if (bulletPrefab.TryGetComponent<ExplosionDamage2D>(out var explosion))
            {
                penetrationInfo = $"Radius: <color={numColor}>{explosion.radius:F1}</color>";
            }
        }

        // Build text (Knife.cs style)
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{transform.name} Stats</b>");

        // ✅ Upgrades: enabled / total (in children)
        var allUpgrades = GetComponentsInChildren<WeaponUpgrades>(true);
        int enabledUpgrades = 0;
        for (int i = 0; i < allUpgrades.Length; i++)
        {
            var u = allUpgrades[i];
            if (u != null && u.enabled && u.gameObject.activeInHierarchy)
                enabledUpgrades++;
        }
        sb.AppendLine($"Upgrades: <color={numColor}>{enabledUpgrades}</color>/<color={numColor}>{allUpgrades.Length}</color>");


        sb.AppendLine($"Damage: <color={numColor}>{damage}</color>");
        sb.AppendLine($"Attack Delay: {delay}");
        sb.AppendLine($"Proj Speed: <color={numColor}>{shootForce:F1}</color>");
        sb.AppendLine($"Lifetime: <color={numColor}>{bulletLifetime:F1}</color>s");
        sb.AppendLine($"Projectile Count: <color={numColor}>{Mathf.Max(1, projectileCount)}</color>");
        sb.AppendLine($"Penetration: {penetrationInfo}");
        sb.AppendLine($"Crit: <color={numColor}>{(Mathf.Clamp01(critChance) * 100f):F0}</color>% x<color={numColor}>{critMultiplier:F2}</color>");

        if (applyStatusEffectOnHit)
        {
            sb.AppendLine($"Status Effect Chance: <color={numColor}>{statusApplyChance * 100f:F0}</color>%");
            sb.AppendLine($"On Hit: {statusEffectOnHit} (<color={numColor}>{statusEffectDuration:F1}</color>s)");
        }



        // Chain info (guarded)
        if (bulletPrefab != null && bulletPrefab.TryGetComponent<RB2DChainToTag>(out var RB2D))
        {
            sb.AppendLine($"Can Chain <color={numColor}>{RB2D.maxChains}</color> Times");
        }

        if (!string.IsNullOrWhiteSpace(extraTextField))
            sb.AppendLine(extraTextField);

        statsTextInstance.text = sb.ToString();
    }




    private void OnDisable()
    {
        Destroy(statsGameobjectInstance);
    }


    // --- Shooting API ---

    // Call this to shoot at a Transform
    public void ShootTransform(Transform target)
    {
        if (target == null || bulletPrefab == null) return;

        // Per-origin shooting toward the target
        ShootTowards(target.position);
    }

    // Core shooter that handles multiple origins + spread
    public void ShootTowards(Vector3 worldTargetPos)
    {
        if (bulletPrefab == null) return;

        // SFX
        if (shootClip != null && shootSource != null)
            shootSource.PlayOneShot(shootClip);

        float halfSpread = spreadAngle * 0.5f;

        // Use provided origins or fallback to this.transform
        Transform[] origins = (shootTransforms != null && shootTransforms.Length > 0)
            ? shootTransforms
            : new Transform[] { transform };

        for (int oi = 0; oi < origins.Length; oi++)
        {
            var origin = origins[oi];
            if (origin == null) continue;

            // ✅ Direction from THIS origin to target
            Vector2 baseDir = (Vector2)(worldTargetPos - origin.position);
            if (baseDir.sqrMagnitude < 0.0001f) baseDir = (Vector2)origin.right; // per-origin fallback
            baseDir.Normalize();

            int shots = Mathf.Max(1, projectileCount);
            for (int i = 0; i < shots; i++)
            {
                float angle = (spreadAngle > 0f) ? Random.Range(-halfSpread, halfSpread) : 0f;
                float rad = angle * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                Vector2 shootDir = new Vector2(
                    baseDir.x * cos - baseDir.y * sin,
                    baseDir.x * sin + baseDir.y * cos
                );

                var bullet = Instantiate(bulletPrefab, origin.position, Quaternion.identity);

                // Face travel direction
                float rotDeg = Mathf.Atan2(shootDir.y, shootDir.x) * Mathf.Rad2Deg;
                bullet.transform.rotation = Quaternion.Euler(0, 0, rotDeg);

                // Crit calc
                int finalDamage = (Random.value < critChance)
                    ? Mathf.RoundToInt(damage * critMultiplier)
                    : damage;

                if (bullet.TryGetComponent<BulletDamageTrigger>(out var bulletDamage))
                {
                    bulletDamage.damageAmount = finalDamage;
                    bulletDamage.damageType = damageType;
                    bulletDamage.statusApplyChance = statusApplyChance;
                    bulletDamage.applyStatusEffectOnHit = applyStatusEffectOnHit;
                    bulletDamage.statusEffectOnHit = statusEffectOnHit;
                    bulletDamage.statusEffectDuration = statusEffectDuration;
                }
                if (bullet.TryGetComponent<ExplosionDamage2D>(out var explosionDamage))
                    explosionDamage.baseDamage = finalDamage;

                if (bullet.TryGetComponent<Rigidbody2D>(out var rb))
                    rb.linearVelocity = shootDir * shootForce;

                if (bulletLifetime > 0f)
                    Destroy(bullet, bulletLifetime);
            }
        }
    }


    // --- Debug Helpers ---
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }
}
