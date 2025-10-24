using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(Rigidbody))]
public class PlayerGoatControllerOld : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sidestepForce = 10f;

    [Header("Attack Settings")]
    [SerializeField] private float chargeForce = 30f;
    [SerializeField] private float chargeDuration = 0.7f; // change later only for testing

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private Transform groundCheck; // empty child at goats feet
    [SerializeField] private float groundCheckRadius = 0.25f; // Tunable
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask goatLayer; // since the goat should be grounded when hitting the other goat

    [Header("Brace Settings")]
    [SerializeField] private float braceMassMultiplier = 3f; // how many times heavier when bracing

    [Header("Dodge Settings")]
    [SerializeField] private float dodgeDistance = 1.4f; // How far to shift on z-axis
    [SerializeField] private float dodgeDuration = 0.3f; // How long the dodge animation takes
    [SerializeField] private float dodgeReturnSpeed = 5f; // How fast to return to z=0

    [Header("Directional Settings")]
    // --- Visual & Flipping Vars ---
    [SerializeField] private Transform opponent; // Drag the opponent goat here in the Inspector
    [SerializeField] private Transform goatModel; // Drag the child object with the renderer here


    // Store the rotations so we don't create new ones every frame
    private Quaternion facingRight;
    private Quaternion facingLeft;

    private bool attackToTheRight = true;




    private Rigidbody rb;
    private PlayerControls playerControls; // This will hold a reference to our Input Actions
    private Vector2 moveDirection;
    private float originalMass;
    private bool isCharging = false;
    private bool isGrounded = false;
    private bool isDodging = false;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalMass = rb.mass;
        playerControls = new PlayerControls();

        facingRight = Quaternion.Euler(0, 90, 0);
        facingLeft = Quaternion.Euler(0, -90, 0);

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

        float directionToOpponent = opponent.position.x - transform.position.x;
        if (directionToOpponent > 0)
        {
            goatModel.rotation = facingRight;
            attackToTheRight = true;
        }
        else if (directionToOpponent < 0)
        {
            goatModel.rotation = facingLeft;
            attackToTheRight = false;
        }


        // Ground check (make sure groundCheck is set in the Inspector)
        if (groundCheck != null)
        {
            // combine ground + other goat masks and check against both
            int combinedMask = groundLayer.value | goatLayer.value;
            isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, combinedMask, QueryTriggerInteraction.Ignore);
        }

        // Smoothly return to z=9 when not dodging
        if (!isDodging && (transform.position.z) > 9.01f || transform.position.z < 8.99f)
        {
            Vector3 pos = transform.position;
            pos.z = Mathf.Lerp(pos.z, 9f, Time.deltaTime * dodgeReturnSpeed);
            transform.position = pos;
        }

    }

    // FixedUpdate is called on a fixed time step, ideal for physics calculations
    private void FixedUpdate()
    {
        // Create a 3D movement vector from our 2D input
        Vector3 move = new(moveDirection.x, moveDirection.y, 0);

        // Apply the movement to the Rigidbody
        rb.linearVelocity = new Vector3(move.x * moveSpeed, rb.linearVelocity.y, move.z * moveSpeed);
        // }
    }

    // --- Methods that are CALLED BY the Input System ---

    private void OnDodge(InputAction.CallbackContext context)
    {
        Debug.Log("Dodge Action Triggered!");

        if (!isDodging)
        {
            StartCoroutine(DodgeAnimation());
        }
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

    private void OnJump(InputAction.CallbackContext context)
    {
        Debug.Log("Jump Action Triggered!");
        TryJump();
    }


    private void OnBrace(InputAction.CallbackContext context)
    {
        Debug.Log("Bracing! Mass increased to:" + (originalMass * braceMassMultiplier));

        // make goat heavier (more stable)
        rb.mass = originalMass * braceMassMultiplier;
        rb.constraints |= RigidbodyConstraints.FreezePositionX; // hinder movement
    }

    private void OnBraceReleased(InputAction.CallbackContext context)
    {
        Debug.Log("Brace Released! Mass reset to: " + originalMass);
        rb.mass = originalMass;
        rb.constraints &= ~RigidbodyConstraints.FreezePositionX; // can move again
    }

    private void TryJump()
    {
        if (!isGrounded || isCharging) return;


        // Reset vertical velocity so jumps are snappy
        Vector3 v = rb.linearVelocity;
        // v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }


    private System.Collections.IEnumerator ChargeAttack()
    {
        isCharging = true;
        float attackDirection = attackToTheRight ? 1f : -1f;


        // Apply a strong forward force to the right (where opponent is)
        // rb.AddForce(transform.right * chargeForce, ForceMode.Impulse);
        rb.AddForce(transform.right * attackDirection * chargeForce, ForceMode.Impulse);
        // Wait for the charge duration
        yield return new WaitForSeconds(chargeDuration);

        // Slow down after charge
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.5f, rb.linearVelocity.y, rb.linearVelocity.z * 0.5f);

        isCharging = false;
    }


    private System.Collections.IEnumerator DodgeAnimation()
    {
        isDodging = true;

        // dodge direction random for now (left, right)
        float direction = UnityEngine.Random.value > 0.5f ? 1f : -1f;

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + new Vector3(0, 0, dodgeDistance * direction);

        float elapsed = 0f;

        // Animate to dodge position
        while (elapsed < dodgeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dodgeDuration;

            Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
            newPos.x = startPos.x; // Keep x-direction fixed during dodge
            transform.position = newPos;

            yield return null;
        }

        isDodging = false;
        // The Update method will handle returning to z=0
    }



}