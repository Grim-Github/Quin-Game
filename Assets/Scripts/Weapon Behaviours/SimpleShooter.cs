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



    // --- Shooting API ---

    public void ShootTransform(Transform target)
    {
        if (target == null || bulletPrefab == null) return;

        // Get direction to target
        Vector2 direction = (target.position - transform.position).normalized;

        // Shoot in that direction
        Shoot(direction);
    }

    public void Shoot(Vector2 direction)
    {
        if (bulletPrefab == null) return;

        // Play shoot sound
        if (shootClip != null && shootSource != null)
            shootSource.PlayOneShot(shootClip);

        // Spawn bullets
        for (int i = 0; i < projectileCount; i++)
        {
            // Calculate spread angle for this bullet
            float angle = 0f;
            if (projectileCount > 1 && spreadAngle > 0f)
            {
                float step = spreadAngle / (projectileCount - 1);
                angle = (-spreadAngle * 0.5f) + (step * i);
            }

            // Rotate direction by angle
            Vector2 shootDirection = direction;
            if (angle != 0f)
            {
                float rad = angle * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                shootDirection = new Vector2(
                    direction.x * cos - direction.y * sin,
                    direction.x * sin + direction.y * cos
                );
            }

            // Create bullet
            GameObject bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);

            // Point bullet in shoot direction
            float rotationAngle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);

            // Calculate damage (with crit chance)
            int finalDamage = damage;
            if (Random.value < critChance)
                finalDamage = Mathf.RoundToInt(damage * critMultiplier);

            // Set bullet damage
            if (bullet.TryGetComponent<BulletDamageTrigger>(out var bulletDamage))
                bulletDamage.damageAmount = finalDamage;

            // Move bullet
            if (bullet.TryGetComponent<Rigidbody2D>(out var rb))
                rb.linearVelocity = shootDirection * shootForce;

            // Destroy after lifetime
            if (bulletLifetime > 0f)
                Destroy(bullet, bulletLifetime);
        }
    }

    // --- Debug Helpers ---
    private void OnDrawGizmos()
    {
        // Draw debug lines to help visualize shooting direction
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }
}