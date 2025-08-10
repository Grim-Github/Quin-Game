using UnityEngine;

[RequireComponent(typeof(Animator))]
public class DestroyAfterAnimation : MonoBehaviour
{
    [Tooltip("Optional: extra time (seconds) to wait after the animation finishes.")]
    [SerializeField] private float extraDelay = 0f;

    private Animator animator;
    private float destroyTime;

    private void Start()
    {
        animator = GetComponent<Animator>();

        if (animator.runtimeAnimatorController != null)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            float animLength = state.length;
            destroyTime = Time.time + animLength + extraDelay;
        }
        else
        {
            // No animator/clip? Just destroy immediately
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (Time.time >= destroyTime)
        {
            Destroy(gameObject);
        }
    }
}
