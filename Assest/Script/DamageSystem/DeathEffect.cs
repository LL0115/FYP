using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ensure that the GameObject has a DamagableItem component
[RequireComponent(typeof(DamagableItem))]
public class DeathEffect : MonoBehaviour
{
    // Reference to the DamagableItem component
    public DamagableItem DamagableItem;

    // Particle system to play on death
    [SerializeField]
    private ParticleSystem DeathEffectParticle;

    // Private field to track if the death effect has been played
    private bool hasPlayedDeathEffect = false;

    // Initialize the DamagableItem reference
    private void Start()
    {
        // Get the DamagableItem component attached to the same GameObject
        DamagableItem = GetComponent<DamagableItem>();

        if (DamagableItem == null)
        {
            Debug.LogError("DamagableItem component is missing on this GameObject.");
        }
    }

    // Subscribe to the DeathEvent when the script is enabled
    private void OnEnable()
    {
        if (DamagableItem != null)
        {
            DamagableItem.DeathEvent += DamagableItem_DeathEvent;
        }
    }

    // Unsubscribe from the DeathEvent when the script is disabled
    private void OnDisable()
    {
        if (DamagableItem != null)
        {
            DamagableItem.DeathEvent -= DamagableItem_DeathEvent;
        }
    }

    // Method to handle the DeathEvent
    public void DamagableItem_DeathEvent(Vector3 Position)
    {
        // Check if the death effect has already been played
        if (hasPlayedDeathEffect) return;

        // Instantiate the death effect particle system at the position of death
        ParticleSystem effect = Instantiate(DeathEffectParticle, Position, Quaternion.identity);

        // Set the flag to true to indicate that the death effect has been played
        hasPlayedDeathEffect = true;

        // Destroy the particle system after it has finished playing
        Destroy(effect.gameObject, effect.main.duration);
    }
}
