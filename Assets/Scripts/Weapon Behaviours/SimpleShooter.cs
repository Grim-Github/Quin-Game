using TMPro;
using UnityEngine;
using UnityEngine.UI; // Added for Image

public class SimpleShooter : MonoBehaviour
{
    [Header("Projectile Settings")]
    public GameObject bulletPrefab;
    public float shootForce = 10f;
    public int damage = 15;
    public float spawnOffset = 0.5f;
    public float bulletLifetime = 5f;

    [Header("Criticals")]
    [Range(0f, 1f)] public float critChance = 0f;
    [Min(1f)] public float critMultiplier = 2f;

    [Header("Shot Pattern")]
    public int projectileCount = 1;
    public float spreadAngle = 0f;

    [Header("SFX")]
    [SerializeField] private AudioClip shootClip;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statsTextPrefab;
    [SerializeField] private Transform uiParent;
    [TextArea][SerializeField] private string extraTextField = " ";
    [Tooltip("Sprite to show above the stats text.")]
    [SerializeField] private Sprite weaponSprite;

    private WeaponTick wt;
    private TextMeshProUGUI statsTextInstance;
    private Image iconImage;
    private AudioSource shootSource;
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
            statsTextInstance = Instantiate(statsTextPrefab, uiParent);
            statsTextInstance.text = "";

            // Find Image in parent and assign sprite
            iconImage = statsTextInstance.GetComponentInChildren<Image>(true);
            if (iconImage != null && weaponSprite != null)
                iconImage.sprite = weaponSprite;
        }

        wt = GetComponent<WeaponTick>();
        UpdateStatsText();
    }

    private void Update()
    {
        UpdateStatsText();
    }

    private void UpdateStatsText()
    {
        if (statsTextInstance != null)
        {
            string delay = wt != null ? $"{wt.interval:F1}s" : "N/A";
            statsTextInstance.text =
                $"<b>{transform.name} Stats</b>\n" +
                $"Damage: {damage}\n" +
                $"Attack Delay: {delay}\n" +
                $"Proj Speed: {shootForce:F1}\n" +
                $"Lifetime: {bulletLifetime:F1}s\n" +
                $"Projectile Count: {projectileCount}\n" +
                $"Crit: {(Mathf.Clamp01(critChance) * 100f):F0}% x{critMultiplier:F2}\n" +
                extraTextField;
        }
    }

    // --- Helpers ---

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad);
        float sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    private static Vector2 AimPointOf(Transform t)
    {
        // Prefer collider center if available (handles off-center pivots)
        if (t != null)
        {
            if (t.TryGetComponent<Collider2D>(out var col)) return col.bounds.center;
            // Also check children (common on animated targets)
            var childCol = t.GetComponentInChildren<Collider2D>();
            if (childCol != null) return childCol.bounds.center;
        }
        return t != null ? (Vector2)t.position : Vector2.zero;
    }

    // --- Shooting API ---

    public void ShootTransform(Transform target)
    {
        if (target == null || bulletPrefab == null) return;

        Vector2 origin = transform.position;
        Vector2 aimPoint = AimPointOf(target);

        Vector2 direction = (aimPoint - origin);
        if (direction.sqrMagnitude < 1e-6f) return;
        direction.Normalize();

        FireProjectiles(direction, origin);
    }

    public void Shoot(Vector2 direction)
    {
        if (bulletPrefab == null) return;
        if (direction.sqrMagnitude < 1e-6f) return;

        Vector2 origin = transform.position;
        FireProjectiles(direction.normalized, origin);
    }

    private void FireProjectiles(Vector2 baseDirection, Vector2 origin)
    {
        int count = Mathf.Max(1, projectileCount);
        float totalSpread = Mathf.Max(0f, spreadAngle);

        // Evenly distribute across [-half..+half] so spread is centered on aim
        float step = (count > 1) ? totalSpread / (count - 1) : 0f;
        float start = -totalSpread * 0.5f;

        if (shootClip != null && shootSource != null)
            shootSource.PlayOneShot(shootClip);

        float clampedCrit = Mathf.Clamp01(critChance);

        for (int i = 0; i < count; i++)
        {
            float angle = start + step * i;
            Vector2 dir = (angle == 0f) ? baseDirection : Rotate(baseDirection, angle);
            dir.Normalize();

            Vector3 spawnPos = origin + dir * spawnOffset;
            GameObject b = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

            // Rotate so bullet's +Y points along direction.
            // If your prefab faces +X instead, remove the -90f.
            float zRot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            b.transform.rotation = Quaternion.Euler(0f, 0f, zRot);

            // Crit calculation
            bool isCrit = Random.value < clampedCrit;
            float mult = isCrit ? Mathf.Max(1f, critMultiplier) : 1f;
            int finalDamage = Mathf.RoundToInt(damage * mult);

            if (b.TryGetComponent<BulletDamageTrigger>(out var bulletDamage))
                bulletDamage.damageAmount = finalDamage;

            if (b.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
                rb.linearVelocity = dir * shootForce; // stable travel; avoids physics variance of AddForce

            if (bulletLifetime > 0f)
                Destroy(b, bulletLifetime);
        }
    }
}
