using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireTriggerManager : MonoBehaviour
{
    [SerializeField] public FlamethrowerDamage BaseClass;
    
    private float effectRefreshTimer = 0f;
    private const float EFFECT_REFRESH_RATE = 0.5f; // Refresh effect every 0.5 seconds
    private Dictionary<Enemy, float> enemyDamageCooldowns = new Dictionary<Enemy, float>();
    
    private void Start()
    {
        // If BaseClass is not set, try to find it
        if (BaseClass == null)
        {
            BaseClass = GetComponentInParent<FlamethrowerDamage>();
            if (BaseClass == null)
            {
                Debug.LogError("FireTriggerManager: No FlamethrowerDamage component found!");
                enabled = false;
            }
        }
    }
    
    private void Update()
    {
        // Update cooldowns
        List<Enemy> enemiesToRemove = new List<Enemy>();
        
        foreach (var kvp in enemyDamageCooldowns)
        {
            Enemy enemy = kvp.Key;
            float cooldown = kvp.Value - Time.deltaTime;
            
            if (cooldown <= 0f || enemy == null)
            {
                enemiesToRemove.Add(enemy);
            }
            else
            {
                enemyDamageCooldowns[enemy] = cooldown;
            }
        }
        
        // Clean up destroyed enemies or those no longer in range
        foreach (Enemy enemy in enemiesToRemove)
        {
            enemyDamageCooldowns.Remove(enemy);
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Enemy") && BaseClass != null)
        {
            // Get the parent transform
            Transform parentTransform = other.transform.parent;
            
            if (parentTransform != null && Entitysummoner.Enemytransformpairs.TryGetValue(parentTransform, out Enemy enemyHit))
            {
                // Check if this enemy is on cooldown
                if (enemyDamageCooldowns.TryGetValue(enemyHit, out float cooldown) && cooldown > 0f)
                {
                    return; // Skip this enemy, still on cooldown
                }
                
                // Apply damage
                enemyHit.TakeDamage(Mathf.RoundToInt(BaseClass.damage));
                
                // Get owner path index
                int ownerPathIndex = BaseClass.GetOwnerPathIndex();
                
                // Queue damage data with owner information
                GameloopManager.EnqueueDamageData(new GameData.EnemyDamageData(
                    enemyHit, 
                    BaseClass.damage, 
                    enemyHit.DamageResistance,
                    ownerPathIndex
                ));
                
                // Apply flame effect
                Effects flameEffect = new Effects("Flame", BaseClass.damage, BaseClass.fireRate, BaseClass.GetEffectDuration());
                GameData.ApplyEffectData effectData = new GameData.ApplyEffectData(enemyHit, flameEffect);
                GameloopManager.EnqueueEffectData(effectData);
                
                // Set cooldown for this enemy
                enemyDamageCooldowns[enemyHit] = EFFECT_REFRESH_RATE;
                
                // Visual feedback for hit (optional)
                CreateHitFeedback(other.transform.position);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Enemy"))
        {
            Transform parentTransform = other.transform.parent;
            
            if (parentTransform != null && Entitysummoner.Enemytransformpairs.TryGetValue(parentTransform, out Enemy enemyHit))
            {
                // Remove enemy from cooldowns when it exits
                enemyDamageCooldowns.Remove(enemyHit);
            }
        }
    }
    
    private void CreateHitFeedback(Vector3 position)
    {
        // Optional: Create a small particle effect at hit point
        // Could be flames, smoke, or an impact effect
    }
}