using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Entitysummoner : MonoBehaviour
{
    public static List<Enemy> EnemiesInGame;
    public static List<Transform> EnemiesInGameTransform;

    public static Dictionary<Transform, Enemy> Enemytransformpairs;
    public static Dictionary<int, GameObject> EnemyPrefabs;
    public static Dictionary<int, Queue<Enemy>> EnemyObjectPools;
    
    // Keep track of network-instantiated enemies
    private static Dictionary<int, bool> NetworkInstantiatedEnemies;

    private static bool isInit;
    
    public static void Init()
    {
        try
        {
            if(!isInit)
            {
                EnemyPrefabs = new Dictionary<int, GameObject>();
                EnemyObjectPools = new Dictionary<int, Queue<Enemy>>();
                EnemiesInGameTransform = new List<Transform>();
                EnemiesInGame = new List<Enemy>();
                Enemytransformpairs = new Dictionary<Transform, Enemy>();
                NetworkInstantiatedEnemies = new Dictionary<int, bool>();

                // Load all enemy prefabs from Resources folder
                Enemysummondata[] enemyData = Resources.LoadAll<Enemysummondata>("Enemies");
                
                if (enemyData == null || enemyData.Length == 0)
                {
                    Debug.LogError("No enemy prefabs found in Resources/Enemies folder!");
                }
                else
                {
                    Debug.Log($"Loaded {enemyData.Length} enemy prefabs from Resources/Enemies");
                    
                    foreach(Enemysummondata data in enemyData)
                    {
                        if (data.EnemyPrefab != null)
                        {
                            EnemyPrefabs.Add(data.EnemyID, data.EnemyPrefab);
                            EnemyObjectPools.Add(data.EnemyID, new Queue<Enemy>());
                            
                            // Verify network components on prefab
                            PhotonView view = data.EnemyPrefab.GetComponent<PhotonView>();
                            if (view == null && PhotonNetwork.IsConnected)
                            {
                                Debug.LogWarning($"Enemy prefab {data.EnemyPrefab.name} doesn't have a PhotonView! It won't synchronize in multiplayer.");
                            }
                            
                            PhotonTransformView transformView = data.EnemyPrefab.GetComponent<PhotonTransformView>();
                            if (transformView == null && PhotonNetwork.IsConnected)
                            {
                                Debug.LogWarning($"Enemy prefab {data.EnemyPrefab.name} doesn't have a PhotonTransformView! Position/rotation won't sync in multiplayer.");
                            }
                            
                            // Ensure the prefab is actually in Resources folder for Photon network instantiation
                            if (PhotonNetwork.IsConnected)
                            {
                                string prefabPath = "Enemies/" + data.EnemyPrefab.name;
                                GameObject prefabTest = Resources.Load<GameObject>(prefabPath);
                                if (prefabTest == null)
                                {
                                    Debug.LogError($"Enemy prefab {data.EnemyPrefab.name} is not properly located in Resources/{prefabPath}! " +
                                                 "This will cause PhotonNetwork.Instantiate to fail. Move the prefab to the Resources folder.");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogError($"Enemy data with ID {data.EnemyID} has null prefab!");
                        }
                    }
                }
                
                isInit = true;
                Debug.Log("EntitySummoner initialized successfully");
            }
            else
            {
                Debug.LogWarning("Entity Summoner is already initialized");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error initializing EntitySummoner: {e.Message}\n{e.StackTrace}");
        }
    }

    public static Enemy SummonEnemy(int EnemyID, int pathIndex = 0)
    {
        try
        {
            if (!isInit)
            {
                Debug.LogError("EntitySummoner not initialized!");
                return null;
            }

            if (!EnemyPrefabs.ContainsKey(EnemyID))
            {
                Debug.LogWarning($"Enemy ID {EnemyID} not found in prefabs");
                return null;
            }

            // Verify the path index is valid
            Vector3[] nodePositions = GameloopManager.GetNodePositionsForPlayer(pathIndex);
            if (nodePositions == null || nodePositions.Length == 0)
            {
                Debug.LogError($"Invalid path index {pathIndex} - no node positions found");
                // Try using default path instead
                pathIndex = 0;
                nodePositions = GameloopManager.GetNodePositionsForPlayer(pathIndex);
                
                if (nodePositions == null || nodePositions.Length == 0)
                {
                    Debug.LogError("No valid node positions found for any path!");
                    return null;
                }
                
                Debug.LogWarning($"Falling back to default path index 0");
            }

            Enemy SummonedEnemy = null;
            
            // In multiplayer, only the master client should spawn enemies
            bool isNetworkedGame = PhotonNetwork.IsConnected;
            bool isMasterClient = PhotonNetwork.IsMasterClient;
            
            // If we're in a networked game but not the master client, wait for the network to instantiate
            if (isNetworkedGame && !isMasterClient)
            {
                // Non-master clients should not spawn enemies in multiplayer
                return null;
            }
            
            // For single player or master client, handle enemy spawning
            Queue<Enemy> ReferenceQueue = EnemyObjectPools[EnemyID];

            // In multiplayer as master client, always instantiate new networked enemies
            // In single player, reuse from pool if available
            if (!isNetworkedGame)
            {
                // Try to get an enemy from the pool
                while (ReferenceQueue.Count > 0)
                {
                    Enemy pooledEnemy = ReferenceQueue.Dequeue();
                    if (pooledEnemy != null && pooledEnemy.gameObject != null)
                    {
                        SummonedEnemy = pooledEnemy;
                        break;
                    }
                }
            }

            // If no valid enemy found in pool or in multiplayer, create new one
            if (SummonedEnemy == null)
            {
                Vector3 spawnPosition = nodePositions[0];
                
                // Get the enemy prefab and its name
                GameObject enemyPrefab = EnemyPrefabs[EnemyID];
                string enemyPrefabName = enemyPrefab.name;
                
                // In multiplayer, use PhotonNetwork to instantiate
                if (isNetworkedGame && isMasterClient)
                {
                    // Use Resources.Load to verify the prefab is in the Resources folder
                    GameObject resourceCheck = Resources.Load<GameObject>("Enemies/" + enemyPrefabName);
                    if (resourceCheck == null)
                    {
                        Debug.LogError($"Enemy prefab '{enemyPrefabName}' not found in Resources/Enemies folder! " +
                                     "PhotonNetwork.Instantiate requires prefabs to be in Resources folder.");
                        
                        // Fallback to local instantiation for testing
                        GameObject localEnemy = Object.Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
                        SummonedEnemy = localEnemy.GetComponent<Enemy>();
                        if (SummonedEnemy != null)
                        {
                            SummonedEnemy.PlayerPathIndex = pathIndex;
                            Debug.LogWarning($"Using local instantiation for enemy on path {pathIndex} as fallback!");
                        }
                    }
                    else
                    {
                        // Proceed with network instantiation
                        Debug.Log($"Instantiating network enemy at {spawnPosition} for path {pathIndex} with prefab: {enemyPrefabName}");
                        
                        // Pass the path index as an instantiation data (can be retrieved in OnPhotonInstantiate)
                        object[] instantiateData = new object[] { pathIndex };
                        
                        GameObject networkEnemy = PhotonNetwork.Instantiate(
                            "Enemies/" + enemyPrefabName, 
                            spawnPosition, 
                            Quaternion.identity,
                            0,  // Group (0 = default)
                            instantiateData  // Custom data with path index
                        );
                        
                        if (networkEnemy == null)
                        {
                            Debug.LogError($"Failed to network instantiate enemy {enemyPrefabName}");
                            return null;
                        }
                        
                        SummonedEnemy = networkEnemy.GetComponent<Enemy>();
                        
                        // Set the path index directly
                        if (SummonedEnemy != null)
                        {
                            SummonedEnemy.PlayerPathIndex = pathIndex;
                        }
                        else
                        {
                            Debug.LogError($"Failed to get Enemy component from instantiated object");
                            return null;
                        }
                        
                        // Mark this enemy as network instantiated
                        PhotonView pv = networkEnemy.GetComponent<PhotonView>();
                        if (pv != null)
                        {
                            int viewID = pv.ViewID;
                            NetworkInstantiatedEnemies[viewID] = true;
                            
                            Debug.Log($"Network Enemy instantiated: ViewID={viewID}, Position={networkEnemy.transform.position}, Path={pathIndex}");
                        }
                        else
                        {
                            Debug.LogError("Network instantiated enemy has no PhotonView!");
                        }
                    }
                }
                else
                {
                    // Single player - regular instantiation
                    Debug.Log($"Instantiating local enemy at {spawnPosition} for path {pathIndex}");
                    GameObject NewEnemy = Object.Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
                    
                    if (NewEnemy == null)
                    {
                        Debug.LogError("Failed to instantiate enemy");
                        return null;
                    }

                    SummonedEnemy = NewEnemy.GetComponent<Enemy>();
                    if (SummonedEnemy != null)
                    {
                        SummonedEnemy.PlayerPathIndex = pathIndex;
                    }
                    else
                    {
                        Debug.LogError("Enemy component not found on instantiated object");
                        return null;
                    }
                    
                    Debug.Log($"Local Enemy instantiated at position {NewEnemy.transform.position} for path {pathIndex}");
                }
            }
            else
            {
                // Using a pooled enemy, set its path index
                SummonedEnemy.PlayerPathIndex = pathIndex;
                Debug.Log($"Reusing pooled enemy for path {pathIndex}");
            }

            // Activate and initialize the enemy
            SummonedEnemy.gameObject.SetActive(true);
            
            // Explicitly set NodeIndex to 1 (first movement target)
            SummonedEnemy.NodeIndex = 1;
            
            // Force position to first node of the correct path
            SummonedEnemy.transform.position = nodePositions[0];
            
            // Initialize the enemy after setting position and node index
            SummonedEnemy.Init();
            SummonedEnemy.ID = EnemyID;

            // Add to tracking lists
            if (!EnemiesInGame.Contains(SummonedEnemy))
                EnemiesInGame.Add(SummonedEnemy);

            if (!EnemiesInGameTransform.Contains(SummonedEnemy.transform))
                EnemiesInGameTransform.Add(SummonedEnemy.transform);

            if (!Enemytransformpairs.ContainsKey(SummonedEnemy.transform))
                Enemytransformpairs.Add(SummonedEnemy.transform, SummonedEnemy);
            
            Debug.Log($"Enemy ready: ID={SummonedEnemy.ID}, Position={SummonedEnemy.transform.position}, " +
                      $"NodeIndex={SummonedEnemy.NodeIndex}, PathIndex={SummonedEnemy.PlayerPathIndex}");

            return SummonedEnemy;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in SummonEnemy: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }

    public static void DespawnEnemy(Enemy EnemyToRemove)
    {
        try
        {
            if (EnemyToRemove == null || EnemyToRemove.gameObject == null)
            {
                Debug.LogWarning("Attempted to despawn null enemy");
                return;
            }
            
            // Capture the path index before removal for proper node positions
            int pathIndex = EnemyToRemove.PlayerPathIndex;
            
            // Check if this is a network-instantiated enemy
            PhotonView photonView = EnemyToRemove.GetComponent<PhotonView>();
            bool isNetworkEnemy = photonView != null;
            
            if (isNetworkEnemy)
            {
                // Only destroy network objects if we're the master client
                if (PhotonNetwork.IsMasterClient)
                {
                    // Remove from tracking lists first (safe to do for all clients)
                    RemoveEnemyFromTrackingLists(EnemyToRemove);
                    
                    // Remove from network-instantiated tracking
                    if (NetworkInstantiatedEnemies.ContainsKey(photonView.ViewID))
                        NetworkInstantiatedEnemies.Remove(photonView.ViewID);
                    
                    Debug.Log($"Destroying network enemy with ViewID={photonView.ViewID}, PathIndex={pathIndex}");
                    // Destroy the network object
                    PhotonNetwork.Destroy(EnemyToRemove.gameObject);
                }
                else
                {
                    // Non-master clients should remove from tracking lists but not destroy
                    RemoveEnemyFromTrackingLists(EnemyToRemove);
                    Debug.Log($"Non-master client removing enemy from tracking lists only, PathIndex={pathIndex}");
                }
                
                return;
            }
            
            // For non-networked enemies, handle pooling
            Debug.Log($"Processing local enemy despawn for PathIndex={pathIndex}");
            
            // Reset the enemy's health to its maximum value
            EnemyToRemove.Health = EnemyToRemove.MaxHealth;

            // Reset the enemy's position to the starting node of its path
            Vector3[] nodePositions = GameloopManager.GetNodePositionsForPlayer(pathIndex);
            if (nodePositions != null && nodePositions.Length > 0)
                EnemyToRemove.transform.position = nodePositions[0];
            else
                Debug.LogWarning($"Could not find node positions for path {pathIndex} during enemy despawn");

            // Reset the enemy's node index to 0
            EnemyToRemove.NodeIndex = 0;

            // Deactivate the enemy's game object
            EnemyToRemove.gameObject.SetActive(false);

            // Enqueue the enemy back to the object pool
            if (EnemyObjectPools.ContainsKey(EnemyToRemove.ID))
                EnemyObjectPools[EnemyToRemove.ID].Enqueue(EnemyToRemove);
            else
                Debug.LogWarning($"No object pool found for enemy ID {EnemyToRemove.ID}");

            // Remove from tracking lists
            RemoveEnemyFromTrackingLists(EnemyToRemove);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error despawning enemy: {e.Message}\n{e.StackTrace}");
        }
    }
    
    // Helper to reduce code duplication
    public static void RemoveEnemyFromTrackingLists(Enemy enemy)
    {
        try
        {
            if (enemy == null) return;
            
            if (Enemytransformpairs.ContainsKey(enemy.transform))
                Enemytransformpairs.Remove(enemy.transform);
            
            if (EnemiesInGameTransform.Contains(enemy.transform))
                EnemiesInGameTransform.Remove(enemy.transform);
            
            if (EnemiesInGame.Contains(enemy))
                EnemiesInGame.Remove(enemy);
                
            Debug.Log($"Enemy removed from tracking lists. Counts - EnemiesInGame: {EnemiesInGame.Count}, Transforms: {EnemiesInGameTransform.Count}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error removing enemy from tracking lists: {e.Message}");
        }
    }
    
    // Handle when network-instantiated enemies arrive on non-master clients
    public static void RegisterNetworkSpawnedEnemy(Enemy enemy)
    {
        try
        {
            if (enemy == null) return;
            
            // Get the path index from the instantiation data if available
            PhotonView pv = enemy.GetComponent<PhotonView>();
            if (pv != null && pv.InstantiationData != null && pv.InstantiationData.Length > 0)
            {
                try
                {
                    // The first parameter in the instantiation data is the path index
                    int pathIndex = (int)pv.InstantiationData[0];
                    enemy.PlayerPathIndex = pathIndex;
                    
                    Debug.Log($"Network enemy registered with path index {pathIndex} from instantiation data");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error reading path index from instantiation data: {e.Message}");
                    // Use default path index if we can't read from data
                    enemy.PlayerPathIndex = 0;
                }
            }
            else
            {
                Debug.LogWarning("Network enemy has no instantiation data with path index. Using default path 0.");
                enemy.PlayerPathIndex = 0;
            }
            
            // Set initial values for this enemy
            enemy.NodeIndex = 1; // Start at first target node
            
            // Get the appropriate node positions for this path
            Vector3[] nodePositions = GameloopManager.GetNodePositionsForPlayer(enemy.PlayerPathIndex);
            if (nodePositions != null && nodePositions.Length > 0)
            {
                // Ensure it starts at the beginning of the path
                enemy.transform.position = nodePositions[0];
                Debug.Log($"Setting network enemy position to {nodePositions[0]} (start of path {enemy.PlayerPathIndex})");
            }
            else
            {
                Debug.LogError($"No node positions found for path index {enemy.PlayerPathIndex}");
            }
            
            // Add the enemy to our tracking lists
            if (!EnemiesInGame.Contains(enemy))
                EnemiesInGame.Add(enemy);
                
            if (!EnemiesInGameTransform.Contains(enemy.transform))
                EnemiesInGameTransform.Add(enemy.transform);
                
            if (!Enemytransformpairs.ContainsKey(enemy.transform))
                Enemytransformpairs.Add(enemy.transform, enemy);
                
            Debug.Log($"Network enemy registered. Path={enemy.PlayerPathIndex}, Position={enemy.transform.position}, ViewID={pv?.ViewID}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error registering network enemy: {e.Message}\n{e.StackTrace}");
        }
    }
    
    // Clean up all enemies (useful when exiting a scene or game)
    public static void CleanupAllEnemies()
    {
        try
        {
            Debug.Log("Cleaning up all enemies");
            
            // First destroy all network-instantiated enemies if we're master client
            if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
            {
                foreach (Enemy enemy in new List<Enemy>(EnemiesInGame))
                {
                    if (enemy == null) continue;
                    
                    PhotonView pv = enemy.GetComponent<PhotonView>();
                    if (pv != null)
                    {
                        PhotonNetwork.Destroy(enemy.gameObject);
                    }
                }
            }
            
            // Now handle local cleanup
            foreach (Enemy enemy in new List<Enemy>(EnemiesInGame))
            {
                if (enemy != null && enemy.gameObject != null)
                {
                    PhotonView pv = enemy.GetComponent<PhotonView>();
                    if (pv == null) // Only destroy local objects
                    {
                        Object.Destroy(enemy.gameObject);
                    }
                }
            }
            
            // Clear all collections
            EnemiesInGame.Clear();
            EnemiesInGameTransform.Clear();
            Enemytransformpairs.Clear();
            NetworkInstantiatedEnemies.Clear();
            
            // Clear object pools
            foreach (var queue in EnemyObjectPools.Values)
            {
                while (queue.Count > 0)
                {
                    Enemy pooledEnemy = queue.Dequeue();
                    if (pooledEnemy != null && pooledEnemy.gameObject != null)
                    {
                        Object.Destroy(pooledEnemy.gameObject);
                    }
                }
            }
            
            // Reset all dictionaries
            foreach (var id in EnemyObjectPools.Keys)
            {
                EnemyObjectPools[id] = new Queue<Enemy>();
            }
            
            Debug.Log("Enemy cleanup complete");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error cleaning up enemies: {e.Message}\n{e.StackTrace}");
        }
    }
}