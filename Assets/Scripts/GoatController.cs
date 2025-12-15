
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
    [SerializeField] private float chargeDuration = 0.7f;
    [SerializeField] private float pushInitialVelocity = 15f;
    [SerializeField] private float pushDeceleration = 20f;
    [SerializeField] private float maxPushVelocity = 30f;
    [SerializeField] private float pushDuration = 0.5f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpAcceleration = 200f;
    [SerializeField] private float jumpDuration = 0.2f;
    [SerializeField] private float gravityScale = 0.5f;
    [SerializeField] private float fallMultiplier = 10f;
    [SerializeField] private float lowJumpMultiplier = 2f;
    [SerializeField] private Transform platform;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask goatLayer;

    [Header("Brace Settings")]
    [SerializeField] private float braceMassMultiplier = 3f;

    [Header("Dodge Settings")]
    [SerializeField] private float dodgeDistance = 5.6f;
    [SerializeField] private float dodgeDuration = 0.3f;
    [SerializeField] private float dodgeReturnSpeed = 5f;

    [Header("Directional Settings")]
    [SerializeField] private Transform opponent;
    [SerializeField] private Transform goatModel;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private float attackSoundStartTime = 0f;
    [SerializeField] private float attackSoundVolume = 1f;
    [SerializeField] private AudioClip braceSound;
    [SerializeField] private float braceSoundStartTime = 0f;
    [SerializeField] private float braceSoundVolume = 1f;
    [SerializeField] private AudioClip dodgeSound;
    [SerializeField] private float dodgeSoundStartTime = 0f;
    [SerializeField] private float dodgeSoundVolume = 1f;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private float jumpSoundStartTime = 0f;
    [SerializeField] private float jumpSoundVolume = 1f;

    [Header("Stamina Settings")]
    public Image staminaBar;
    public float currentStamina;
    public float maxStamina = 100f;
    public float staminaRegenRate;
    private float dodgeStaminaCost = 10f;
    private float chargeStaminaCost = 20f;
    private float jumpStaminaCost = 5f;
    private float braceInitialCost = 15f;
    private float braceDrainRate = 5f;

    [Header("Lives Settings")]
    [SerializeField] private int maxLives = 3;
    private int currentLives;

    private Coroutine staminaRegenCoroutine;
    private Coroutine braceDrainCoroutine;
    private Coroutine chargeAttackCoroutine;
    private Coroutine dodgeAnimationCoroutine;
    private float staminaRechargeDelay = 2f;
    private float staminaRechargeRate = 15f;

    // Internal state.
    private Rigidbody rb;
    private Collider goatCollider;
    private float originalMass;
    private bool isCharging = false;
    private bool isGrounded = false;
    private bool isBraced = false;
    private bool isDodging = false;
    private GoatController currentAttacker = null;
    private float pushStartTime = -1f;
    private float pushDirection = 0f;

    // Getters for AI observations.
    public bool IsGrounded => isGrounded;
    public bool IsCharging => isCharging;
    public bool IsBraced => isBraced;
    public bool IsDodging => isDodging;
    public bool IsHit => pushStartTime >= 0;
    public bool AttackToTheRight => attackToTheRight;
    public bool IsBeingAttacked => currentAttacker != null;
    public GoatController CurrentAttacker => currentAttacker;
    public Transform Opponent => opponent;
    public int CurrentLives => currentLives;
    public int MaxLives => maxLives;

    private bool isJumping = false;
    private float jumpStartTime = -1f;

    private Vector2 moveDirection;

    // Stores rotations.
    private Quaternion facingRight;
    private Quaternion facingLeft;
    private bool attackToTheRight = true;

    // Jump queue.
    private bool jumpRequested = false;
    private bool jumpUsedThisGround = false;

    [SerializeField] private float jumpCooldown = 0.05f;
    private float jumpCooldownTimer = 0f;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    // Prevents updates during reset.
    private bool isResetting = false;

    // Sets the original position and rotation.
    public void SetOriginalPositionAndRotation()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    // Resets the goat controller.
    public void Reset()
    {
        Debug.Log("Resetting GoatController on object with tag: " + gameObject.tag);
        isResetting = true;

        // Stops all coroutines.
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

        // Resets state flags.
        isCharging = false;
        isDodging = false;
        isBraced = false;
        currentAttacker = null;
        pushStartTime = -1f;
        pushDirection = 0f;
        isGrounded = false;

        jumpRequested = false;
        jumpUsedThisGround = false;
        jumpCooldownTimer = 0f;
        isJumping = false;
        jumpStartTime = -1f;
        moveDirection = Vector2.zero;

        // Resets physics.
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Resets transform.
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        // Resets stamina.
        ResetStamina();

        isResetting = false;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        goatCollider = GetComponent<Collider>();
        originalMass = rb.mass;

        // Ensures audio source exists.
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Disables gravity.
        rb.useGravity = false;

        facingRight = Quaternion.Euler(0, 90, 0);
        facingLeft = Quaternion.Euler(0, -90, 0);

        // Initialises lives.
        InitializeLives();
    }

    // Plays an action sound.
    private void PlayActionSound(AudioClip clip, float startTime = 0f, float clipVolume = 1f)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.volume = clipVolume;
            audioSource.time = Mathf.Clamp(startTime, 0f, clip.length - 0.01f);
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (isResetting) return;

        // Updates facing direction.
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

        if (isGrounded) jumpUsedThisGround = false;

        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= Time.deltaTime;

        // Returns to z=9 when not dodging.
        if (!isDodging && (transform.position.z) > 9.01f || transform.position.z < 8.99f)
        {
            Vector3 pos = transform.position;
            pos.z = Mathf.Lerp(pos.z, 9f, Time.deltaTime * dodgeReturnSpeed);
            transform.position = pos;
        }
    }

    private void FixedUpdate()
    {
        if (isResetting) return;

        if (isGrounded) jumpUsedThisGround = false;

        // Applies movement.
        if (currentAttacker == null)
        {
            rb.linearVelocity = new Vector3(moveDirection.x * moveSpeed, rb.linearVelocity.y, 0);
        }

        // Applies push force.
        if (currentAttacker != null || pushStartTime >= 0)
        {
            ApplyAttackPush();
        }

        // Applies jump acceleration.
        if (isJumping)
        {
            float jumpElapsed = Time.time - jumpStartTime;
            if (jumpElapsed < jumpDuration)
            {
                rb.AddForce(Vector3.up * jumpAcceleration, ForceMode.Acceleration);
            }
            else
            {
                isJumping = false;
                jumpStartTime = -1f;
            }
        }

        // Applies custom gravity.
        ApplyJumpFallAcceleration();

        bool willJump = jumpRequested && isGrounded && !isCharging && !jumpUsedThisGround &&
                        jumpCooldownTimer <= 0f && currentStamina >= jumpStaminaCost;

        // Clamps Y velocity when grounded.
        if (isGrounded && !willJump)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        TryProcessJump();
    }

    // Moves the goat.
    public void Move(Vector2 direction)
    {
        moveDirection = direction;
    }

    // Attacks.
    public void Attack()
    {
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

    // Dodges.
    public void Dodge(Vector2 direction)
    {
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

    // Braces.
    public void Brace(bool shouldBrace)
    {
        if (shouldBrace == isBraced || !isGrounded) return;

        if (shouldBrace && currentStamina < braceInitialCost)
        {
            return;
        }

        isBraced = shouldBrace;

        if (shouldBrace)
        {
            PlayActionSound(braceSound, braceSoundStartTime, braceSoundVolume);

            currentStamina -= braceInitialCost;
            if (staminaBar != null)
                staminaBar.fillAmount = currentStamina / maxStamina;

            rb.mass = originalMass * braceMassMultiplier;
            rb.constraints |= RigidbodyConstraints.FreezePositionX;

            if (staminaRegenCoroutine != null)
                StopCoroutine(staminaRegenCoroutine);

            braceDrainCoroutine = StartCoroutine(DrainStaminaWhileBracing());
        }
        else
        {
            if (audioSource != null && audioSource.clip == braceSound)
            {
                audioSource.Stop();
            }

            rb.mass = originalMass;
            rb.constraints &= ~RigidbodyConstraints.FreezePositionX;

            if (braceDrainCoroutine != null)
                StopCoroutine(braceDrainCoroutine);

            if (staminaRegenCoroutine != null)
                StopCoroutine(staminaRegenCoroutine);

            staminaRegenCoroutine = StartCoroutine(RechargeStamina());
        }
    }

    // Jumps.
    public void Jump()
    {
        jumpRequested = true;
    }

    // Applies custom gravity.
    private void ApplyJumpFallAcceleration()
    {
        float customGravity = Physics.gravity.y * gravityScale;

        rb.linearVelocity += customGravity * Time.fixedDeltaTime * Vector3.up;

        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += (fallMultiplier - 1) * customGravity * Time.fixedDeltaTime * Vector3.up;
        }
        else if (rb.linearVelocity.y > 0 && !jumpRequested)
        {
            rb.linearVelocity += (lowJumpMultiplier - 1) * customGravity * Time.fixedDeltaTime * Vector3.up;
        }
    }

    // Processes jump request.
    private void TryProcessJump()
    {
        if (!jumpRequested) return;

        jumpRequested = false;

        if (!isGrounded || isCharging || jumpUsedThisGround) return;
        if (jumpCooldownTimer > 0f) return;

        if (currentStamina < jumpStaminaCost) return;

        PlayActionSound(jumpSound, jumpSoundStartTime, jumpSoundVolume);
        currentStamina -= jumpStaminaCost;
        if (staminaBar != null)
            staminaBar.fillAmount = currentStamina / maxStamina;

        isJumping = true;
        jumpStartTime = Time.time;

        jumpUsedThisGround = true;
        jumpCooldownTimer = jumpCooldown;

        if (staminaRegenCoroutine != null)
            StopCoroutine(staminaRegenCoroutine);

        staminaRegenCoroutine = StartCoroutine(RechargeStamina());
    }

    // Performs charge attack.
    private IEnumerator ChargeAttack()
    {
        isCharging = true;
        PlayActionSound(attackSound, attackSoundStartTime, attackSoundVolume);
        float attackDirection = attackToTheRight ? 1f : -1f;

        currentStamina -= chargeStaminaCost;
        if (staminaBar != null)
            staminaBar.fillAmount = currentStamina / maxStamina;

        rb.AddForce(attackDirection * chargeForce * transform.right, ForceMode.Impulse);
        yield return new WaitForSeconds(chargeDuration);

        rb.linearVelocity = new Vector3(rb.linearVelocity.x * 0.5f, rb.linearVelocity.y, rb.linearVelocity.z * 0.5f);

        isCharging = false;
        chargeAttackCoroutine = null;
    }

    // Performs dodge animation.
    private IEnumerator DodgeAnimation()
    {
        isDodging = true;
        PlayActionSound(dodgeSound, dodgeSoundStartTime, dodgeSoundVolume);

        currentStamina -= dodgeStaminaCost;
        if (staminaBar != null)
            staminaBar.fillAmount = currentStamina / maxStamina;

        float direction = UnityEngine.Random.value > 0.5f ? 1f : -1f;

        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + new Vector3(0, 0, dodgeDistance * direction);

        float elapsed = 0f;

        while (elapsed < dodgeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dodgeDuration;

            Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
            newPos.x = startPos.x;
            transform.position = newPos;

            yield return null;
        }

        isDodging = false;
        dodgeAnimationCoroutine = null;
    }

    // Recharges stamina.
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

    // Drains stamina while bracing.
    private IEnumerator DrainStaminaWhileBracing()
    {
        while (isBraced && currentStamina > 0)
        {
            currentStamina -= braceDrainRate / 10f;

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                Brace(false);
            }

            if (staminaBar != null)
                staminaBar.fillAmount = currentStamina / maxStamina;

            yield return new WaitForSeconds(0.1f);
        }
    }

    // Resets stamina.
    public void ResetStamina()
    {
        currentStamina = 100f;
        if (staminaBar != null)
            staminaBar.fillAmount = 1f;
    }

    // Initialises lives.
    public void InitializeLives()
    {
        currentLives = maxLives;
    }

    // Loses a life.
    public bool LoseLife()
    {
        if (currentLives > 0)
        {
            currentLives--;
            Debug.Log($"[GoatController] {gameObject.name} lost a life. Remaining: {currentLives}/{maxLives}");
            return currentLives <= 0;
        }
        return true;
    }

    // Resets lives.
    public void ResetLives()
    {
        currentLives = maxLives;
    }

    // Handles collision enter.
    private void OnCollisionEnter(Collision collision)
    {
        if (platform != null && collision.gameObject.transform == platform)
        {
            isGrounded = true;
        }

        GoatController otherGoat = collision.gameObject.GetComponent<GoatController>();
        if (otherGoat != null && otherGoat != this)
        {
            if (isCharging)
            {
                otherGoat.BeingAttacked(this);
            }
        }
    }

    // Handles collision stay.
    private void OnCollisionStay(Collision collision)
    {
        if (platform != null && collision.gameObject.transform == platform)
        {
            isGrounded = true;
        }

        GoatController otherGoat = collision.gameObject.GetComponent<GoatController>();
        if (otherGoat != null && otherGoat != this)
        {
            if (isCharging)
            {
                otherGoat.BeingAttacked(this);
            }
        }
    }

    // Handles collision exit.
    private void OnCollisionExit(Collision collision)
    {
        if (platform != null && collision.gameObject.transform == platform)
        {
            isGrounded = false;
        }

        GoatController otherGoat = collision.gameObject.GetComponent<GoatController>();
        if (otherGoat != null && otherGoat != this)
        {
            if (currentAttacker != null && currentAttacker == otherGoat)
            {
                currentAttacker = null;
            }
        }
    }

    // Called when being attacked.
    public void BeingAttacked(GoatController attacker)
    {
        if (attacker == null) return;

        if (attacker.IsCharging)
        {
            currentAttacker = attacker;
        }
        else
        {
            if (currentAttacker == attacker)
            {
                currentAttacker = null;
            }
        }
    }

    // Applies attack push.
    private void ApplyAttackPush()
    {
        if (currentAttacker == null)
        {
            if (pushStartTime >= 0 && Time.time - pushStartTime < pushDuration)
            {
                ApplyPushMomentum();
            }
            else if (pushStartTime >= 0)
            {
                pushStartTime = -1f;
            }
            return;
        }

        if (!currentAttacker.IsCharging)
        {
            if (pushStartTime >= 0 && Time.time - pushStartTime < pushDuration)
            {
                ApplyPushMomentum();
            }
            else
            {
                currentAttacker = null;
                pushStartTime = -1f;
            }
            return;
        }

        float attackerAttackDirection = currentAttacker.AttackToTheRight ? 1f : -1f;

        if (pushStartTime < 0 || pushDirection != attackerAttackDirection)
        {
            pushStartTime = Time.time;
            pushDirection = attackerAttackDirection;

            float effectiveInitialVelocity = pushInitialVelocity;
            if (isBraced)
            {
                effectiveInitialVelocity *= 0.3f;
            }

            float currentXVelocity = rb.linearVelocity.x;
            float newXVelocity = currentXVelocity + (pushDirection * effectiveInitialVelocity);

            float effectiveMaxVelocity = isBraced ? maxPushVelocity * 0.5f : maxPushVelocity;
            if (Mathf.Abs(newXVelocity) > effectiveMaxVelocity)
            {
                newXVelocity = Mathf.Sign(newXVelocity) * effectiveMaxVelocity;
            }

            rb.linearVelocity = new Vector3(newXVelocity, rb.linearVelocity.y, rb.linearVelocity.z);
        }
        else
        {
            ApplyPushMomentum();
        }
    }

    // Applies push momentum.
    private void ApplyPushMomentum()
    {
        if (pushStartTime < 0) return;

        if (Time.time - pushStartTime >= pushDuration)
        {
            pushStartTime = -1f;
            return;
        }

        float effectiveDeceleration = pushDeceleration;
        if (isBraced)
        {
            effectiveDeceleration *= 3f;
        }

        float currentXVelocity = rb.linearVelocity.x;

        float newXVelocity = Mathf.MoveTowards(currentXVelocity, 0f, effectiveDeceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newXVelocity, rb.linearVelocity.y, rb.linearVelocity.z);
    }
}