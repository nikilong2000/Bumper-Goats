using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float movementThreshold = 0.5f;

    private Animator animator;
    private GoatController goatController;
    private Rigidbody rb;

    // Stores previous states.
    private bool wasCharging = false;
    private bool wasGrounded = false;
    private bool wasDodging = false;
    private bool wasBracing = false;
    private bool wasHit = false;

    void Start()
    {
        animator = GetComponent<Animator>();

        goatController = GetComponent<GoatController>();
        if (goatController == null) goatController = GetComponentInParent<GoatController>();

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = GetComponentInParent<Rigidbody>();

        if (animator == null) Debug.LogError("PlayerAnimation: Animator component not found on " + gameObject.name);
        if (goatController == null) Debug.LogWarning("PlayerAnimation: GoatController not found on " + gameObject.name + " or its parents!");
        if (rb == null) Debug.LogWarning("PlayerAnimation: Rigidbody not found on " + gameObject.name + " or its parents!");

        // Initialises animator parameters.
        if (animator != null)
        {
            animator.SetBool("IsRunningForward", false);
            animator.SetBool("IsRunningBackward", false);
            animator.SetBool("IsBracing", false);
        }
    }

    void Update()
    {
        if (animator == null || goatController == null || rb == null)
            return;

        // Gets current velocity.
        float velocityX = rb.linearVelocity.x;

        // Gets states from controller.
        bool isBracing = goatController.IsBraced;
        bool isCharging = goatController.IsCharging;
        bool isGrounded = goatController.IsGrounded;
        bool isDodging = goatController.IsDodging;
        bool isHit = goatController.IsHit;

        // Updates running animation.
        bool isRunningForward = !isBracing && velocityX > movementThreshold;
        bool isRunningBackward = !isBracing && velocityX < -movementThreshold;

        animator.SetBool("IsRunningForward", isRunningForward);
        animator.SetBool("IsRunningBackward", isRunningBackward);
        animator.SetBool("IsBracing", isBracing);

        // Triggers jump animation.
        if (wasGrounded && !isGrounded)
        {
            animator.SetTrigger("DoJump");
        }

        // Triggers attack animation.
        if (!wasCharging && isCharging)
        {
            animator.SetTrigger("DoAttack");
        }

        // Triggers dodge animation.
        if (!wasDodging && isDodging)
        {
            animator.SetTrigger("DoSpinLeft");
        }

        // Triggers hit animation.
        if (!wasHit && isHit)
        {
            animator.SetTrigger("DoHit");
        }

        // Stores states for next frame.
        wasCharging = isCharging;
        wasGrounded = isGrounded;
        wasDodging = isDodging;
        wasBracing = isBracing;
        wasHit = isHit;
    }
}
