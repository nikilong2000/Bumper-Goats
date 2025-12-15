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
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (mainMenuCanvasGroup != null)
        {
            mainMenuCanvasGroup.alpha = 0f;
            mainMenuCanvasGroup.gameObject.SetActive(false);
        }

        StartCoroutine(PlayOpeningSequence());
    }

    private IEnumerator PlayOpeningSequence()
    {
        // 1. Setup Camera at Start Position
        if (targetCamera != null && startPosition != null)
        {
            targetCamera.transform.SetPositionAndRotation(startPosition.position, startPosition.rotation);
        }

        // 2. Fly Camera
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            if (targetCamera != null && startPosition != null && endPosition != null)
            {
                float t = elapsed / flyDuration;

                targetCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition.position, endPosition.position, t), Quaternion.Lerp(startPosition.rotation, endPosition.rotation, t));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (targetCamera != null && endPosition != null)
        {
            targetCamera.transform.SetPositionAndRotation(endPosition.position, endPosition.rotation);
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

            mainMenuCanvasGroup.interactable = true;
            mainMenuCanvasGroup.blocksRaycasts = true;
        }
    }
}
