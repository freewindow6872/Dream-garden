using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator2D : MonoBehaviour
{
    [SerializeField] private PlayerController2D controller;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (controller == null)
            controller = GetComponentInParent<PlayerController2D>();
    }

    private void Update()
    {
        if (controller == null) return;

        animator.SetFloat("Speed", controller.HorizontalSpeed());
        animator.SetFloat("VerticalSpeed", controller.VerticalSpeed());
        animator.SetBool("Grounded", controller.IsGrounded());
    }
}
