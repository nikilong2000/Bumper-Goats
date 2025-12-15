using UnityEngine;

[RequireComponent(typeof(GoatController))]
public class GoatCombatEffects : MonoBehaviour
{
    [Header("Effects Prefabs")]
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private float hitSoundVolume = 1.0f;

    private GoatController goatController;
    private bool hasPlayedEffectForCurrentCharge = false;

    private void Awake()
    {
        goatController = GetComponent<GoatController>();
    }

    private void Update()
    {
        if (!goatController.IsCharging)
        {
            hasPlayedEffectForCurrentCharge = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TrySpawnEffect(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TrySpawnEffect(collision);
    }

    private void TrySpawnEffect(Collision collision)
    {
        // If already played the effect for this charge, do nothing.
        if (hasPlayedEffectForCurrentCharge) return;

        if (collision.gameObject.TryGetComponent<GoatController>(out var otherGoat))
        {
            if (goatController.IsCharging)
            {
                SpawnEffects(collision);
                hasPlayedEffectForCurrentCharge = true;
            }
        }
    }

    private void SpawnEffects(Collision collision)
    {
        if (hitSound != null)
        {
            audioSource.clip = hitSound;
            audioSource.Play();
        }

        if (hitEffectPrefab != null && collision.contactCount > 0)
        {
            // Get the first point where the colliders touched
            ContactPoint contact = collision.contacts[0];

            // Instantiate the effect at the exact contact point
            GameObject hitVFX = Instantiate(hitEffectPrefab, contact.point, Quaternion.LookRotation(contact.normal));

            // Destroy the effect after 2 seconds so game doesn't lag
            Destroy(hitVFX, 1.0f);
        }
    }
}
