using UnityEngine;

public class ArenaShrinking : MonoBehaviour
{
    private GameObject arenaObject;
    public static ArenaShrinking Instance { get; private set; }

    public float reduceSpeed = 5.0f;
    public float gracePeriod = 1.0f;
    private float timer = 0.0f;
    private Vector3 initialScale;
    private Vector3 minScale = new Vector3(5.0f, 1.0f, 5.0f);

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Get object by tag
        arenaObject = GameObject.FindGameObjectWithTag("Platform");

        if (arenaObject == null)
        {
            Debug.LogError("Arena object not found");
        }

        // Store the initial scale
        initialScale = arenaObject.transform.localScale;

        // Start timer
        timer = 0.0f;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer > gracePeriod && arenaObject != null)
        {
            // Reduce scale
            Vector3 currentScale = arenaObject.transform.localScale;
            
            // Only reduce if above minimum
            if (currentScale.x > minScale.x)
            {
                currentScale.x -= reduceSpeed * Time.deltaTime;
                currentScale.x = Mathf.Max(currentScale.x, minScale.x);
            }
            
            if (currentScale.z > minScale.z)
            {
                currentScale.z -= reduceSpeed * Time.deltaTime;
                currentScale.z = Mathf.Max(currentScale.z, minScale.z);
            }

            arenaObject.transform.localScale = currentScale;
        }
    }

    public void ResetArenaSize()
    {
        if (arenaObject != null)
        {
            arenaObject.transform.localScale = initialScale;
            timer = 0.0f;
        }
    }
}
