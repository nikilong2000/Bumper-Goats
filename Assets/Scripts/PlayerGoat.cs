using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(GoatController))]
public class PlayerGoatController : MonoBehaviour
{
    private GoatController goatController;
    private PlayerControls playerControls; // This will hold a reference to our Input Actions
    private Vector2 moveDirection;

    private void Awake()
    {   
        goatController = GetComponent<GoatController>();
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
        playerControls.Goat.Jump.performed += OnJump;
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
        // Pass movement input to GoatController
        goatController.Move(moveDirection);
    }

    // --- Methods that are CALLED BY the Input System ---

    private void OnDodge(InputAction.CallbackContext context)
    {
        Debug.Log("Dodge Action Triggered!");
        goatController.Dodge(moveDirection);
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        Debug.Log("Charge Action Triggered!");
        goatController.Attack();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        Debug.Log("Jump Action Triggered!");
        goatController.Jump();
    }

    private void OnBrace(InputAction.CallbackContext context)
    {
        Debug.Log("Bracing!");
        goatController.Brace(true);
    }

    private void OnBraceReleased(InputAction.CallbackContext context)
    {
        Debug.Log("Brace Released!");
        goatController.Brace(false);
    }
}