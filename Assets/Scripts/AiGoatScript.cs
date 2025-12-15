using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

// Requires a Rigidbody and GoatController to work properly.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(GoatController))]
public class AiGoatScript : Agent
{
    [Header("References")]
    [SerializeField] private Transform platformTransform;

    [Header("Environment Settings")]
    [SerializeField] private float platformRadius = 5f;

    private Rigidbody rb;
    private Rigidbody opponentRb;
    private GoatController goatController;
    private GoatController opponentController;
    private Vector3 previousOpponentPosition = Vector3.zero;

    // Tracks if an attack happened.
    private bool attackExecuted = false;
    private float attackStartTime = -1f;
    private float attackTimeout = 1.5f;

    // Tracks if a dodge happened.
    private bool dodgeExecuted = false;
    private float dodgeStartTime = -1f;
    private float dodgeTimeout = 0.5f;
    private bool opponentWasAttackingWhenDodged = false;

    // Tracks if a jump happened.
    private bool jumpExecuted = false;
    private float jumpStartTime = -1f;
    private float jumpTimeout = 0.5f;
    private bool opponentWasAttackingWhenJumped = false;

    // Tracks if a brace happened.
    private bool braceExecuted = false;
    private float braceStartTime = -1f;
    private float braceTimeout = 1.0f;
    private bool opponentWasAttackingWhenBraced = false;

    private bool wasHitThisFrame = false;
    private bool wasHitLastFrame = false;

    // Tracks stamina for penalties.
    private float previousStamina = 100f;

    [Header("Episode Settings")]
    [SerializeField] private float maxEpisodeTime = 60f;

    private float episodeStartTime;

