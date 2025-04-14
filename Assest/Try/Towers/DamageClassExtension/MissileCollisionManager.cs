using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileCollisionManager : MonoBehaviour
{
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private ParticleSystem explosionSystem;
    [SerializeField] private AudioClip explosionSound;
    [SerializeField] private bool showExplosionRadius = false;
    
    // Add a public layer mask in case MissileDamage changes
    [SerializeField] public LayerMask enemyLayer;
    
    private MissileDamage missileDamage;
    private TowerBehaviour towerBehaviour;
    private AudioSource audioSource;

    private void Awake()
    {
        missileDamage = GetComponent<MissileDamage>();
        towerBehaviour = GetComponent<TowerBehaviour>();
        
        if (missileDamage == null)
        {
            Debug.LogError("MissileDamage component is missing!");
        }
        else
        {
            // Get the enemy layer from MissileDamage if it's not set
            if (enemyLayer.value == 0)
            {
                enemyLayer = missileDamage.enemyLayer;
            }
        }
        
        // Setup audio source for explosion sounds
        if (explosionSound != null && GetComponent<AudioSource>() == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.playOnAwake = false;
        }
        else
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnDrawGizmos()
    {
        if (showExplosionRadius)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }

    public void HandleMissileExplosion(Vector3 explosionPoint, float damage, int ownerPathIndex = 0)
    {
        // Use the tower's owner if not specified
        if (ownerPathIndex == 0 && towerBehaviour != null)
        {
            ownerPathIndex = towerBehaviour.ownerPathIndex;
        }
        
        // Play explosion effect
        if (explosionSystem != null)
        {
            var explosion = Instantiate(explosionSystem, explosionPoint, Quaternion.identity);
            explosion.Play();
            Destroy(explosion.gameObject, explosion.main.duration);
        }
        
        // Play explosion sound
        if (audioSource != null && explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, explosionPoint, 1.0f);
        }

        Debug.Log($"Current dictionary count: {Entitysummoner.Enemytransformpairs.Count}");
        
        // Use this class's enemyLayer instead of missileDamage.EnemyLayer
        Collider[] enemiesInRadius = Physics.OverlapSphere(explosionPoint, explosionRadius, enemyLayer);
        
        foreach (var enemyCollider in enemiesInRadius)
        {
            // Get the parent transform (Basic(Clone))
            Transform parentTransform = enemyCollider.transform.parent;
            
            if (parentTransform != null && Entitysummoner.Enemytransformpairs.TryGetValue(parentTransform, out Enemy enemyHit))
            {
                float distance = Vector3.Distance(explosionPoint, enemyCollider.transform.position);
                float damageFalloff = 1f - (distance / explosionRadius);
                float finalDamage = damage * Mathf.Max(damageFalloff, 0.5f);
                
                // Apply damage to enemy
                enemyHit.TakeDamage(Mathf.RoundToInt(finalDamage));

                // Create damage data with owner information
                GameData.EnemyDamageData damageData = new GameData.EnemyDamageData(
                    enemyHit,
                    finalDamage,
                    enemyHit.DamageResistance,
                    ownerPathIndex // Add owner path index here
                );

                GameloopManager.EnqueueDamageData(damageData);
                Debug.Log($"Successfully queued damage: {finalDamage} to enemy: {enemyHit.name} from owner: {ownerPathIndex}");
            }
            else
            {
                Debug.LogError($"Failed to find enemy in dictionary. Collider: {enemyCollider.name}, Parent: {(parentTransform != null ? parentTransform.name : "null")}");
            }
        }
    }
}

public class HomingMissile : MonoBehaviour
{
    private Enemy target;
    private float damage;
    private MissileDamage missileSystem;
    private MissileCollisionManager collisionManager;
    private int ownerPathIndex = 0;
    
    [SerializeField] private float speed = 15f;
    [SerializeField] private float rotateSpeed = 200f;
    [SerializeField] private GameObject missileModel;
    [SerializeField] private float missileLifetime = 5f;
    [SerializeField] private float targetProximityThreshold = 0.5f;
    
    private TrailRenderer trailRenderer;
    private bool isInitialized = false;
    private bool hasExploded = false;

    public void Initialize(Enemy target, float damage, MissileDamage missileSystem, MissileCollisionManager collisionManager, int ownerPathIndex = 0)
    {
        this.target = target;
        this.damage = damage;
        this.missileSystem = missileSystem;
        this.collisionManager = collisionManager;
        this.ownerPathIndex = ownerPathIndex;
        isInitialized = true;

        // Since the missile's forward is up, we need to rotate it to face the target initially
        Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(directionToTarget) * Quaternion.Euler(90f, 0f, 0f);

        if (missileModel != null)
        {
            missileModel.transform.localRotation = Quaternion.identity;
        }

        SetupTrailRenderer();
        Destroy(gameObject, missileLifetime);
    }

    private void SetupTrailRenderer()
    {
        trailRenderer = GetComponent<TrailRenderer>();
        if (trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
            trailRenderer.startWidth = 0.2f;
            trailRenderer.endWidth = 0.1f;
            trailRenderer.time = 0.3f;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.yellow, 0.0f), 
                    new GradientColorKey(Color.red, 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f), 
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            trailRenderer.colorGradient = gradient;
        }
    }

    private void Update()
    {
        if (hasExploded) return;
        
        if (!isInitialized || target == null)
        {
            Explode();
            return;
        }

        Vector3 targetDirection = (target.transform.position - transform.position).normalized;
        
        // Calculate rotation with 90-degree offset since missile's forward is up
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection) * Quaternion.Euler(90f, 0f, 0f);
        
        // Smoothly rotate towards target
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        
        // Move using up direction since that's our missile's forward
        transform.Translate(Vector3.up * speed * Time.deltaTime);

        float distanceToTarget = Vector3.Distance(transform.position, target.transform.position);
        if (distanceToTarget < targetProximityThreshold)
        {
            Explode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasExploded) return;
        
        if (other.gameObject.layer == LayerMask.NameToLayer("Enemy") || 
            (target != null && other.gameObject == target.gameObject))
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        
        if (collisionManager != null)
        {
            // Optional: Debug log to verify explosion
            Debug.Log($"Missile exploding with damage: {damage} from owner: {ownerPathIndex}");
            collisionManager.HandleMissileExplosion(transform.position, damage, ownerPathIndex);
        }
        else
        {
            Debug.LogError("CollisionManager is null during explosion!");
        }
        
        Destroy(gameObject);
    }
    
    // If target is destroyed before missile hits, this will catch it
    private void FixedUpdate()
    {
        if (target == null && isInitialized && !hasExploded)
        {
            Explode();
        }
    }
}