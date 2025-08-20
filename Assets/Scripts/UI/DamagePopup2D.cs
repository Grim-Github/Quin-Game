// DamagePopup2D.cs
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class DamagePopup2D : MonoBehaviour
{
    public float lifetime = 0.8f;
    public float floatSpeed = 1.5f;     // units/sec upward
    public float fadeStart = 0.3f;      // seconds before end to start fading

    private TextMeshProUGUI tmp;
    private float age;
    private Color baseColor;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>() ?? GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null) { Debug.LogWarning("DamagePopup2D: no TextMeshProUGUI found."); enabled = false; return; }
        baseColor = tmp.color;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;

        // move up
        transform.position += Vector3.up * floatSpeed * dt;

        // fade near the end
        float tLeft = lifetime - age;
        if (tLeft <= fadeStart)
        {
            float a = Mathf.Clamp01(tLeft / Mathf.Max(0.0001f, fadeStart));
            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
        }

        if (age >= lifetime) Destroy(gameObject);
    }

    public void SetText(string text)
    {
        if (tmp != null) tmp.text = text;
    }
}
