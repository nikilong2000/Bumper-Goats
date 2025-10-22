using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class AiGoatScript : Agent
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform centerOfPlatform;

    [Header("Environment Settings")]
    [SerializeField] private float platformRadius = 5f;
    [SerializeField] private float moveSpeed = 4f;

    private Rigidbody rb;
    private Rigidbody playerRb;

    // Store initial positions for episode reset
    private Vector3 aiStartPosition;
    private Vector3 playerStartPosition;

    /// <summary>
    /// Called once when the agent is first initialized
    /// </summary>
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        if (playerTransform != null)
        {
            playerRb = playerTransform.GetComponent<Rigidbody>();
        }

        // Store starting positions
        aiStartPosition = transform.position;
        playerStartPosition = playerTransform.position;
    }

    /// <summary>
    /// Called at the start of each training episode
    /// Reset the environment to a clean state
    /// </summary>
    public override void OnEpisodeBegin()
    {
        // Reset AI goat position and physics
        transform.position = aiStartPosition;
        transform.rotation = Quaternion.identity;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Reset player goat position and physics
        if (playerTransform != null)
        {
            playerTransform.position = playerStartPosition;
            playerTransform.rotation = Quaternion.identity;

            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
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
        Vector3 relativePosition = transform.position - centerOfPlatform.position;
        sensor.AddObservation(relativePosition);

        // 2. AI's linearVelocity (3 values: x, y, z)
        // This helps the AI understand its current momentum
        sensor.AddObservation(rb.linearVelocity);

        // 3. AI's distance from platform edge (1 value)
        // Critical for self-preservation
        float distanceFromCenter = Vector3.Distance(transform.position, centerOfPlatform.position);
        float distanceToEdge = platformRadius - distanceFromCenter;
        sensor.AddObservation(distanceToEdge / platformRadius); // Normalized 0-1

        // 4. AI's forward direction (2 values: x, z on ground plane)
        // Helps AI understand which way it's facing
        Vector3 forward = transform.forward;
        sensor.AddObservation(new Vector2(forward.x, forward.z));

        // --- Player Awareness (6 observations) ---

        if (playerTransform != null)
        {
            // 5. Vector from AI to Player (3 values: x, y, z)
            // This is the most important observation for pushing
            Vector3 directionToPlayer = playerTransform.position - transform.position;
            sensor.AddObservation(directionToPlayer);

            // 6. Player's linearVelocity (3 values: x, y, z)
            // Helps AI predict where player is moving
            if (playerRb != null)
            {
                sensor.AddObservation(playerRb.linearVelocity);
            }
            else
            {
                sensor.AddObservation(Vector3.zero);
            }
        }
        else // If player reference is missing, observe zeros
        {
            sensor.AddObservation(Vector3.zero); // Direction to player
            sensor.AddObservation(Vector3.zero); // Player linearVelocity
        }
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
        float moveZ = actions.ContinuousActions[1];

        // Apply movement (simple version for now)
        Vector3 movement = new Vector3(moveX, 0, moveZ);
        transform.position += movement * moveSpeed * Time.fixedDeltaTime;

        // --- Small Penalty for Existing (Time Cost) ---
        // This encourages the AI to finish episodes quickly
        AddReward(-0.001f);

        // --- Penalty for Being Near Edge (Self-Preservation) ---
        float distanceFromCenter = Vector3.Distance(transform.position, centerOfPlatform.position);
        float distanceToEdge = platformRadius - distanceFromCenter;

        if (distanceToEdge < 1.5f) // Danger zone
        {
            AddReward(-0.01f);
        }

        // --- Small Reward for Being Close to Player (Engagement) ---
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            // Inverse distance reward: closer = better
            AddReward(0.01f / (1.0f + distanceToPlayer));
        }
    }

    /// <summary>
    /// Heuristic mode - allows you to manually control the AI for testing
    /// Useful for debugging and creating demonstration recordings
    /// </summary>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;

        // Use arrow keys or WASD to control
        continuousActions[0] = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
        continuousActions[1] = Input.GetAxisRaw("Vertical");   // W/S or Up/Down
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
    public void OnPlayerFellOff()
    {
        SetReward(+1.0f); // Large positive reward for winning
        EndEpisode();
    }
}