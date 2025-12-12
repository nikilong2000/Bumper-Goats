using System.Collections;
using UnityEngine;

public class OpeningSceneController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform startPosition;
    [SerializeField] private Transform endPosition;
    [SerializeField] private float flyDuration = 3f;

    [Header("UI Settings")]
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;
    [SerializeField] private float uiFadeDuration = 1f;

    private void Start()
    {
        // Ensure camera is assigned
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // Initialize UI state
        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.alpha = 0f;
            mainMenuCanvasGroup.gameObject.SetActive(false); // Ensure it's disabled initially if desired, or just invisible
            // However, to fade it in, it needs to be active but invisible.
            // Let's keep it active but alpha 0, or activate it before fading.
            // The prompt says "main menu UI should be activated", implying SetActive(true).
            mainMenuCanvasGroup.gameObject.SetActive(false);
        }

        StartCoroutine(PlayOpeningSequence());
    }

    private IEnumerator PlayOpeningSequence()
    {
        // 1. Setup Camera at Start Position
        if (targetCamera != null && startPosition != null)
        {
            targetCamera.transform.position = startPosition.position;
            targetCamera.transform.rotation = startPosition.rotation;
        }

        // 2. Fly Camera
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            if (targetCamera != null && startPosition != null && endPosition != null)
            {
                float t = elapsed / flyDuration;
                // Optional: Use smooth step for nicer movement
                // t = Mathf.SmoothStep(0f, 1f, t); 

                targetCamera.transform.position = Vector3.Lerp(startPosition.position, endPosition.position, t);
                targetCamera.transform.rotation = Quaternion.Lerp(startPosition.rotation, endPosition.rotation, t);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure we reach the exact end position
        if (targetCamera != null && endPosition != null)
        {
            targetCamera.transform.position = endPosition.position;
            targetCamera.transform.rotation = endPosition.rotation;
        }

        // 3. Activate and Fade In UI
        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.gameObject.SetActive(true);

            elapsed = 0f;
            while (elapsed < uiFadeDuration)
            {
                float t = elapsed / uiFadeDuration;
                mainMenuCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

                elapsed += Time.deltaTime;
                yield return null;
            }
            mainMenuCanvasGroup.alpha = 1f;

            // Enable interactions if blocked by CanvasGroup
            mainMenuCanvasGroup.interactable = true;
            mainMenuCanvasGroup.blocksRaycasts = true;
        }
    }
}
