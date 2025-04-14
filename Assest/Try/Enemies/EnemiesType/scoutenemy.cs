using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class ScoutEnemy : Enemy
{
    [SerializeField] private TrailRenderer speedTrail;
    [SerializeField] private float evadeChance = 0.3f; // 30% chance to dodge
    [SerializeField] private GameObject evadeEffectPrefab;
    [SerializeField] private Color scoutColor = new Color(0.2f, 0.8f, 1f); // Light blue color
    
    private Renderer myRenderer;
    private Material originalMaterial;
    private Color originalColor;
    
    protected override void Awake()
    {
        // Call base class Awake first
        base.Awake();
        
        // Scout-specific setup
        
        // Find renderer component (adjust if your model has a different structure)
        myRenderer = GetComponentInChildren<Renderer>();
        
        if (myRenderer != null && myRenderer.material != null)
        {
            // Store original material and set scout color
            originalMaterial = myRenderer.material;
            originalColor = originalMaterial.color;
            originalMaterial.color = scoutColor;
        }
        
        // Set up trail if assigned
        if (speedTrail != null)
        {
            speedTrail.enabled = true;
        }
        
        // Make scout slightly smaller in appearance
        transform.localScale = transform.localScale * 0.85f;
    }
    
    public override void Init()
    {
        // Call base initialization
        base.Init();
        
        // Scout-specific modifications
        
        // Scouts are faster but have less health
        Speed *= 2.0f; // Double speed
        MaxHealth *= 0.6f; // 60% of normal health
        Health = MaxHealth; // Set current health to match
        
        // Scouts deal less damage to the base
        DamageToBase = Mathf.Max(1, DamageToBase - 1); // Reduce by 1, minimum 1
        
        // Update health bar to reflect new max health
        if (HPBar != null)
        {
            HPBar.UpdateHealthBar(MaxHP, CurrentHP);
        }
        
        Debug.Log($"Scout initialized: Health={Health}, Speed={Speed}");
    }
    
    // Override ProcessDamage to add evasion chance
    protected override void ProcessDamage(int damage)
    {
        // Check if scout evades the damage
        if (Random.value < evadeChance)
        {
            // Scout successfully evaded
            Debug.Log($"Scout {gameObject.name} evaded {damage} damage!");
            
            // Show evasion effect
            StartCoroutine(ShowEvadeEffect());
            
            // Don't call base.ProcessDamage since we're evading
            return;
        }
        
        // Didn't evade, so process damage normally
        base.ProcessDamage(damage);
    }
    
    private IEnumerator ShowEvadeEffect()
    {
        // Visual feedback for evading
        if (myRenderer != null)
        {
            // Flash the scout white briefly
            myRenderer.material.color = Color.white;
            
            // Instantiate evasion effect if provided
            if (evadeEffectPrefab != null)
            {
                Instantiate(evadeEffectPrefab, transform.position, Quaternion.identity);
            }
            
            yield return new WaitForSeconds(0.15f);
            
            // Restore original color
            myRenderer.material.color = scoutColor;
        }
        else
        {
            yield return null;
        }
    }
    
    public override void ResetEnemy()
    {
        base.ResetEnemy();
        
        // Additional reset logic for scout
        if (speedTrail != null)
        {
            speedTrail.Clear();
            speedTrail.enabled = true;
        }
    }
}