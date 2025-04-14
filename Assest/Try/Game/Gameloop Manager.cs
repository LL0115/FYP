using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Jobs;
using System.Linq;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

public class GameloopManager : MonoBehaviourPunCallbacks
{
    private static GameloopManager _instance;
    public static GameloopManager Instance { get { return _instance; } }
    
    // Reference to all PlayerStat components in the scene, one per player
    private PlayerStat[] playerStats;
    
    // Track which player index the local player is controlling
    private int localPlayerPathIndex = 0;
    public static List<TowerBehaviour> TowersInGame;
    
    // Multi-path support: Array of node position arrays, one for each player path
    [Tooltip("Array of node positions for each player's path")]
    public Vector3[][] AllNodePositions;
    public static float[] NodeDistances;
    
    // For backward compatibility
    public Vector3[] NodePositions { get { return AllNodePositions != null && AllNodePositions.Length > 0 ? AllNodePositions[0] : null; } }

    private static Queue<GameData.ApplyEffectData> EffectQueue;
    private static Queue<Enemy> EnemiesToRemove;
    
    // Update your queues to use the new struct
    public Queue<EnemySpawnInfo> EnemyIDsToSummon = new Queue<EnemySpawnInfo>();
    private static Queue<GameData.EnemyDamageData> DamageDatas;

    private PlayerStat PlayerStatistics;
    // NEW: References to the node parent transforms
    [SerializeField] private Transform nodesParent1; // Reference to "Nodes" in Map
    [SerializeField] private Transform nodesParent2; // Reference to "Nodes2" in Map2

    // For backward compatibility
    public Transform NodeParent { get { return nodesParent1; } }
    public bool loopshouldend;

    private LevelData[] levels;
    public int currentLevelIndex = 0;
    public int currentWaveIndex = 0;
    public int latestWaveIndex = 0;
    public bool isSpawningWave = false;
    [SerializeField] private bool autoStartOnLoad = true;
    private bool isGameRunning = false;
    private GameManager gameManager;
    private GameUIEvent gameUIEvent;
    
    // Added for multiplayer
    [SerializeField] new private PhotonView photonView;
    private int frameCounter = 0;
    // Add a new property for tracking synchronized spawn timers
    private float nextSynchedSpawnTime = 0f;

    // New struct to hold enemy spawn information with path
    public struct EnemySpawnInfo
    {
        public int EnemyID;
        public int PathIndex;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        _instance = this;
        
        // Find nodes in the hierarchy if not assigned in inspector
        FindNodesInScene();
        
        // Initialize multi-path support
        InitializeNodePaths();
        
        // Find all PlayerStat components in the scene
        playerStats = FindObjectsOfType<PlayerStat>();
        
        // Determine which path index is for the local player
        if (PhotonNetwork.IsConnected)
        {
            // In multiplayer, use the player's actor number to determine path index
            // (Actor numbers start at 1, so subtract 1 to get 0-based index)
            localPlayerPathIndex = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % AllNodePositions.Length;
            Debug.Log($"Local player will use path index {localPlayerPathIndex}");
            
            // Ensure each PlayerStat knows its path index
            AssignPathIndicesToPlayerStats();
        }
        
        photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            // This object was not created through PhotonNetwork.Instantiate
            Debug.LogWarning("Adding PhotonView at runtime. This object should ideally be instantiated via PhotonNetwork.Instantiate");
            
            // Add PhotonView component without trying to set ViewID manually
            photonView = gameObject.AddComponent<PhotonView>();
            
