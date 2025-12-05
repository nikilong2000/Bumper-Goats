using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class AiGoatScript : Agent
{
    [Header("References")]
    [SerializeField] private Transform opponentTransform;
    [SerializeField] private Transform platformTransform;

    [Header("Environment Settings")]
    [SerializeField] private float platformRadius = 5f;

    private Rigidbody rb;
    private Rigidbody opponentRb;

    private GoatController goatController;
    private GoatController opponentController;

    private Vector3 previousOpponentPosition = Vector3.zero;

    // Agent field
    // private bool _aiBracing = false;

    // Action tracking for rewards
    private bool attackExecuted = false;
    private float attackStartTime = -1f;
    private float attackTimeout = 1.5f; // Time window to check if attack hit

    private bool dodgeExecuted = false;
    private float dodgeStartTime = -1f;
    private float dodgeTimeout = 0.5f; // Time window to check if dodge avoided attack
    private bool opponentWasAttackingWhenDodged = false;

    private bool jumpExecuted = false;
    private float jumpStartTime = -1f;
    private float jumpTimeout = 0.5f; // Time window to check if jump avoided attack
    private bool opponentWasAttackingWhenJumped = false;

    private bool braceExecuted = false;
    private float braceStartTime = -1f;
    private float braceTimeout = 1.0f; // Time window to check if brace avoided attack
    private bool opponentWasAttackingWhenBraced = false;

    private bool wasHitThisFrame = false;
    private bool wasHitLastFrame = false;

    [Header("Episode Settings")]
    [SerializeField] private float maxEpisodeTime = 60f; // Maximum episode time in seconds

    private float episodeStartTime;

    /// <summary>
    /// Called once when the agent is first initialized
    /// </summary>
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        goatController = GetComponent<GoatController>();
        goatController.SetOriginalPositionAndRotation();
        if (opponentTransform != null)
        {
            opponentRb = opponentTransform.GetComponent<Rigidbody>();
            opponentController = opponentTransform.GetComponent<GoatController>();
            opponentController.SetOriginalPositionAndRotation();
        }

    }

    /// <summary>
    /// Called at the start of each training episode
    /// Reset the environment to a clean state
    /// </summary>
    public override void OnEpisodeBegin()
    {
        episodeStartTime = Time.time; // Track when episode started
        // Reset AI goat position and physics
        goatController.Reset();
        if (opponentTransform != null)
        {
            // Reset opponent goat position and physics only if it's a player
            opponentController.Reset();
            previousOpponentPosition = opponentTransform.position;
        }
        else
        {
            previousOpponentPosition = Vector3.zero;
        }

        // Reset action tracking
        attackExecuted = false;
        attackStartTime = -1f;
        dodgeExecuted = false;
        dodgeStartTime = -1f;
        opponentWasAttackingWhenDodged = false;
        jumpExecuted = false;
        jumpStartTime = -1f;
        opponentWasAttackingWhenJumped = false;
        braceExecuted = false;
        braceStartTime = -1f;
        opponentWasAttackingWhenBraced = false;
        wasHitThisFrame = false;
        wasHitLastFrame = false;
    }

    /// <summary>
    /// Collect observations - this is what the AI "sees"
    /// Think of this as the AI's sensory input
    /// Total observations: 25 values
    /// - Self-awareness: 14 (position, velocity, distance, forward, states, stamina)
    /// - Opponent awareness: 11 (direction, velocity, states, distance)
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        // --- AI's Self-Awareness (14 observations) ---

        // 1. AI's position relative to platform center (3 values: x, y, z)
        // This helps the AI know where it is on the platform
        Vector3 relativePosition = transform.position - platformTransform.position;
        sensor.AddObservation(relativePosition);

        // 2. AI's linearVelocity (3 values: x, y, z)
        // This helps the AI understand its current momentum
        sensor.AddObservation(rb.linearVelocity);

        // 3. AI's distance from platform edge (1 value)
        // Critical for self-preservation
        float distanceFromCenter = Vector3.Distance(transform.position, platformTransform.position);
        float distanceToEdge = GetPlatformRadius() - distanceFromCenter;
        sensor.AddObservation(distanceToEdge / platformRadius); // Normalized 0-1

        // 4. AI's forward direction (2 values: x, z on ground plane)
        // Helps AI understand which way it's facing
        Vector3 forward = transform.forward;
        sensor.AddObservation(new Vector2(forward.x, forward.z));

        // 5. AI's state information (3 values: isGrounded, isCharging, isBraced, isDodging)
        // Helps AI understand its current state
        sensor.AddObservation(goatController.IsGrounded ? 1f : 0f);
        sensor.AddObservation(goatController.IsCharging ? 1f : 0f);
        sensor.AddObservation(goatController.IsBraced ? 1f : 0f);
        sensor.AddObservation(goatController.IsDodging ? 1f : 0f);

        // stamina observation
        sensor.AddObservation(goatController.currentStamina / goatController.maxStamina);

        // --- Opponent Awareness (6 observations) ---

        if (opponentTransform != null && opponentController != null)
        {
            // 6. Vector from AI to Player (3 values: x, y, z)
            // This is the most important observation for pushing
            Vector3 directionToOpponent = opponentTransform.position - transform.position;
            sensor.AddObservation(directionToOpponent);

            // 7. Opponent's linearVelocity (3 values: x, y, z)
            // Helps AI predict where player is moving
            if (opponentRb != null)
            {
                sensor.AddObservation(opponentRb.linearVelocity);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
            }

            // 8. Opponent's state information (4 values: isGrounded, isCharging, isBraced, isDodging)
            // Helps AI understand the opponent's current state
            sensor.AddObservation(opponentController.IsGrounded ? 1f : 0f);
            sensor.AddObservation(opponentController.IsCharging ? 1f : 0f);
            sensor.AddObservation(opponentController.IsBraced ? 1f : 0f);
            sensor.AddObservation(opponentController.IsDodging ? 1f : 0f);

            // 9. Opponent's distance from platform edge (1 value)
            // Critical for self-preservation
            float oppDistanceFromCenter = Vector3.Distance(opponentTransform.position, platformTransform.position);
            float oppDistanceToEdge = GetPlatformRadius() - oppDistanceFromCenter;
            sensor.AddObservation(oppDistanceToEdge / platformRadius);
        }
        else // If player reference is missing, observe zeros
        {
            sensor.AddObservation(Vector3.zero); // Direction to player
            sensor.AddObservation(Vector3.zero); // Player linearVelocity
            for (int i = 0; i < 5; i++)
            {
                sensor.AddObservation(0f); // Opponent state information
            }
        }

    }

    private float GetPlatformRadius()
    {
        if (ArenaShrinking.Instance != null)
        {
            return ArenaShrinking.Instance.PlatformRadius;
        }

        // Fallback if ArenaShrinking not found (shouldn't happen in normal gameplay)
        Debug.LogWarning("ArenaShrinking.Instance not found, using fallback radius");
        return platformRadius; // Use serialized fallback value
    }

    /// <summary>
    /// Execute actions - this is what the AI "does"
    /// Called every FixedUpdate during training
    /// </summary>
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Check if episode has exceeded maximum time
        float elapsedTime = Time.time - episodeStartTime;
        if (elapsedTime >= maxEpisodeTime)
        {
            // End episode due to timeout - give neutral/small negative reward
            AddReward(-0.1f);
            EndEpisode();
            return;
        }

        // --- Continuous Actions: Movement (2 actions) ---
        // Range: -1 to +1 for each axis
        float moveX = actions.ContinuousActions[0];

        // Use GoatController for movement
        Vector2 moveDirection = new Vector2(moveX, 0f);
        goatController.Move(moveDirection);

        // --- Discrete Actions: Combat Actions (4 actions) ---
        // 0: No action, 1: Attack, 2: Dodge, 3: Jump, 4: Brace
        int actionType = actions.DiscreteActions[0];
        string actionName = "";
        switch (actionType)
        {
            case 1:
                goatController.Attack();
                actionName = "Attack";
                // Track attack execution
                if (!attackExecuted || !goatController.IsCharging)
                {
                    attackExecuted = true;
                    attackStartTime = Time.time;
                }
                break;
            case 2:
                goatController.Dodge(moveDirection);
                actionName = "Dodge";
                // Track dodge execution
                if (!dodgeExecuted || !goatController.IsDodging)
                {
                    dodgeExecuted = true;
                    dodgeStartTime = Time.time;
                    // Record if opponent was attacking when we dodged
                    opponentWasAttackingWhenDodged = (opponentController != null && opponentController.IsCharging);
                }
                break;
            case 3:
                goatController.Jump();
                actionName = "Jump";
                // Track jump execution
                if (!jumpExecuted)
                {
                    jumpExecuted = true;
                    jumpStartTime = Time.time;
                    // Record if opponent was attacking when we jumped
                    opponentWasAttackingWhenJumped = (opponentController != null && opponentController.IsCharging);
                }
                break;
            case 4:
                goatController.Brace(true);
                actionName = "Brace";
                // Track brace execution
                if (!braceExecuted || !goatController.IsBraced)
                {
                    braceExecuted = true;
                    braceStartTime = Time.time;
                    // Record if opponent was attacking when we braced
                    opponentWasAttackingWhenBraced = (opponentController != null && opponentController.IsCharging);
                }
                break;
            case 0: actionName = "No action"; break;
            default: actionName = "No action"; break;
        }
        // Debug.Log("stamina: " + goatController.currentStamina + " grounded: " + goatController.IsGrounded);

        if (actionType != 4 && goatController.IsBraced) goatController.Brace(false);
        else if (actionType == 4 && !goatController.IsBraced) goatController.Brace(true);

        // --- Small Penalty for Existing (Time Cost) ---
        // This encourages the AI to finish episodes quickly
        AddReward(-0.001f);

        // --- Penalty for Being Near Edge (Self-Preservation) ---
        float distanceFromCenter = Vector3.Distance(transform.position, platformTransform.position);
        float distanceToEdge = GetPlatformRadius() - distanceFromCenter;
        float normalizedDistanceToEdge = distanceToEdge / GetPlatformRadius(); // 0 = at edge, 1 = at center

        // Reward staying away from edge (smooth gradient), but only penalize near edge, don't reward center
        if (normalizedDistanceToEdge < 0.2f)
        {
            AddReward(-0.01f * (0.2f - normalizedDistanceToEdge));
        }

        // --- Engagement reward (encourages moving toward opponent) ---
        if (opponentTransform != null)
        {
            float distanceToOpponent = Vector3.Distance(transform.position, opponentTransform.position);

            // Reward being close to opponent (encourages engagement)
            float proximityReward = 0.02f / (1.0f + distanceToOpponent);
            AddReward(proximityReward);


            // Reward if opponent has been moved away and towards the edge
            Vector3 opponentMovement = opponentTransform.position - previousOpponentPosition;
            float opponentMoveDistance = opponentMovement.magnitude;
            if (opponentMoveDistance > 0.01f) // Opponent actually moved
            {
                // Get the direction the goat is facing (on ground plane)
                Vector3 facingDirection = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

                // Get the direction opponent moved (on ground plane)
                Vector3 opponentMoveDirection = new Vector3(opponentMovement.x, 0f, opponentMovement.z).normalized;

                // Calculate how well the push aligns with facing direction
                float alignment = Vector3.Dot(facingDirection, opponentMoveDirection);

                // Reward pushing in the direction we're facing (0 = perpendicular, 1 = perfect alignment)
                if (alignment > 0.3f) // Only reward if push is somewhat aligned with facing direction
                {
                    float pushReward = 0.15f * alignment * opponentMoveDistance;
                    AddReward(pushReward);

                    // BONUS: Extra reward if push also moves opponent toward edge
                    float opponentDistFromCenter = Vector3.Distance(opponentTransform.position, platformTransform.position);
                    float previousOpponentDistFromCenter = Vector3.Distance(previousOpponentPosition, platformTransform.position);

                    if (opponentDistFromCenter > previousOpponentDistFromCenter) // Moved away from center
                    {
                        float edgeBonus = (opponentDistFromCenter - previousOpponentDistFromCenter) / GetPlatformRadius();
                        AddReward(0.2f * edgeBonus * alignment); // Bonus scaled by alignment
                    }
                }
            }

            previousOpponentPosition = opponentTransform.position;
        }

        // --- Check if AI was hit this frame ---
        wasHitThisFrame = (goatController.IsBeingAttacked && opponentController != null &&
                          goatController.CurrentAttacker == opponentController);

        // Penalize AI for being hit
        if (wasHitThisFrame && !wasHitLastFrame)
        {
            AddReward(-0.5f); // Penalty for getting hit
        }
        wasHitLastFrame = wasHitThisFrame;

        // --- Reward/Penalize Attack Action ---
        if (attackExecuted && attackStartTime >= 0)
        {
            float timeSinceAttack = Time.time - attackStartTime;

            // Check if attack hit (opponent is being attacked by us)
            bool attackHit = (opponentController != null && opponentController.IsBeingAttacked &&
                             opponentController.CurrentAttacker == goatController);

            if (attackHit)
            {
                // Reward successful hit
                AddReward(0.3f);
                attackExecuted = false; // Reset after successful hit
                attackStartTime = -1f;
            }
            else if (timeSinceAttack > attackTimeout)
            {
                // Attack timed out without hitting - penalize
                AddReward(-0.01f);
                attackExecuted = false;
                attackStartTime = -1f;
            }
            // If still charging, keep tracking
            else if (!goatController.IsCharging)
            {
                // Attack ended without hitting
                AddReward(-0.01f);
                attackExecuted = false;
                attackStartTime = -1f;
            }
        }

        // --- Reward/Penalize Dodge Action ---
        if (dodgeExecuted && dodgeStartTime >= 0)
        {
            float timeSinceDodge = Time.time - dodgeStartTime;

            if (timeSinceDodge <= dodgeTimeout)
            {
                // Check if dodge successfully avoided attack
                if (opponentWasAttackingWhenDodged && !wasHitThisFrame)
                {
                    // Successfully dodged an attack - reward
                    AddReward(0.2f);
                    dodgeExecuted = false;
                    dodgeStartTime = -1f;
                    opponentWasAttackingWhenDodged = false;
                }
                else if (opponentWasAttackingWhenDodged && wasHitThisFrame)
                {
                    // Tried to dodge but still got hit - small penalty
                    AddReward(-0.05f);
                    dodgeExecuted = false;
                    dodgeStartTime = -1f;
                    opponentWasAttackingWhenDodged = false;
                }
            }
            else
            {
                // Dodge timeout - evaluate result
                if (opponentWasAttackingWhenDodged)
                {
                    // Dodged when opponent was attacking - check if we avoided it
                    if (!wasHitThisFrame)
                    {
                        // Successfully avoided - reward
                        AddReward(0.15f);
                    }
                    else
                    {
                        // Still got hit - small penalty
                        AddReward(-0.05f);
                    }
                }
                else
                {
                    // Dodged when opponent wasn't attacking - small penalty for unnecessary action
                    AddReward(-0.002f);
                }
                dodgeExecuted = false;
                dodgeStartTime = -1f;
                opponentWasAttackingWhenDodged = false;
            }
        }

        // --- Reward/Penalize Jump Action ---
        if (jumpExecuted && jumpStartTime >= 0)
        {
            float timeSinceJump = Time.time - jumpStartTime;

            if (timeSinceJump <= jumpTimeout)
            {
                // Check if jump successfully avoided attack
                if (opponentWasAttackingWhenJumped && !wasHitThisFrame)
                {
                    // Successfully jumped to avoid attack - reward
                    AddReward(0.2f);
                    jumpExecuted = false;
                    jumpStartTime = -1f;
                    opponentWasAttackingWhenJumped = false;
                }
                else if (opponentWasAttackingWhenJumped && wasHitThisFrame)
                {
                    // Tried to jump but still got hit - small penalty
                    AddReward(-0.05f);
                    jumpExecuted = false;
                    jumpStartTime = -1f;
                    opponentWasAttackingWhenJumped = false;
                }
            }
            else
            {
                // Jump timeout - evaluate result
                if (opponentWasAttackingWhenJumped)
                {
                    // Jumped when opponent was attacking - check if we avoided it
                    if (!wasHitThisFrame)
                    {
                        // Successfully avoided - reward
                        AddReward(0.15f);
                    }
                    else
                    {
                        // Still got hit - small penalty
                        AddReward(-0.05f);
                    }
                }
                else
                {
                    // Jumped when opponent wasn't attacking - small penalty for unnecessary action
                    AddReward(-0.02f);
                }
                jumpExecuted = false;
                jumpStartTime = -1f;
                opponentWasAttackingWhenJumped = false;
            }
        }

        // --- Reward/Penalize Brace Action ---
        if (braceExecuted && braceStartTime >= 0)
        {
            float timeSinceBrace = Time.time - braceStartTime;
            bool opponentCurrentlyAttacking = (opponentController != null && opponentController.IsCharging);

            if (goatController.IsBraced)
            {
                // Still bracing - check if it's helping avoid damage
                if (opponentWasAttackingWhenBraced && wasHitThisFrame)
                {
                    // Being hit while bracing - check if brace reduced damage
                    // If we're still on platform and not being pushed much, brace helped
                    float braceDistanceFromCenter = Vector3.Distance(transform.position, platformTransform.position);
                    if (braceDistanceFromCenter < GetPlatformRadius() * 0.8f)
                    {
                        // Brace helped reduce push - small reward
                        AddReward(0.1f);
                    }
                }
                else if (opponentWasAttackingWhenBraced && !wasHitThisFrame)
                {
                    // Bracing and avoiding attack - reward
                    AddReward(0.15f);
                }
            }
            else
            {
                // Stopped bracing - evaluate if it was successful
                if (timeSinceBrace <= braceTimeout)
                {
                    if (opponentWasAttackingWhenBraced && !wasHitThisFrame)
                    {
                        // Successfully braced to avoid attack - reward
                        AddReward(0.2f);
                    }
                    else if (opponentWasAttackingWhenBraced && wasHitThisFrame)
                    {
                        // Braced but still got hit - small penalty
                        AddReward(-0.05f);
                    }
                    else if (!opponentWasAttackingWhenBraced)
                    {
                        // Braced when opponent wasn't attacking - small penalty for unnecessary action
                        AddReward(-0.02f);
                    }
                }
                braceExecuted = false;
                braceStartTime = -1f;
                opponentWasAttackingWhenBraced = false;
            }
        }

        Debug.Log("Movement: " + moveDirection + " Action: " + actionName + " Total reward: " + GetCumulativeReward());
    }

    /// <summary>
    /// Heuristic mode - allows you to manually control the AI for testing
    /// Useful for debugging and creating demonstration recordings
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

        // Use A/D or Left/Right arrow keys to control movement (same as player)
        continuousActions[0] = Input.GetAxisRaw("Horizontal");

        // Use same keys as player controls for combat actions
        if (Input.GetKey(KeyCode.Space)) discreteActions[0] = 1; // Attack (Space)
        else if (Input.GetKey(KeyCode.Q)) discreteActions[0] = 2; // Dodge (Q)
        else if (Input.GetKey(KeyCode.W)) discreteActions[0] = 3; // Jump (W)
        else if (Input.GetKey(KeyCode.S)) discreteActions[0] = 4; // Brace (S)
        else discreteActions[0] = 0; // No action
    }

    /// <summary>
    /// Called when AI falls off the platform
    /// This should be called by your FallZoneDetector
    /// </summary>
    public void OnAIFellOff()
    {
        SetReward(-10.0f); // Large negative reward for losing
        EndEpisode();
    }

    /// <summary>
    /// Called when player falls off the platform
    /// This should be called by your FallZoneDetector
    /// </summary>
    public void OnOpponentFellOff()
    {
        SetReward(+10.0f); // Large positive reward for winning
        EndEpisode();
    }
}