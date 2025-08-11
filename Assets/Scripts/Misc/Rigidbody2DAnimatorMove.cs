using UnityEngine;

public class Rigidbody2DAnimatorMove : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (animator == null || rb == null) return;

        bool moving = rb.linearVelocity.sqrMagnitude > 0.01f; // small threshold to avoid flickering
        animator.SetBool("isMoving", moving);
    }
}
