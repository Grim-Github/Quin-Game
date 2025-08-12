using TMPro;
using UnityEngine;
using UnityEngine.UI; // Added for Image

public class SimpleShooter : MonoBehaviour
{
    [Header("Projectile Settings")]
    public GameObject bulletPrefab;
    public float shootForce = 10f;
    public int damage = 15;
    public float bulletLifetime = 5f;

    [Header("Criticals")]
    [Range(0f, 1f)] public float critChance = 0f;
    [Min(1f)] public float critMultiplier = 2f;

    [Header("Shot Pattern")]
    public int projectileCount = 1;
    [Tooltip("Total cone in degrees. Each projectile gets a random angle within [-spread/2, +spread/2].")]
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

            statsTextInstance.text =
                $"<b>{transform.name} Stats</b>\n" +
                $"Damage: {damage}\n" +
                $"Attack Delay: {delay}\n" +
                $"Proj Speed: {shootForce:F1}\n" +
                $"Lifetime: {bulletLifetime:F1}s\n" +
                $"Projectile Count: {projectileCount}\n" +
                $"Penetration: {penetrationInfo}\n" +
                $"Crit: {(Mathf.Clamp01(critChance) * 100f):F0}% x{critMultiplier:F2}\n" +
                extraTextField;
        }
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
            GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

            // Point projectile
            float rotationAngle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);

            // Calculate damage (with crit)
            int finalDamage = damage;
            if (Random.value < critChance)
                finalDamage = Mathf.RoundToInt(damage * critMultiplier);

            // Apply damage to the correct component type
            if (bullet.TryGetComponent<BulletDamageTrigger>(out var bulletDamage))
                bulletDamage.damageAmount = finalDamage;

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
