
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
    [SerializeField] private float pushForce = 800f; // Force applied to opponent when hit during charge

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 120f;
    [SerializeField] private Transform platform;
    [SerializeField] private float groundCheckRadius = 0.25f; // Tunable tolerance for ground detection
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
    private Coroutine chargeAttackCoroutine;
    private Coroutine dodgeAnimationCoroutine;
    private float staminaRechargeDelay = 2f; // Delay before stamina starts recharging
    private float staminaRechargeRate = 5f; // Stamina points per second

    // Internal state
    private Rigidbody rb;
    private float originalMass;
    // private bool attackToTheRight = true;
    private bool isCharging = false;
    private bool isGrounded = false;
    private bool isBraced = false;
    private bool isDodging = false;
    private GoatController currentAttacker = null; // Track who is currently attacking us

    // Getters for AI observations
    public bool IsGrounded => isGrounded;
    public bool IsCharging => isCharging;
    public bool IsBraced => isBraced;
    public bool IsDodging => isDodging;
    public bool AttackToTheRight => attackToTheRight; // Getter for attack direction

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

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    
    // Reset lock to prevent Update/FixedUpdate from interfering during reset
    private bool isResetting = false;

    public void SetOriginalPositionAndRotation()
    {
        // Debug.Log($"[GoatController] SetOriginalPositionAndRotation() executed on {gameObject.name}");
        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    public void Reset()
    {
        Debug.Log("Resetting GoatController on object with tag: " + gameObject.tag);
        // Lock updates to prevent interference
        isResetting = true;
        
        // Stop all ongoing coroutines
        if (staminaRegenCoroutine != null)
        {
            StopCoroutine(staminaRegenCoroutine);
            staminaRegenCoroutine = null;
        }
        
        if (braceDrainCoroutine != null)
        {
            StopCoroutine(braceDrainCoroutine);
            braceDrainCoroutine = null;
        }
        
        if (chargeAttackCoroutine != null)
        {
            StopCoroutine(chargeAttackCoroutine);
            chargeAttackCoroutine = null;
        }
        
        if (dodgeAnimationCoroutine != null)
        {
            StopCoroutine(dodgeAnimationCoroutine);
            dodgeAnimationCoroutine = null;
        }
        
        // Reset state flags
        isCharging = false;
        isDodging = false;
        isBraced = false;
        currentAttacker = null;
        
        // Reset all state flags
        jumpRequested = false;
        jumpUsedThisGround = false;
        jumpCooldownTimer = 0f;
        moveDirection = Vector2.zero;
        
        // Reset physics state
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Reset position and rotation
        transform.position = originalPosition;
        transform.rotation = originalRotation;
        
        // Reset stamina
        ResetStamina();
        
        // Unlock updates - reset is complete
        isResetting = false;
    }

    private void Awake()
    {
        // Debug.Log($"[GoatController] Awake() executed on {gameObject.name}");
        rb = GetComponent<Rigidbody>();
        originalMass = rb.mass;

        facingRight = Quaternion.Euler(0, 90, 0);
        facingLeft = Quaternion.Euler(0, -90, 0);
    }

    private void Update()
    {
        // Skip update if reset is in progress
        if (isResetting) return;
        
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


        // Ground check using platform only
        bool groundedOnPlatform = false;
        
        // Check if grounded on platform using platform Transform
        if (platform != null)
        {
            // Get the platform's surface Y position
            float platformSurfaceY = platform.position.y;
            
            // Try to get platform's actual surface height from renderer bounds
            Renderer platformRenderer = platform.GetComponent<Renderer>();
            if (platformRenderer != null)
            {
                // Surface is at the top of the bounds
                platformSurfaceY = platformRenderer.bounds.max.y;
            }
            
            // Use goat's position directly for ground check
            float goatY = transform.position.y;
            
            // Check if goat is close to platform surface (within groundCheckRadius)
            // Allow a small tolerance above the surface (for when goat is slightly above)
            float distanceToPlatformY = goatY - platformSurfaceY;
            bool isAtCorrectHeight = distanceToPlatformY >= -groundCheckRadius && distanceToPlatformY <= groundCheckRadius;
            
            // Also check if goat is horizontally over the platform (not off the edge)
            // Calculate horizontal distance from platform center
            Vector3 goatPos = transform.position;
            Vector3 platformPos = platform.position;
            float horizontalDistance = Vector3.Distance(new Vector3(goatPos.x, 0, goatPos.z), new Vector3(platformPos.x, 0, platformPos.z));
            
            // Get platform radius (check ArenaShrinking singleton first, then fallback to renderer bounds)
            float platformRadius = 10f; // Default fallback
            if (ArenaShrinking.Instance != null)
            {
                platformRadius = ArenaShrinking.Instance.PlatformRadius;
            }
            else if (platformRenderer != null)
            {
                // Use renderer bounds as fallback
                Bounds bounds = platformRenderer.bounds;
                platformRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            }
            
            // Goat is grounded only if it's at the right height AND horizontally over the platform
            groundedOnPlatform = isAtCorrectHeight && horizontalDistance <= platformRadius;
        }
        
        // Only check platform for grounded state
        isGrounded = groundedOnPlatform;

        // Reset jump lock when grounded
        if (isGrounded) jumpUsedThisGround = false;

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
        // Skip fixed update if reset is in progress
        if (isResetting) return;
        
        // Perform ground check in FixedUpdate to ensure it's current when we use it
        // This prevents timing issues where Update() hasn't run yet this frame
        bool groundedOnPlatform = false;
        
        // Check if grounded on platform using platform Transform
        if (platform != null)
        {
            // Get the platform's surface Y position
            // Account for platform's scale if it has a renderer or collider
            float platformSurfaceY = platform.position.y;
            
            // Try to get platform's actual surface height from renderer bounds
            Renderer platformRenderer = platform.GetComponent<Renderer>();
            if (platformRenderer != null)
            {
                // Surface is at the top of the bounds
                platformSurfaceY = platformRenderer.bounds.max.y;
            }
            else
            {
                // Fallback: assume platform center is at its surface, or add half scale
                // Most platforms are thin, so position.y should be close to surface
                platformSurfaceY = platform.position.y;
            }
            
            // Use goat's position directly for ground check
            float goatY = transform.position.y;
            
            // Check if goat is close to platform surface (within groundCheckRadius)
            // Allow a small tolerance above the surface (for when goat is slightly above)
            float distanceToPlatformY = goatY - platformSurfaceY;
            bool isAtCorrectHeight = distanceToPlatformY >= -groundCheckRadius && distanceToPlatformY <= groundCheckRadius;
            
            // Also check if goat is horizontally over the platform (not off the edge)
            // Calculate horizontal distance from platform center
            Vector3 goatPos = transform.position;
            Vector3 platformPos = platform.position;
            float horizontalDistance = Vector3.Distance(new Vector3(goatPos.x, 0, goatPos.z), new Vector3(platformPos.x, 0, platformPos.z));
            
            // Get platform radius (check ArenaShrinking singleton first, then fallback to renderer bounds)
            float platformRadius = 10f; // Default fallback
            if (ArenaShrinking.Instance != null)
            {
                platformRadius = ArenaShrinking.Instance.PlatformRadius;
            }
            else if (platformRenderer != null)
            {
                // Use renderer bounds as fallback
                Bounds bounds = platformRenderer.bounds;
                platformRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            }
            
            // Goat is grounded only if it's at the right height AND horizontally over the platform
            groundedOnPlatform = isAtCorrectHeight && horizontalDistance <= platformRadius;
        }
        
        // Only check platform for grounded state
        isGrounded = groundedOnPlatform;
        
        // Apply horizontal movement (X-axis only) - do this first
        // If we're being attacked, we'll add the attack force after
        if (currentAttacker == null)
        {
            // Normal movement: set velocity directly
            rb.linearVelocity = new Vector3(moveDirection.x * moveSpeed, rb.linearVelocity.y, 0);
        }
        
        // Apply attack push force if we're being attacked
        // This adds to the current velocity/forces
        if (currentAttacker != null)
        {
            Debug.Log($"[GoatController] Linear Velocity Before ApplyAttackPush: {rb.linearVelocity}");
            ApplyAttackPush();
            Debug.Log($"[GoatController] Linear Velocity After ApplyAttackPush: {rb.linearVelocity}");
        }
        
        // Handle grounding for all cases
        // Clamp Y velocity to 0 when grounded to prevent floating
        // This must be done AFTER setting velocity so gravity can work when not grounded
        if (isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        TryProcessJump();
        if (currentAttacker != null)
            Debug.Log($"[GoatController] Linear Velocity After FixedUpdate: {rb.linearVelocity}");

    }

    // Public interface for actions
    public void Move(Vector2 direction)
    {
        // Debug.Log($"[GoatController] Move() executed on {gameObject.name} with direction: {direction}");
        moveDirection = direction;
    }

    public void Attack()
    {
        // Debug.Log($"[GoatController] Attack() executed on {gameObject.name}");

        // Don't charge if already charging or stamina does not allow it
        if (!isCharging && (currentStamina >= chargeStaminaCost) && (currentStamina > 0))
        {
            if (chargeAttackCoroutine != null)
                StopCoroutine(chargeAttackCoroutine);
            chargeAttackCoroutine = StartCoroutine(ChargeAttack());

            if (staminaRegenCoroutine != null)
                StopCoroutine(staminaRegenCoroutine);

            staminaRegenCoroutine = StartCoroutine(RechargeStamina());
        }
    }

    public void Dodge(Vector2 direction)
    {
        // Debug.Log($"[GoatController] Dodge() executed on {gameObject.name} with direction: {direction}");

        // Don't dodge if already dodging, not grounded (jumping), or stamina does not allow it
        if (!isDodging && isGrounded && (currentStamina >= dodgeStaminaCost) && (currentStamina > 0))
        {
            if (dodgeAnimationCoroutine != null)
                StopCoroutine(dodgeAnimationCoroutine);
            dodgeAnimationCoroutine = StartCoroutine(DodgeAnimation());

            if (staminaRegenCoroutine != null)
                StopCoroutine(staminaRegenCoroutine);

            staminaRegenCoroutine = StartCoroutine(RechargeStamina());
        }
    }

    public void Brace(bool shouldBrace)
    {
        // Debug.Log($"[GoatController] Brace() executed on {gameObject.name} with shouldBrace: {shouldBrace}");
        if (shouldBrace == isBraced || !isGrounded) return;

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
        // Debug.Log($"[GoatController] Jump() executed on {gameObject.name}");
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

        // Debug.Log($"[GoatController] TryProcessJump() - Jump executed on {gameObject.name}");
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
        // Debug.Log($"[GoatController] ChargeAttack() coroutine started on {gameObject.name}");
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
        chargeAttackCoroutine = null;
    }

    private IEnumerator DodgeAnimation()
    {
        // Debug.Log($"[GoatController] DodgeAnimation() coroutine started on {gameObject.name}");
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
        dodgeAnimationCoroutine = null;
        // The Update method will handle returning to z=0
    }

    private IEnumerator RechargeStamina()
    {
        // Debug.Log($"[GoatController] RechargeStamina() coroutine started on {gameObject.name}");
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
        // Debug.Log($"[GoatController] DrainStaminaWhileBracing() coroutine started on {gameObject.name}");
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
        // Debug.Log($"[GoatController] ResetStamina() executed on {gameObject.name}");
        currentStamina = 100f;
        if (staminaBar != null)
            staminaBar.fillAmount = 1f;
    }

    /// <summary>
    /// Called every frame while this goat is colliding with another object
    /// If we're attacking and colliding with opponent, notify them they're being attacked
    /// </summary>
    private void OnCollisionStay(Collision collision)
    {
        // Check if we're colliding with the opponent
        if (opponent != null && collision.gameObject.transform == opponent)
        {
            // If we're currently attacking (charging), notify the opponent
            if (isCharging)
            {
                GoatController opponentController = opponent.GetComponent<GoatController>();
                if (opponentController != null)
                {
                    opponentController.BeingAttacked(this);
                }
            }
        }
    }
    
    /// <summary>
    /// Called when collision with opponent ends
    /// Clear the attacker reference
    /// </summary>
    private void OnCollisionExit(Collision collision)
    {
        // Check if we stopped colliding with the opponent
        if (opponent != null && collision.gameObject.transform == opponent)
        {
            // Clear attacker if it was the opponent
            if (currentAttacker != null && currentAttacker.transform == opponent)
            {
                currentAttacker = null;
            }
        }
    }

    /// <summary>
    /// Called when this goat is being attacked by the opponent
    /// Sets the attacker reference so force can be applied in FixedUpdate
    /// </summary>
    /// <param name="attacker">The GoatController of the goat attacking us</param>
    public void BeingAttacked(GoatController attacker)
    {
        if (attacker == null) return;

        // Only track attacker if they're currently charging (attacking)
        if (attacker.IsCharging)
        {
            currentAttacker = attacker;
        }
        else
        {
            // Clear attacker if they're no longer attacking
            if (currentAttacker == attacker)
            {
                currentAttacker = null;
            }
        }
    }

    /// <summary>
    /// Applies push force from the current attacker
    /// Called in FixedUpdate for consistent physics timestep
    /// </summary>
    private void ApplyAttackPush()
    {
        if (currentAttacker == null) return;

        // Verify attacker is still charging
        if (!currentAttacker.IsCharging)
        {
            Debug.Log("Attacker is not charging, clearing attacker");
            currentAttacker = null;
            return;
        }

        // Calculate effective push force
        // If we're bracing, reduce push force significantly
        float effectivePushForce = pushForce;
        if (isBraced)
        {
            effectivePushForce *= 0.3f; // Reduce push force by 70% when bracing
        }

        // Calculate push direction based on attacker's attack direction
        // Use attacker's attack direction for more predictable pushes
        // If attacker is attacking to the right, push us to the right (positive X)
        // If attacker is attacking to the left, push us to the left (negative X)
        float attackerAttackDirection = currentAttacker.AttackToTheRight ? 1f : -1f;
        Vector3 pushDirection = new Vector3(attackerAttackDirection, 0f, 0f).normalized;

        // Apply push force directly to velocity for immediate effect
        // This ensures the push happens even if other forces are applied
        Vector3 currentVel = rb.linearVelocity;
        Vector3 pushVelocity = pushDirection * effectivePushForce * Time.fixedDeltaTime;
        rb.linearVelocity = new Vector3(currentVel.x + pushVelocity.x, currentVel.y, currentVel.z);
        
        Debug.Log($"Pushed by {effectivePushForce} in direction {pushDirection}, new X velocity: {rb.linearVelocity.x}");
    }
}