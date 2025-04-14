using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

[DisallowMultipleComponent]
public class PlayerAct : MonoBehaviour
{
    [SerializeField]
    private GunBank GunSelect;

    [SerializeField]
    private Texture2D CrosshairTexture;

    [SerializeField]
    private float CrosshairSize = 50f;

    private bool isMultiplayer;
    private PhotonView photonView;

    private void Start()
    {
        // Check if we're using the multiplayer shooting system
        photonView = GetComponent<PhotonView>();
        isMultiplayer = photonView != null;
    }

    private void Update()
    {
        // Skip if this is a multiplayer game and we don't own this player
        if (isMultiplayer && !photonView.IsMine) return;

        // Only handle shooting in single player mode
        if (!isMultiplayer)
        {
            // Handle shooting
            if (Mouse.current.leftButton.wasPressedThisFrame && GunSelect != null && GunSelect.ActiveGun != null)
            {
                GunSelect.ActiveGun.Shoot();
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame && GunSelect != null && GunSelect.ActiveGun != null)
            {
                GunSelect.ActiveGun.ResetShoot();
            }
        }

        // Handle gun switching
        if (Keyboard.current.f1Key.wasPressedThisFrame && GunSelect != null)
        {
            if (isMultiplayer)
            {
                // Use RPC to synchronize gun switching
                photonView.RPC("RPCSwitchGun", RpcTarget.All);
            }
            else
            {
                GunSelect.SwitchGun();
            }
        }
    }

    [PunRPC]
    private void RPCSwitchGun()
    {
        if (GunSelect != null)
        {
            GunSelect.SwitchGun();
        }
    }

    private void OnGUI()
    {
        // Only show crosshair for local player in multiplayer
        if (isMultiplayer && !photonView.IsMine) return;

        if (CrosshairTexture != null)
        {
            // Calculate crosshair position to be at screen center
            float xMin = (Screen.width / 2) - (CrosshairSize / 2) + 10f;
            float yMin = (Screen.height / 2) - (CrosshairSize / 2) + 35f;
            GUI.DrawTexture(new Rect(xMin, yMin, CrosshairSize, CrosshairSize), CrosshairTexture);
        }
    }
}