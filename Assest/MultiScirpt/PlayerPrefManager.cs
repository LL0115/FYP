using UnityEngine;
using Photon.Pun;

public class PlayerNetworkManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private string playerPrefabName = "PlayerPrefab"; // Name of prefab in Resources folder
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        // When we enter the game scene, spawn our player
        if (PhotonNetwork.IsConnected)
        {
            SpawnPlayer();
        }
        else
        {
            Debug.LogError("Not connected to Photon Network!");
        }
    }

    private void SpawnPlayer()
    {
        // Choose a spawn point
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIndex = PhotonNetwork.LocalPlayer.ActorNumber % spawnPoints.Length;
            spawnPosition = spawnPoints[spawnIndex].position;
            spawnRotation = spawnPoints[spawnIndex].rotation;
        }
        else
        {
            // Default spawn if no points defined
            spawnPosition = new Vector3(0, 1, 0);
            spawnRotation = Quaternion.identity;
        }

        // Instantiate the networked player
        GameObject playerObj = PhotonNetwork.Instantiate(
            playerPrefabName,
            spawnPosition,
            spawnRotation);

        // Make sure the player has a PhotonView component
        PhotonView photonView = playerObj.GetComponent<PhotonView>();

        if (photonView != null)
        {
            // For the local player (the one we control)
            if (photonView.IsMine)
            {
                Debug.Log($"Player spawned for {PhotonNetwork.LocalPlayer.NickName}");

                // Find and activate UI component for local player
                Transform uiTransform = playerObj.transform.Find("MainGameUI");
                if (uiTransform != null)
                {
                    uiTransform.gameObject.SetActive(true);
                    Debug.Log("UI component found and activated for local player");
                    
                    // Make sure the GameUIEvent has a proper reference to player stats
                    GameUIEvent uiEvent = uiTransform.GetComponent<GameUIEvent>();
                    if (uiEvent != null)
                    {
                        // Set up references to PlayerStat and other components
                        StartCoroutine(SetupUIReferences(uiEvent, playerObj));
                    }
                }
                else
                {
                    Debug.LogError("UI object not found in player prefab!");
                }
                
                // Enable camera for local player if needed
                Camera playerCamera = playerObj.GetComponentInChildren<Camera>();
                if (playerCamera != null)
                {
                    playerCamera.enabled = true;
                }
            }
            else
            {
                // This is another player's character
                Debug.Log($"Remote player spawned: {photonView.Owner.NickName}");
                
                // Disable UI for remote players
                Transform uiTransform = playerObj.transform.Find("MainGameUI");
                if (uiTransform != null)
                {
                    uiTransform.gameObject.SetActive(false);
                }
                
                // Disable camera for remote players
                Camera[] remoteCameras = playerObj.GetComponentsInChildren<Camera>();
                foreach (Camera cam in remoteCameras)
                {
                    cam.enabled = false;
                }
            }
        }
        else
        {
            Debug.LogError("PhotonView component not found on player prefab!");
        }
    }

    // Helper method to set up references after the UI has been initialized
    private System.Collections.IEnumerator SetupUIReferences(GameUIEvent uiEvent, GameObject playerObj)
    {
        // Wait one frame to make sure Start() has finished
        yield return null;
        
        // Get the PlayerStat component from the player
        PlayerStat playerStat = playerObj.GetComponent<PlayerStat>();

        // Set the _playerStat reference in GameUIEvent using reflection
        if (playerStat != null)
        {
            System.Reflection.FieldInfo fieldInfo = typeof(GameUIEvent).GetField("_playerStat", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(uiEvent, playerStat);
                Debug.Log("Successfully set _playerStat reference via reflection");
            }
        }
        
        // Set up other necessary references if needed
        GameloopManager gameloopManager = FindObjectOfType<GameloopManager>();
        if (gameloopManager != null)
        {
            System.Reflection.FieldInfo fieldInfo = typeof(GameUIEvent).GetField("_gameloopManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(uiEvent, gameloopManager);
                Debug.Log("Successfully set _gameloopManager reference via reflection");
            }
        }
    }
}