using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FallZoneDetector : MonoBehaviour
{
    [Header("Goats")]
    [SerializeField] private GameObject goat1;
    [SerializeField] private GameObject goat2;

    [Header("Round Manager")]
    [SerializeField] private RoundManager roundManager;

    private void Reset()
    {
        // Make sure this collider is a trigger
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void Awake()
    {
        // Try to find RoundManager if not assigned
        if (roundManager == null)
        {
            roundManager = FindFirstObjectByType<RoundManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the collider belongs to one of our tracked goats
        GameObject fallenGoat = other.gameObject;
        bool isGoat1 = (goat1 != null && fallenGoat == goat1);
        bool isGoat2 = (goat2 != null && fallenGoat == goat2);

        if (!isGoat1 && !isGoat2)
        {
            return; // Not one of our tracked goats
        }

        // Determine the other goat
        GameObject otherGoat = (fallenGoat == goat1) ? goat2 : goat1;

        // Check if the fallen goat has AiGoatScript (is an AI goat)
        AiGoatScript fallenGoatAI = fallenGoat.GetComponent<AiGoatScript>();
        bool fallenGoatIsAI = fallenGoatAI != null;

        // Check if the other goat has AiGoatScript (is an AI goat)
        AiGoatScript otherGoatAI = (otherGoat != null) ? otherGoat.GetComponent<AiGoatScript>() : null;
        bool otherGoatIsAI = otherGoatAI != null;

        // If RoundManager exists, use it to handle the round/match logic
        if (roundManager != null && !roundManager.IsRoundEnding())
        {
            Debug.Log($"{fallenGoat.name} fell off");
            roundManager.OnGoatFell(fallenGoat);

            // Also notify AI agents for rewards (they will handle their own episode ending)
            // Disabling, since AI agents don't need to train anymore
            // if (fallenGoatIsAI)
            // {
            //     // AI goat fell - it lost the round
            //     fallenGoatAI.OnAIFellOff();
            // }
            // else if (otherGoatIsAI)
            // {
            //     // Player goat fell - AI won the round
            //     otherGoatAI.OnOpponentFellOff();
            // }
        }
        else
        {
            // Fallback to training behavior if RoundManager is not available
            if (fallenGoatIsAI)
            {
                Debug.Log("AI goat fell off");
                // Notify the fallen AI goat that it fell off
                fallenGoatAI.OnAIFellOff();

                // If the other goat is also AI, notify it that its opponent fell off
                if (otherGoatIsAI)
                {
                    otherGoatAI.OnOpponentFellOff();
                }
            }
            else
            {
                Debug.Log("Player goat fell off");
                // If the other goat is AI, notify it that its opponent (player) fell off
                if (otherGoatIsAI)
                {
                    otherGoatAI.OnOpponentFellOff();
                }
            }
        }

        // Reset arena size
        if (ArenaShrinking.Instance != null)
        {
            ArenaShrinking.Instance.ResetArenaSize();
        }
    }
}
