using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(Rigidbody))]


public class PlayerGoatController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sidestepForce = 10f;

    private Rigidbody rb;
    private PlayerControls playerControls; // This will hold a reference to our Input Actions
    private Vector2 moveDirection;

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

    // FixedUpdate is called on a fixed time step, ideal for physics calculations
    private void FixedUpdate()
    {
        // Create a 3D movement vector from our 2D input
        // (x from moveDirection.x, y is 0, z from moveDirection.y)
        Vector3 move = new(moveDirection.x, 0, moveDirection.y);

        // Apply the movement to the Rigidbody
        rb.linearVelocity = new Vector3(move.x * moveSpeed, rb.linearVelocity.y, move.z * moveSpeed);
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
        // Future logic for charging goes here
    }


    private void OnBrace(InputAction.CallbackContext context)
    {
        Debug.Log("Bracing!");
        // Future logic for starting the brace stance goes here
    }

    private void OnBraceReleased(InputAction.CallbackContext context)
    {
        Debug.Log("Brace Released!");
        // Future logic for ending the brace stance goes here
    }


}