using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class EnemyNetworkSync : MonoBehaviourPun, IPunObservable
{
    private Enemy enemyComponent;
    private new PhotonView photonView;
    private Vector3 networkPosition;
    private float networkLerpTime = 0.1f; // Time to lerp to network position

    // Add variables for speed sync
    private float lastSyncedSpeedMultiplier = 1.0f;
    
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        enemyComponent = GetComponent<Enemy>();
        
        if (enemyComponent == null)
        {
            Debug.LogError("EnemyNetworkSync requires an Enemy component!");
            enabled = false;
            return;
        }
        
        // Initialize network position
        networkPosition = transform.position;
    }
    
    private void Update()
    {
        // Only smooth movement for remote enemies (not ones we control)
        if (!photonView.IsMine && PhotonNetwork.IsConnected)
        {
            // Smoothly move to the networked position
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime / networkLerpTime);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this enemy, send the data
            stream.SendNext(transform.position);
            stream.SendNext(enemyComponent.NodeIndex);

            // Send the current health
            float currentHealth = enemyComponent.GetHealth();
            stream.SendNext(currentHealth);
            
            // Send the speed multiplier
            // Access the private field via reflection if needed
            float speedMultiplier = (float)typeof(Enemy)
                .GetField("speedMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(enemyComponent);
                
            // Alternative approach if the GetField call doesn't work
            // You could add a public method to Enemy to expose speedMultiplier
            
            // Send the speed multiplier
            stream.SendNext(speedMultiplier);
        }
        else
        {
            // Network enemy, receive data
            networkPosition = (Vector3)stream.ReceiveNext();
            enemyComponent.NodeIndex = (int)stream.ReceiveNext();

            // Receive health value
            float receivedHealth = (float)stream.ReceiveNext();
            float currentHealth = enemyComponent.GetHealth();

            // If health has changed, we need to update it
            if (receivedHealth != currentHealth)
            {
                // IMPORTANT: We only update the visual state, NOT apply damage again!
                // Directly set health instead of calling TakeDamage
                enemyComponent.Health = receivedHealth;

                // Force update the health bar
                enemyComponent.ForceUpdateHealthBar();

                // If health is now zero, handle death manually
                if (receivedHealth <= 0 && currentHealth > 0)
                {
                    // We need to call TakeDamage(0) to trigger the death logic
                    // This won't cause additional damage
                    enemyComponent.TakeDamage(0);
                }
            }
            
            // Receive speed multiplier
            float receivedSpeedMultiplier = (float)stream.ReceiveNext();
            
            // If speed has changed, update it
            if (!Mathf.Approximately(receivedSpeedMultiplier, lastSyncedSpeedMultiplier))
            {
                lastSyncedSpeedMultiplier = receivedSpeedMultiplier;
                
                // Call method to update speed (needs to be added to Enemy class)
                if (receivedSpeedMultiplier != 1.0f)
                {
                    // Using our newly added method on Enemy
                    // This will handle visual updates too
                    enemyComponent.ApplySpeedDebuff(receivedSpeedMultiplier / lastSyncedSpeedMultiplier, 0);
                }
                else
                {
                    // Reset speed using reflection to avoid adding yet another method
                    typeof(Enemy)
                        .GetField("speedMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .SetValue(enemyComponent, 1.0f);
                        
                    // Call method to remove visuals
                    typeof(Enemy)
                        .GetMethod("RemoveSpeedDebuffVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(enemyComponent, null);
                }
                
                Debug.Log($"Synced enemy speed multiplier to {receivedSpeedMultiplier}");
            }
        }
    }
}