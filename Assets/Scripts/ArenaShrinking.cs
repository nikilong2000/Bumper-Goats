using UnityEngine;

public class ArenaShrinking : MonoBehaviour
{
    private GameObject arenaObject;
    public static ArenaShrinking Instance { get; private set; }

    public bool shrinkingDisabled = false;

    public float reduceSpeed = 0.3f;
    public float gracePeriod = 10.0f;
    private float timer = 0.0f;
    private Vector3 initialScale;
    private Vector3 minScale = new Vector3(5.0f, 1.0f, 5.0f);

    private float platformRadius;
    public float PlatformRadius => platformRadius;

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
        // Finds the arena object.
        arenaObject = GameObject.FindGameObjectWithTag("Platform");

        if (arenaObject == null)
        {
            Debug.LogError("Arena object not found");
        }

        // Stores the initial scale.
        initialScale = arenaObject.transform.localScale;

        // Calculates the platform radius.
        UpdatePlatformRadius();

        // Starts the timer.
        timer = 0.0f;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer > gracePeriod && arenaObject != null && !shrinkingDisabled)
        {
            // Shrinks the arena.
            Vector3 currentScale = arenaObject.transform.localScale;

            // Checks minimum size.
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

            // Updates the radius.
            UpdatePlatformRadius();
        }
    }

    private void UpdatePlatformRadius()
    {
        if (arenaObject == null) return;

        // Finds the mesh collider.
        MeshCollider meshCollider = arenaObject.GetComponentInChildren<MeshCollider>();
        if (meshCollider != null)
        {
            // Gets the bounds.
            Bounds bounds = meshCollider.bounds;
            // Sets the radius.
            platformRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        }
        else
        {
            // Tries the renderer bounds.
            Renderer renderer = arenaObject.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                platformRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            }
            else
            {
                Debug.LogWarning("ArenaShrinking: Could not find MeshCollider or Renderer in arena object or its children to calculate platform radius");
            }
        }
    }

    public void ResetArenaSize()
    {
        if (arenaObject != null)
        {
            arenaObject.transform.localScale = initialScale;
            timer = 0.0f;
            UpdatePlatformRadius();
        }
    }
}