    // Initialises the agent.
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        goatController = GetComponent<GoatController>();
        goatController.SetOriginalPositionAndRotation();
        Transform opponentTransform = goatController.Opponent;
        if (opponentTransform != null)
        {
            Debug.Log("Opponent found: " + opponentTransform.name);
            opponentRb = opponentTransform.GetComponent<Rigidbody>();
            opponentController = opponentTransform.GetComponent<GoatController>();
            opponentController.SetOriginalPositionAndRotation();
        }
    }

    // Resets the episode.
    public override void OnEpisodeBegin()
    {
        episodeStartTime = Time.time;

        // Resets the goat.
        goatController.Reset();
        Transform opponentTransform = goatController.Opponent;
        if (opponentTransform != null)
        {
            // Resets the opponent.
            opponentController.Reset();
            previousOpponentPosition = opponentTransform.position;
        }
        else
        {
            previousOpponentPosition = Vector3.zero;
        }

        // Resets all tracking variables.
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
        previousStamina = goatController.maxStamina;
    }

    // Collects observations for the AI.
    public override void CollectObservations(VectorSensor sensor)
    {
        // Observes position relative to the platform.
        Vector3 relativePosition = transform.position - platformTransform.position;
        sensor.AddObservation(relativePosition);

        // Observes velocity.
        sensor.AddObservation(rb.linearVelocity);

        // Observes distance to the edge.
        float distanceFromCenter = Vector3.Distance(transform.position, platformTransform.position);
        float distanceToEdge = GetPlatformRadius() - distanceFromCenter;
        sensor.AddObservation(distanceToEdge / platformRadius);

        // Observes forward direction.
        Vector3 forward = transform.forward;
        sensor.AddObservation(new Vector2(forward.x, forward.z));

        // Observes current state.
        sensor.AddObservation(goatController.IsGrounded ? 1f : 0f);
        sensor.AddObservation(goatController.IsCharging ? 1f : 0f);
        sensor.AddObservation(goatController.IsBraced ? 1f : 0f);
        sensor.AddObservation(goatController.IsDodging ? 1f : 0f);

        // Observes stamina.
        sensor.AddObservation(goatController.currentStamina / goatController.maxStamina);

        Transform opponentTransform = goatController.Opponent;
        if (opponentTransform != null && opponentController != null)
        {
            // Observes direction to the opponent.
            Vector3 directionToOpponent = opponentTransform.position - transform.position;
            sensor.AddObservation(directionToOpponent);

            // Observes opponent's velocity.
            if (opponentRb != null)
            {
                sensor.AddObservation(opponentRb.linearVelocity);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
            }

            // Observes opponent's state.
            sensor.AddObservation(opponentController.IsGrounded ? 1f : 0f);
            sensor.AddObservation(opponentController.IsCharging ? 1f : 0f);
            sensor.AddObservation(opponentController.IsBraced ? 1f : 0f);
            sensor.AddObservation(opponentController.IsDodging ? 1f : 0f);

            // Observes opponent's distance to the edge.
            float oppDistanceFromCenter = Vector3.Distance(opponentTransform.position, platformTransform.position);
            float oppDistanceToEdge = GetPlatformRadius() - oppDistanceFromCenter;
            sensor.AddObservation(oppDistanceToEdge / platformRadius);
        }
        else
        {
            // Observes zeros if no opponent.
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
            for (int i = 0; i < 5; i++)
            {
                sensor.AddObservation(0f);
            }
        }
    }

    // Gets the platform radius.
    private float GetPlatformRadius()
    {
        if (ArenaShrinking.Instance != null)
        {
            return ArenaShrinking.Instance.PlatformRadius;
        }

        Debug.LogWarning("ArenaShrinking.Instance not found, using fallback radius");
        return platformRadius;
    }

    // Performs actions based on AI decisions.
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Checks for timeout.
        float elapsedTime = Time.time - episodeStartTime;
        if (elapsedTime >= maxEpisodeTime)
        {
            AddValidReward(-0.1f);
            EndEpisode();
            return;
        }

        // Moves the goat.
        float moveX = actions.ContinuousActions[0];
        Vector2 moveDirection = new Vector2(moveX, 0f);
        goatController.Move(moveDirection);

        // Performs combat actions.
        int actionType = actions.DiscreteActions[0];
        float staminaBeforeAction = goatController.currentStamina;

        switch (actionType)
        {
            case 1: // Attack.
                if (goatController.IsDodging || goatController.IsBraced || !goatController.IsGrounded)
                {
                    AddValidReward(-0.05f);
                }
                else if (goatController.currentStamina >= 20f)
                {
                    goatController.Attack();
                    if (!attackExecuted || !goatController.IsCharging)
                    {
                        attackExecuted = true;
                        attackStartTime = Time.time;
                    }
                }
                else
                {
                    AddValidReward(-0.05f);
                }
                break;
            case 2: // Dodge.
                if (goatController.IsCharging || goatController.IsDodging || goatController.IsBraced || !goatController.IsGrounded)
                {
                    AddValidReward(-0.05f);
                }
                else if (goatController.currentStamina >= 10f && goatController.IsGrounded)
                {
                    goatController.Dodge(moveDirection);
                    if (!dodgeExecuted || !goatController.IsDodging)
                    {
                        dodgeExecuted = true;
                        dodgeStartTime = Time.time;
                        opponentWasAttackingWhenDodged = (opponentController != null && opponentController.IsCharging);
                    }
                }
                else
                {
                    AddValidReward(-0.03f);
                }
                break;
            case 3: // Jump.
                if (goatController.IsCharging || goatController.IsDodging || goatController.IsBraced || !goatController.IsGrounded)
                {
                    AddValidReward(-0.05f);
                }
                else if (goatController.currentStamina >= 5f)
                {
                    goatController.Jump();
                    if (!jumpExecuted)
                    {
                        jumpExecuted = true;
                        jumpStartTime = Time.time;
                        opponentWasAttackingWhenJumped = (opponentController != null && opponentController.IsCharging);
                    }
                }
                else
                {
                    AddValidReward(-0.02f);
                }
                break;
            case 4: // Brace.
                if (!goatController.IsBraced && (goatController.IsCharging || goatController.IsDodging || !goatController.IsGrounded))
                {
                    AddValidReward(-0.05f);
                }
                else if (goatController.currentStamina >= 15f)
                {
                    goatController.Brace(true);
                    if (!braceExecuted || !goatController.IsBraced)
                    {
                        braceExecuted = true;
                        braceStartTime = Time.time;
                        opponentWasAttackingWhenBraced = (opponentController != null && opponentController.IsCharging);
                    }
                }
                else
                {
                    AddValidReward(-0.03f);
                }
                break;
            case 0: // No action.
                float noActionReward = 0.01f;

                if (goatController.IsCharging || goatController.IsDodging || goatController.IsBraced || !goatController.IsGrounded)
                {
                    noActionReward += 0.002f;
                }

                if (goatController.currentStamina < 30f)
                {
                    noActionReward += 0.001f;
                }

                AddValidReward(noActionReward);
                break;
            default:
                break;
        }

        // Updates brace state.
        if (actionType != 4 && goatController.IsBraced) goatController.Brace(false);
        else if (actionType == 4 && !goatController.IsBraced && goatController.currentStamina >= 15f) goatController.Brace(true);

        previousStamina = staminaBeforeAction;

        // Penalises time passing.
        AddValidReward(-0.001f);

        // Rewards stamina management.
        float staminaRatio = goatController.currentStamina / goatController.maxStamina;
        if (staminaRatio > 0.5f)
        {
            AddValidReward(0.001f * (staminaRatio - 0.5f));
        }
        else if (staminaRatio < 0.2f)
        {
            AddValidReward(-0.002f * (0.2f - staminaRatio));
        }

        // Penalises being near the edge.
        float distanceFromCenter = Vector3.Distance(transform.position, platformTransform.position);
        float distanceToEdge = GetPlatformRadius() - distanceFromCenter;
        float normalizedDistanceToEdge = distanceToEdge / GetPlatformRadius();

        if (normalizedDistanceToEdge < 0.2f)
        {
            AddValidReward(-0.01f * (0.2f - normalizedDistanceToEdge));
        }

        // Rewards engagement.
        Transform opponentTransform = goatController.Opponent;
        if (opponentTransform != null)
        {
            float distanceToOpponent = Vector3.Distance(transform.position, opponentTransform.position);

            // Rewards proximity.
            float proximityReward = 0.02f / (1.0f + distanceToOpponent);
            AddValidReward(proximityReward);

            // Rewards pushing.
            Vector3 opponentMovement = opponentTransform.position - previousOpponentPosition;
            float opponentMoveDistance = opponentMovement.magnitude;
            if (opponentMoveDistance > 0.01f)
            {
                Vector3 facingDirection = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
                Vector3 opponentMoveDirection = new Vector3(opponentMovement.x, 0f, opponentMovement.z).normalized;
                float alignment = Vector3.Dot(facingDirection, opponentMoveDirection);

                if (alignment > 0.3f)
                {
                    float pushReward = 0.15f * alignment * opponentMoveDistance;
                    AddValidReward(pushReward);

                    float opponentDistFromCenter = Vector3.Distance(opponentTransform.position, platformTransform.position);
                    float previousOpponentDistFromCenter = Vector3.Distance(previousOpponentPosition, platformTransform.position);

                    if (opponentDistFromCenter > previousOpponentDistFromCenter)
                    {
                        float edgeBonus = (opponentDistFromCenter - previousOpponentDistFromCenter) / GetPlatformRadius();
                        AddValidReward(0.2f * edgeBonus * alignment);
                    }
                }
            }

            previousOpponentPosition = opponentTransform.position;
        }

        // Checks if hit.
        wasHitThisFrame = (goatController.IsBeingAttacked && opponentController != null &&
                          goatController.CurrentAttacker == opponentController);

        if (wasHitThisFrame && !wasHitLastFrame)
        {
            AddValidReward(-0.5f);
        }
        wasHitLastFrame = wasHitThisFrame;

        // Penalises if opponent is on top.
        if (opponentTransform != null)
        {
            float opponentY = opponentTransform.position.y;
            float goatY = transform.position.y;
            float horizontalDistance = Vector3.Distance(
                new Vector3(opponentTransform.position.x, 0f, opponentTransform.position.z),
                new Vector3(transform.position.x, 0f, transform.position.z)
            );

            if (opponentY > goatY + 0.5f && horizontalDistance < 1.5f)
            {
                AddValidReward(-0.02f);
            }
        }

        // Evaluates attack.
        if (attackExecuted && attackStartTime >= 0)
        {
            float timeSinceAttack = Time.time - attackStartTime;
            bool attackHit = (opponentController != null && opponentController.IsBeingAttacked &&
                             opponentController.CurrentAttacker == goatController);

            if (attackHit)
            {
                AddValidReward(0.6f);
                attackExecuted = false;
                attackStartTime = -1f;
            }
            else if (timeSinceAttack > attackTimeout)
            {
                AddValidReward(-0.1f);
                attackExecuted = false;
                attackStartTime = -1f;
            }
            else if (!goatController.IsCharging)
            {
                AddValidReward(-0.1f);
                attackExecuted = false;
                attackStartTime = -1f;
            }
        }

        // Evaluates dodge.
        if (dodgeExecuted && dodgeStartTime >= 0)
        {
            float timeSinceDodge = Time.time - dodgeStartTime;

            if (timeSinceDodge <= dodgeTimeout)
            {
                if (opponentWasAttackingWhenDodged && !wasHitThisFrame)
                {
                    AddValidReward(0.1f);
                    dodgeExecuted = false;
                    dodgeStartTime = -1f;
                    opponentWasAttackingWhenDodged = false;
                }
                else if (opponentWasAttackingWhenDodged && wasHitThisFrame)
                {
                    AddValidReward(-0.1f);
                    dodgeExecuted = false;
                    dodgeStartTime = -1f;
                    opponentWasAttackingWhenDodged = false;
                }
            }
            else
            {
                if (opponentWasAttackingWhenDodged)
                {
                    if (!wasHitThisFrame)
                    {
                        AddValidReward(0.08f);
                    }
                    else
                    {
                        AddValidReward(-0.1f);
                    }
                }
                else
                {
                    AddValidReward(-0.05f);
                }
                dodgeExecuted = false;
                dodgeStartTime = -1f;
                opponentWasAttackingWhenDodged = false;
            }
        }

        // Evaluates jump.
        if (jumpExecuted && jumpStartTime >= 0)
        {
            float timeSinceJump = Time.time - jumpStartTime;

            if (timeSinceJump <= jumpTimeout)
            {
                if (opponentWasAttackingWhenJumped && !wasHitThisFrame)
                {
                    AddValidReward(0.1f);
                    jumpExecuted = false;
                    jumpStartTime = -1f;
                    opponentWasAttackingWhenJumped = false;
                }
                else if (opponentWasAttackingWhenJumped && wasHitThisFrame)
                {
                    AddValidReward(-0.1f);
                    jumpExecuted = false;
                    jumpStartTime = -1f;
                    opponentWasAttackingWhenJumped = false;
                }
            }
            else
            {
                if (opponentWasAttackingWhenJumped)
                {
                    if (!wasHitThisFrame)
                    {
                        AddValidReward(0.08f);
                    }
                    else
                    {
                        AddValidReward(-0.1f);
                    }
                }
                else
                {
                    AddValidReward(-0.05f);
                }
                jumpExecuted = false;
                jumpStartTime = -1f;
                opponentWasAttackingWhenJumped = false;
            }
        }

        // Evaluates brace.
        if (braceExecuted && braceStartTime >= 0)
        {
            float timeSinceBrace = Time.time - braceStartTime;
            bool opponentCurrentlyAttacking = (opponentController != null && opponentController.IsCharging);

            if (goatController.IsBraced)
            {
                if (opponentWasAttackingWhenBraced && wasHitThisFrame)
                {
                    float braceDistanceFromCenter = Vector3.Distance(transform.position, platformTransform.position);
                    if (braceDistanceFromCenter < GetPlatformRadius() * 0.8f)
                    {
                        AddValidReward(0.05f);
                    }
                }
                else if (opponentWasAttackingWhenBraced && !wasHitThisFrame)
                {
                    AddValidReward(0.08f);
                }
            }
            else
            {
                if (timeSinceBrace <= braceTimeout)
                {
                    if (opponentWasAttackingWhenBraced && !wasHitThisFrame)
                    {
                        AddValidReward(0.1f);
                    }
                    else if (opponentWasAttackingWhenBraced && wasHitThisFrame)
                    {
                        AddValidReward(-0.1f);
                    }
                    else if (!opponentWasAttackingWhenBraced)
                    {
                        AddValidReward(-0.05f);
                    }
                }
                braceExecuted = false;
                braceStartTime = -1f;
                opponentWasAttackingWhenBraced = false;
            }
        }
    }

    // Controls the AI manually.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

        float moveInput = 0f;
        if (Input.GetKey(KeyCode.RightArrow)) moveInput = 1f;
        else if (Input.GetKey(KeyCode.LeftArrow)) moveInput = -1f;
        continuousActions[0] = moveInput;

        if (Input.GetKey(KeyCode.Alpha0)) discreteActions[0] = 1; // Attack.
        else if (Input.GetKey(KeyCode.Alpha9)) discreteActions[0] = 2; // Dodge.
        else if (Input.GetKey(KeyCode.UpArrow)) discreteActions[0] = 3; // Jump.
        else if (Input.GetKey(KeyCode.DownArrow)) discreteActions[0] = 4; // Brace.
        else discreteActions[0] = 0; // No action.
    }

    // Adds a valid reward.
    private void AddValidReward(float reward)
    {
        if (!float.IsInfinity(reward) && !float.IsNaN(reward))
        {
            AddReward(reward);
        }
    }

    // Called when the AI falls.
    public void OnAIFellOff()
    {
        SetReward(-10.0f);
        EndEpisode();
    }

    // Called when the opponent falls.
    public void OnOpponentFellOff()
    {
        SetReward(+10.0f);
        EndEpisode();
    }
}