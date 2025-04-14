using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class EnemyNetworkSync : MonoBehaviourPun, IPunObservable
{
    private Enemy enemyComponent;
    private new PhotonView photonView;
    private Vector3 networkPosition;
    private float networkLerpTime = 0.1f; // Time to lerp to network position
    
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
        }
    }
}