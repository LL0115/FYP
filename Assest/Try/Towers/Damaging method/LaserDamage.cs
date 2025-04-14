using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserDamage : MonoBehaviour, IDamageMethod
{
    [Header("Laser Setup")]
    [SerializeField] private Transform laserPivot;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float laserWidth = 0.1f;
    [SerializeField] private Material laserMaterial;
    [SerializeField] private Color laserColor = Color.red;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private Light laserLight;
    [SerializeField] private AudioClip laserSound;
    [SerializeField] private bool pulseEffect = true;
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float pulseIntensity = 0.2f;

    private float damage;
    private float fireRate;
    private float delay;
    private AudioSource audioSource;
    private TowerBehaviour towerBehaviour;
    private GameObject currentImpactEffect;
    private float pulseValue;

    private void Awake()
    {
        towerBehaviour = GetComponent<TowerBehaviour>();
        
        // If no line renderer is assigned, create one
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            SetupLineRenderer();
        }
        
        // If no laser pivot is assigned, use the tower pivot
        if (laserPivot == null && towerBehaviour != null)
        {
            laserPivot = towerBehaviour.TowerPivot;
        }
        
        // Setup audio source
        if (laserSound != null && GetComponent<AudioSource>() == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.playOnAwake = false;
            audioSource.loop = true;
        }
        else
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        // Setup light source if needed
        if (laserLight == null)
        {
            GameObject lightObj = new GameObject("LaserLight");
            lightObj.transform.parent = laserPivot ? laserPivot : transform;
            lightObj.transform.localPosition = Vector3.zero;
            
            laserLight = lightObj.AddComponent<Light>();
            laserLight.type = LightType.Point;
            laserLight.color = laserColor;
            laserLight.range = 5f;
            laserLight.intensity = 2f;
            laserLight.enabled = false;
        }
        
        // Initialize line renderer
        SetupLineRenderer();
    }
    
    private void SetupLineRenderer()
    {
        if (lineRenderer == null) return;
        
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth * 0.8f;
        
        if (laserMaterial != null)
        {
            lineRenderer.material = laserMaterial;
        }
        else
        {
            // Set default material properties if no material is provided
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
        
        lineRenderer.startColor = laserColor;
        lineRenderer.endColor = laserColor;
        lineRenderer.enabled = false;
    }

    public void Init(float damage, float fireRate)
    {
        this.damage = damage;
        this.fireRate = fireRate;
        this.delay = 1f / fireRate;
    }

    public void DamageTick(Enemy target)
    {
        if (target != null && target.RootPart != null)
        {
            // Enable laser visual
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, laserPivot ? laserPivot.position : transform.position);
            lineRenderer.SetPosition(1, target.RootPart.position);
            
            // Enable light
            if (laserLight != null)
            {
                laserLight.enabled = true;
                laserLight.transform.position = target.RootPart.position;
                
                // Pulse effect
                if (pulseEffect)
                {
                    pulseValue = (Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity) + 1f;
                    laserLight.intensity = 2f * pulseValue;
                    lineRenderer.startWidth = laserWidth * pulseValue;
                    lineRenderer.endWidth = laserWidth * 0.8f * pulseValue;
                }
            }
            
            // Play sound
            if (audioSource != null && laserSound != null && !audioSource.isPlaying)
            {
                audioSource.clip = laserSound;
                audioSource.Play();
            }
            
            // Handle impact effect
            UpdateImpactEffect(target.RootPart.position);

            // Apply damage on interval
            if (delay > 0f)
            {
                delay -= Time.deltaTime;
                return;
            }
            
            // Apply damage and send damage data with owner information
            target.TakeDamage(Mathf.RoundToInt(damage));
            
            int ownerPathIndex = towerBehaviour != null ? towerBehaviour.ownerPathIndex : 0;
            
            GameloopManager.EnqueueDamageData(new GameData.EnemyDamageData(
                target, 
                damage, 
                target.DamageResistance,
                ownerPathIndex
            ));
            
            // Reset delay
            delay = 1f / fireRate;
            return;
        }

        // No target, disable visual effects
        lineRenderer.enabled = false;
        
        if (laserLight != null)
        {
            laserLight.enabled = false;
        }
        
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        // Disable impact effect
        if (currentImpactEffect != null)
        {
            currentImpactEffect.SetActive(false);
        }
    }
    
    private void UpdateImpactEffect(Vector3 impactPosition)
    {
        if (impactEffectPrefab != null)
        {
            // Create impact effect if it doesn't exist
            if (currentImpactEffect == null)
            {
                currentImpactEffect = Instantiate(impactEffectPrefab, impactPosition, Quaternion.identity);
                currentImpactEffect.transform.parent = transform;
            }
            
            // Update impact effect position
            currentImpactEffect.transform.position = impactPosition;
            currentImpactEffect.SetActive(true);
        }
    }
    
    private void OnDisable()
    {
        // Clean up effects
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        
        if (laserLight != null)
        {
            laserLight.enabled = false;
        }
        
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        if (currentImpactEffect != null)
        {
            currentImpactEffect.SetActive(false);
        }
    }
}