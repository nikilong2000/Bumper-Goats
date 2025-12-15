using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(GoatController))]
public class PlayerGoatController : MonoBehaviour
{
    private GoatController goatController;
    private PlayerControls playerControls;
    private Vector2 moveDirection;

    private void Awake()
    {
        goatController = GetComponent<GoatController>();
        playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        // Enables controls.
        playerControls.Goat.Enable();

        // Subscribes to movement events.
        playerControls.Goat.Move.performed += OnMove;
        playerControls.Goat.Move.canceled += OnMoveCanceled;

        // Subscribes to action events.
        playerControls.Goat.Dodge.performed += OnDodge;
        playerControls.Goat.Attack.performed += OnAttack;
        playerControls.Goat.Jump.performed += OnJump;
        playerControls.Goat.Brace.performed += OnBrace;
        playerControls.Goat.Brace.canceled += OnBraceReleased;
    }

    private void OnDisable()
    {
        // Unsubscribes from events.
        playerControls.Goat.Move.performed -= OnMove;
        playerControls.Goat.Move.canceled -= OnMoveCanceled;
        playerControls.Goat.Dodge.performed -= OnDodge;
        playerControls.Goat.Attack.performed -= OnAttack;
        playerControls.Goat.Jump.performed -= OnJump;
        playerControls.Goat.Brace.performed -= OnBrace;
        playerControls.Goat.Brace.canceled -= OnBraceReleased;

        // Disables controls.
        playerControls.Goat.Disable();
    }

    private void Update()
    {
        // Moves the goat.
        goatController.Move(moveDirection);
    }

    // Handles movement input.
    private void OnMove(InputAction.CallbackContext context) => moveDirection = context.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext context) => moveDirection = Vector2.zero;

    // Handles action inputs.
    private void OnDodge(InputAction.CallbackContext context) => goatController.Dodge(moveDirection);
    private void OnAttack(InputAction.CallbackContext context) => goatController.Attack();
    private void OnJump(InputAction.CallbackContext context) => goatController.Jump();
    private void OnBrace(InputAction.CallbackContext context) => goatController.Brace(true);
    private void OnBraceReleased(InputAction.CallbackContext context) => goatController.Brace(false);
}