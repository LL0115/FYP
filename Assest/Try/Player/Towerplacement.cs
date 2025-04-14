using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Photon.Pun;

public class TowerPlacement : MonoBehaviourPunCallbacks
{
    [SerializeField] private LayerMask PlacementCheckMask;
    [SerializeField] private LayerMask PlacementCollideMask;
    [SerializeField] private Camera PlayerCamera;
    [SerializeField] private PlayerStat playerStatistics;
    
    public GameObject CurrentPlacingTower;
    
    // References to tower prefabs in Resources folder
    [SerializeField] private string[] towerPrefabNames;
    
    // Material colors for valid/invalid placement
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    
    private Renderer towerRenderer;
    private Material originalMaterial;
    private bool canPlace = false;
    
    void Start()
    {
        // Only run tower placement on the local player's controller
        if (!photonView.IsMine && PhotonNetwork.IsConnected)
        {
            enabled = false;
            return;
        }
        
        if (PlayerCamera == null)
        {
            PlayerCamera = Camera.main;
        }
        
        // Find player stat if not set
        if (playerStatistics == null)
        {
            // Try to find PlayerStat on this object or parent
            playerStatistics = GetComponent<PlayerStat>();
            if (playerStatistics == null)
            {
                playerStatistics = GetComponentInParent<PlayerStat>();
            }
            
            // If still not found, try to find by looking for one that belongs to this player
            if (playerStatistics == null)
            {
                PlayerStat[] allStats = FindObjectsOfType<PlayerStat>();
                foreach (PlayerStat stat in allStats)
                {
                    if (stat.photonView != null && stat.photonView.IsMine)
                    {
                        playerStatistics = stat;
                        break;
                    }
                }
            }
            
            if (playerStatistics == null)
            {
                Debug.LogError("PlayerStat component not found! Tower placement will not work.");
            }
        }
        
        // Initialize materials if not set
        if (validPlacementMaterial == null)
        {
            validPlacementMaterial = new Material(Shader.Find("Standard"));
            validPlacementMaterial.color = new Color(0, 1, 0, 0.5f);
        }
        
        if (invalidPlacementMaterial == null)
        {
            invalidPlacementMaterial = new Material(Shader.Find("Standard"));
            invalidPlacementMaterial.color = new Color(1, 0, 0, 0.5f);
        }
    }
    
