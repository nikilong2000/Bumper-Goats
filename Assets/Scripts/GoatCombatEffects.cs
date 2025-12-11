using UnityEngine;

[RequireComponent(typeof(GoatController))]
public class GoatCombatEffects : MonoBehaviour
{
    [Header("Effects Prefabs")]
    [SerializeField] private GameObject hitEffectPrefab;  // Assign your Spark/Flash prefab here
    [SerializeField] private GameObject dustEffectPrefab; // Assign your Dust prefab here

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
        // Reset the flag when we are no longer charging
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
        // If we already played the effect for this charge, do nothing
        if (hasPlayedEffectForCurrentCharge) return;

        // 1. Check if we hit the other goat
        if (collision.gameObject.TryGetComponent<GoatController>(out var otherGoat))
        {
            // Check if we are attacking (charging)
            if (goatController.IsCharging)
            {
                SpawnEffects(collision);
                hasPlayedEffectForCurrentCharge = true;
            }
        }
    }

    private void SpawnEffects(Collision collision)
    {
        // Play hit sound
        if (hitSound != null)
        {
            // Play at the contact point so it has spatial position
            // Vector3 soundPosition = collision.contactCount > 0 ? collision.contacts[0].point : transform.position;
            // AudioSource.PlayClipAtPoint(hitSound, soundPosition, hitSoundVolume);

            audioSource.clip = hitSound;
            audioSource.Play();
        }

        // --- 1. THE HIT EFFECT (Head/Chest) ---
        if (hitEffectPrefab != null && collision.contactCount > 0)
        {
            // Get the first point where the colliders touched
            ContactPoint contact = collision.contacts[0];

            // Instantiate the effect at the exact contact point
            // Quaternion.LookRotation(contact.normal) makes the sparks fly OUT from the impact
            GameObject hitVFX = Instantiate(hitEffectPrefab, contact.point, Quaternion.LookRotation(contact.normal));

            // Clean up: Destroy the effect after 2 seconds so game doesn't lag
            Destroy(hitVFX, 1.0f);
        }

        // --- 2. THE DUST EFFECT (Feet) ---
        if (dustEffectPrefab != null)
        {
            // We want this at the goat's feet, not the impact point.
            // We take the Goat's current position, but move it down slightly to floor level.
            Vector3 feetPosition = transform.position;
            // Assuming the pivot is at the center, we might need to adjust. 
            // If the pivot is at the feet, transform.position is fine.
            // The user suggested feetPosition.y = 0.1f;
            // Let's try to be a bit more dynamic or stick to the user's suggestion if it makes sense.
            // User suggestion: feetPosition.y = 0.1f;
            feetPosition.y = 0.1f; // Just slightly above 0 so it doesn't clip into the floor

            // Rotation: Pointing backwards (away from the hit) looks best for recoil
            // If we are the attacker, recoil might be backwards relative to us.
            Quaternion dustRotation = Quaternion.LookRotation(-transform.forward);

            GameObject dustVFX = Instantiate(dustEffectPrefab, feetPosition, dustRotation);
            Destroy(dustVFX, 1.0f);
        }
    }
}
