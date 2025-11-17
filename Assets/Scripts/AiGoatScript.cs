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

    // Store initial positions for episode reset
    private Vector3 startPosition;
    private Vector3 opponentStartPosition;

    private Quaternion startRotation;
    private Quaternion opponentStartRotation;

    // Agent field
    private bool _aiBracing = false;

    /// <summary>
    /// Called once when the agent is first initialized
    /// </summary>
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        goatController = GetComponent<GoatController>();
        if (opponentTransform != null)
        {
            opponentRb = opponentTransform.GetComponent<Rigidbody>();
            opponentController = opponentTransform.GetComponent<GoatController>();
        }

        // Store starting positions
        startPosition = transform.position;
        opponentStartPosition = opponentTransform.position;
        startRotation = transform.rotation;
        opponentStartRotation = opponentTransform.rotation;
    }

    /// <summary>
    /// Called at the start of each training episode
    /// Reset the environment to a clean state
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Reset AI goat position and physics
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Reset opponent goat position and physics only if it's a player
        // Temporary disabled to test self-training
        if (opponentTransform != null)
        {
            // Check if the opponent is a player by looking for PlayerGoatController component
            PlayerGoatController playerController = opponentTransform.GetComponent<PlayerGoatController>();
            if (playerController != null) // Only reset if it's a player
            {
                opponentTransform.position = opponentStartPosition;
                opponentTransform.rotation = opponentStartRotation;

                if (opponentRb != null)
                {
                    opponentRb.linearVelocity = Vector3.zero;
                    opponentRb.angularVelocity = Vector3.zero;
                }
            }
        }
    }

    /// <summary>
    /// Collect observations - this is what the AI "sees"
    /// Think of this as the AI's sensory input
    /// Total observations: 15 values
    /// </summary>
    public override void CollectObservations(VectorSensor sensor)
    {
        // --- AI's Self-Awareness (9 observations) ---

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

        // --- Opponent Awareness (6 observations) ---

        if (opponentTransform != null)
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
        // --- Continuous Actions: Movement (2 actions) ---
        // Range: -1 to +1 for each axis
        float moveX = actions.ContinuousActions[0];

        // Use GoatController for movement
        Vector2 moveDirection = new Vector2(moveX, 0f);
        goatController.Move(moveDirection);

        // --- Discrete Actions: Combat Actions (4 actions) ---
        // 0: No action, 1: Attack, 2: Dodge, 3: Jump, 4: Brace
        int actionType = actions.DiscreteActions[0];
        switch (actionType)
        {
            case 1: goatController.Attack(); break;
            case 2: goatController.Dodge(moveDirection); break;
            case 3: goatController.Jump(); break;
            case 0: break;         
            default: break;
        }

        if (actionType != 4 && goatController.IsBraced) goatController.Brace(false);
        else if (actionType == 4 && !goatController.IsBraced) goatController.Brace(true);
    
        // --- Small Penalty for Existing (Time Cost) ---
        // This encourages the AI to finish episodes quickly
        AddReward(-0.0005f);

        // --- Penalty for Being Near Edge (Self-Preservation) ---
        float distanceFromCenter = Vector3.Distance(transform.position, platformTransform.position);
        float distanceToEdge = GetPlatformRadius() - distanceFromCenter;
        float normalizedDistanceToEdge = distanceToEdge / GetPlatformRadius(); // 0 = at edge, 1 = at center

        // Reward staying away from edge (smooth gradient)
        AddReward(0.01f * normalizedDistanceToEdge);

        // Opponent positioning
        if (opponentTransform != null)
        {
            float opponentDistFromCenter = Vector3.Distance(opponentTransform.position, platformTransform.position);
            float opponentDistToEdge = GetPlatformRadius() - opponentDistFromCenter;
            
            // Reward when opponent is closer to edge than you are
            if (opponentDistToEdge < distanceToEdge)
            {
                float advantage = (distanceToEdge - opponentDistToEdge) / GetPlatformRadius();
                AddReward(0.05f * advantage); // Stronger reward for positioning advantage
            }
        }
    }

    /// <summary>
    /// Heuristic mode - allows you to manually control the AI for testing
    /// Useful for debugging and creating demonstration recordings
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

        // Use arrow keys or WASD to control movement
        continuousActions[0] = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right

        // Use number keys for combat actions
        if (Input.GetKey(KeyCode.Alpha1)) discreteActions[0] = 1; // Attack
        else if (Input.GetKey(KeyCode.Alpha2)) discreteActions[0] = 2; // Dodge
        else if (Input.GetKey(KeyCode.Alpha3)) discreteActions[0] = 3; // Jump
        else if (Input.GetKey(KeyCode.Alpha4)) discreteActions[0] = 4; // Brace
        else discreteActions[0] = 0; // No action
    }

    /// <summary>
    /// Called when AI falls off the platform
    /// This should be called by your FallZoneDetector
    /// </summary>
    public void OnAIFellOff()
    {
        SetReward(-1.0f); // Large negative reward for losing
        EndEpisode();
    }

    /// <summary>
    /// Called when player falls off the platform
    /// This should be called by your FallZoneDetector
    /// </summary>
    public void OnOpponentFellOff()
    {
        SetReward(+1.0f); // Large positive reward for winning
        EndEpisode();
    }
}