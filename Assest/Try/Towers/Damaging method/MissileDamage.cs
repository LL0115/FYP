using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileDamage : MonoBehaviour, IDamageMethod
{
    [Header("Missile Configuration")]
    [SerializeField] private Transform towerHead;
    [SerializeField] private GameObject missilePrefab;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private AudioClip launchSound;
    [SerializeField] private GameObject launchEffectPrefab;
    
    [Header("Targeting")]
    [SerializeField] public LayerMask enemyLayer;
    [SerializeField] private float missileLaunchAngleThreshold = 10f;
    
    private float delay;
    private float damage;
    private float fireRate;
    private AudioSource audioSource;
    private TowerBehaviour towerBehaviour;
    private MissileCollisionManager collisionManager;

    private void Awake()
    {
        // Get necessary components
        towerBehaviour = GetComponent<TowerBehaviour>();
        collisionManager = GetComponent<MissileCollisionManager>();
        
        // Set up audio source if needed
        if (launchSound != null && GetComponent<AudioSource>() == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.playOnAwake = false;
        }
        else
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        // Error checking
        if (collisionManager == null)
        {
            Debug.LogError("MissileCollisionManager component is missing!");
        }
        
        // Use tower pivot if towerHead is not set
        if (towerHead == null && towerBehaviour != null)
        {
            towerHead = towerBehaviour.TowerPivot;
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

        // Handle firing delay
        if (delay > 0f)
        {
            delay -= Time.deltaTime;
            return;
        }

        // Get the tower head if it's not set
        if (towerHead == null)
        {
            if (towerBehaviour != null && towerBehaviour.TowerPivot != null)
            {
                towerHead = towerBehaviour.TowerPivot;
            }
            else
            {
                towerHead = transform;
            }
        }

        // Rotate towards target
        Vector3 direction = (target.transform.position - towerHead.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        towerHead.rotation = Quaternion.RotateTowards(towerHead.rotation, lookRotation, Time.deltaTime * rotationSpeed);

        // Check if we're facing the target closely enough to fire
        float angleToTarget = Quaternion.Angle(towerHead.rotation, lookRotation);
        if (angleToTarget < missileLaunchAngleThreshold)
        {
            FireMissile(target);
            delay = 1f / fireRate;
        }
    }

    private void FireMissile(Enemy target)
    {
        // Calculate spawn position in front of the tower
        Vector3 spawnPosition = towerHead.position + towerHead.forward * 1.5f;
        
        // Calculate initial direction to target
        Vector3 directionToTarget = (target.transform.position - spawnPosition).normalized;
        
        // Create rotation that looks at target with 90-degree offset for upward-facing missile
        Quaternion initialRotation = Quaternion.LookRotation(directionToTarget) * Quaternion.Euler(90f, 0f, 0f);
        
        // Instantiate the missile
        GameObject missile = Instantiate(missilePrefab, spawnPosition, initialRotation);
        HomingMissile homingMissile = missile.GetComponent<HomingMissile>();
        if (homingMissile == null)
        {
            homingMissile = missile.AddComponent<HomingMissile>();
        }
        
        // Pass owner information to the missile
        int ownerPathIndex = towerBehaviour != null ? towerBehaviour.ownerPathIndex : 0;
        homingMissile.Initialize(target, damage, this, collisionManager, ownerPathIndex);
        
        // Play launch effects
        PlayLaunchEffects(spawnPosition);
    }
    
    private void PlayLaunchEffects(Vector3 position)
    {
        // Play sound
        if (audioSource != null && launchSound != null)
        {
            audioSource.PlayOneShot(launchSound);
        }
        
        // Show launch effect
        if (launchEffectPrefab != null)
        {
            GameObject effect = Instantiate(launchEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
}