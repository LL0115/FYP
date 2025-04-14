using UnityEngine;
using Photon.Pun;

public class ToggleableDoor : MonoBehaviourPun
{
    [PunRPC]
    public void ToggleDoorRPC(bool destroy)
    {
        if (destroy)
        {
            // If we're instructed to destroy the door (open the doorway)
            if (PhotonNetwork.IsConnected && photonView.IsMine)
            {
                PhotonNetwork.Destroy(gameObject);
            }
            else if (!PhotonNetwork.IsConnected)
            {
                Destroy(gameObject);
            }
            Debug.Log("Door has been opened (destroyed)");
        }
        else
        {
            // If we're instructed to create the door (close the doorway)
            Debug.Log("Door has been created");
        }
    }
}