/*
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Define a class for enemy health points, implementing the IDamagableItem interface
public class EnemyHP : MonoBehaviour, DamagableItem
{
    // Private field to store the current health points
    [SerializeField]
    private int currentHP;

    // Private field to store the maximum health points
    [SerializeField]
    private int maxHP;

    // Private field to track if the enemy is dead
    private bool isDead = false;

    // Property to get the current health points
    public int CurrentHP
    {
        get { return currentHP; }
        private set { currentHP = value; }
    }

    // Property to get the maximum health points
    public int MaxHP
    {
        get { return maxHP; }
        private set { maxHP = value; }
    }

    // Event triggered when damage is taken
    public event DamagableItem.TakenDamage DamageDealt;
    // Event triggered when the item dies
    public event DamagableItem.Death DeathEvent;
    // Event triggered when health is updated
    public event System.Action HealthUpdated;

    // Method to apply damage to the item
    public void TakeDamage(int damage)
    {
        // If the enemy is already dead, do nothing
        if (isDead) return;

        // Reduce the current health points by the damage amount
        CurrentHP -= damage;
        // Log the current health points
        Debug.Log($"Enemy took {damage} damage, current HP: {CurrentHP}");
        // Trigger the DamageDealt event
        DamageDealt?.Invoke(damage);
        // Trigger the HealthUpdated event
        HealthUpdated?.Invoke();

        // If current health points are less than or equal to zero, trigger the DeathEvent
        if (CurrentHP <= 0)
        {
            isDead = true;
            Debug.Log("Enemy died.");
            DeathEvent?.Invoke(transform.position);
        }
    }
}
*/