    void Update()
    {
        // Only process on the local player
        if (!photonView.IsMine && PhotonNetwork.IsConnected)
            return;
            
        if (CurrentPlacingTower != null)
        {
            // Make cursor visible during placement
            if (!Cursor.visible)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            
            Ray camRay = PlayerCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit HitInfo;
            
            if (Physics.Raycast(camRay, out HitInfo, 100f, PlacementCollideMask))
            {
                // Position tower at raycast hit point
                CurrentPlacingTower.transform.position = HitInfo.point;
                
                // Cancel placement with Q key
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    CancelPlacement();
                    return;
                }
                
                // Check if placement is valid
                BoxCollider TowerCollider = CurrentPlacingTower.GetComponent<BoxCollider>();
                if (TowerCollider == null)
                {
                    Debug.LogError("Tower needs a BoxCollider component for placement checks!");
                    return;
                }
                
                TowerCollider.isTrigger = true;
                Vector3 Boxcenter = CurrentPlacingTower.transform.position + TowerCollider.center;
                Vector3 halfextents = TowerCollider.size / 2;
                
                // Valid placement: on "Canplace" tag and not colliding with other objects
                canPlace = HitInfo.collider.gameObject.CompareTag("Canplace") && 
                           !Physics.CheckBox(Boxcenter, halfextents, Quaternion.identity, PlacementCheckMask, QueryTriggerInteraction.Ignore);
                
                // Update placement visual feedback
                UpdatePlacementVisual(canPlace);
                
                // Click to place tower
                if (Input.GetMouseButtonDown(0) && canPlace)
                {
                    PlaceTower();
                }
            }
        }
    }
    
    private void UpdatePlacementVisual(bool valid)
    {
        if (towerRenderer == null && CurrentPlacingTower != null)
        {
            towerRenderer = CurrentPlacingTower.GetComponentInChildren<Renderer>();
            if (towerRenderer != null)
            {
                originalMaterial = towerRenderer.material;
            }
        }
        
        if (towerRenderer != null)
        {
            towerRenderer.material = valid ? validPlacementMaterial : invalidPlacementMaterial;
        }
    }
    
    private void CancelPlacement()
    {
        if (CurrentPlacingTower != null)
        {
            Destroy(CurrentPlacingTower);
            CurrentPlacingTower = null;
            
            if (!IsShopOpen())
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }
    
    private void PlaceTower()
    {
        if (CurrentPlacingTower != null)
        {
            TowerBehaviour towerBehaviour = CurrentPlacingTower.GetComponent<TowerBehaviour>();
            if (towerBehaviour != null)
            {
                Debug.Log($"Attempting to place tower: {towerBehaviour.towerName}, Type: {towerBehaviour.TowerType}");
                
                int towerIndex = GetTowerPrefabIndex(towerBehaviour);
                int cost = towerBehaviour.SummonCost;
                
                Debug.Log($"Tower index: {towerIndex}, Cost: {cost}, Player money: {playerStatistics.GetMoney()}");
                
                // Check if player has enough money
                if (playerStatistics.GetMoney() < cost)
                {
                    Debug.LogWarning($"Not enough money to place tower! Have {playerStatistics.GetMoney()}, need {cost}");
                    CancelPlacement();
                    return;
                }
                
                Vector3 position = CurrentPlacingTower.transform.position;
                Quaternion rotation = CurrentPlacingTower.transform.rotation;
                
                // Destroy the preview object
                Destroy(CurrentPlacingTower);
                CurrentPlacingTower = null;
                
                // Place tower and spend money differently based on whether we're in multiplayer
                if (PhotonNetwork.IsConnected)
                {
                    Debug.Log($"Multiplayer mode: Instantiating network tower");
                    
                    // Directly instantiate the tower on the network
                    string resourcePath = towerPrefabNames[towerIndex];
                    GameObject tower = PhotonNetwork.Instantiate(resourcePath, position, rotation, 0);
                    
                    // Set up the tower properties
                    TowerBehaviour placedTower = tower.GetComponent<TowerBehaviour>();
                    if (placedTower != null)
                    {
                        // Configure the tower with an RPC to ensure all clients have the same settings
                        placedTower.photonView.RPC("RPC_SetupTower", RpcTarget.AllBuffered, playerStatistics.PathIndex);
                    }
                    
                    // Use the local playerStatistics to spend money
                    if (playerStatistics.photonView.IsMine)
                    {
                        // If this is our own player stat, directly spend money
                        Debug.Log($"Directly spending money: {cost}");
                        playerStatistics.SpendMoney(cost);
                    }
                    else
                    {
                        // This should never happen, but log it if it does
                        Debug.LogError($"Trying to place tower using another player's stats! This is a logic error.");
                    }
                }
                else
                {
                    // Single player mode - directly place tower and spend money
                    Debug.Log($"Single player mode: Directly placing tower and spending money");
                    
                    // Local placement logic
                    GameObject towerPrefab = Resources.Load<GameObject>(towerPrefabNames[towerIndex]);
                    if (towerPrefab != null)
                    {
                        GameObject tower = Instantiate(towerPrefab, position, rotation);
                        TowerBehaviour placedTower = tower.GetComponent<TowerBehaviour>();
                        
                        if (placedTower != null)
                        {
                            placedTower.ownerPathIndex = playerStatistics.PathIndex;
                            GameloopManager.TowersInGame.Add(placedTower);
                            
                            BoxCollider collider = tower.GetComponent<BoxCollider>();
                            if (collider != null)
                            {
                                collider.isTrigger = false;
                            }
                        }
                    }
                    
                    // Local money spending
                    playerStatistics.SpendMoney(cost);
                }
                
                // Reset cursor
                if (!IsShopOpen())
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }
            else
            {
                Debug.LogError("TowerBehaviour component is missing on the tower prefab");
                Destroy(CurrentPlacingTower);
                CurrentPlacingTower = null;
            }
        }
    }
    
    [PunRPC]
    private void RPC_PlaceTower(int towerIndex, Vector3 position, Quaternion rotation, int ownerPathIndex)
    {
        Debug.Log($"RPC_PlaceTower called: Index={towerIndex}, Position={position}");
        
        // Validate index
        if (towerIndex < 0 || towerIndex >= towerPrefabNames.Length)
        {
            Debug.LogError($"Tower index {towerIndex} is out of range (0-{towerPrefabNames.Length-1})");
            return;
        }
        
        // Load the tower prefab
        string resourcePath = towerPrefabNames[towerIndex];
        Debug.Log($"Loading tower prefab from Resources: {resourcePath}");
        
        GameObject towerPrefab = Resources.Load<GameObject>(resourcePath);
        if (towerPrefab == null)
        {
            Debug.LogError($"Tower prefab '{resourcePath}' not found in Resources!");
            return;
        }
        
        // Instantiate the tower
        GameObject tower = Instantiate(towerPrefab, position, rotation);
        
        // Set tower properties
        TowerBehaviour towerBehaviour = tower.GetComponent<TowerBehaviour>();
        if (towerBehaviour != null)
        {
            Debug.Log($"Tower placed: {towerBehaviour.towerName}, Type: {towerBehaviour.TowerType}");
            towerBehaviour.ownerPathIndex = ownerPathIndex;
            GameloopManager.TowersInGame.Add(towerBehaviour);
        }
        else
        {
            Debug.LogError($"Tower prefab has no TowerBehaviour component!");
        }
        
        // Set collider to non-trigger
        BoxCollider collider = tower.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }
    }
    
    private int GetTowerPrefabIndex(TowerBehaviour tower)
    {
        string towerName = tower.towerName;
        string towerType = tower.TowerType.ToString();
        
        Debug.Log($"Looking for tower of type: {towerType}, name: {towerName}");
        
        // First try to match by name (more specific)
        for (int i = 0; i < towerPrefabNames.Length; i++)
        {
            GameObject prefab = Resources.Load<GameObject>(towerPrefabNames[i]);
            if (prefab != null)
            {
                TowerBehaviour prefabTower = prefab.GetComponent<TowerBehaviour>();
                if (prefabTower != null && prefabTower.towerName == towerName)
                {
                    Debug.Log($"Found tower by name at index {i}: {towerPrefabNames[i]}");
                    return i;
                }
            }
        }
        
        // Then try to match by type
        for (int i = 0; i < towerPrefabNames.Length; i++)
        {
            GameObject prefab = Resources.Load<GameObject>(towerPrefabNames[i]);
            if (prefab != null)
            {
                TowerBehaviour prefabTower = prefab.GetComponent<TowerBehaviour>();
                if (prefabTower != null && prefabTower.TowerType.ToString() == towerType)
                {
                    Debug.Log($"Found tower by type at index {i}: {towerPrefabNames[i]}");
                    return i;
                }
            }
        }
        
        // Not found, log details of what we're looking for and what's available
        Debug.LogWarning($"Tower with name '{towerName}' and type '{towerType}' not found in prefab list.");
        Debug.LogWarning("Available tower prefabs:");
        
        for (int i = 0; i < towerPrefabNames.Length; i++)
        {
            GameObject prefab = Resources.Load<GameObject>(towerPrefabNames[i]);
            if (prefab != null)
            {
                TowerBehaviour prefabTower = prefab.GetComponent<TowerBehaviour>();
                if (prefabTower != null)
                {
                    Debug.LogWarning($"  {i}: Name='{prefabTower.towerName}', Type='{prefabTower.TowerType}'");
                }
            }
        }
        
        // Last resort - return default
        Debug.LogError($"Defaulting to tower index 0. This is likely not what you want!");
        return 0;
    }
    
    private bool IsShopOpen()
    {
        GameUIEvent gameUI = FindObjectOfType<GameUIEvent>();
        if (gameUI != null)
        {
            return gameUI._shopUI != null && gameUI._shopUI.style.display == DisplayStyle.Flex;
        }
        return false;
    }
    
    public void SetTowerToPlace(GameObject tower)
    {
        if (tower == null)
        {
            Debug.LogError("Attempted to place a null tower prefab!");
            return;
        }
        
        TowerBehaviour towerBehaviour = tower.GetComponent<TowerBehaviour>();
        if (towerBehaviour == null)
        {
            Debug.LogError("Tower prefab is missing TowerBehaviour component!");
            return;
        }
        
        int TowerCost = towerBehaviour.SummonCost;
        
        if (playerStatistics == null)
        {
            Debug.LogError("PlayerStat reference is missing! Cannot check money.");
            return;
        }
        
        if (playerStatistics.GetMoney() >= TowerCost)
        {
            // Cancel any current placement
            if (CurrentPlacingTower != null)
            {
                Destroy(CurrentPlacingTower);
            }
            
            // Create new placement preview
            CurrentPlacingTower = Instantiate(tower, Vector3.zero, Quaternion.identity);
            
            // Reset renderer reference for visual feedback
            towerRenderer = null;
            
            Debug.Log($"Started placing tower: {tower.name}, Cost: {TowerCost}");
        }
        else
        {
            Debug.Log($"Not enough money to place {tower.name}. Need {TowerCost}, have {playerStatistics.GetMoney()}");
        }
    }
}