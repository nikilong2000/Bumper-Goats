using UnityEngine;

[RequireComponent(typeof(GoatController))]
public class GoatCombatEffects : MonoBehaviour
{
    [Header("Effects Prefabs")]
    [SerializeField] private GameObject hitEffectPrefab;  // Assign your Spark/Flash prefab here
    [SerializeField] private GameObject dustEffectPrefab; // Assign your Dust prefab here

    [Header("Settings")]
    [SerializeField] private float hitForceThreshold = 1.0f; // Minimum speed to trigger an effect

    private GoatController goatController;

    private void Awake()
    {
        goatController = GetComponent<GoatController>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 1. Check if we hit the other goat

        if (collision.gameObject.TryGetComponent<GoatController>(out var otherGoat))
        {
            // Check if either goat is attacking (charging)
            // We trigger the effect if this goat is charging OR the other goat is charging
            if (otherGoat.IsCharging)
            {
                // // Optional: Only play effect if the impact was hard enough
                // if (collision.relativeVelocity.magnitude > hitForceThreshold)
                // {
                SpawnEffects(collision);
                // }
            }
        }
    }

    private void SpawnEffects(Collision collision)
    {
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
