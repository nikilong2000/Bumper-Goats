
using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class GoatController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 16f;

    [Header("Attack Settings")]
    [SerializeField] private float chargeForce = 200f;
    [SerializeField] private float chargeDuration = 0.7f; // change later only for testing

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 120f;
    [SerializeField] private float fallMultiplier = 10f; // How much faster to fall (higher = less floaty)
    [SerializeField] private float lowJumpMultiplier = 2f; // Gravity multiplier when not holding jump
    [SerializeField] private Transform groundCheck; // empty child at goats feet
    [SerializeField] private float groundCheckRadius = 0.25f; // Tunable
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask goatLayer; // since the goat should be grounded when hitting the other goat

    [Header("Brace Settings")]
    [SerializeField] private float braceMassMultiplier = 3f; // how many times heavier when bracing

    [Header("Dodge Settings")]
    [SerializeField] private float dodgeDistance = 5.6f; // How far to shift on z-axis
    [SerializeField] private float dodgeDuration = 0.3f; // How long the dodge animation takes
    [SerializeField] private float dodgeReturnSpeed = 5f; // How fast to return to z=0

    [Header("Directional Settings")]
    // --- Visual & Flipping Vars ---
    [SerializeField] private Transform opponent; // Drag the opponent goat here in the Inspector
    [SerializeField] private Transform goatModel; // Drag the child object with the renderer here

    // // Store the rotations so we don't create new ones every frame
    // private Quaternion facingRight;
    // private Quaternion facingLeft;

    [Header("Stamina Settings")]
    public Image staminaBar;
    public float currentStamina;
    public float maxStamina = 100f;
    public float staminaRegenRate;
    private float dodgeStaminaCost = 10f;
    private float chargeStaminaCost = 20f;
    private float jumpStaminaCost = 5f;
    private float braceInitialCost = 15f; // Upfront cost to activate brace
    private float braceDrainRate = 5f; // Stamina per second while bracing

    private Coroutine staminaRegenCoroutine;
    private Coroutine braceDrainCoroutine;
    private float staminaRechargeDelay = 2f; // Delay before stamina starts recharging
    private float staminaRechargeRate = 15f; // Stamina points per second

    // Internal state
    private Rigidbody rb;
    private float originalMass;
    // private bool attackToTheRight = true;
    private bool isCharging = false;
    private bool isGrounded = false;
    private bool isBraced = false;
    private bool isDodging = false;

    // Getters for AI observations
    public bool IsGrounded => isGrounded;
    public bool IsCharging => isCharging;
    public bool IsBraced => isBraced;
    public bool IsDodging => isDodging;

    // private bool isJumping = false;
    private float jumpStartXVelocity; // Store x-velocity when jump starts

    private Vector2 moveDirection;

    // Store the rotations so we don't create new ones every frame
    private Quaternion facingRight;
    private Quaternion facingLeft;
    private bool attackToTheRight = true;


    // --- Jump queue/lock ---
    private bool jumpRequested = false;
    private bool jumpUsedThisGround = false;

    [SerializeField] private float jumpCooldown = 0.05f;
    private float jumpCooldownTimer = 0f;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalMass = rb.mass;

        facingRight = Quaternion.Euler(0, 90, 0);
        facingLeft = Quaternion.Euler(0, -90, 0);

        // Initialize stamina if not set
        if (currentStamina <= 0) currentStamina = maxStamina;
    }

    private void OnEnable()
    {
        // Ensure stamina regen starts when the script is enabled
        if (staminaRegenCoroutine != null) StopCoroutine(staminaRegenCoroutine);
        staminaRegenCoroutine = StartCoroutine(RechargeStamina());

        // Reset flags that might be stuck if disabled during action
        isCharging = false;
        isDodging = false;
        isBraced = false;
        jumpRequested = false;

        // Reset mass if it was stuck in brace mode
        if (rb != null)
        {
            rb.mass = originalMass;
            rb.constraints &= ~RigidbodyConstraints.FreezePositionX;
        }
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

        // Reset jump lock when grounded
        // Only reset if we are not moving upwards significantly (prevents resetting during takeoff)
        if (isGrounded && rb.linearVelocity.y <= 0.1f)
            jumpUsedThisGround = false;

        // Cooldown timer
        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

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
        rb.linearVelocity = new Vector3(move.x * moveSpeed, rb.linearVelocity.y, move.z * moveSpeed);

        ApplyJumpFallAcceleration();

        TryProcessJump();
    }

    private void ApplyJumpFallAcceleration()
    {
        if (rb.linearVelocity.y < 0)
        {
            // Falling down - apply stronger gravity
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpRequested)
        {
            // Moving up but jump button not held - apply moderate gravity for shorter jumps
            rb.linearVelocity += (lowJumpMultiplier - 1) * Physics.gravity.y * Time.fixedDeltaTime * Vector3.up;
        }
    }

    // Public interface for actions
    public void Move(Vector2 direction)
    {
        moveDirection = direction;
    }

    public void Attack()
    {
        // Debug.Log("Charge Action Triggered!");

        // Don't charge if already charging or stamina does not allow it
        if (!isCharging && (currentStamina >= chargeStaminaCost) && (currentStamina > 0))
        {
            StartCoroutine(ChargeAttack());

            if (staminaRegenCoroutine != null)
                StopCoroutine(staminaRegenCoroutine);

            staminaRegenCoroutine = StartCoroutine(RechargeStamina());
        }
    }

    public void Dodge(Vector2 direction)
    {
        // Debug.Log("Dodge Action Triggered!");

        // Don't dodge if already dodging or stamina does not allow it
        if (!isDodging && (currentStamina >= dodgeStaminaCost) && (currentStamina > 0))
        {
            StartCoroutine(DodgeAnimation());

            if (staminaRegenCoroutine != null)
                StopCoroutine(staminaRegenCoroutine);

            staminaRegenCoroutine = StartCoroutine(RechargeStamina());
        }
    }

    public void Brace(bool shouldBrace)
    {
        if (shouldBrace == isBraced) return;

        // If trying to brace but not enough stamina, prevent it
        if (shouldBrace && currentStamina < braceInitialCost)
        {
            // Debug.Log("Not enough stamina to brace!");
            return;
        }

        isBraced = shouldBrace;

        if (shouldBrace)
        {
            // Debug.Log("Bracing! Mass increased to:" + (originalMass * braceMassMultiplier));

            // Deduct initial stamina cost
            currentStamina -= braceInitialCost;
            if (staminaBar != null)
                staminaBar.fillAmount = currentStamina / maxStamina;

            // make goat heavier (more stable)
            rb.mass = originalMass * braceMassMultiplier;
            rb.constraints |= RigidbodyConstraints.FreezePositionX; // hinder movement

            // Stop stamina regen and start draining
            if (staminaRegenCoroutine != null)
                StopCoroutine(staminaRegenCoroutine);

            braceDrainCoroutine = StartCoroutine(DrainStaminaWhileBracing());
        }
        else
        {
            // Debug.Log("Brace Released! Mass reset to: " + originalMass);
            rb.mass = originalMass;
            rb.constraints &= ~RigidbodyConstraints.FreezePositionX; // can move again

            // Stop draining and start regen
            if (braceDrainCoroutine != null)
                StopCoroutine(braceDrainCoroutine);

            if (staminaRegenCoroutine != null)
                StopCoroutine(staminaRegenCoroutine);

            staminaRegenCoroutine = StartCoroutine(RechargeStamina());
        }
    }

    public void Jump()
    {
        jumpRequested = true;
    }

    private void TryProcessJump()
    {
        if (!jumpRequested) return;

        // Clear request immediately to prevent it from lingering
        jumpRequested = false;

        // Check all conditions
        if (!isGrounded || isCharging || jumpUsedThisGround) return;
        // Check cooldown
        if (jumpCooldownTimer > 0f) return;

        if (currentStamina < jumpStaminaCost) return;

        // Execute the jump 
        currentStamina -= jumpStaminaCost;
        if (staminaBar != null)
            staminaBar.fillAmount = currentStamina / maxStamina;

        Vector3 v = rb.linearVelocity;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        jumpUsedThisGround = true;
        jumpCooldownTimer = jumpCooldown; // Start cooldown

        if (staminaRegenCoroutine != null)
            StopCoroutine(staminaRegenCoroutine);

        staminaRegenCoroutine = StartCoroutine(RechargeStamina());
    }

    private IEnumerator ChargeAttack()
    {
        isCharging = true;
        float attackDirection = attackToTheRight ? 1f : -1f;

        // Stamina cost for making the charge
        // Debug.Log("Charging attack, stamina cost applied.");
        currentStamina -= chargeStaminaCost;
        if (staminaBar != null)
            staminaBar.fillAmount = currentStamina / maxStamina;

        // Apply a strong forward force to the right (where opponent is)
        // rb.AddForce(transform.right * chargeForce, ForceMode.Impulse);
        rb.AddForce(transform.right * attackDirection * chargeForce, ForceMode.Impulse);
        // Wait for the charge duration
        yield return new WaitForSeconds(chargeDuration);

        // Slow down after charge
        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.5f, rb.linearVelocity.y, rb.linearVelocity.z * 0.5f);

        isCharging = false;
    }

    private IEnumerator DodgeAnimation()
    {
        isDodging = true;

        // Stamina cost for making the dodge
        // Debug.Log("Dodging, stamina cost applied.");
        currentStamina -= dodgeStaminaCost;
        if (staminaBar != null)
            staminaBar.fillAmount = currentStamina / maxStamina;

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

    private IEnumerator RechargeStamina()
    {
        yield return new WaitForSeconds(staminaRechargeDelay);

        while (currentStamina < maxStamina)
        {
            currentStamina += staminaRechargeRate / 10f;

            if (currentStamina > maxStamina)
                currentStamina = maxStamina;

            if (staminaBar != null)
                staminaBar.fillAmount = currentStamina / maxStamina;

            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator DrainStaminaWhileBracing()
    {
        while (isBraced && currentStamina > 0)
        {
            currentStamina -= braceDrainRate / 10f;

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                // Force release brace when stamina runs out
                Brace(false);
            }

            if (staminaBar != null)
                staminaBar.fillAmount = currentStamina / maxStamina;

            yield return new WaitForSeconds(0.1f);
        }
    }

    public void ResetStamina()
    {
        currentStamina = 100f;
        if (staminaBar != null)
            staminaBar.fillAmount = 1f;
    }
}