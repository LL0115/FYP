using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class HealerEnemy : Enemy
{
    [SerializeField] private float healRadius = 5.0f;
    [SerializeField] private int healAmount = 10;
    [SerializeField] private float healCooldown = 3.0f;
    [SerializeField] private Color healerColor = new Color(0.5f, 1f, 0.5f); // Light green
    [SerializeField] private GameObject healEffectPrefab;
    [SerializeField] private GameObject aoeHealEffectPrefab;
    [SerializeField] private LayerMask enemyLayerMask;
    
    private Renderer myRenderer;
    private bool canHeal = true;
    private float nextHealTime = 0f;
    
    protected override void Awake()
    {
        // Call base class Awake first
        base.Awake();
        
        // Healer-specific setup
        myRenderer = GetComponentInChildren<Renderer>();
        
        if (myRenderer != null && myRenderer.material != null)
        {
            myRenderer.material.color = healerColor;
        }
        
        // Make healer a bit different in appearance
        transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
    }
    
    public override void Init()
    {
        // Call base initialization
        base.Init();
        
        // Healer-specific modifications
        MaxHealth *= 0.6f; // 60% of normal health
        Health = MaxHealth; // Set current health to match
        
        if (PhotonNetwork.IsConnected && photonView != null && PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_SyncMaxHealth", RpcTarget.Others, MaxHealth);
        }
        
        // Reset healing timer
        nextHealTime = Time.time + 1.0f; // Initial delay before first heal
        
        // Update health bar to reflect new max health
        if (HPBar != null)
        {
            HPBar.UpdateHealthBar(MaxHP, CurrentHP);
        }
        
        Debug.Log($"Healer initialized: Health={Health}, Speed={Speed}");
    }
    
    private void Update()
    {
        // Only master client handles healing logic in multiplayer
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            // Try healing nearby enemies when timer is up
            if (Time.time >= nextHealTime)
            {
                TryHealNearbyEnemies();
                nextHealTime = Time.time + healCooldown;
            }
        }
    }
    
    private void TryHealNearbyEnemies()
    {
        // Find nearby enemies
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, healRadius, enemyLayerMask);
        List<Enemy> nearbyWoundedEnemies = new List<Enemy>();
        
        // Check which ones need healing
        foreach (Collider col in nearbyColliders)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            
            // Skip ourselves and fully healed enemies
            if (enemy != null && enemy != this && enemy.Health < enemy.MaxHealth)
            {
                nearbyWoundedEnemies.Add(enemy);
            }
        }
        
        // If there are wounded enemies, heal them
        if (nearbyWoundedEnemies.Count > 0)
        {
            StartCoroutine(HealNearbyEnemies(nearbyWoundedEnemies));
        }
    }
    
    private IEnumerator HealNearbyEnemies(List<Enemy> enemiesToHeal)
    {
        // Visual effect for healer
        ShowHealingEffect(true);
        
        // If this is a networked game, sync the effect to all clients
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            photonView.RPC("RPC_StartHealEffect", RpcTarget.Others);
        }
        
        // Spawn area effect
        if (aoeHealEffectPrefab != null)
        {
            GameObject aoeEffect = Instantiate(aoeHealEffectPrefab, transform.position, Quaternion.identity);
            Destroy(aoeEffect, 2f); // Clean up after 2 seconds
        }
        
        // Heal each enemy
        foreach (Enemy enemy in enemiesToHeal)
        {
            if (enemy != null)
            {
                int actualHealAmount = Mathf.Min(healAmount, Mathf.RoundToInt(enemy.MaxHealth - enemy.Health));
                enemy.Health += actualHealAmount;
                
                // Update the enemy's health bar
                if (enemy.HPBar != null)
                {
                    enemy.HPBar.UpdateHealthBar(enemy.MaxHP, enemy.CurrentHP);
                }
                
                // Spawn heal effect at target
                if (healEffectPrefab != null)
                {
                    GameObject effect = Instantiate(healEffectPrefab, enemy.transform.position, Quaternion.identity);
                    Destroy(effect, 2f); // Clean up after 2 seconds
                }
                
                // If this is a networked game, sync the target heal effect to all clients
                if (PhotonNetwork.IsConnected && photonView != null && enemy.photonView != null)
                {
                    photonView.RPC("RPC_ShowTargetHealEffect", RpcTarget.Others, 
                        enemy.transform.position.x, 
                        enemy.transform.position.y, 
                        enemy.transform.position.z,
                        enemy.photonView.ViewID);
                }
            }
            
            yield return new WaitForSeconds(0.1f); // Small delay between heals
        }
        
        // Return to normal color
        ShowHealingEffect(false);
        
        // If this is a networked game, sync the effect end to all clients
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            photonView.RPC("RPC_EndHealEffect", RpcTarget.Others);
        }
    }
    
    [PunRPC]
    private void RPC_StartHealEffect()
    {
        // Visual effects for clients
        ShowHealingEffect(true);
        
        // Spawn area effect
        if (aoeHealEffectPrefab != null)
        {
            GameObject aoeEffect = Instantiate(aoeHealEffectPrefab, transform.position, Quaternion.identity);
            Destroy(aoeEffect, 2f); // Clean up after 2 seconds
        }
    }
    
    [PunRPC]
    private void RPC_EndHealEffect()
    {
        // End visual effects for clients
        ShowHealingEffect(false);
    }
    
    [PunRPC]
    private void RPC_ShowTargetHealEffect(float x, float y, float z, int targetViewID)
    {
        // Show target heal effect at position
        Vector3 position = new Vector3(x, y, z);
        if (healEffectPrefab != null)
        {
            GameObject effect = Instantiate(healEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 2f); // Clean up after 2 seconds
        }
        
        // Update UI for the healed enemy
        if (PhotonNetwork.IsConnected)
        {
            PhotonView targetView = PhotonView.Find(targetViewID);
            if (targetView != null)
            {
                Enemy targetEnemy = targetView.GetComponent<Enemy>();
                if (targetEnemy != null && targetEnemy.HPBar != null)
                {
                    targetEnemy.HPBar.UpdateHealthBar(targetEnemy.MaxHP, targetEnemy.CurrentHP);
                }
            }
        }
    }
    
    private void ShowHealingEffect(bool active)
    {
        // Update renderer color
        if (myRenderer != null)
        {
            myRenderer.material.color = active ? Color.green : healerColor;
        }
    }
    
    public override void ResetEnemy()
    {
        base.ResetEnemy();
        
        // Additional reset logic for healer
        if (myRenderer != null)
        {
            myRenderer.material.color = healerColor;
        }
        
        // Reset healing timer
        nextHealTime = Time.time + 1.0f;
    }
    
    // Visual debugging - show heal radius
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healRadius);
    }
}