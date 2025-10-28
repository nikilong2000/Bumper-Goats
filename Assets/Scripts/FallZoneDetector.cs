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
            if (playerGo != null && playerRespawnPoint != null)
            {
                // Get the Rigidbody and respawn the player
                Rigidbody playerRb = playerGo.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    Respawn(playerRb, playerRespawnPoint);
                }
            }

            // Find the AI GameObject by its tag
            GameObject aiGo = GameObject.FindWithTag(aiTag);
            if (aiGo != null && aiRespawnPoint != null)
            {
                // Get the Rigidbody and respawn the AI + reset stamina
                Rigidbody aiRb = aiGo.GetComponent<Rigidbody>();
                GoatController playerController = playerGo.GetComponent<GoatController>();
                if (aiRb != null)
                {
                    Respawn(aiRb, aiRespawnPoint);
                    if (playerController != null)
                    {
                        ResetStamina(playerController);
                    }
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
        controller.currentStamina = 100f; // Reset to max stamina
        controller.staminaBar.fillAmount = 1f; // Fill the stamina bar
    }
}
