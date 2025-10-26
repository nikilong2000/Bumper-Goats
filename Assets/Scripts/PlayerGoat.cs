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

        // Movement
        playerControls.Goat.Move.performed += OnMove;
        playerControls.Goat.Move.canceled  += OnMoveCanceled;

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
        // Unsubscribe to avoid duplicate callbacks on re-enable
        playerControls.Goat.Move.performed  -= OnMove;
        playerControls.Goat.Move.canceled   -= OnMoveCanceled;
        playerControls.Goat.Dodge.performed -= OnDodge;
        playerControls.Goat.Attack.performed -= OnAttack;
        playerControls.Goat.Jump.performed  -= OnJump;
        playerControls.Goat.Brace.performed -= OnBrace;
        playerControls.Goat.Brace.canceled -= OnBraceReleased;
        
        // Stop listening for actions to avoid errors
        playerControls.Goat.Disable();
    }

    private void Update()
    {
        // Pass movement input to GoatController
        goatController.Move(moveDirection);
    }

    // --- Methods that are CALLED BY the Input System ---
    private void OnMove(InputAction.CallbackContext context)        => moveDirection = context.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext context)  => moveDirection = Vector2.zero;

    private void OnDodge(InputAction.CallbackContext context)  => goatController.Dodge(moveDirection);
    private void OnAttack(InputAction.CallbackContext context) => goatController.Attack();

    // Queue the jump (don’t jump immediately here)
    private void OnJump(InputAction.CallbackContext context)   => goatController.Jump();

    private void OnBrace(InputAction.CallbackContext context)         => goatController.Brace(true);
    private void OnBraceReleased(InputAction.CallbackContext context) => goatController.Brace(false);
}