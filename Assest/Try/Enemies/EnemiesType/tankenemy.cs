using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class TankEnemy : Enemy
{
    [SerializeField] private float damageResistance = 0.5f; // Takes 50% less damage
    [SerializeField] private Color tankColor = Color.red;
    
    private Renderer myRenderer;
    
    protected override void Awake()
    {
        // Call base class Awake first
        base.Awake();
        
        // Tank-specific setup
        myRenderer = GetComponentInChildren<Renderer>();
        
        if (myRenderer != null && myRenderer.material != null)
        {
            myRenderer.material.color = tankColor;
        }
        
        // Make tank larger in appearance
        transform.localScale = transform.localScale * 1.3f;
    }
    
    public override void Init()
    {
        // Call base initialization
        base.Init();
        
        // Tank-specific modifications
        Speed *= 0.6f; // 60% of normal speed
        MaxHealth *= 2.5f; // 250% of normal health
        Health = MaxHealth; // Set current health to match
        
        // Tanks deal more damage to the base
        DamageToBase += 2; // Increase by 2
        
        // Set damage resistance
        DamageResistance = 1.0f - damageResistance;
        
        // Update health bar to reflect new max health
        if (HPBar != null)
        {
            HPBar.UpdateHealthBar(MaxHP, CurrentHP);
        }
        
        Debug.Log($"Tank initialized: Health={Health}, Speed={Speed}");
    }
    
    // Override ProcessDamage to add damage resistance
    protected override void ProcessDamage(int damage)
    {
        // Apply damage resistance (less damage taken)
        int reducedDamage = Mathf.RoundToInt(damage * (1f - damageResistance));
        
        // Show simple visual feedback for damage resistance
        if (damage > 0 && reducedDamage < damage && myRenderer != null)
        {
            StartCoroutine(FlashColor(Color.blue, 0.15f));
            
            // If this is a networked game, sync the effect to all clients
            if (PhotonNetwork.IsConnected && photonView != null)
            {
                photonView.RPC("RPC_FlashColor", RpcTarget.Others);
            }
        }
        
        // Process the reduced damage
        base.ProcessDamage(reducedDamage);
    }
    
    [PunRPC]
    private void RPC_FlashColor()
    {
        StartCoroutine(FlashColor(Color.blue, 0.15f));
    }
    
    private IEnumerator FlashColor(Color flashColor, float duration)
    {
        if (myRenderer != null && myRenderer.material != null)
        {
            // Flash the tank color briefly
            myRenderer.material.color = flashColor;
            
            yield return new WaitForSeconds(duration);
            
            // Restore original color
            myRenderer.material.color = tankColor;
        }
        else
        {
            yield return null;
        }
    }
    
    public override void ResetEnemy()
    {
        base.ResetEnemy();
        
        // Additional reset logic for tank
        if (myRenderer != null)
        {
            myRenderer.material.color = tankColor;
        }
    }
}