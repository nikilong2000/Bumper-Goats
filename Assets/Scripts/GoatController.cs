
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
    [SerializeField] private float pushInitialVelocity = 30f; // Initial velocity boost when first hit (creates bounce effect)
    [SerializeField] private float pushAcceleration = 200f; // Continuous acceleration during push (for sustained momentum)
    [SerializeField] private float maxPushVelocity = 200f; // Maximum velocity from push to prevent runaway
    [SerializeField] private float pushDuration = 0.5f; // How long the push momentum lasts (creates extended movement)

    [Header("Jump Settings")]
    [SerializeField] private float jumpAcceleration = 200f; // Jump acceleration force (applied over time for smoother jump)
    [SerializeField] private float jumpDuration = 0.2f; // How long to apply jump acceleration (longer = higher jump)
    [SerializeField] private float gravityScale = 0.5f; // Overall gravity multiplier (lower = floatier, higher jumps)
    [SerializeField] private float fallMultiplier = 10f; // How much faster to fall (higher = less floaty)
    [SerializeField] private float lowJumpMultiplier = 2f; // Gravity multiplier when not holding jump
    [SerializeField] private Transform platform;
    [SerializeField] private float groundCheckRadius = 0.25f; // Tunable tolerance for ground detection
    [SerializeField] private float groundCheckSinkTolerance = 0.5f; // How far below surface the goat can be and still be considered grounded
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
    private float staminaRechargeRate = 15f; // Stamina points per second

    // Internal state
    private Rigidbody rb;
    private Collider goatCollider;
    private float originalMass;
    // private bool attackToTheRight = true;
    private bool isCharging = false;
    private bool isGrounded = false;
    private bool isBraced = false;
    private bool isDodging = false;
    private GoatController currentAttacker = null; // Track who is currently attacking us
    private float pushStartTime = -1f; // When the current push started (-1 means no active push)
    private float pushDirection = 0f; // Direction of the current push (1 or -1)
    private bool isCollidingWithPlatform = false; // Track if we're colliding with the platform

    // Getters for AI observations
    public bool IsGrounded => isGrounded;
    public bool IsCharging => isCharging;
    public bool IsBraced => isBraced;
    public bool IsDodging => isDodging;
    public bool AttackToTheRight => attackToTheRight; // Getter for attack direction
    public bool IsBeingAttacked => currentAttacker != null; // Getter to check if being attacked
    public GoatController CurrentAttacker => currentAttacker; // Getter for current attacker
    public Transform Opponent => opponent; // Getter for opponent transform

    private bool isJumping = false;
    private float jumpStartTime = -1f;

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
        pushStartTime = -1f;
        pushDirection = 0f;
        isCollidingWithPlatform = false;
        isGrounded = false;
        
        // Reset all state flags
        jumpRequested = false;
        jumpUsedThisGround = false;
        jumpCooldownTimer = 0f;
        isJumping = false;
        jumpStartTime = -1f;
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
        goatCollider = GetComponent<Collider>();
        originalMass = rb.mass;
        
        // Disable built-in gravity so we can use custom gravity scale
        rb.useGravity = false;

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

        // Ground state is now determined by collision detection (see OnCollisionEnter/Stay/Exit)
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
        
        // Ground state is now determined by collision detection (see OnCollisionEnter/Stay/Exit)
        // Reset jump lock when grounded
        if (isGrounded) jumpUsedThisGround = false;
        
        // Apply horizontal movement (X-axis only) - do this first
        // If we're being attacked, we'll add the attack force after
        if (currentAttacker == null)
        {
            // Normal movement: set velocity directly
            rb.linearVelocity = new Vector3(moveDirection.x * moveSpeed, rb.linearVelocity.y, 0);
        }
        
        // Apply attack push force if we're being attacked OR if we're still in push momentum phase
        // This allows momentum to continue even after collision ends
        if (currentAttacker != null || pushStartTime >= 0)
        {
            Debug.Log($"[GoatController] Linear Velocity Before ApplyAttackPush: {rb.linearVelocity}");
            ApplyAttackPush();
            Debug.Log($"[GoatController] Linear Velocity After ApplyAttackPush: {rb.linearVelocity}");
        }
        
        // Apply jump acceleration if currently jumping
        if (isJumping)
        {
            float jumpElapsed = Time.time - jumpStartTime;
            if (jumpElapsed < jumpDuration)
            {
                // Apply acceleration-based force over time for smoother jump
                // ForceMode.Acceleration ignores mass and applies acceleration directly
                rb.AddForce(Vector3.up * jumpAcceleration, ForceMode.Acceleration);
            }
            else
            {
                // Jump duration complete
                isJumping = false;
                jumpStartTime = -1f;
            }
        }
        
        // Apply custom jump/fall acceleration for smoother, more responsive jumping
        // This makes falling faster and allows variable jump height
        ApplyJumpFallAcceleration();
        
        // Check for jump BEFORE clamping Y velocity to ensure jump force isn't interfered with
        bool willJump = jumpRequested && isGrounded && !isCharging && !jumpUsedThisGround && 
                        jumpCooldownTimer <= 0f && currentStamina >= jumpStaminaCost;
        
        // Handle grounding for all cases
        // Clamp Y velocity to 0 when grounded to prevent floating
        // Skip clamping if we're about to jump (jump force will override it anyway)
        // This must be done AFTER setting velocity so gravity can work when not grounded
        if (isGrounded && !willJump)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        TryProcessJump();
        if (currentAttacker != null)
            Debug.Log($"[GoatController] Linear Velocity After FixedUpdate: {rb.linearVelocity}");

        // Debug.Log($"[GoatController] Linear Velocity After FixedUpdate: {rb.linearVelocity}, isGrounded: {isGrounded}");

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
    
    private void ApplyJumpFallAcceleration()
    {
        // Apply custom gravity scale for floatier feel
        float customGravity = Physics.gravity.y * gravityScale;
        
        // Always apply base gravity (scaled)
        rb.linearVelocity += Vector3.up * customGravity * Time.fixedDeltaTime;
        
        if (rb.linearVelocity.y < 0)
        {
            // Falling down - apply additional stronger gravity for snappier landing
            rb.linearVelocity += Vector3.up * customGravity * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpRequested)
        {
            // Moving up but jump button not held - apply additional moderate gravity for shorter jumps
            rb.linearVelocity += (lowJumpMultiplier - 1) * customGravity * Time.fixedDeltaTime * Vector3.up;
        }
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

        // Start acceleration-based jump (applied over multiple frames for smoother feel)
        isJumping = true;
        jumpStartTime = Time.time;

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
    private void OnCollisionEnter(Collision collision)
    {
        // Check if we're colliding with the platform
        if (platform != null && collision.gameObject.transform == platform)
        {
            isCollidingWithPlatform = true;
            isGrounded = true;
        }
        
        // Check if we're colliding with another goat (opponent)
        // More robust: check for GoatController component instead of relying on opponent reference
        GoatController otherGoat = collision.gameObject.GetComponent<GoatController>();
        if (otherGoat != null && otherGoat != this)
        {
            // If we're currently attacking (charging), notify the opponent
            if (isCharging)
            {
                otherGoat.BeingAttacked(this);
            }
        }
    }
    
    private void OnCollisionStay(Collision collision)
    {
        // Check if we're colliding with the platform
        if (platform != null && collision.gameObject.transform == platform)
        {
            isCollidingWithPlatform = true;
            isGrounded = true;
        }
        
        // Check if we're colliding with another goat (opponent)
        // More robust: check for GoatController component instead of relying on opponent reference
        GoatController otherGoat = collision.gameObject.GetComponent<GoatController>();
        if (otherGoat != null && otherGoat != this)
        {
            // If we're currently attacking (charging), notify the opponent
            if (isCharging)
            {
                otherGoat.BeingAttacked(this);
            }
        }
    }
    
    /// <summary>
    /// Called when collision ends
    /// Clear the attacker reference and update ground state
    /// </summary>
    private void OnCollisionExit(Collision collision)
    {
        // Check if we stopped colliding with the platform
        if (platform != null && collision.gameObject.transform == platform)
        {
            isCollidingWithPlatform = false;
            isGrounded = false;
        }
        
        // Check if we stopped colliding with another goat (opponent)
        GoatController otherGoat = collision.gameObject.GetComponent<GoatController>();
        if (otherGoat != null && otherGoat != this)
        {
            // Clear attacker if it was this opponent
            if (currentAttacker != null && currentAttacker == otherGoat)
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
    /// Creates momentum-based push with initial boost and sustained force
    /// </summary>
    private void ApplyAttackPush()
    {
        // If no current attacker but we have active push momentum, continue it
        if (currentAttacker == null)
        {
            if (pushStartTime >= 0 && Time.time - pushStartTime < pushDuration)
            {
                // Continue applying momentum even though collision ended
                ApplyPushMomentum();
            }
            else if (pushStartTime >= 0)
            {
                // Push duration expired, clear it
                pushStartTime = -1f;
            }
            return;
        }

        // Verify attacker is still charging
        if (!currentAttacker.IsCharging)
        {
            // Attacker stopped charging, but continue push momentum if it just started
            if (pushStartTime >= 0 && Time.time - pushStartTime < pushDuration)
            {
                // Continue applying momentum even though collision ended
                ApplyPushMomentum();
            }
            else
            {
                currentAttacker = null;
                pushStartTime = -1f;
            }
            return;
        }

        // Calculate push direction based on attacker's attack direction
        float attackerAttackDirection = currentAttacker.AttackToTheRight ? 1f : -1f;
        
        // Check if this is a new push (first frame of collision)
        if (pushStartTime < 0 || pushDirection != attackerAttackDirection)
        {
            // New push - apply initial velocity boost for bounce effect
            pushStartTime = Time.time;
            pushDirection = attackerAttackDirection;
            
            float effectiveInitialVelocity = pushInitialVelocity;
            if (isBraced)
            {
                effectiveInitialVelocity *= 0.3f; // Reduce initial boost when bracing
            }
            
            // Apply initial velocity boost
            float currentXVelocity = rb.linearVelocity.x;
            float newXVelocity = currentXVelocity + (pushDirection * effectiveInitialVelocity);
            
            // Clamp to max velocity
            float effectiveMaxVelocity = isBraced ? maxPushVelocity * 0.5f : maxPushVelocity;
            if (Mathf.Abs(newXVelocity) > effectiveMaxVelocity)
            {
                newXVelocity = Mathf.Sign(newXVelocity) * effectiveMaxVelocity;
            }
            
            rb.linearVelocity = new Vector3(newXVelocity, rb.linearVelocity.y, rb.linearVelocity.z);
            Debug.Log($"Initial push boost: {effectiveInitialVelocity}, new velocity: {newXVelocity:F2}");
        }
        else
        {
            // Continue applying sustained push force
            ApplyPushMomentum();
        }
    }
    
    /// <summary>
    /// Applies continuous push momentum (called during push and after collision ends)
    /// </summary>
    private void ApplyPushMomentum()
    {
        if (pushStartTime < 0) return;
        
        // Check if push duration has expired
        if (Time.time - pushStartTime >= pushDuration)
        {
            pushStartTime = -1f;
            return;
        }
        
        // Calculate effective push acceleration
        float effectiveAcceleration = pushAcceleration;
        if (isBraced)
        {
            effectiveAcceleration *= 0.3f; // Reduce push acceleration by 70% when bracing
        }
        
        // Get current velocity
        float currentXVelocity = rb.linearVelocity.x;
        float effectiveMaxVelocity = isBraced ? maxPushVelocity * 0.5f : maxPushVelocity;
        
        // Calculate target velocity (in push direction, up to max)
        float targetVelocity = pushDirection * effectiveMaxVelocity;
        
        // Gradually accelerate toward target velocity
        float velocityChange = effectiveAcceleration * Time.fixedDeltaTime;
        float newXVelocity;
        
        if (Mathf.Sign(currentXVelocity) == Mathf.Sign(targetVelocity))
        {
            // Already moving in push direction, accelerate toward max
            newXVelocity = Mathf.MoveTowards(currentXVelocity, targetVelocity, velocityChange);
        }
        else
        {
            // Moving opposite direction, accelerate in push direction
            newXVelocity = currentXVelocity + (pushDirection * velocityChange);
            // Clamp to max velocity
            if (Mathf.Abs(newXVelocity) > effectiveMaxVelocity)
            {
                newXVelocity = Mathf.Sign(newXVelocity) * effectiveMaxVelocity;
            }
        }
        
        // Apply the new velocity
        rb.linearVelocity = new Vector3(newXVelocity, rb.linearVelocity.y, rb.linearVelocity.z);
    }
}