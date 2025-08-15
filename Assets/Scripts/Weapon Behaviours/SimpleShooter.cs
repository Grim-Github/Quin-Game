using TMPro;
using UnityEngine;
using UnityEngine.UI; // For Image

public class SimpleShooter : MonoBehaviour
{
    [Header("Projectile Settings")]
    public GameObject bulletPrefab;
    public Transform shootTransform; // Optional: where to spawn the bullet
    public float shootForce = 10f;
    public int damage = 15;
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
    public WeaponUpgrades nextUpgrade;
    PowerUpChooser powerUpChooser;

    private void Awake()
    {
        shootSource = GetComponent<AudioSource>();
        powerUpChooser = GameObject.FindAnyObjectByType<PowerUpChooser>();

        if (shootTransform == null)
        {
            shootTransform = transform; // Default to self if not set
        }

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


    private void UpdateStatsText()
    {
        if (statsTextInstance == null) return;

        // Compute dynamic fields
        string delay = wt != null ? $"{wt.interval:F1}s" : "N/A";

        string penetrationInfo = "N/A";
        if (bulletPrefab != null)
        {




            if (bulletPrefab.TryGetComponent<BulletDamageTrigger>(out var bullet))
            {
                penetrationInfo = bullet.penetration.ToString();
            }
            else if (bulletPrefab.TryGetComponent<ExplosionDamage2D>(out var explosion))
            {
                penetrationInfo = $"Radius: {explosion.radius:F1}";
            }
        }

        // Build text (Knife.cs style)
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{transform.name} Stats</b>");
        sb.AppendLine($"Damage: {damage}");
        sb.AppendLine($"Attack Delay: {delay}");
        sb.AppendLine($"Proj Speed: {shootForce:F1}");
        sb.AppendLine($"Lifetime: {bulletLifetime:F1}s");
        sb.AppendLine($"Projectile Count: {Mathf.Max(1, projectileCount)}");
        sb.AppendLine($"Penetration: {penetrationInfo}");
        sb.AppendLine($"Crit: {(Mathf.Clamp01(critChance) * 100f):F0}% x{critMultiplier:F2}");




        if (applyStatusEffectOnHit)
        {
            sb.AppendLine($"Status Effect Chance: {statusApplyChance * 100f:F0}%");
            sb.AppendLine($"On Hit: {statusEffectOnHit} ({statusEffectDuration:F1}s)");
        }




        if (bulletPrefab.TryGetComponent<RB2DChainToTag>(out var RB2D))
        {
            sb.AppendLine($"Can Chain {RB2D.maxChains} Times");
        }



        if (!string.IsNullOrWhiteSpace(extraTextField))
            sb.AppendLine(extraTextField);

        statsTextInstance.text = sb.ToString();
    }


    // --- Shooting API ---

    public void ShootTransform(Transform target)
    {
        if (target == null || bulletPrefab == null) return;

        Vector2 dir = (target.position - transform.position);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right; // fallback
        dir.Normalize();

        Shoot(dir);
    }

    public void Shoot(Vector2 direction)
    {
        if (bulletPrefab == null) return;

        // Normalize and guard zero
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();

        // Play shoot sound
        if (shootClip != null && shootSource != null)
            shootSource.PlayOneShot(shootClip);

        float halfSpread = spreadAngle * 0.5f;

        for (int i = 0; i < Mathf.Max(1, projectileCount); i++)
        {
            // Completely random spread per projectile, even if projectileCount == 1
            float angle = (spreadAngle > 0f)
                ? Random.Range(-halfSpread, halfSpread)
                : 0f;

            // Rotate direction by angle
            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            Vector2 shootDirection = new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos
            );

            // Create projectile
            GameObject bullet = Instantiate(bulletPrefab, shootTransform.position, Quaternion.identity);

            // Point projectile
            float rotationAngle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);

            // Calculate damage (with crit)
            int finalDamage = damage;
            if (Random.value < critChance)
                finalDamage = Mathf.RoundToInt(damage * critMultiplier);

            // Apply damage to the correct component type
            if (bullet.TryGetComponent<BulletDamageTrigger>(out var bulletDamage))
            {
                bulletDamage.damageAmount = finalDamage;
                bulletDamage.statusApplyChance = statusApplyChance;
                bulletDamage.applyStatusEffectOnHit = applyStatusEffectOnHit;
                bulletDamage.statusEffectOnHit = statusEffectOnHit;
                bulletDamage.statusEffectDuration = statusEffectDuration;
            }

            if (bullet.TryGetComponent<ExplosionDamage2D>(out var explosionDamage))
                explosionDamage.baseDamage = finalDamage;

            // Move projectile (2D)
            if (bullet.TryGetComponent<Rigidbody2D>(out var rb))
                rb.linearVelocity = shootDirection * shootForce;

            // Lifetime
            if (bulletLifetime > 0f)
                Destroy(bullet, bulletLifetime);
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
