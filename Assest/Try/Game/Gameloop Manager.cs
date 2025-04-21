using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using System.Linq;

public class GameloopManager : MonoBehaviourPunCallbacks
{
    private static GameloopManager _instance;
    public static GameloopManager Instance { get { return _instance; } }
    private PlayerStat[] playerStats;
    private int localPlayerPathIndex = 0;
    public static List<TowerBehaviour> TowersInGame;
    public Vector3[][] AllNodePositions;
    public static float[] NodeDistances;
    public Vector3[] NodePositions { get { return AllNodePositions != null && AllNodePositions.Length > 0 ? AllNodePositions[0] : null; } }
    private static Queue<GameData.ApplyEffectData> EffectQueue;
    private static Queue<Enemy> EnemiesToRemove;
    public Queue<EnemySpawnInfo> EnemyIDsToSummon = new Queue<EnemySpawnInfo>();
    private static Queue<GameData.EnemyDamageData> DamageDatas;
    private PlayerStat PlayerStatistics;
    [SerializeField] private Transform nodesParent1;
    [SerializeField] private Transform nodesParent2;
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
    [SerializeField] new private PhotonView photonView;
    private int frameCounter = 0;

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

        FindNodesInScene();
        InitializeNodePaths();
        playerStats = FindObjectsOfType<PlayerStat>();

        if (PhotonNetwork.IsConnected)
        {
            localPlayerPathIndex = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % AllNodePositions.Length;
            Debug.Log($"Local player path: {localPlayerPathIndex}");
            AssignPathIndicesToPlayerStats();
        }

        photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            photonView = gameObject.AddComponent<PhotonView>();
            Debug.LogWarning("Added PhotonView to GameloopManager.");
        }
    }

    private void FindNodesInScene()
    {
        if (nodesParent1 == null)
        {
            GameObject nodesObj = GameObject.Find("Map/Nodes");
            if (nodesObj != null)
                nodesParent1 = nodesObj.transform;
            else
                Debug.LogError("Map/Nodes not found!");
        }

        if (nodesParent2 == null)
        {
            GameObject nodesObj = GameObject.Find("Map2/Nodes2");
            if (nodesObj != null)
                nodesParent2 = nodesObj.transform;
            else
                Debug.LogError("Map2/Nodes2 not found!");
        }
    }

    private void InitializeNodePaths()
    {
        AllNodePositions = new Vector3[2][];

        if (nodesParent1 != null && nodesParent1.childCount > 0)
        {
            AllNodePositions[0] = new Vector3[nodesParent1.childCount];
            for (int i = 0; i < nodesParent1.childCount; i++)
                AllNodePositions[0][i] = nodesParent1.GetChild(i).position;
            Debug.Log($"Path 0: {AllNodePositions[0].Length} nodes: {string.Join(", ", AllNodePositions[0].Select(v => v.ToString()))}");
        }
        else
        {
            Debug.LogWarning("nodesParent1 missing");
            AllNodePositions[0] = new Vector3[0];
        }

        if (nodesParent2 != null && nodesParent2.childCount > 0)
        {
            AllNodePositions[1] = new Vector3[nodesParent2.childCount];
            for (int i = 0; i < nodesParent2.childCount; i++)
                AllNodePositions[1][i] = nodesParent2.GetChild(i).position;
            Debug.Log($"Path 1: {AllNodePositions[1].Length} nodes: {string.Join(", ", AllNodePositions[1].Select(v => v.ToString()))}");
        }
        else
        {
            Debug.LogWarning("nodesParent2 missing");
            AllNodePositions[1] = new Vector3[0];
        }

        if (AllNodePositions[0].Length > 1)
        {
            NodeDistances = new float[AllNodePositions[0].Length - 1];
            for (int i = 0; i < NodeDistances.Length; i++)
                NodeDistances[i] = Vector3.Distance(AllNodePositions[0][i], AllNodePositions[0][i + 1]);
        }
        else
        {
            NodeDistances = new float[0];
        }
    }

    private void AssignPathIndicesToPlayerStats()
    {
        if (playerStats == null || playerStats.Length == 0)
        {
            Debug.LogWarning("No PlayerStat components");
            return;
        }

        Dictionary<int, int> actorToPathMap = new Dictionary<int, int>();
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            int actorNum = player.ActorNumber;
            int pathIndex = (actorNum - 1) % AllNodePositions.Length;
            actorToPathMap[actorNum] = pathIndex;
        }

        bool[] pathAssigned = new bool[AllNodePositions.Length];
        for (int i = 0; i < playerStats.Length; i++)
        {
            if (playerStats[i] != null && playerStats[i].photonView != null && playerStats[i].photonView.Owner != null)
            {
                int actorNum = playerStats[i].photonView.Owner.ActorNumber;
                if (actorToPathMap.TryGetValue(actorNum, out int pathIndex))
                {
                    playerStats[i].PathIndex = pathIndex;
                    pathAssigned[pathIndex] = true;
                }
            }
        }

        if (PhotonNetwork.LocalPlayer != null)
        {
            int localActorNum = PhotonNetwork.LocalPlayer.ActorNumber;
            if (actorToPathMap.TryGetValue(localActorNum, out int localPathIndex) && !pathAssigned[localPathIndex])
            {
                for (int i = 0; i < playerStats.Length; i++)
                {
                    if (playerStats[i] != null && (playerStats[i].photonView == null || playerStats[i].photonView.IsMine))
                    {
                        playerStats[i].PathIndex = localPathIndex;
                        pathAssigned[localPathIndex] = true;
                        break;
                    }
                }
            }
        }
    }

    public void RefreshPlayerStatsArray()
    {
        playerStats = FindObjectsOfType<PlayerStat>();
        Debug.Log($"Found {playerStats.Length} PlayerStat components");
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
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "TDgameScene")
        {
            currentLevelIndex = 0;
            currentWaveIndex = 0;
            latestWaveIndex = 0;
            isSpawningWave = false;
            loopshouldend = false;
            isGameRunning = true;
            FindNodesInScene();
            Start();
        }
    }

    private void Start()
    {
        Entitysummoner.Init();
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
        {
            gameManager = GameManager.Instance;
            if (gameManager != null)
                gameManager.OnGameStateChanged += HandleGameStateChanged;
            return;
        }

        StopAllCoroutines();
        ClearAllEnemies();
        RefreshPlayerStatsArray();

        levels = LevelManager.GetLevels();
        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("Failed to get levels!");
            return;
        }

        gameManager = GameManager.Instance;
        if (gameManager != null)
            gameManager.OnGameStateChanged += HandleGameStateChanged;

        gameUIEvent = FindObjectOfType<GameUIEvent>();
        PlayerStatistics = FindObjectOfType<PlayerStat>();
        EffectQueue = new Queue<GameData.ApplyEffectData>();
        DamageDatas = new Queue<GameData.EnemyDamageData>();
        TowersInGame = new List<TowerBehaviour>();
        EnemyIDsToSummon = new Queue<EnemySpawnInfo>();
        EnemiesToRemove = new Queue<Enemy>();
        Entitysummoner.Init();

        if (AllNodePositions == null || AllNodePositions.Length == 0)
            InitializeNodePaths();

        if (autoStartOnLoad)
            StartGame();
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
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return;

        StopAllCoroutines();
        ClearAllEnemies();
        ResetGameState();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        PlayerMov playerMov = FindObjectOfType<PlayerMov>();
        if (playerMov != null)
        {
            playerMov.enabled = true;
            playerMov.SetCursorState(false);
        }

        Time.timeScale = 1f;

        currentLevelIndex = 0;
        currentWaveIndex = 0;
        latestWaveIndex = 0;
        isGameRunning = true;
        loopshouldend = false;
        isSpawningWave = false;

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

        EffectQueue?.Clear();
        DamageDatas?.Clear();
        EnemyIDsToSummon?.Clear();
        EnemiesToRemove?.Clear();

        if (TowersInGame != null)
        {
            foreach (var tower in TowersInGame.ToList())
            {
                if (tower != null && tower.gameObject != null)
                    Destroy(tower.gameObject);
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

        if (EffectQueue != null) EffectQueue.Clear();
        if (EnemiesToRemove != null) EnemiesToRemove.Clear();
        if (EnemyIDsToSummon != null) EnemyIDsToSummon.Clear();
        if (DamageDatas != null) DamageDatas.Clear();

        if (Entitysummoner.EnemiesInGame != null)
        {
            foreach (var enemy in Entitysummoner.EnemiesInGame.ToList())
            {
                if (enemy != null && enemy.gameObject != null)
                    Entitysummoner.DespawnEnemy(enemy);
            }
            Entitysummoner.EnemiesInGame.Clear();
        }

        if (Entitysummoner.EnemiesInGameTransform != null)
            Entitysummoner.EnemiesInGameTransform.Clear();

        if (Entitysummoner.Enemytransformpairs != null)
            Entitysummoner.Enemytransformpairs.Clear();
    }

    private void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public static Vector3[] GetNodePositionsForPlayer(int playerPathIndex)
    {
        if (Instance == null || Instance.AllNodePositions == null)
        {
            Debug.LogError($"Cannot get nodes for path {playerPathIndex}");
            return null;
        }

        if (playerPathIndex < 0 || playerPathIndex >= Instance.AllNodePositions.Length)
        {
            Debug.LogError($"Invalid path index {playerPathIndex}. Valid: 0 to {Instance.AllNodePositions.Length - 1}");
            return Instance.AllNodePositions[0];
        }

        return Instance.AllNodePositions[playerPathIndex];
    }

    public static PlayerStat GetPlayerStatForPath(int playerPathIndex)
    {
        if (Instance == null || Instance.playerStats == null)
            return null;

        foreach (PlayerStat stat in Instance.playerStats)
        {
            if (stat != null && stat.PathIndex == playerPathIndex)
                return stat;
        }

        return Instance.playerStats.Length > 0 ? Instance.playerStats[0] : null;
    }

    IEnumerator GameLoop()
    {
        frameCounter = 0;
        while (!loopshouldend)
        {
            frameCounter++;
            if (frameCounter % 300 == 0)
                Debug.Log($"GameLoop frame {frameCounter}, {Entitysummoner.EnemiesInGame?.Count ?? 0} enemies");

            if (!isGameRunning)
            {
                yield return null;
                continue;
            }

            if (!isSpawningWave && IsWaveComplete())
                StartCoroutine(SpawnWave());

            SummonQueuedEnemies();
            MoveEnemies();
            ProcessTowers();
            ProcessEffects();
            ProcessEnemyTicks();
            ProcessDamage();
            ProcessEnemyRemovals();

            if (PlayerStatistics != null && PlayerStatistics.GetLives() <= 0)
            {
                HandleGameOver();
                break;
            }

            if (currentLevelIndex >= levels.Length && IsWaveComplete())
            {
                HandleVictoryCondition();
                break;
            }

            yield return null;
        }
    }

    private void SummonQueuedEnemies()
    {
        if (EnemyIDsToSummon != null && EnemyIDsToSummon.Count > 0)
        {
            int count = EnemyIDsToSummon.Count;
            for (int i = 0; i < count; i++)
            {
                if (EnemyIDsToSummon.Count > 0)
                {
                    EnemySpawnInfo spawnInfo = EnemyIDsToSummon.Dequeue();
                    Entitysummoner.SummonEnemy(spawnInfo.EnemyID, spawnInfo.PathIndex);
                }
            }
        }
    }

    private void MoveEnemies()
    {
        if (Entitysummoner.EnemiesInGame != null && Entitysummoner.EnemiesInGame.Count > 0)
        {
            for (int i = 0; i < Entitysummoner.EnemiesInGame.Count; i++)
            {
                Enemy enemy = Entitysummoner.EnemiesInGame[i];
                if (enemy == null)
                    continue;

                Vector3[] enemyNodePositions = GetNodePositionsForPlayer(enemy.PlayerPathIndex);
                if (enemyNodePositions == null || enemyNodePositions.Length == 0)
                {
                    Debug.LogError($"No nodes for enemy ID={enemy.ID}, Path={enemy.PlayerPathIndex}");
                    continue;
                }

                if (enemy.PlayerPathIndex == 1 && enemyNodePositions == AllNodePositions[0])
                {
                    Debug.LogError($"Enemy ID={enemy.ID} on path 1 using path 0 nodes!");
                    enemyNodePositions = AllNodePositions[1];
                }

                if (enemy.NodeIndex == 0 && Vector3.Distance(enemy.transform.position, enemyNodePositions[0]) < 0.1f)
                    enemy.NodeIndex = 1;

                if (enemy.NodeIndex < enemyNodePositions.Length)
                {
                    Vector3 targetPosition = enemyNodePositions[enemy.NodeIndex];
                    enemy.transform.position = Vector3.MoveTowards(
                        enemy.transform.position,
                        targetPosition,
                        enemy.Speed * Time.deltaTime * 5);

                    float distanceToTarget = Vector3.Distance(enemy.transform.position, targetPosition);
                    if (distanceToTarget < 0.1f)
                    {
                        enemy.NodeIndex++;
                        enemy.transform.position = targetPosition;

                        if (enemy.NodeIndex >= enemyNodePositions.Length)
                        {
                            enemy.ReachedEnd();
                            EnqueueEnemyToRemove(enemy);
                        }
                    }

                    if (frameCounter % 30 == 0)
                    {
                        Debug.Log($"Enemy ID={enemy.ID}, Path={enemy.PlayerPathIndex}, Node={enemy.NodeIndex}, Pos={enemy.transform.position}, Target={targetPosition}");
                    }
                }
            }
        }
    }

    private void ProcessTowers()
    {
        if (TowersInGame != null)
        {
            foreach (TowerBehaviour tower in TowersInGame.ToList())
            {
                if (tower != null)
                {
                    tower.Target = TowerTargetting.GetTarget(tower, TowerTargetting.TargetType.First);
                    tower.Tick();
                }
            }
        }
    }

    private void ProcessEffects()
    {
        if (EffectQueue != null && EffectQueue.Count > 0)
        {
            int effectCount = EffectQueue.Count;
            for (int i = 0; i < effectCount; i++)
            {
                if (EffectQueue.Count > 0)
                {
                    GameData.ApplyEffectData effectData = EffectQueue.Dequeue();
                    if (effectData.EnemytoAffect != null && effectData.EffectToApply != null)
                    {
                        if (effectData.EnemytoAffect.ActiveEffects != null)
                        {
                            var existingEffect = effectData.EnemytoAffect.ActiveEffects.Find(
                                x => x != null && x.EffectName == effectData.EffectToApply.EffectName);

                            if (existingEffect == null)
                                effectData.EnemytoAffect.ActiveEffects.Add(effectData.EffectToApply);
                            else
                                existingEffect.ExpireTime = effectData.EffectToApply.ExpireTime;
                        }
                    }
                }
            }
        }
    }

    private void ProcessEnemyTicks()
    {
        if (Entitysummoner.EnemiesInGame != null)
        {
            foreach (Enemy enemy in Entitysummoner.EnemiesInGame.ToList())
            {
                if (enemy != null)
                    enemy.Tick();
            }
        }
    }

    private void ProcessDamage()
    {
        if (DamageDatas != null && DamageDatas.Count > 0)
        {
            int damageCount = DamageDatas.Count;
            for (int i = 0; i < damageCount; i++)
            {
                if (DamageDatas.Count > 0)
                {
                    GameData.EnemyDamageData damageData = DamageDatas.Dequeue();
                    if (damageData.targetedEnemy != null)
                    {
                        damageData.targetedEnemy.Health -= damageData.TotalDamage / damageData.Resistance;
                        if (PlayerStatistics != null)
                            PlayerStatistics.AddMoney((int)damageData.TotalDamage);
                        if (damageData.targetedEnemy.Health <= 0f)
                            EnqueueEnemyToRemove(damageData.targetedEnemy);
                    }
                }
            }
        }
    }

    private void ProcessEnemyRemovals()
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
                        Entitysummoner.DespawnEnemy(enemyToRemove);
                }
            }
        }
    }

    private void HandleGameOver()
    {
        if (gameManager != null)
        {
            gameManager.PlayerDied();
            StartCoroutine(DelayedGameOver());
        }
    }

    private IEnumerator DelayedGameOver()
    {
        yield return new WaitForSeconds(0.2f);
        if (gameUIEvent != null)
        {
            Debug.Log($"Showing GameOver with latestWaveIndex={latestWaveIndex} on client (IsMaster={PhotonNetwork.IsMasterClient})");
            gameUIEvent.ShowGameOver(latestWaveIndex);
        }
    }

    private void HandleVictoryCondition()
    {
        if (gameUIEvent != null)
        {
            Debug.Log($"Showing Victory with latestWaveIndex={latestWaveIndex} on client (IsMaster={PhotonNetwork.IsMasterClient})");
            gameUIEvent.ShowVictory(latestWaveIndex);
        }
    }

    IEnumerator SpawnWave()
    {
        bool safeToSpawn = true;
        WaveData currentWave = null;
        LevelData currentLevel = null;

        if (levels == null || currentLevelIndex >= levels.Length)
        {
            safeToSpawn = false;
        }
        else
        {
            currentLevel = levels[currentLevelIndex];
            if (currentLevel == null || currentLevel.waves == null || currentWaveIndex >= currentLevel.waves.Count)
            {
                safeToSpawn = false;
            }
            else
            {
                currentWave = currentLevel.waves[currentWaveIndex];
                if (currentWave == null || currentWave.enemies == null)
                    safeToSpawn = false;
            }
        }

        if (!safeToSpawn)
        {
            Debug.LogWarning($"Cannot spawn wave: LevelIndex={currentLevelIndex}, WaveIndex={currentWaveIndex}");
            yield break;
        }

        isSpawningWave = true;

        double waveStartTime = 0;
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            waveStartTime = PhotonNetwork.Time + 1.0;
            photonView.RPC("SyncWaveStartTime", RpcTarget.All, currentLevelIndex, currentWaveIndex, waveStartTime);
            while (PhotonNetwork.Time < waveStartTime)
                yield return null;
        }

        Entitysummoner summoner = FindObjectOfType<Entitysummoner>();
        if (summoner != null)
        {
            Debug.Log($"Spawning wave with {currentWave.enemies.Count} enemy types for paths 0 and 1");
            summoner.SummonWaveSimultaneous(currentWave.enemies, new int[] { 0, 1 });
        }
        else
        {
            Debug.LogError("Entitysummoner not found!");
        }

        if (PhotonNetwork.IsConnected)
        {
            double delayEndTime = PhotonNetwork.Time + currentWave.delayAfterWave;
            while (PhotonNetwork.Time < delayEndTime)
                yield return null;
        }
        else
        {
            yield return new WaitForSeconds(currentWave.delayAfterWave);
        }

        currentWaveIndex++;
        latestWaveIndex = currentWaveIndex;

        if (PhotonNetwork.IsConnected && photonView != null)
        {
            double waveEndTime = PhotonNetwork.Time;
            Debug.Log($"Master sending SyncWaveComplete: LevelIndex={currentLevelIndex}, WaveIndex={currentWaveIndex}, LatestWaveIndex={latestWaveIndex}");
            photonView.RPC("SyncWaveComplete", RpcTarget.All, currentLevelIndex, currentWaveIndex, latestWaveIndex, waveEndTime);
        }

        isSpawningWave = false;
    }

    [PunRPC]
    private void SyncWaveStartTime(int levelIndex, int waveIndex, double startTime)
    {
        currentLevelIndex = levelIndex;
        currentWaveIndex = waveIndex;
        Debug.Log($"SyncWaveStartTime received: LevelIndex={levelIndex}, WaveIndex={waveIndex} on client (IsMaster={PhotonNetwork.IsMasterClient})");
    }

    [PunRPC]
    private void SyncWaveComplete(int levelIndex, int waveIndex, int latestWaveIdx, double endTime)
    {
        currentLevelIndex = levelIndex;
        currentWaveIndex = waveIndex;
        latestWaveIndex = latestWaveIdx; // Fixed: Update field with parameter
        Debug.Log($"SyncWaveComplete received: LevelIndex={levelIndex}, WaveIndex={waveIndex}, LatestWaveIndex={latestWaveIdx} on client (IsMaster={PhotonNetwork.IsMasterClient})");
    }

    [PunRPC]
    public void ApplyPowerUpRPC(int typeInt, int targetPlayerPathIndex, float effectValue, float duration)
    {
        PowerUpType type = (PowerUpType)typeInt;
        PowerUp.ApplyPowerUpEffect(type, targetPlayerPathIndex, effectValue, duration);
    }

    [PunRPC]
    public void TakeDamage(int amount)
    {
        if (PlayerStatistics != null)
        {
            PlayerStatistics.DecreaseLives(amount);
            if (PlayerStatistics.GetLives() <= 0 && gameManager != null)
                gameManager.PlayerDied();
        }
    }

    private bool IsWaveComplete()
    {
        return Entitysummoner.EnemiesInGame.Count == 0 && EnemyIDsToSummon.Count == 0;
    }

    public static void EnqueueEffectData(GameData.ApplyEffectData effectData)
    {
        if (EffectQueue != null)
            EffectQueue.Enqueue(effectData);
    }

    public static void EnqueueDamageData(GameData.EnemyDamageData damageData)
    {
        if (DamageDatas != null)
            DamageDatas.Enqueue(damageData);
    }

    public static void EnqueueEnemyIDToSummon(int enemyId, int pathIndex = 0)
    {
        if (Instance != null && Instance.EnemyIDsToSummon != null)
            Instance.EnemyIDsToSummon.Enqueue(new EnemySpawnInfo { EnemyID = enemyId, PathIndex = pathIndex });
    }

    public static void EnqueueEnemyToRemove(Enemy enemyToRemove)
    {
        if (EnemiesToRemove != null)
            EnemiesToRemove.Enqueue(enemyToRemove);
    }
}