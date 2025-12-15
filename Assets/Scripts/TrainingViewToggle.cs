using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

public class TrainingViewToggle : MonoBehaviour
{
    [Tooltip("Time scale during training. Use 1 to watch, >1 to speed up.")]
    public float trainingTimeScale = 1f;

    [Tooltip("Normal time scale when *not* training.")]
    public float normalTimeScale = 1f;

    float baseFixed;

    void Awake()
    {
        // Stores the initial fixed delta time.
        baseFixed = Time.fixedDeltaTime;
    }

    void Start()
    {
        bool communicatorOn = Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;

        // Checks if any agent is using default behaviour.
        var allBps = Object.FindObjectsByType<BehaviorParameters>(FindObjectsSortMode.None);
        bool anyDefault = allBps.Any(bp => bp.BehaviorType == BehaviorType.Default);

        // Determines if training is active.
        bool effectiveTraining = communicatorOn && anyDefault;

        float targetScale = effectiveTraining ? trainingTimeScale : normalTimeScale;

        // Sets the time scale and fixed delta time.
        Time.timeScale = targetScale;
        Time.fixedDeltaTime = baseFixed * targetScale;
    }
}
