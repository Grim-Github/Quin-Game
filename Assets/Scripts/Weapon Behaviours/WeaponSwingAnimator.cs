using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Knife))]
public class WeaponSwingAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("The total angle of the swing in degrees.")]
    public float swingAngle = 120f;
    [Tooltip("The duration of one full swing (out and back).")]
    public float swingDuration = 0.3f;
    [Tooltip("The offset of the sprite from the pivot point.")]
    public Vector3 spriteOffset = new Vector3(0.75f, 0, 0);
    [Tooltip("The scale of the weapon sprite.")]
    public float spriteScale = 1f;
    [Tooltip("The sorting order of the swinging sprite.")]
    public int sortingOrder = 1;

    private GameObject _spriteObject;
    private GameObject _spriteHolder;
    private Coroutine _swingCoroutine;
    private Knife _knife;

    void Awake()
    {
        _knife = GetComponent<Knife>();
    }

    void Start()
    {
        _spriteObject = new GameObject("WeaponSwingPivot");
        _spriteObject.transform.SetParent(transform);
        _spriteObject.transform.localPosition = Vector3.zero;

        _spriteHolder = new GameObject("SpriteHolder");
        _spriteHolder.transform.SetParent(_spriteObject.transform);
        _spriteHolder.transform.localPosition = spriteOffset;
        _spriteHolder.transform.localScale = Vector3.one * spriteScale;

        var renderer = _spriteHolder.AddComponent<SpriteRenderer>();
        if (_knife.weaponSprite != null)
        {
            renderer.sprite = _knife.weaponSprite;
        }
        else
        {
            Debug.LogWarning("WeaponSwingAnimator: The referenced Knife script is missing a weaponSprite.", this);
        }
        renderer.sortingOrder = sortingOrder;

        _spriteObject.SetActive(false);
    }

    public void Swing()
    {
        if (gameObject.activeInHierarchy)
        {
            if (_swingCoroutine != null)
            {
                StopCoroutine(_swingCoroutine);
            }
            _swingCoroutine = StartCoroutine(SwingCoroutine());
        }
    }

    private IEnumerator SwingCoroutine()
    {
        _spriteObject.SetActive(true);

        float halfDuration = swingDuration / 2f;
        
        Quaternion swingStartOffset = Quaternion.Euler(0, 0, swingAngle / 2f);
        Quaternion swingEndOffset = Quaternion.Euler(0, 0, -swingAngle / 2f);

        float timer = 0f;
        while (timer < halfDuration)
        {
            _spriteObject.transform.localRotation = Quaternion.Slerp(swingStartOffset, swingEndOffset, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        _spriteObject.transform.localRotation = swingEndOffset;

        timer = 0f;
        while (timer < halfDuration)
        {
            _spriteObject.transform.localRotation = Quaternion.Slerp(swingEndOffset, swingStartOffset, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        
        _spriteObject.SetActive(false);
        _swingCoroutine = null;
    }
}