            // IMPORTANT: We will NOT try to manually allocate a ViewID
            // Let Photon handle ViewID allocation naturally through ownership transfer
        }
    }

     // Find the node parents in the scene hierarchy
    private void FindNodesInScene()
    {
        // Find Map/Nodes if not assigned
        if (nodesParent1 == null)
        {
            GameObject nodesObj = GameObject.Find("Map/Nodes");
            if (nodesObj != null)
            {
                nodesParent1 = nodesObj.transform;
                Debug.Log("Found Nodes in Map");
            }
            else
            {
                Debug.LogError("Could not find Map/Nodes in scene hierarchy!");
            }
        }
        
        // Find Map2/Nodes2 if not assigned
        if (nodesParent2 == null)
        {
            GameObject nodesObj = GameObject.Find("Map2/Nodes2");
            if (nodesObj != null)
            {
                nodesParent2 = nodesObj.transform;
                Debug.Log("Found Nodes2 in Map2");
            }
            else
            {
                Debug.LogError("Could not find Map2/Nodes2 in scene hierarchy!");
            }
        }
    }

    // Initialize the node paths from the scene objects
    private void InitializeNodePaths()
    {
        // Create an array to hold both paths
        AllNodePositions = new Vector3[2][];
        
        // Initialize path 1 from nodesParent1
        if (nodesParent1 != null && nodesParent1.childCount > 0)
        {
            AllNodePositions[0] = new Vector3[nodesParent1.childCount];
            for (int i = 0; i < nodesParent1.childCount; i++)
            {
                AllNodePositions[0][i] = nodesParent1.GetChild(i).position;
            }
            Debug.Log($"Path 1 initialized with {nodesParent1.childCount} nodes");
        }
        else
        {
            Debug.LogWarning("nodesParent1 is missing or has no children, creating empty path");
            AllNodePositions[0] = new Vector3[0];
        }
        
        // Initialize path 2 from nodesParent2
        if (nodesParent2 != null && nodesParent2.childCount > 0)
        {
            AllNodePositions[1] = new Vector3[nodesParent2.childCount];
            for (int i = 0; i < nodesParent2.childCount; i++)
            {
                AllNodePositions[1][i] = nodesParent2.GetChild(i).position;
            }
            Debug.Log($"Path 2 initialized with {nodesParent2.childCount} nodes");
        }
        else
        {
            Debug.LogWarning("nodesParent2 is missing or has no children, creating empty path");
            AllNodePositions[1] = new Vector3[0];
        }
        
        // Calculate node distances for path 1 (backward compatibility)
        if (AllNodePositions[0].Length > 1)
        {
            NodeDistances = new float[AllNodePositions[0].Length - 1];
            for (int i = 0; i < NodeDistances.Length; i++)
            {
                NodeDistances[i] = Vector3.Distance(AllNodePositions[0][i], AllNodePositions[0][i + 1]);
            }
        }
        else
        {
            NodeDistances = new float[0];
        }
    }

    // Ensure each PlayerStat knows its path index
    // In GameloopManager.cs
    private void AssignPathIndicesToPlayerStats()
    {
        try
        {
            if (playerStats == null || playerStats.Length == 0)
            {
                Debug.LogWarning("No PlayerStat components found to assign path indices");
                return;
            }
            
            // Create mapping of actor numbers to path indices
            Dictionary<int, int> actorToPathMap = new Dictionary<int, int>();
            
            // First, assign indices to players with photon views
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                int actorNum = player.ActorNumber;
                int pathIndex = (actorNum - 1) % AllNodePositions.Length;
                actorToPathMap[actorNum] = pathIndex;
                Debug.Log($"Mapped Actor {actorNum} to path index {pathIndex}");
            }
            
            // Now assign to PlayerStats, making sure each path has exactly one PlayerStat
            bool[] pathAssigned = new bool[AllNodePositions.Length];
            
            // First pass - assign PlayerStats with PhotonViews
            for (int i = 0; i < playerStats.Length; i++)
            {
                if (playerStats[i] != null && playerStats[i].photonView != null && playerStats[i].photonView.Owner != null)
                {
                    int actorNum = playerStats[i].photonView.Owner.ActorNumber;
                    if (actorToPathMap.TryGetValue(actorNum, out int pathIndex))
                    {
                        playerStats[i].PathIndex = pathIndex;
                        pathAssigned[pathIndex] = true;
                        Debug.Log($"Assigned path index {pathIndex} to PlayerStat for player {actorNum} (Owner: {playerStats[i].photonView.Owner.NickName})");
                    }
                }
            }
            
            // Second pass - assign local PlayerStat if not already assigned
            if (PhotonNetwork.LocalPlayer != null)
            {
                int localActorNum = PhotonNetwork.LocalPlayer.ActorNumber;
                if (actorToPathMap.TryGetValue(localActorNum, out int localPathIndex))
                {
                    if (!pathAssigned[localPathIndex])
                    {
                        // Find a local PlayerStat without a path
                        for (int i = 0; i < playerStats.Length; i++)
                        {
                            if (playerStats[i] != null && (playerStats[i].photonView == null || playerStats[i].photonView.IsMine))
                            {
                                playerStats[i].PathIndex = localPathIndex;
                                pathAssigned[localPathIndex] = true;
                                Debug.Log($"Assigned local path index {localPathIndex} to local PlayerStat (index {i})");
                                break;
                            }
                        }
                    }
                }
            }
            
            // Final pass - make sure all paths are assigned
            for (int pathIdx = 0; pathIdx < AllNodePositions.Length; pathIdx++)
            {
                if (!pathAssigned[pathIdx])
                {
                    // Find an unassigned PlayerStat
                    for (int i = 0; i < playerStats.Length; i++)
                    {
                        if (playerStats[i] != null && playerStats[i].PathIndex == 0 && !pathAssigned[playerStats[i].PathIndex])
                        {
                            playerStats[i].PathIndex = pathIdx;
                            pathAssigned[pathIdx] = true;
                            Debug.Log($"Fallback assignment: path index {pathIdx} to PlayerStat {i}");
                            break;
                        }
                    }
                }
            }
            
            // Last resort - ensure at least one PlayerStat exists for each path
            for (int pathIdx = 0; pathIdx < AllNodePositions.Length; pathIdx++)
            {
                if (!pathAssigned[pathIdx] && playerStats.Length > 0)
                {
                    // Create a new PlayerStat if necessary
                    Debug.LogWarning($"No PlayerStat assigned to path {pathIdx}, will use PlayerStat[0] for all paths");
                    // We don't actually create a new one, just make the first one handle all paths
                    break;
                }
            }
            
            // Final debug log
            for (int i = 0; i < playerStats.Length; i++)
            {
                if (playerStats[i] != null)
                {
                    Debug.Log($"Final PlayerStat[{i}]: PathIndex={playerStats[i].PathIndex}, " +
                            $"Owner={(playerStats[i].photonView?.Owner?.NickName ?? "none")}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error assigning path indices: {e.Message}\n{e.StackTrace}");
        }
    }

    // Add this method:
    public void RefreshPlayerStatsArray()
    {
        // Find all PlayerStat components in the scene
        playerStats = FindObjectsOfType<PlayerStat>();
        
        Debug.Log($"RefreshPlayerStatsArray: Found {playerStats.Length} PlayerStat components");
        for (int i = 0; i < playerStats.Length; i++)
        {
            if (playerStats[i] != null)
            {
                Debug.Log($"  PlayerStat[{i}]: PathIndex={playerStats[i].PathIndex}");
            }
        }
        
        // If no PlayerStats were found, this is a critical error
        if (playerStats == null || playerStats.Length == 0)
        {
            Debug.LogError("No PlayerStat components found in the scene!");
        }
    }

    public override void OnEnable()
    {
        base.OnEnable(); 
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "TDgameScene")
        {
            // Reset all necessary variables
            currentLevelIndex = 0;
            currentWaveIndex = 0;
            latestWaveIndex = 0;
            isSpawningWave = false;
            loopshouldend = false;
            isGameRunning = true;

            // Find nodes in new scene
            FindNodesInScene();

            // Reinitialize everything
            Start();
        }
    }

    private void Start()
    {
        // Only the master client should control the game loop
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Non-master client initializing GameloopManager in listen-only mode");
            // Non-master clients will still need these references
            gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged += HandleGameStateChanged;
            }
            return;
        }

        // Clear previous state
        StopAllCoroutines();
        ClearAllEnemies();
        // Make sure playerStats array is initialized
        RefreshPlayerStatsArray();

        levels = LevelManager.GetLevels();
        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("Failed to get levels from LevelManager!");
            return;
        }

        gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        gameUIEvent = FindObjectOfType<GameUIEvent>();
        if (gameUIEvent == null)
        {
            Debug.LogError("GameUIEvent not found in the scene!");
        }

        PlayerStatistics = FindObjectOfType<PlayerStat>();
        EffectQueue = new Queue<GameData.ApplyEffectData>();
        DamageDatas = new Queue<GameData.EnemyDamageData>();
        TowersInGame = new List<TowerBehaviour>();
        EnemyIDsToSummon = new Queue<EnemySpawnInfo>();
        EnemiesToRemove = new Queue<Enemy>();
        Entitysummoner.Init();

        // Make sure our node paths are initialized correctly
        if (AllNodePositions == null || AllNodePositions.Length == 0 || AllNodePositions[0] == null)
        {
            InitializeNodePaths();
        }

        // Auto start if configured
        if (autoStartOnLoad)
        {
            StartGame();
        }
    }

    private void HandleGameStateChanged(GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.Playing:
                StartGame();
                break;
            case GameManager.GameState.Paused:
                PauseGame();
                break;
            case GameManager.GameState.GameOver:
                StopGame();
                break;
            case GameManager.GameState.Victory:
                HandleVictory();
                break;
        }
    }

    public void StartGame()
    {
        // Only the master client controls the game loop
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Non-master client cannot start game loop");
            return;
        }

        // Clear all enemies and start the game loop
        StopAllCoroutines();
        ClearAllEnemies();
        ResetGameState();

        // Reset cursor state
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        PlayerMov playerMov = FindObjectOfType<PlayerMov>();
        if (playerMov != null)
        {
            playerMov.enabled = true;
            playerMov.SetCursorState(false);
        }

        // Ensure time scale is set correctly
        Time.timeScale = 1f;

        // Reset game variables
        currentLevelIndex = 0;
        currentWaveIndex = 0;
        latestWaveIndex = 0;
        isGameRunning = true;
        loopshouldend = false;
        isSpawningWave = false;

        // Start the game loop
        StartCoroutine(GameLoop());
    }

    private void ResetGameState()
    {
        currentLevelIndex = 0;
        currentWaveIndex = 0;
        latestWaveIndex = 0;
        isGameRunning = true;
        loopshouldend = false;
        isSpawningWave = false;

        // Clear all queues
        EffectQueue?.Clear();
        DamageDatas?.Clear();
        EnemyIDsToSummon?.Clear();
        EnemiesToRemove?.Clear();

        // Clear towers
        if (TowersInGame != null)
        {
            foreach (var tower in TowersInGame.ToList())
            {
                if (tower != null && tower.gameObject != null)
                {
                    Destroy(tower.gameObject);
                }
            }
            TowersInGame.Clear();
        }
    }

    public void PauseGame()
    {
        isGameRunning = false;
    }

    public void HandleVictory()
    {
        StopGame();
    }

    public void StartSpecificLevel(int levelIndex)
    {
        if (levelIndex < levels.Length)
        {
            currentLevelIndex = levelIndex;
            currentWaveIndex = 0;
            latestWaveIndex = 0;
        }
    }

    public void StopGame()
    {
        isGameRunning = false;
        loopshouldend = true;
        StopAllCoroutines();
        ClearAllEnemies();
    }

    private void ClearAllEnemies()
    {
        StopAllCoroutines();

        // Clear all queues
        if (EffectQueue != null) EffectQueue.Clear();
        if (EnemiesToRemove != null) EnemiesToRemove.Clear();
        if (EnemyIDsToSummon != null) EnemyIDsToSummon.Clear();
        if (DamageDatas != null) DamageDatas.Clear();

        // Safely destroy all existing enemies
        if (Entitysummoner.EnemiesInGame != null)
        {
            foreach (var enemy in Entitysummoner.EnemiesInGame.ToList())
            {
                if (enemy != null && enemy.gameObject != null)
                {
                    Destroy(enemy.gameObject);
                }
            }
            Entitysummoner.EnemiesInGame.Clear();
        }

        // Clear all lists
        if (Entitysummoner.EnemiesInGameTransform != null)
            Entitysummoner.EnemiesInGameTransform.Clear();

        if (Entitysummoner.Enemytransformpairs != null)
            Entitysummoner.Enemytransformpairs.Clear();

        // Clear object pools
        if (Entitysummoner.EnemyObjectPools != null)
        {
            foreach (var pool in Entitysummoner.EnemyObjectPools.Values)
            {
                while (pool.Count > 0)
                {
                    var enemy = pool.Dequeue();
                    if (enemy != null && enemy.gameObject != null)
                    {
                        Destroy(enemy.gameObject);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Helper method to get the correct node positions for a specific player path
    public static Vector3[] GetNodePositionsForPlayer(int playerPathIndex)
    {
        if (Instance == null || Instance.AllNodePositions == null)
        {
            Debug.LogError("GameloopManager not properly initialized!");
            return null;
        }
        
        // Ensure the index is valid by taking modulo of the array length
        int validIndex = playerPathIndex % Instance.AllNodePositions.Length;
        return Instance.AllNodePositions[validIndex];
    }
    
    // Helper method to get the PlayerStat for a specific path
    public static PlayerStat GetPlayerStatForPath(int playerPathIndex)
    {
        if (Instance == null)
        {
            Debug.LogError("GameloopManager Instance is null when trying to get PlayerStat for path " + playerPathIndex);
            return null;
        }
        
        if (Instance.playerStats == null)
        {
            Debug.LogError("playerStats array is null in GameloopManager");
            return null;
        }
        
        // Debug info - log all available playerStats and their paths
        Debug.Log($"Looking for PlayerStat with path index {playerPathIndex}. Available PlayerStats: {Instance.playerStats.Length}");
        for (int i = 0; i < Instance.playerStats.Length; i++)
        {
            PlayerStat stat = Instance.playerStats[i];
            if (stat != null)
            {
                Debug.Log($"  PlayerStat[{i}]: PathIndex={stat.PathIndex}, Owner={(stat.photonView?.Owner?.NickName ?? "none")}");
            }
            else
            {
                Debug.Log($"  PlayerStat[{i}] is null");
            }
        }
        
        // First, try to find exact match
        foreach (PlayerStat stat in Instance.playerStats)
        {
            if (stat != null && stat.PathIndex == playerPathIndex)
            {
                Debug.Log($"Found exact PathIndex match for {playerPathIndex}: {stat.gameObject.name}");
                return stat;
            }
        }
        
        // If no exact match, try the master client's PlayerStat as fallback
        if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
        {
            foreach (PlayerStat stat in Instance.playerStats)
            {
                if (stat != null && stat.photonView != null && stat.photonView.IsMine)
                {
                    Debug.Log($"No exact match found for path {playerPathIndex}, using master client's PlayerStat as fallback");
                    return stat;
                }
            }
        }
        
        // Last resort - use the first available PlayerStat
        for (int i = 0; i < Instance.playerStats.Length; i++)
        {
            if (Instance.playerStats[i] != null)
            {
                Debug.LogWarning($"No matching PlayerStat found for path {playerPathIndex}, using first available PlayerStat (index {i}) as fallback");
                return Instance.playerStats[i];
            }
        }
        
        Debug.LogError($"Could not find any valid PlayerStat for path index {playerPathIndex}!");
        return null;
    }

    IEnumerator GameLoop()
    {
        Debug.Log("GameLoop started");
        frameCounter = 0;
        
        while (!loopshouldend)
        {
            frameCounter++;
            
            if (frameCounter % 300 == 0) // Log every ~5 seconds
            {
                Debug.Log($"GameLoop active - frame {frameCounter}, {Entitysummoner.EnemiesInGame?.Count ?? 0} enemies");
            }
            
            if (!isGameRunning)
            {
                yield return null;
                continue;
            }

            // Simple check without try-catch for spawning waves
            if (!isSpawningWave && IsWaveComplete())
            {
                StartCoroutine(SpawnWave());
            }

            // All game logic in separate methods with try-catch inside them
            SummonQueuedEnemies();
            MoveEnemies();
            ProcessTowers();
            ProcessEffects();
            ProcessEnemyTicks();
            ProcessDamage();
            ProcessEnemyRemovals();
            
            // Check for game over and victory - no try-catch at this level
            bool gameOver = false;
            if (PlayerStatistics != null && PlayerStatistics.GetLives() <= 0)
            {
                gameOver = true;
                HandleGameOver();
            }
            
            if (currentLevelIndex >= levels.Length && IsWaveComplete())
            {
                gameOver = true;
                HandleVictoryCondition();
            }
            
            if (gameOver)
            {
                break;
            }

            yield return null;
        }
        
        Debug.Log("GameLoop ended");
    }

    // Helper methods with try-catch inside
    private void SummonQueuedEnemies()
    {
        try
        {
            if (EnemyIDsToSummon != null && EnemyIDsToSummon.Count > 0)
            {
                int count = EnemyIDsToSummon.Count;
                for (int i = 0; i < count; i++)
                {
                    if (EnemyIDsToSummon.Count > 0)
                    {
                        EnemySpawnInfo spawnInfo = EnemyIDsToSummon.Dequeue();
                        Enemy spawnedEnemy = Entitysummoner.SummonEnemy(spawnInfo.EnemyID, spawnInfo.PathIndex);
                        
                        if (spawnedEnemy == null)
                        {
                            Debug.LogWarning($"Failed to spawn enemy with ID {spawnInfo.EnemyID} for path {spawnInfo.PathIndex}");
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error summoning enemies: {e.Message}");
        }
    }

    // Modified MoveEnemies method to handle multi-path enemies
    private void MoveEnemies()
    {
        try
        {
            if (Entitysummoner.EnemiesInGame != null && Entitysummoner.EnemiesInGame.Count > 0)
            {
                if (frameCounter % 300 == 0) // Log less frequently
                {
                    Debug.Log($"Moving {Entitysummoner.EnemiesInGame.Count} enemies");
                }
                
                for (int i = 0; i < Entitysummoner.EnemiesInGame.Count; i++)
                {
                    Enemy enemy = Entitysummoner.EnemiesInGame[i];
                    
                    if (enemy == null)
                    {
                        if (frameCounter % 300 == 0) // Log less frequently
                            Debug.LogWarning($"Enemy at index {i} is null, skipping");
                        continue;
                    }

                    // Get the correct node positions for this enemy's path
                    Vector3[] enemyNodePositions = GetNodePositionsForPlayer(enemy.PlayerPathIndex);
                    
                    if (enemyNodePositions == null || enemyNodePositions.Length == 0)
                    {
                        Debug.LogError($"No node positions found for enemy path index {enemy.PlayerPathIndex}");
                        continue;
                    }

                    // Important: Force NodeIndex to 1 if it's 0 and the enemy is at the starting position
                    if (enemy.NodeIndex == 0 && 
                        Vector3.Distance(enemy.transform.position, enemyNodePositions[0]) < 0.1f)
                    {
                        enemy.NodeIndex = 1;
                    }
                    
                    if (enemy.NodeIndex < enemyNodePositions.Length)
                    {
                        Vector3 targetPosition = enemyNodePositions[enemy.NodeIndex];
                        
                        // Move the enemy
                        enemy.transform.position = Vector3.MoveTowards(
                            enemy.transform.position, 
                            targetPosition, 
                            enemy.Speed * Time.deltaTime * 5);
                            
                        // Check if reached target with a small threshold
                        float distanceToTarget = Vector3.Distance(enemy.transform.position, targetPosition);
                        
                        if (distanceToTarget < 0.1f)  // Using a 10cm threshold
                        {
                            // Reached the node, move to next one
                            enemy.NodeIndex++;
                            
                            // Snap to exact position to prevent drift
                            enemy.transform.position = targetPosition;
                            
                            // Check if reached the end
                            if (enemy.NodeIndex >= enemyNodePositions.Length)
                            {
                                Debug.Log($"Enemy {i} reached the end of path {enemy.PlayerPathIndex}");
                                enemy.ReachedEnd();
                                EnqueueEnemyToRemove(enemy);
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error moving enemies: {e.Message}\n{e.StackTrace}");
        }
    }

    private void ProcessTowers()
    {
        try
        {
            if (TowersInGame != null)
            {
                foreach (TowerBehaviour Tower in TowersInGame.ToList())
                {
                    if (Tower != null)
                    {
                        Tower.Target = TowerTargetting.GetTarget(Tower, TowerTargetting.TargetType.First);
                        Tower.Tick();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error ticking towers: {e.Message}");
        }
    }

    private void ProcessEffects()
    {
        try
        {
            if (EffectQueue != null && EffectQueue.Count > 0)
            {
                int effectCount = EffectQueue.Count;
                for (int i = 0; i < effectCount; i++)
                {
                    if (EffectQueue.Count > 0)
                    {
                        GameData.ApplyEffectData CurrentEffectData = EffectQueue.Dequeue();
                        if (CurrentEffectData.EnemytoAffect != null && CurrentEffectData.EffectToApply != null)
                        {
                            if (CurrentEffectData.EnemytoAffect.ActiveEffects != null)
                            {
                                var existingEffect = CurrentEffectData.EnemytoAffect.ActiveEffects.Find(
                                    x => x != null && x.EffectName == CurrentEffectData.EffectToApply.EffectName);
                                    
                                if (existingEffect == null)
                                {
                                    CurrentEffectData.EnemytoAffect.ActiveEffects.Add(CurrentEffectData.EffectToApply);
                                }
                                else
                                {
                                    existingEffect.ExpireTime = CurrentEffectData.EffectToApply.ExpireTime;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error applying effects: {e.Message}");
        }
    }

    private void ProcessEnemyTicks()
    {
        try
        {
            if (Entitysummoner.EnemiesInGame != null)
            {
                foreach (Enemy CurrentEnemy in Entitysummoner.EnemiesInGame.ToList())
                {
                    if (CurrentEnemy != null)
                    {
                        CurrentEnemy.Tick();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error ticking enemies: {e.Message}");
        }
    }

    private void ProcessDamage()
    {
        try
        {
            if (DamageDatas != null && DamageDatas.Count > 0)
            {
                int damageCount = DamageDatas.Count;
                for (int i = 0; i < damageCount; i++)
                {
                    if (DamageDatas.Count > 0)
                    {
                        GameData.EnemyDamageData CurrentDamageData = DamageDatas.Dequeue();
                        if (CurrentDamageData.targetedEnemy != null)
                        {
                            CurrentDamageData.targetedEnemy.Health -= CurrentDamageData.TotalDamage / CurrentDamageData.Resistance;
                            
                            if (PlayerStatistics != null)
                            {
                                PlayerStatistics.AddMoney((int)CurrentDamageData.TotalDamage);
                            }

                            if (CurrentDamageData.targetedEnemy.Health <= 0f)
                            {
                                EnqueueEnemyToRemove(CurrentDamageData.targetedEnemy);
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error damaging enemies: {e.Message}");
        }
    }

    private void ProcessEnemyRemovals()
    {
        try
        {
            if (EnemiesToRemove != null && EnemiesToRemove.Count > 0)
            {
                int countToRemove = EnemiesToRemove.Count;
                for (int i = 0; i < countToRemove; i++)
                {
                    if (EnemiesToRemove.Count > 0)
                    {
                        Enemy enemyToRemove = EnemiesToRemove.Dequeue();
                        if (enemyToRemove != null)
                        {
                            if (frameCounter % 300 == 0) // Log less frequently
                                Debug.Log($"Removing enemy from game");
                                
                            Entitysummoner.DespawnEnemy(enemyToRemove);
                        }
                        else
                        {
                            Debug.LogWarning("Attempted to remove null enemy");
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error removing enemies: {e.Message}\n{e.StackTrace}");
        }
    }

    private void HandleGameOver()
    {
        try
        {
            Debug.Log("Game Over - Notifying GameManager");
            
            // Tell the GameManager this player died
            if (gameManager != null)
            {
                // This is the critical part - make sure PlayerDied is called
                gameManager.PlayerDied();
                
                // Add a slight delay to ensure GameManager can process this before showing UI
                StartCoroutine(DelayedGameOver());
                }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error handling game over: {e.Message}");
        }
    }

    private IEnumerator DelayedGameOver()
    {
        yield return new WaitForSeconds(0.2f);
        
        if (gameUIEvent != null)
        {
            gameUIEvent.ShowGameOver(latestWaveIndex);
        }
    }

    private void HandleVictoryCondition()
    {
        try
        {
            Debug.Log("Victory!");
            
            if (gameUIEvent != null)
            {
                gameUIEvent.ShowVictory(latestWaveIndex);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error handling victory: {e.Message}");
        }
    }

    // Restructured SpawnWave coroutine to avoid yield in try-catch
    IEnumerator SpawnWave()
    {
        bool safeToSpawn = true;
        WaveData currentWave = null;
        LevelData currentLevel = null;
        
        // Initial validations - no yields in this section
        if (levels == null)
        {
            Debug.LogError("Levels array is null!");
            safeToSpawn = false;
        }
        else if (currentLevelIndex >= levels.Length)
        {
            Debug.LogError($"Current level index {currentLevelIndex} is out of range!");
            safeToSpawn = false;
        }
        else
        {
            currentLevel = levels[currentLevelIndex];
            if (currentLevel == null || currentLevel.waves == null)
            {
                Debug.LogError("Current level or waves list is null!");
                safeToSpawn = false;
            }
            else if (currentWaveIndex >= currentLevel.waves.Count)
            {
                currentLevelIndex++;
                currentWaveIndex = 0;
                safeToSpawn = false;
            }
            else
            {
                currentWave = currentLevel.waves[currentWaveIndex];
                if (currentWave == null || currentWave.enemies == null)
                {
                    Debug.LogError("Current wave or enemies list is null!");
                    safeToSpawn = false;
                }
            }
        }
        
        if (!safeToSpawn)
        {
            yield break;
        }
        
        isSpawningWave = true;
        
        // Synchronize wave start across all clients using network time
        double waveStartTime = 0;
        
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            // Calculate start time: Current network time + 1 second buffer
            waveStartTime = PhotonNetwork.Time + 1.0;
            
            // Send RPC to all clients (including master) with exact start time
            SendSyncWaveStartTime(currentLevelIndex, currentWaveIndex, waveStartTime);
            
            // Wait until the synchronized start time
            while (PhotonNetwork.Time < waveStartTime)
            {
                yield return null;
            }
            
            Debug.Log($"Starting wave at synchronized time: {PhotonNetwork.Time}");
        }
        
        // For each player path, spawn the wave enemies
        for (int pathIndex = 0; pathIndex < AllNodePositions.Length; pathIndex++)
        {
            foreach (EnemyWaveInfo enemyWave in currentWave.enemies)
            {
                if (enemyWave == null || enemyWave.enemyData == null)
                {
                    Debug.LogError("EnemyWaveInfo or enemyData is null!");
                    continue;
                }

                for (int i = 0; i < enemyWave.count; i++)
                {
                    if (PhotonNetwork.IsConnected && photonView != null)
                    {
                        // Calculate precise spawn time based on network time
                        double exactSpawnTime = PhotonNetwork.Time + enemyWave.spawnInterval;
                        
                        // Synchronize the exact spawn time across all clients
                        SendSyncSpawnEnemyAtTime(enemyWave.enemyData.EnemyID, pathIndex, exactSpawnTime);
                        
                        // Wait until the synchronized spawn interval has passed
                        double waitUntil = PhotonNetwork.Time + enemyWave.spawnInterval;
                        while (PhotonNetwork.Time < waitUntil)
                        {
                            yield return null;
                        }
                    }
                    else
                    {
                        // Local mode - just spawn directly
                        EnqueueEnemyIDToSummon(enemyWave.enemyData.EnemyID, pathIndex);
                        yield return new WaitForSeconds(enemyWave.spawnInterval);
                    }
                }
            }
        }

        // Use network time for the delay after wave if in multiplayer
        if (PhotonNetwork.IsConnected)
        {
            double delayEndTime = PhotonNetwork.Time + currentWave.delayAfterWave;
            while (PhotonNetwork.Time < delayEndTime)
            {
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(currentWave.delayAfterWave);
        }
        
        // Finish up - no yields here
        currentWaveIndex++;
        latestWaveIndex = currentWaveIndex;
        
        // Synchronize wave completion
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            double waveEndTime = PhotonNetwork.Time;
            SendSyncWaveComplete(currentLevelIndex, currentWaveIndex, latestWaveIndex, waveEndTime);
        }
        
        isSpawningWave = false;
    }

    // Helper methods to send RPCs with try-catch but no yield
    private void SendSyncWaveStartTime(int levelIndex, int waveIndex, double startTime)
    {
        try
        {
            photonView.RPC("SyncWaveStartTime", RpcTarget.All, levelIndex, waveIndex, startTime);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error syncing wave start time: {e.Message}");
        }
    }

    private void SendSyncSpawnEnemyAtTime(int enemyId, int pathIndex, double spawnTime)
    {
        try
        {
            photonView.RPC("SyncSpawnEnemyAtTime", RpcTarget.All, enemyId, pathIndex, spawnTime);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error syncing enemy spawn: {e.Message}");
        }
    }

    private void SendSyncWaveComplete(int levelIndex, int waveIndex, int latestWaveIdx, double endTime)
    {
        try
        {
            photonView.RPC("SyncWaveComplete", RpcTarget.All, levelIndex, waveIndex, latestWaveIdx, endTime);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error syncing wave completion: {e.Message}");
        }
    }

    [PunRPC]
    private void SyncWaveStartTime(int levelIndex, int waveIndex, double startTime)
    {
        // Update level and wave indices
        currentLevelIndex = levelIndex;
        currentWaveIndex = waveIndex;
        
        Debug.Log($"Wave {waveIndex} synchronized to start at network time: {startTime}, current time: {PhotonNetwork.Time}");
    }

    [PunRPC]
    private void SyncSpawnEnemyAtTime(int enemyId, int pathIndex, double spawnTime)
    {
        // Start a coroutine to spawn the enemy at the exact time
        StartCoroutine(SpawnEnemyAtExactTime(enemyId, pathIndex, spawnTime));
    }

    private IEnumerator SpawnEnemyAtExactTime(int enemyId, int pathIndex, double spawnTime)
    {
        // Calculate time to wait
        double timeToWait = spawnTime - PhotonNetwork.Time;
        
        // If we're ahead of schedule, wait the remaining time
        if (timeToWait > 0)
        {
            yield return new WaitForSeconds((float)timeToWait);
        }
        
        // Spawn the enemy at the exact time
        Debug.Log($"Spawning enemy ID {enemyId} on path {pathIndex} at time {PhotonNetwork.Time}");
        
        // Use Entitysummoner directly for synchronized spawning
        Enemy spawnedEnemy = Entitysummoner.SummonEnemy(enemyId, pathIndex);
        
        if (spawnedEnemy == null)
        {
            Debug.LogWarning($"Failed to spawn enemy with ID {enemyId} for path {pathIndex}");
        }
    }

    [PunRPC]
    private void SyncWaveComplete(int levelIndex, int waveIndex, int latestWaveIdx, double endTime)
    {
        Debug.Log($"Wave {waveIndex-1} completed at synchronized time: {endTime}");
        
        // Update our wave counters
        currentLevelIndex = levelIndex;
        currentWaveIndex = waveIndex;
        latestWaveIndex = latestWaveIdx;
    }

    [PunRPC]
    public void ApplyPowerUpRPC(int powerUpTypeInt, int targetPlayerPathIndex, float effectValue, float duration)
    {
        PowerUpType powerUpType = (PowerUpType)powerUpTypeInt;
        
        // Apply the effect
        PowerUp.ApplyPowerUpEffect(powerUpType, targetPlayerPathIndex, effectValue, duration);
        
        // Notify players about what happened
        if (GameManager.Instance != null)
        {
            Player sourcePlayer = PhotonNetwork.LocalPlayer;
            string targetPlayerName = "Unknown";
            
            // Find the player who owns the target path
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                foreach (PlayerStat stat in FindObjectsOfType<PlayerStat>())
                {
                    if (stat.photonView != null && stat.photonView.Owner == player && stat.PathIndex == targetPlayerPathIndex)
                    {
                        targetPlayerName = player.NickName;
                        break;
                    }
                }
            }
            
            // Create appropriate notification based on power-up type
            if (powerUpType == PowerUpType.ExtraLife)
            {
                if (targetPlayerPathIndex == GameloopManager.Instance.localPlayerPathIndex)
                {
                    // Notify about self-buff
                    GameManager.Instance.SendPlayerNotification(
                        sourcePlayer,
                        GameManager.NotificationType.Achievement,
                        $"gained {effectValue} extra lives!"
                    );
                }
            }
            else
            {
                // Create notification about debuff
                string debuffType = powerUpType == PowerUpType.EnemySpeedDebuff ? "speed" : "health";
                string message = $"sent a {debuffType} debuff to {targetPlayerName}'s enemies!";
                
                GameManager.Instance.SendPlayerNotification(
                    sourcePlayer,
                    GameManager.NotificationType.Death,
                    message
                );
            }
        }
    }

    [PunRPC]
    public void TakeDamage(int amount)
    {
        // Process damage to player's lives
        if (PlayerStatistics != null)
        {
            PlayerStatistics.DecreaseLives(amount);
            
            // If player is out of lives, notify GameManager
            if (PlayerStatistics.GetLives() <= 0 && gameManager != null)
            {
                gameManager.PlayerDied();
            }
        }
    }

    private bool IsWaveComplete()
    {
        return Entitysummoner.EnemiesInGame.Count == 0 && EnemyIDsToSummon.Count == 0;
    }

    public static void EnqueueEffectData(GameData.ApplyEffectData effectData)
    {
        if (EffectQueue != null)
        {
            EffectQueue.Enqueue(effectData);
        }
        else
        {
            Debug.LogError("EffectQueue is not initialized!");
        }
    }

    public static void EnqueueDamageData(GameData.EnemyDamageData DamageData)
    {
        if (DamageDatas != null)
        {
            DamageDatas.Enqueue(DamageData);
        }
        else
        {
            Debug.LogError("DamageDatas is not initialized!");
        }
    }

    // Updated method to enqueue enemy with path information
    public static void EnqueueEnemyIDToSummon(int enemyId, int pathIndex = 0)
    {
        if (Instance == null || Instance.EnemyIDsToSummon == null)
        {
            Debug.LogError("Cannot enqueue enemy - GameloopManager not initialized!");
            return;
        }
        
        // Create a custom struct to hold both ID and path information
        Instance.EnemyIDsToSummon.Enqueue(new EnemySpawnInfo { EnemyID = enemyId, PathIndex = pathIndex });
    }

    public static void EnqueueEnemyToRemove(Enemy EnemyToRemove)
    {
        if (EnemiesToRemove != null)
        {
            EnemiesToRemove.Enqueue(EnemyToRemove);
        }
        else
        {
            Debug.LogError("EnemiesToRemove is not initialized!");
        }
    }
}