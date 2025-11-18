using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FallZoneDetector : MonoBehaviour
{
    [Header("Respawn Points")]
    [SerializeField] private Transform playerRespawnPoint;
    [SerializeField] private Transform aiRespawnPoint;

    [Header("Tags")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string aiTag = "AI";

    private void Reset()
    {
        // Make sure this collider is a trigger
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object that entered is tagged as Player or AI
        if (other.CompareTag(playerTag) || other.CompareTag(aiTag))
        {
            // Find the player GameObject by its tag
            GameObject playerGo = GameObject.FindWithTag(playerTag);
            // Find the AI GameObject by its tag
            GameObject aiGo = GameObject.FindWithTag(aiTag);

            bool playerActive = playerGo != null && playerRespawnPoint != null;
            bool aiActive = aiGo != null && aiRespawnPoint != null;

            if (other.CompareTag(playerTag)){
                Debug.Log("Player fell off");
                if (aiActive) {
                    // Get the Rigidbody and respawn the AI
                    Rigidbody aiRb = aiGo.GetComponent<Rigidbody>();

                    GoatController aiController = aiGo.GetComponent<GoatController>();
                    if (aiController != null) ResetStamina(aiController);

                    // during ai training self-train
                    AiGoatScript aiGoatScript = aiGo.GetComponent<AiGoatScript>();
                    // AiGoatScript ai2GoatScript = playerGo.GetComponent<AiGoatScript>();
                    if (aiGoatScript != null) aiGoatScript.OnOpponentFellOff();
                    // if (ai2GoatScript != null) ai2GoatScript.OnAIFellOff();

                    Debug.Log("Respawning AI");
                    if (aiRb != null) Respawn(aiRb, aiRespawnPoint);
                }
                if (playerActive) {
                    // Get the Rigidbody and respawn the player
                    Rigidbody playerRb = playerGo.GetComponent<Rigidbody>();

                    GoatController playerController = playerGo.GetComponent<GoatController>();
                    if (playerController != null) ResetStamina(playerController);

                    Debug.Log("Respawning Player");
                    if (playerRb != null) Respawn(playerRb, playerRespawnPoint);
                }
            } else if (other.CompareTag(aiTag)){
                Debug.Log("AI fell off");
                if (aiActive) {
                    // Get the Rigidbody and respawn the AI
                    Rigidbody aiRb = aiGo.GetComponent<Rigidbody>();

                    GoatController aiController = aiGo.GetComponent<GoatController>();
                    if (aiController != null) ResetStamina(aiController);

                    // during ai training self-train
                    AiGoatScript aiGoatScript = aiGo.GetComponent<AiGoatScript>();
                    // AiGoatScript ai2GoatScript = playerGo.GetComponent<AiGoatScript>();
                    if (aiGoatScript != null) aiGoatScript.OnAIFellOff();
                    // if (ai2GoatScript != null) ai2GoatScript.OnOpponentFellOff();

                    if (aiRb != null) Respawn(aiRb, aiRespawnPoint);
                }
                if (playerActive) {
                    // Get the Rigidbody and respawn the Player
                    Rigidbody playerRb = playerGo.GetComponent<Rigidbody>();

                    GoatController playerController = playerGo.GetComponent<GoatController>();
                    if (playerController != null) ResetStamina(playerController);

                    if (playerRb != null) Respawn(playerRb, playerRespawnPoint);
                }
            }
        }

        // Reset arena size
        ArenaShrinking.Instance.ResetArenaSize();
    }

    private void Respawn(Rigidbody rb, Transform target)
    {
        // Zero motion first
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Teleport to target (position + rotation)
        rb.position = target.position;
        rb.rotation = target.rotation;
    }

    private void ResetStamina(GoatController controller)
    {
        controller.ResetStamina();
    }
}
