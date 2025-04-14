using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

public class TowerSelector : MonoBehaviourPun
{
    public TowerUpgradeUI upgradeUI;
    private PlayerStat playerStat;
    private PlayerMov playerMov; // Reference to your player movement script
    private GameUIEvent gameUI;  // Reference to your game UI
    private bool isSelectingTower = false;

    void Start()
    {
        playerStat = GetComponent<PlayerStat>();
        playerMov = GetComponent<PlayerMov>();
        gameUI = GetComponentInChildren<GameUIEvent>();

        // Only enable this script for the local player
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            enabled = false;
            return;
        }
    }

    // In your tower selection code:
    void Update()
    {
        // Toggle tower selection mode with Alt key
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            isSelectingTower = true;
            playerMov.SetCursorState(true);
        }
        else if (Input.GetKeyUp(KeyCode.LeftAlt))
        {
            // Only hide cursor if we're not showing upgrade UI
            if (!upgradeUI.IsVisible())
            {
                isSelectingTower = false;
                playerMov.SetCursorState(false);
            }
        }

        // Only check for tower selection when in selection mode
        if (isSelectingTower && Input.GetMouseButtonDown(0)) // Left click
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                TowerBehaviour tower = hit.collider.GetComponent<TowerBehaviour>();
                if (tower != null && tower.ownerPathIndex == playerStat.PathIndex)
                {
                    upgradeUI.ShowTowerInfo(tower);
                }
                else if (!upgradeUI.IsMouseOverUI()) // Only hide if not clicking on UI
                {
                    upgradeUI.HideUpgradeUI();
                }
            }
        }

        // Right-click to close upgrade UI
        if (Input.GetMouseButtonDown(1)) // Right click
        {
            upgradeUI.HideUpgradeUI();
            if (!Input.GetKey(KeyCode.LeftAlt))
            {
                isSelectingTower = false;
                playerMov.SetCursorState(false);
            }
        }
    }
}