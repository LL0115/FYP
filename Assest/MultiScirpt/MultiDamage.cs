using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class MultiDamage : MonoBehaviourPunCallbacks, IPunObservable
{
    private DamagableItem damagable;
    private PhotonView photonViewGet;
    private int lastSyncedHP;

    private void Awake()
    {
        photonViewGet = GetComponent<PhotonView>();
        damagable = GetComponent<DamagableItem>();

        if (damagable == null)
        {
            Debug.LogError("NetworkDamagable requires a component that implements DamagableItem interface!", this);
            enabled = false;
            return;
        }

        lastSyncedHP = damagable.CurrentHP;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send HP to others
            stream.SendNext(damagable.CurrentHP);
        }
        else
        {
            // Receive HP from the master client
            int receivedHP = (int)stream.ReceiveNext();

            // If HP has decreased, handle damage synchronization
            if (receivedHP != lastSyncedHP)
            {
                // Special handling for Enemy class
                var enemy = GetComponent<Enemy>();
                if (enemy != null)
                {
 
                    float prevHealth = enemy.Health;

                    enemy.Health = receivedHP;

                    // Force update HPBAr
                    enemy.ForceUpdateHealthBar();

                    // If health is zero or below and was above zero before, trigger death logic
                    if (receivedHP <= 0 && prevHealth > 0)
                    {
                        enemy.TakeDamage(0);
                    }
                }
                else
                {
                    // Handle other types of objects
                    if (receivedHP < lastSyncedHP)
                    {
                        int damage = lastSyncedHP - receivedHP;
                        damagable.TakeDamage(damage);
                    }
                }

                lastSyncedHP = receivedHP;
            }
        }
    }

    public void ApplyDamageNetwork(int damage, int senderPathIndex = -1)
    {
        // In multiplayer, let the master client handle all damage
        if (PhotonNetwork.IsConnected)
        {
            var pv = photonViewGet;
            if (pv != null)
            {
                // Send damage to master client 
                pv.RPC("RPC_ApplyDamage", RpcTarget.MasterClient, damage, senderPathIndex);
                return;
            }
        }

        // Apply damage locally 
        damagable.TakeDamage(damage);
        lastSyncedHP = damagable.CurrentHP;
    }
    [PunRPC]
    public void RPC_ApplyDamage(int damage, int senderPathIndex = -1)
    {
        // Only process if we're the master client
        if (!PhotonNetwork.IsMasterClient) return;

        // check if the sender is the master client
        if (damagable is Enemy enemy && senderPathIndex >= 0)
        {
            // If path doesn't match, no damage
            if (enemy.PlayerPathIndex != senderPathIndex)
            {
                Debug.LogWarning($"Rejected damage from path {senderPathIndex} to enemy on path {enemy.PlayerPathIndex}");
                return;
            }
        }

        damagable.TakeDamage(damage);
        lastSyncedHP = damagable.CurrentHP;
    }
}