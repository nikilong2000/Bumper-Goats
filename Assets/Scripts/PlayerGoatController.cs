using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(Rigidbody))]
public class PlayerGoatController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sidestepForce = 10f;

    [Header("Attack Settings")]
    public float chargeForce = 1000f;
    public float chargeDuration = 10f; // change later only for testing

    [Header("Jump Settings")]
    public float jumpForce = 8f;
    public Transform groundCheck;           // Create an empty child at the goat’s feet and assign here
    public float groundCheckRadius = 0.25f; // Tunable
    public LayerMask groundLayers;          // Set this to your ground layers in the Inspector




    private Rigidbody rb;
    private PlayerControls playerControls; // This will hold a reference to our Input Actions
    private Vector2 moveDirection;
    private bool isCharging = false;
    private bool isGrounded = false;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        // Start listening for actions from the "Goat" action map
        playerControls.Goat.Enable();

        playerControls.Goat.Move.performed += ctx => moveDirection = ctx.ReadValue<Vector2>();
        playerControls.Goat.Move.canceled += ctx => moveDirection = Vector2.zero;

        // For button press actions (performed when the button is pressed down)
        playerControls.Goat.Dodge.performed += OnDodge;
        playerControls.Goat.Attack.performed += OnAttack;
        playerControls.Goat.Brace.performed += OnBrace;
        playerControls.Goat.Brace.canceled += OnBraceReleased; // Listen for button release too
    }

    // OnDisable is called when the object becomes disabled or inactive
    private void OnDisable()
    {
        // Stop listening for actions to avoid errors
        playerControls.Goat.Disable();
    }

    private void Update()
    {
        // Ground check (make sure groundCheck is set in the Inspector)
        if (groundCheck != null)
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

        // Jump on W press (new Input System keyboard)
        if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
        {
            TryJump();
        }
    }

    // FixedUpdate is called on a fixed time step, ideal for physics calculations
    private void FixedUpdate()
    {
        // Create a 3D movement vector from our 2D input
        // (x from moveDirection.x, y is 0, z from moveDirection.y)
        Vector3 move = new(moveDirection.x, moveDirection.y, 0);

        // Apply the movement to the Rigidbody
        rb.linearVelocity = new Vector3(move.x * moveSpeed, rb.linearVelocity.y, move.z * moveSpeed); // fixed: use rb.velocity

    }

    // --- Methods that are CALLED BY the Input System ---

    private void OnDodge(InputAction.CallbackContext context)
    {
        Debug.Log("Dodge Action Triggered!");
        // We'll add a force to the side. "transform.right" gives us the goat's local right direction.
        // We can check the moveDirection to see if they were holding A or D to decide which way to step.
        rb.AddForce(moveDirection.x * sidestepForce * transform.right, ForceMode.Impulse);
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        Debug.Log("Charge Action Triggered!");

        // Don't charge if already charging
        if (!isCharging)
        {
            StartCoroutine(ChargeAttack());
        }
    }


    private void OnBrace(InputAction.CallbackContext context)
    {
        Debug.Log("Bracing!");
    }

    private void OnBraceReleased(InputAction.CallbackContext context)
    {
        Debug.Log("Brace Released!");
    }

    private void TryJump()
    {
        if (!isGrounded || isCharging) return;

        // Reset vertical velocity so jumps are snappy
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }


    private System.Collections.IEnumerator ChargeAttack()
    {
        isCharging = true;

        // Apply a strong forward force to the right (where opponent is)
        rb.AddForce(transform.right * chargeForce, ForceMode.Impulse);

        // Wait for the charge duration
        yield return new WaitForSeconds(chargeDuration);

        // Slow down after charge
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.5f, rb.linearVelocity.y, rb.linearVelocity.z * 0.5f);

        isCharging = false;
    }


}