using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlamethrowerDamage : MonoBehaviour, IDamageMethod
{
    [Header("Flamethrower Setup")]
    [SerializeField] private Collider fireTrigger;
    [SerializeField] private ParticleSystem fireSystem;
    [SerializeField] private float flameRange = 5f;
    [SerializeField] private float flameWidth = 2f;
    
    [Header("Visual Effects")]
    [SerializeField] private Light fireLight;
    [SerializeField] private Color fireColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private float fireSoundVolume = 0.7f;
    
    [Header("Effect Settings")]
    [SerializeField] private float effectDuration = 5f;

    [HideInInspector] public float damage;
    [HideInInspector] public float fireRate;
    
    private AudioSource audioSource;
    private TowerBehaviour towerBehaviour;
    private Transform towerPivot;
    private bool isActive = false;

    private void Awake()
    {
        towerBehaviour = GetComponent<TowerBehaviour>();
        
        // Set up fire trigger if not assigned
        if (fireTrigger == null)
        {
            GameObject triggerObj = new GameObject("FireTrigger");
            triggerObj.transform.parent = transform;
            triggerObj.transform.localPosition = Vector3.forward * flameRange/2;
            
            BoxCollider boxCollider = triggerObj.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(flameWidth, flameWidth, flameRange);
            boxCollider.center = new Vector3(0, 0, flameRange/2);
            
            fireTrigger = boxCollider;
            
            // Add the trigger manager
            FireTriggerManager triggerManager = triggerObj.AddComponent<FireTriggerManager>();
            triggerManager.BaseClass = this;
        }
        
        // Get tower pivot
        if (towerBehaviour != null)
        {
            towerPivot = towerBehaviour.TowerPivot;
        }
        
        // Setup fire system if not assigned
        if (fireSystem == null)
        {
            GameObject fireObj = new GameObject("FireSystem");
            fireObj.transform.parent = towerPivot != null ? towerPivot : transform;
            fireObj.transform.localPosition = Vector3.forward * 0.5f;
            fireObj.transform.localRotation = Quaternion.identity;
            
            fireSystem = fireObj.AddComponent<ParticleSystem>();
            
            // Set up a basic fire particle system
            var main = fireSystem.main;
            main.startColor = fireColor;
            main.startSpeed = 10f;
            main.startSize = 0.5f;
            main.duration = 1f;
            main.loop = true;
            
            var emission = fireSystem.emission;
            emission.rateOverTime = 50f;
            
            var shape = fireSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.1f;
            
            var colorOverLifetime = fireSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            
            // Create a gradient for the color over lifetime
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(fireColor, 0.0f), 
                    new GradientColorKey(new Color(fireColor.r, fireColor.g, 0f, 0.5f), 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f), 
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            colorOverLifetime.color = gradient;
        }
        
        // Setup audio source if needed
        if (fireSound != null && GetComponent<AudioSource>() == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.loop = true;
            audioSource.volume = fireSoundVolume;
            audioSource.clip = fireSound;
        }
        else
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        // Setup light if needed
        if (fireLight == null && fireSystem != null)
        {
            GameObject lightObj = new GameObject("FireLight");
            lightObj.transform.parent = fireSystem.transform;
            lightObj.transform.localPosition = Vector3.zero;
            
            fireLight = lightObj.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = fireColor;
            fireLight.range = flameRange;
            fireLight.intensity = 2f;
        }
        
        // Initially disable the trigger
        if (fireTrigger != null)
        {
            fireTrigger.enabled = false;
        }
    }

    public void Init(float damage, float fireRate)
    {
        this.damage = damage;
        this.fireRate = fireRate;
    }
    
    public void DamageTick(Enemy target)
    {
        bool shouldBeActive = target != null;
        
        // Only process state changes
        if (isActive != shouldBeActive)
        {
            isActive = shouldBeActive;
            
            // Update trigger state
            if (fireTrigger != null)
            {
                fireTrigger.enabled = isActive;
            }
            
            // Update particle system
            if (fireSystem != null)
            {
                if (isActive && !fireSystem.isPlaying)
                {
                    fireSystem.Play();
                    
                    // Play sound
                    if (audioSource != null && fireSound != null && !audioSource.isPlaying)
                    {
                        audioSource.Play();
                    }
                    
                    // Enable light
                    if (fireLight != null)
                    {
                        fireLight.enabled = true;
                    }
                }
                else if (!isActive && fireSystem.isPlaying)
                {
                    fireSystem.Stop();
                    
                    // Stop sound
                    if (audioSource != null && audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                    
                    // Disable light
                    if (fireLight != null)
                    {
                        fireLight.enabled = false;
                    }
                }
            }
        }
        
        // Rotate flamethrower towards target if we have a target
        if (target != null && towerPivot != null)
        {
            Vector3 direction = (target.transform.position - towerPivot.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            towerPivot.rotation = Quaternion.Slerp(towerPivot.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
    
    public float GetEffectDuration()
    {
        return effectDuration;
    }
    
    public int GetOwnerPathIndex()
    {
        return towerBehaviour != null ? towerBehaviour.ownerPathIndex : 0;
    }
    
    private void OnDisable()
    {
        // Ensure effects are stopped
        if (fireSystem != null && fireSystem.isPlaying)
        {
            fireSystem.Stop();
        }
        
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        if (fireLight != null)
        {
            fireLight.enabled = false;
        }
        
        if (fireTrigger != null)
        {
            fireTrigger.enabled = false;
        }
    }
}