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
                    // during ai training self-train
                    AiGoatScript aiGoatScript = aiGo.GetComponent<AiGoatScript>();
                    AiGoatScript ai2GoatScript = playerGo.GetComponent<AiGoatScript>();
                    if (aiGoatScript != null) aiGoatScript.OnOpponentFellOff();
                    if (ai2GoatScript != null) ai2GoatScript.OnAIFellOff();
                }
            } else if (other.CompareTag(aiTag)){
                Debug.Log("AI fell off");
                if (aiActive) {
                    // during ai training self-train
                    AiGoatScript aiGoatScript = aiGo.GetComponent<AiGoatScript>();
                    AiGoatScript ai2GoatScript = playerGo.GetComponent<AiGoatScript>();
                    if (aiGoatScript != null) aiGoatScript.OnAIFellOff();
                    if (ai2GoatScript != null) ai2GoatScript.OnOpponentFellOff();
                }
            }
        }

        // Reset arena size
        ArenaShrinking.Instance.ResetArenaSize();
    }
}
