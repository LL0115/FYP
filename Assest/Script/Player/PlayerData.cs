using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerData : MonoBehaviour, DamagableItem
{
    // Public fields to store the current and maximum health points, visible in the Inspector
    [SerializeField]
    private int currentHP;

    [SerializeField]
    private int maxHP;

    // Private field to track if the player is dead
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
    public event System.Action<int> DamageDealt;
    // Event triggered when the item dies
    public event System.Action<Vector3> DeathEvent;

    // Reference to the HealthBarPrefab
    [SerializeField]
    private HealthBarPrefab healthBarPrefab;

    // Method to apply damage to the item
    public void TakeDamage(int damage)
    {
        // If the player is already dead, do nothing
        if (isDead) return;

        // Reduce the current health points by the damage amount
        CurrentHP -= damage;
        // Log the current health points
        Debug.Log($"Player took {damage} damage, current HP: {CurrentHP}");
        // Trigger the DamageDealt event
        DamageDealt?.Invoke(damage);

        // If current health points are less than or equal to zero, trigger the DeathEvent
        if (CurrentHP <= 0)
        {
            isDead = true;
            Debug.Log("Player died.");
            DeathEvent?.Invoke(transform.position);
        }
    }

    // Method to set the player's HP
    public void SetHP(int hp)
    {
        if (hp > MaxHP)
        {
            CurrentHP = MaxHP;
        }
        else
        {
            CurrentHP = hp;
        }
    }

    // Method to set the player's maximum HP
    public void SetMaxHP(int maxHp)
    {
        MaxHP = maxHp;
        if (CurrentHP > MaxHP)
        {
            CurrentHP = MaxHP;
        }
    }

    // Method to initialize the player's HP (optional)
    private void Start()
    {
        // Initialize the player's HP to the maximum HP at the start
        CurrentHP = MaxHP;

        // Create the health bar
        if (healthBarPrefab != null)
        {
            healthBarPrefab.CreateHealthBar(transform, this);
        }
    }
}