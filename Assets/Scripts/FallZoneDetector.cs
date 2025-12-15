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
        // Ensures the collider is a trigger.
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    private void Awake()
    {
        // Finds the round manager if missing.
        if (roundManager == null)
        {
            roundManager = FindFirstObjectByType<RoundManager>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Checks if the collider is a tracked goat.
        GameObject fallenGoat = other.gameObject;
        bool isGoat1 = (goat1 != null && fallenGoat == goat1);
        bool isGoat2 = (goat2 != null && fallenGoat == goat2);

        if (!isGoat1 && !isGoat2)
        {
            return;
        }

        // Identifies the other goat.
        GameObject otherGoat = (fallenGoat == goat1) ? goat2 : goat1;

        // Checks if goats are AI.
        AiGoatScript fallenGoatAI = fallenGoat.GetComponent<AiGoatScript>();
        bool fallenGoatIsAI = fallenGoatAI != null;

        AiGoatScript otherGoatAI = (otherGoat != null) ? otherGoat.GetComponent<AiGoatScript>() : null;
        bool otherGoatIsAI = otherGoatAI != null;

        // Handles round logic.
        if (roundManager != null)
        {
            if (!roundManager.IsRoundEnding())
            {
                Debug.Log($"{fallenGoat.name} fell off");
                roundManager.OnGoatFell(fallenGoat);
            }
            return;
        }

        // Handles training logic.
        if (fallenGoatIsAI)
        {
            Debug.Log("AI goat fell off");
            fallenGoatAI.OnAIFellOff();

            if (otherGoatIsAI)
            {
                otherGoatAI.OnOpponentFellOff();
            }
        }
        else
        {
            Debug.Log("Player goat fell off");
            if (otherGoatIsAI)
            {
                otherGoatAI.OnOpponentFellOff();
            }
        }
    }
}
