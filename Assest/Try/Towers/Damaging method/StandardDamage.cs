using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDamageMethod
{
    void DamageTick(Enemy Target);
    void Init(float Damage, float FireRate);
}

public class StandardDamage : MonoBehaviour, IDamageMethod
{
    [Header("Visual & Audio Effects")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private Transform muzzlePoint;
    
    [Header("Projectile Settings (Optional)")]
    [SerializeField] private bool useProjectile = false;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 20f;
    
    private float damage;
    private float fireRate;
    private float delay;
    private AudioSource audioSource;
    private TowerBehaviour towerBehaviour;
    
    private void Awake()
    {
        towerBehaviour = GetComponent<TowerBehaviour>();
        
        // Set up audio source if needed
        if (fireSound != null && GetComponent<AudioSource>() == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.playOnAwake = false;
        }
        else
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        // Create a muzzle point if none is set and we're using projectiles
        if (muzzlePoint == null)
        {
            if (towerBehaviour != null && towerBehaviour.TowerPivot != null)
            {
                // Try to find an existing muzzle point
                muzzlePoint = towerBehaviour.TowerPivot.Find("MuzzlePoint");
                
                // If not found, create one
                if (muzzlePoint == null && (useProjectile || muzzleFlashPrefab != null))
                {
                    muzzlePoint = new GameObject("MuzzlePoint").transform;
                    muzzlePoint.SetParent(towerBehaviour.TowerPivot);
                    muzzlePoint.localPosition = new Vector3(0, 0.5f, 1f); // Adjust this position as needed
                }
            }
        }
    }

    public void Init(float damage, float fireRate)
    {
        this.damage = damage;
        this.fireRate = fireRate;
        this.delay = 1f / fireRate;
    }
    
    public void DamageTick(Enemy target)
    {
        if (target == null) return;
        
        if (delay > 0f)
        {
            delay -= Time.deltaTime;
            return;
        }
        
        // Reset firing delay
        delay = 1f / fireRate;
        
        // Handle projectile-based or direct damage
        if (useProjectile && projectilePrefab != null)
        {
            FireProjectile(target);
        }
        else
        {
            // Direct damage (original behavior)
            ApplyDamage(target);
        }
        
        // Play effects
        PlayFireEffects();
    }
    
    private void ApplyDamage(Enemy target)
    {
        int damageAmount = Mathf.RoundToInt(damage);
        target.TakeDamage(damageAmount);
        
        // For statistics tracking
        if (towerBehaviour != null)
        {
            GameloopManager.EnqueueDamageData(new GameData.EnemyDamageData(
                target, 
                damage, 
                target.DamageResistance,
                towerBehaviour.ownerPathIndex
            ));
        }
        else
        {
            GameloopManager.EnqueueDamageData(new GameData.EnemyDamageData(
                target, 
                damage, 
                target.DamageResistance
            ));
        }
    }
    
    private void FireProjectile(Enemy target)
    {
        if (muzzlePoint == null) muzzlePoint = transform;
        
        // Instantiate projectile
        GameObject projectileObj = Instantiate(projectilePrefab, muzzlePoint.position, muzzlePoint.rotation);
        StandardProjectile projectile = projectileObj.GetComponent<StandardProjectile>();
        
        // Setup projectile
        if (projectile == null)
        {
            projectile = projectileObj.AddComponent<StandardProjectile>();
        }
        
        projectile.Setup(target, damage, projectileSpeed, towerBehaviour?.ownerPathIndex ?? 0);
    }
    
    private void PlayFireEffects()
    {
        // Play sound
        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound);
        }
        
        // Show muzzle flash
        if (muzzleFlashPrefab != null && muzzlePoint != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation);
            Destroy(flash, 2f); // Auto-destroy after 2 seconds
        }
    }
}

// Simple projectile class - you can put this in a separate file if preferred
public class StandardProjectile : MonoBehaviour
{
    private Enemy target;
    private float damage;
    private float speed;
    private int ownerPathIndex;
    private bool hasHit = false;
    
    public void Setup(Enemy target, float damage, float speed, int ownerPathIndex)
    {
        this.target = target;
        this.damage = damage;
        this.speed = speed;
        this.ownerPathIndex = ownerPathIndex;
        
        // Safety - destroy after 5 seconds even if it doesn't hit
        Destroy(gameObject, 5f);
    }
    
    private void Update()
    {
        if (hasHit || target == null)
        {
            Destroy(gameObject);
            return;
        }
        
        // Move towards target
        Vector3 targetPosition = target.transform.position;
        Vector3 direction = (targetPosition - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction);
        
        // Simple hit detection based on distance
        float hitDistance = 0.5f; // Adjust based on enemy size
        if (Vector3.Distance(transform.position, targetPosition) < hitDistance)
        {
            HitTarget();
        }
    }
    
    // Optional - add collider-based hit detection
    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null && enemy == target)
        {
            HitTarget();
        }
    }
    
    private void HitTarget()
    {
        if (hasHit) return;
        hasHit = true;
        
        // Apply damage
        target.TakeDamage(Mathf.RoundToInt(damage));
        
        // Report damage for statistics
        GameloopManager.EnqueueDamageData(new GameData.EnemyDamageData(
            target, 
            damage, 
            target.DamageResistance,
            ownerPathIndex
        ));
        
        // Optional: impact effect
        // Instantiate(impactEffect, transform.position, Quaternion.identity);
        
        Destroy(gameObject);
    }
}