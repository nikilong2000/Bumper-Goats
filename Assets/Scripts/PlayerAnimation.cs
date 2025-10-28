using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float movementThreshold = 0.5f; // Minimum velocity to trigger running animation

    private Animator animator;
    private GoatController goatController;
    private Rigidbody rb;

    // Previous frame states for detecting triggers
    private bool wasCharging = false;
    private bool wasGrounded = false;  // Changed to false - don't assume grounded at start
    private bool wasDodging = false;
    private bool wasBracing = false;

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
        
        // Initialize all animator parameters to false/default state at start
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

        // Get current velocity
        float velocityX = rb.linearVelocity.x;

        // Get states from GoatController
        bool isBracing = goatController.IsBraced;
        bool isCharging = goatController.IsCharging;
        bool isGrounded = goatController.IsGrounded;
        bool isDodging = goatController.IsDodging;

        // Movement booleans - simple velocity check
        // Don't show running animations while bracing (prevents transition conflicts)
        bool isRunningForward = !isBracing && velocityX > movementThreshold;
        bool isRunningBackward = !isBracing && velocityX < -movementThreshold;

        animator.SetBool("IsRunningForward", isRunningForward);
        animator.SetBool("IsRunningBackward", isRunningBackward);
        animator.SetBool("IsBracing", isBracing);  // Set every frame like the original code
        
        // Jump - when leaving ground
        if (wasGrounded && !isGrounded)
        {
            animator.SetTrigger("DoJump");
        }

        // Attack - when starting charge
        if (!wasCharging && isCharging)
        {
            animator.SetTrigger("DoAttack");
        }

        // Dodge - when starting dodge
        if (!wasDodging && isDodging)
        {
            animator.SetTrigger("DoSpinLeft");
        }

        // Store states for next frame
        wasCharging = isCharging;
        wasGrounded = isGrounded;
        wasDodging = isDodging;
        wasBracing = isBracing;
    }
}
