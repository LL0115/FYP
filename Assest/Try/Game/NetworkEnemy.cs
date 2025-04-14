using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NetworkEnemy : MonoBehaviourPunCallbacks
{
    private Enemy enemyComponent;
    
    private void Awake()
    {
        enemyComponent = GetComponent<Enemy>();
    }
    
    private void Start()
    {
        // Register this network-spawned enemy with the Entitysummoner
        if (enemyComponent != null)
        {
            Entitysummoner.RegisterNetworkSpawnedEnemy(enemyComponent);
            enemyComponent.Init();
        }
    }
    
    // Optional: Handle synchronization of enemy state if needed
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Writing data - send to network
            stream.SendNext(enemyComponent.Health);
            stream.SendNext(enemyComponent.NodeIndex);
            // Add other important state variables if needed
        }
        else
        {
            // Reading data - receive from network
            enemyComponent.Health = (float)stream.ReceiveNext();
            enemyComponent.NodeIndex = (int)stream.ReceiveNext();
            // Receive other important state variables if needed
        }
    }
}