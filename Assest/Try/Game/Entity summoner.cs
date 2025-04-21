using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using System.Linq;

public class Entitysummoner : MonoBehaviour
{
    public static List<Enemy> EnemiesInGame;
    public static List<Transform> EnemiesInGameTransform;
    public static Dictionary<Transform, Enemy> Enemytransformpairs;
    public static Dictionary<int, GameObject> EnemyPrefabs;
    public static Dictionary<int, Queue<Enemy>> EnemyObjectPools;
    private static Dictionary<int, bool> NetworkInstantiatedEnemies;
    private Dictionary<PowerUpType, PowerUpEffectData> activeDebuffs = new Dictionary<PowerUpType, PowerUpEffectData>();
    public int PlayerPathIndex = 0;
    private static bool isInit;
    private class PowerUpEffectData
    {
        public PowerUpType type;
        public float value;
        public float duration;
        public float startTime;
    }
    private PhotonView photonView;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            photonView = gameObject.AddComponent<PhotonView>();
            Debug.LogWarning("Added PhotonView to Entitysummoner.");
        }
    }

    public static void Init()
    {
        try
        {
            if (!isInit)
            {
                EnemyPrefabs = new Dictionary<int, GameObject>();
                EnemyObjectPools = new Dictionary<int, Queue<Enemy>>();
                EnemiesInGameTransform = new List<Transform>();
                EnemiesInGame = new List<Enemy>();
                Enemytransformpairs = new Dictionary<Transform, Enemy>();
                NetworkInstantiatedEnemies = new Dictionary<int, bool>();

                Enemysummondata[] enemyData = Resources.LoadAll<Enemysummondata>("Enemies");
                if (enemyData == null || enemyData.Length == 0)
                {
                    Debug.LogError("No enemy prefabs in Resources/Enemies!");
                }
                else
                {
                    Debug.Log($"Loaded {enemyData.Length} enemy prefabs");
                    foreach (Enemysummondata data in enemyData)
                    {
                        if (data.EnemyPrefab != null)
                        {
                            EnemyPrefabs.Add(data.EnemyID, data.EnemyPrefab);
                            EnemyObjectPools.Add(data.EnemyID, new Queue<Enemy>());
                            PhotonView view = data.EnemyPrefab.GetComponent<PhotonView>();
                            if (view == null && PhotonNetwork.IsConnected)
                            {
                                Debug.LogWarning($"Enemy prefab {data.EnemyPrefab.name} lacks PhotonView!");
                            }
                            if (PhotonNetwork.IsConnected)
                            {
                                string prefabPath = "Enemies/" + data.EnemyPrefab.name;
                                if (Resources.Load<GameObject>(prefabPath) == null)
                                {
                                    Debug.LogError($"Enemy prefab {data.EnemyPrefab.name} not in Resources/{prefabPath}!");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogError($"Enemy data ID {data.EnemyID} has null prefab!");
                        }
                    }
                }
                isInit = true;
                Debug.Log("EntitySummoner initialized");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error initializing EntitySummoner: {e.Message}\n{e.StackTrace}");
        }
    }

    public void SummonWaveSimultaneous(List<EnemyWaveInfo> enemiesToSpawn, int[] pathIndices)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Only master client can spawn waves.");
            return;
        }

        double spawnTime = PhotonNetwork.Time + 1.0;
        // Validate and serialize EnemyWaveInfo data
        List<int> validEnemyIDs = new List<int>();
        List<int> validCountsPath0 = new List<int>();
        List<int> validCountsPath1 = new List<int>();
        List<float> validSpawnIntervals = new List<float>();

        foreach (var enemyInfo in enemiesToSpawn)
        {
            if (enemyInfo.enemyData == null || enemyInfo.enemyData.EnemyID <= 0)
            {
                Debug.LogWarning("Skipping EnemyWaveInfo with null or invalid enemyData");
                continue;
            }
            if (enemyInfo.countPath0 < 0 || enemyInfo.countPath1 < 0)
            {
                Debug.LogWarning($"Invalid counts for EnemyID {enemyInfo.enemyData.EnemyID}: Path0={enemyInfo.countPath0}, Path1={enemyInfo.countPath1}. Setting to 0.");
                enemyInfo.countPath0 = Mathf.Max(0, enemyInfo.countPath0);
                enemyInfo.countPath1 = Mathf.Max(0, enemyInfo.countPath1);
            }
            validEnemyIDs.Add(enemyInfo.enemyData.EnemyID);
            validCountsPath0.Add(enemyInfo.countPath0);
            validCountsPath1.Add(enemyInfo.countPath1);
            validSpawnIntervals.Add(Mathf.Max(0, enemyInfo.spawnInterval));
            Debug.Log($"Queueing enemy type ID={enemyInfo.enemyData.EnemyID}: Path0={enemyInfo.countPath0}, Path1={enemyInfo.countPath1}, Interval={enemyInfo.spawnInterval}");
        }

        if (validEnemyIDs.Count == 0)
        {
            Debug.LogWarning("No valid enemies to spawn in wave!");
            return;
        }

        photonView.RPC("SpawnWaveAtTime", RpcTarget.All, validEnemyIDs.ToArray(), validCountsPath0.ToArray(), validCountsPath1.ToArray(), validSpawnIntervals.ToArray(), pathIndices, spawnTime);
    }

    [PunRPC]
    private void SpawnWaveAtTime(int[] enemyIDs, int[] countsPath0, int[] countsPath1, float[] spawnIntervals, int[] pathIndices, double spawnTime)
    {
        StartCoroutine(ExecuteSpawnAtTime(enemyIDs, countsPath0, countsPath1, spawnIntervals, pathIndices, spawnTime));
    }

    private IEnumerator ExecuteSpawnAtTime(int[] enemyIDs, int[] countsPath0, int[] countsPath1, float[] spawnIntervals, int[] pathIndices, double spawnTime)
    {
        while (PhotonNetwork.Time < spawnTime)
        {
            yield return null;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            PreparePlaceholders(enemyIDs, countsPath0, countsPath1, spawnIntervals, pathIndices);
            yield break;
        }

        for (int i = 0; i < enemyIDs.Length; i++)
        {
            int enemyID = enemyIDs[i];
            int countPath0 = countsPath0[i];
            int countPath1 = countsPath1[i];
            float spawnInterval = spawnIntervals[i];

            if (!EnemyPrefabs.ContainsKey(enemyID))
            {
                Debug.LogWarning($"Enemy ID {enemyID} not found in EnemyPrefabs");
                continue;
            }
            GameObject enemyPrefab = EnemyPrefabs[enemyID];
            string prefabPath = "Enemies/" + enemyPrefab.name;

            if (Resources.Load<GameObject>(prefabPath) == null)
            {
                Debug.LogError($"Enemy prefab {prefabPath} not in Resources!");
                continue;
            }

            foreach (int pathIndex in pathIndices)
            {
                int count = pathIndex == 0 ? countPath0 : countPath1;
                if (count <= 0) continue;

                Vector3[] nodePositions = GameloopManager.GetNodePositionsForPlayer(pathIndex);
                if (nodePositions == null || nodePositions.Length == 0)
                {
                    Debug.LogError($"Invalid path index {pathIndex}: No node positions");
                    continue;
                }

                Debug.Log($"Spawning {count} enemies of ID {enemyID} on path {pathIndex} at {nodePositions[0]}");

                for (int j = 0; j < count; j++)
                {
                    object[] instantiateData = new object[] { pathIndex };
                    GameObject networkEnemy = PhotonNetwork.Instantiate(
                        prefabPath,
                        nodePositions[0],
                        Quaternion.identity,
                        0,
                        instantiateData
                    );

                    Enemy summonedEnemy = networkEnemy.GetComponent<Enemy>();
                    if (summonedEnemy == null)
                    {
                        Debug.LogError($"No Enemy component on {prefabPath}");
                        PhotonNetwork.Destroy(networkEnemy);
                        continue;
                    }

                    summonedEnemy.PlayerPathIndex = pathIndex;
                    summonedEnemy.NodeIndex = 1;
                    summonedEnemy.transform.position = nodePositions[0];
                    summonedEnemy.ID = enemyID;
                    summonedEnemy.Init();

                    if (!EnemiesInGame.Contains(summonedEnemy))
                        EnemiesInGame.Add(summonedEnemy);
                    if (!EnemiesInGameTransform.Contains(summonedEnemy.transform))
                        EnemiesInGameTransform.Add(summonedEnemy.transform);
                    if (!Enemytransformpairs.ContainsKey(summonedEnemy.transform))
                        Enemytransformpairs.Add(summonedEnemy.transform, summonedEnemy);

                    PhotonView pv = networkEnemy.GetComponent<PhotonView>();
                    if (pv != null)
                        NetworkInstantiatedEnemies[pv.ViewID] = true;

                    Debug.Log($"Spawned enemy: ID={enemyID}, Path={pathIndex}, ViewID={pv?.ViewID}, Pos={networkEnemy.transform.position}, Data={string.Join(", ", instantiateData)}");

                    ApplyDebuffsToEnemy(summonedEnemy);

                    if (j < count - 1 && spawnInterval > 0)
                        yield return new WaitForSeconds(spawnInterval);
                }
            }
        }

        Debug.Log($"Wave spawned for paths {string.Join(", ", pathIndices)} at time {spawnTime}");
    }

    private void PreparePlaceholders(int[] enemyIDs, int[] countsPath0, int[] countsPath1, float[] spawnIntervals, int[] pathIndices)
    {
        for (int i = 0; i < enemyIDs.Length; i++)
        {
            int enemyID = enemyIDs[i];
            int countPath0 = countsPath0[i];
            int countPath1 = countsPath1[i];

            if (!EnemyPrefabs.ContainsKey(enemyID))
            {
                Debug.LogWarning($"Enemy ID {enemyID} not found for placeholder");
                continue;
            }

            foreach (int pathIndex in pathIndices)
            {
                int count = pathIndex == 0 ? countPath0 : countPath1;
                if (count <= 0) continue;

                Vector3[] nodePositions = GameloopManager.GetNodePositionsForPlayer(pathIndex);
                if (nodePositions == null || nodePositions.Length == 0)
                {
                    Debug.LogError($"Invalid path index {pathIndex} for placeholder");
                    continue;
                }

                for (int j = 0; j < count; j++)
                {
                    GameObject placeholder = Object.Instantiate(EnemyPrefabs[enemyID], nodePositions[0], Quaternion.identity);
                    Enemy placeholderEnemy = placeholder.GetComponent<Enemy>();
                    placeholderEnemy.PlayerPathIndex = pathIndex;
                    placeholderEnemy.NodeIndex = 1;
                    placeholderEnemy.ID = enemyID;
                    placeholderEnemy.gameObject.SetActive(true);

                    StartCoroutine(WaitForNetworkEnemy(enemyID, pathIndex, placeholder));
                }
            }
        }
    }

    private IEnumerator WaitForNetworkEnemy(int enemyID, int pathIndex, GameObject placeholder)
    {
        float timeout = 2f;
        float startTime = Time.time;

        while (Time.time < startTime + timeout)
        {
            Enemy networkEnemy = EnemiesInGame.Find(e => e.ID == enemyID && e.PlayerPathIndex == pathIndex && e.GetComponent<PhotonView>() != null);
            if (networkEnemy != null)
            {
                networkEnemy.transform.position = placeholder.transform.position;
                networkEnemy.NodeIndex = 1;
                networkEnemy.PlayerPathIndex = pathIndex;
                networkEnemy.Init();
                Destroy(placeholder);
                Debug.Log($"Replaced placeholder: ID={enemyID}, Path={pathIndex}");
                yield break;
            }
            yield return null;
        }

        Destroy(placeholder);
        Debug.LogWarning($"Timeout waiting for network enemy: ID={enemyID}, Path={pathIndex}");
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
                Debug.LogWarning($"Enemy ID {EnemyID} not found");
                return null;
            }

            Vector3[] nodePositions = GameloopManager.GetNodePositionsForPlayer(pathIndex);
            if (nodePositions == null || nodePositions.Length == 0)
            {
                Debug.LogError($"Invalid path index {pathIndex}");
                return null;
            }

            Enemy SummonedEnemy = null;
            bool isNetworkedGame = PhotonNetwork.IsConnected;
            bool isMasterClient = PhotonNetwork.IsMasterClient;

            if (isNetworkedGame && !isMasterClient)
            {
                return null;
            }

            Queue<Enemy> ReferenceQueue = EnemyObjectPools[EnemyID];
            if (!isNetworkedGame)
            {
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

            if (SummonedEnemy == null)
            {
                Vector3 spawnPosition = nodePositions[0];
                GameObject enemyPrefab = EnemyPrefabs[EnemyID];
                string enemyPrefabName = enemyPrefab.name;

                if (isNetworkedGame && isMasterClient)
                {
                    GameObject resourceCheck = Resources.Load<GameObject>("Enemies/" + enemyPrefabName);
                    if (resourceCheck == null)
                    {
                        Debug.LogError($"Enemy prefab '{enemyPrefabName}' not in Resources/Enemies!");
                        GameObject localEnemy = Object.Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
                        SummonedEnemy = localEnemy.GetComponent<Enemy>();
                        SummonedEnemy.PlayerPathIndex = pathIndex;
                    }
                    else
                    {
                        object[] instantiateData = new object[] { pathIndex };
                        GameObject networkEnemy = PhotonNetwork.Instantiate(
                            "Enemies/" + enemyPrefabName,
                            spawnPosition,
                            Quaternion.identity,
                            0,
                            instantiateData
                        );
                        SummonedEnemy = networkEnemy.GetComponent<Enemy>();
                        if (SummonedEnemy == null)
                        {
                            Debug.LogError("No Enemy component");
                            PhotonNetwork.Destroy(networkEnemy);
                            return null;
                        }
                        SummonedEnemy.PlayerPathIndex = pathIndex;
                        PhotonView pv = networkEnemy.GetComponent<PhotonView>();
                        if (pv != null)
                            NetworkInstantiatedEnemies[pv.ViewID] = true;
                        Debug.Log($"Network Enemy: ViewID={pv?.ViewID}, Path={pathIndex}, Pos={networkEnemy.transform.position}, Data={string.Join(", ", instantiateData)}");
                    }
                }
                else
                {
                    GameObject NewEnemy = Object.Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
                    SummonedEnemy = NewEnemy.GetComponent<Enemy>();
                    if (SummonedEnemy == null)
                    {
                        Debug.LogError("No Enemy component");
                        return null;
                    }
                    SummonedEnemy.PlayerPathIndex = pathIndex;
                }
            }
            else
            {
                SummonedEnemy.PlayerPathIndex = pathIndex;
            }

            SummonedEnemy.gameObject.SetActive(true);
            SummonedEnemy.NodeIndex = 1;
            SummonedEnemy.transform.position = nodePositions[0];
            SummonedEnemy.Init();
            SummonedEnemy.ID = EnemyID;

            if (!EnemiesInGame.Contains(SummonedEnemy))
                EnemiesInGame.Add(SummonedEnemy);
            if (!EnemiesInGameTransform.Contains(SummonedEnemy.transform))
                EnemiesInGameTransform.Add(SummonedEnemy.transform);
            if (!Enemytransformpairs.ContainsKey(SummonedEnemy.transform))
                Enemytransformpairs.Add(SummonedEnemy.transform, SummonedEnemy);

            Debug.Log($"Enemy ready: ID={SummonedEnemy.ID}, Path={SummonedEnemy.PlayerPathIndex}, Pos={SummonedEnemy.transform.position}");

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

            int pathIndex = EnemyToRemove.PlayerPathIndex;
            PhotonView photonView = EnemyToRemove.GetComponent<PhotonView>();
            bool isNetworkEnemy = photonView != null;

            if (isNetworkEnemy)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    RemoveEnemyFromTrackingLists(EnemyToRemove);
                    if (NetworkInstantiatedEnemies.ContainsKey(photonView.ViewID))
                        NetworkInstantiatedEnemies.Remove(photonView.ViewID);
                    PhotonNetwork.Destroy(EnemyToRemove.gameObject);
                }
                else
                {
                    RemoveEnemyFromTrackingLists(EnemyToRemove);
                }
                return;
            }

            EnemyToRemove.Health = EnemyToRemove.MaxHealth;
            Vector3[] nodePositions = GameloopManager.GetNodePositionsForPlayer(pathIndex);
            if (nodePositions != null && nodePositions.Length > 0)
                EnemyToRemove.transform.position = nodePositions[0];

            EnemyToRemove.NodeIndex = 0;
            EnemyToRemove.gameObject.SetActive(false);

            if (EnemyObjectPools.ContainsKey(EnemyToRemove.ID))
                EnemyObjectPools[EnemyToRemove.ID].Enqueue(EnemyToRemove);

            RemoveEnemyFromTrackingLists(EnemyToRemove);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error despawning enemy: {e.Message}\n{e.StackTrace}");
        }
    }

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
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error removing enemy: {e.Message}");
        }
    }

    public static void RegisterNetworkSpawnedEnemy(Enemy enemy)
    {
        try
        {
            if (enemy == null)
            {
                Debug.LogError("Attempted to register null enemy");
                return;
            }

            PhotonView pv = enemy.GetComponent<PhotonView>();
            if (pv == null)
            {
                Debug.LogError("Network enemy has no PhotonView!");
                return;
            }

            int pathIndex = 0;
            if (pv.InstantiationData != null && pv.InstantiationData.Length > 0)
            {
                pathIndex = (int)pv.InstantiationData[0];
                enemy.PlayerPathIndex = pathIndex;
                Debug.Log($"Registered enemy: ViewID={pv.ViewID}, Path={pathIndex}, Data={string.Join(", ", pv.InstantiationData)}");
            }
            else
            {
                Debug.LogError($"No instantiation data for ViewID={pv.ViewID}. Using path 0!");
                enemy.PlayerPathIndex = 0;
            }

            enemy.NodeIndex = 1;
            Vector3[] nodePositions = GameloopManager.GetNodePositionsForPlayer(pathIndex);
            if (nodePositions != null && nodePositions.Length > 0)
            {
                enemy.transform.position = nodePositions[0];
            }
            else
            {
                Debug.LogError($"No nodes for path {pathIndex}");
            }

            if (!EnemiesInGame.Contains(enemy))
                EnemiesInGame.Add(enemy);

            if (!EnemiesInGameTransform.Contains(enemy.transform))
                EnemiesInGameTransform.Add(enemy.transform);

            if (!Enemytransformpairs.ContainsKey(enemy.transform))
                Enemytransformpairs.Add(enemy.transform, enemy);

            enemy.Init();
            Debug.Log($"Enemy registered: ID={enemy.ID}, Path={enemy.PlayerPathIndex}, Pos={enemy.transform.position}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error registering enemy: {e.Message}\n{e.StackTrace}");
        }
    }

    public void RegisterDebuff(PowerUpType type, float value, float duration, float startTime)
    {
        activeDebuffs[type] = new PowerUpEffectData
        {
            type = type,
            value = value,
            duration = duration,
            startTime = startTime
        };

        if (duration > 0)
        {
            StartCoroutine(RemoveDebuffAfterDelay(type, duration));
        }
    }

    private IEnumerator RemoveDebuffAfterDelay(PowerUpType type, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (activeDebuffs.ContainsKey(type))
        {
            activeDebuffs.Remove(type);
        }
    }

    public void ApplyDebuffsToEnemy(Enemy enemy)
    {
        if (enemy == null)
        {
            Debug.LogWarning("Attempted to apply debuffs to null enemy");
            return;
        }

        foreach (var debuff in activeDebuffs.Values)
        {
            if (debuff.duration > 0 && Time.time > debuff.startTime + debuff.duration)
                continue;

            float remainingDuration = debuff.duration > 0 ? (debuff.startTime + debuff.duration) - Time.time : 0;
            if (remainingDuration <= 0 && debuff.duration > 0) continue;

            switch (debuff.type)
            {
                case PowerUpType.EnemySpeedDebuff:
                    float speedMultiplier = 1f + (debuff.value / 100f);
                    enemy.ApplySpeedDebuff(speedMultiplier, remainingDuration);
                    break;
                case PowerUpType.EnemyHealthDebuff:
                    break;
            }
        }
    }

    public static void CleanupAllEnemies()
    {
        try
        {
            if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient)
            {
                foreach (Enemy enemy in new List<Enemy>(EnemiesInGame))
                {
                    if (enemy == null) continue;
                    PhotonView pv = enemy.GetComponent<PhotonView>();
                    if (pv != null)
                        PhotonNetwork.Destroy(enemy.gameObject);
                }
            }

            foreach (Enemy enemy in new List<Enemy>(EnemiesInGame))
            {
                if (enemy != null && enemy.gameObject != null)
                {
                    PhotonView pv = enemy.GetComponent<PhotonView>();
                    if (pv == null)
                        Object.Destroy(enemy.gameObject);
                }
            }

            EnemiesInGame.Clear();
            EnemiesInGameTransform.Clear();
            Enemytransformpairs.Clear();
            NetworkInstantiatedEnemies.Clear();

            foreach (var queue in EnemyObjectPools.Values)
            {
                while (queue.Count > 0)
                {
                    Enemy pooledEnemy = queue.Dequeue();
                    if (pooledEnemy != null && pooledEnemy.gameObject != null)
                        Object.Destroy(pooledEnemy.gameObject);
                }
            }

            foreach (var id in EnemyObjectPools.Keys)
            {
                EnemyObjectPools[id] = new Queue<Enemy>();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error cleaning up enemies: {e.Message}\n{e.StackTrace}");
        }
    }
}