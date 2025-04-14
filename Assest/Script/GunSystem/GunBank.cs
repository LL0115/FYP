using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

[DisableAnnotation]
public class GunBank : MonoBehaviour
{
    [SerializeField]
    private GunType Gun;

    [SerializeField]
    private Transform GunParents;

    [SerializeField]
    private List<ScirptableGun> Guns;

    [Space]
    [Header("RunTime Error")]

    public ScirptableGun ActiveGun;

    private PhotonView photonView;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    private void Start()
    {
        // In multiplayer, only the owner initializes the gun
        if (photonView != null && !photonView.IsMine)
        {
            // For non-owners, wait for the OnPhotonSerializeView to sync the gun type
            return;
        }

        SpawnGun(Gun);
    }

    public void SwitchGun()
    {
        int currentIndex = Guns.FindIndex(g => g.Type == Gun);
        int nextIndex = (currentIndex + 1) % Guns.Count;
        Gun = Guns[nextIndex].Type;
        SpawnGun(Gun);
    }

    private void SpawnGun(GunType gunType)
    {
        ScirptableGun gun = Guns.Find(g => g.Type == gunType);

        if (gun != null)
        {
            if (ActiveGun != null)
            {
                Destroy(ActiveGun.Model);
            }

            ActiveGun = gun;
            ActiveGun.Spawn(GunParents, this);
        }
        else
        {
            Debug.LogError("Gun not found");
            return;
        }
    }

    // Add this for Photon PUN syncing
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send the current gun type
            stream.SendNext((int)Gun);
        }
        else
        {
            // Receive the gun type
            GunType receivedGunType = (GunType)stream.ReceiveNext();

            // Only update if the gun type has changed
            if (Gun != receivedGunType)
            {
                Gun = receivedGunType;
                SpawnGun(Gun);
            }
        }
    }
}