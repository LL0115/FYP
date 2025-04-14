using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Photon.Pun;

public class TowerTargetting 
{
    public enum TargetType
    {
        First,
        Last,
        Closest,
        Strong,
        Weak
    }

    // Cache for node positions to avoid repeated lookups
    private static Dictionary<int, Vector3[]> nodePositionsCache = new Dictionary<int, Vector3[]>();
    private static Dictionary<int, float[]> nodeDistancesCache = new Dictionary<int, float[]>();
    
    // Clear cache when level changes or for cleanup
    public static void ClearCache()
    {
        nodePositionsCache.Clear();
        nodeDistancesCache.Clear();
    }

    public static Enemy GetTarget(TowerBehaviour CurrentTower, TargetType targetMethod)
    {
        if (CurrentTower == null)
        {
            Debug.LogError("CurrentTower is null");
            return null;
        }

        Collider[] EnemiesInRange = Physics.OverlapSphere(CurrentTower.transform.position, CurrentTower.Range, CurrentTower.EnemiesLayer);

        // Skip calculation if no enemies in range
        if (EnemiesInRange == null || EnemiesInRange.Length == 0)
        {
            return null;
        }

        // Get the tower's owner path index
        int towerPathIndex = CurrentTower.ownerPathIndex;
        
        // Get the node positions for the tower's path
        Vector3[] nodePositions;
        if (nodePositionsCache.ContainsKey(towerPathIndex))
        {
            nodePositions = nodePositionsCache[towerPathIndex];
        }
        else
        {
            nodePositions = GameloopManager.GetNodePositionsForPlayer(towerPathIndex);
            if (nodePositions != null && nodePositions.Length > 0)
            {
                nodePositionsCache[towerPathIndex] = nodePositions;
            }
        }
        
        if (nodePositions == null || nodePositions.Length == 0)
        {
            Debug.LogError($"No node positions available for targeting calculation on path {towerPathIndex}");
            return null;
        }

        // Calculate node distances for this path if not already cached
        float[] nodeDistances;
        if (nodeDistancesCache.ContainsKey(towerPathIndex))
        {
            nodeDistances = nodeDistancesCache[towerPathIndex];
        }
        else if (GameloopManager.NodeDistances != null && GameloopManager.NodeDistances.Length > 0)
        {
            nodeDistances = GameloopManager.NodeDistances;
            nodeDistancesCache[towerPathIndex] = nodeDistances;
        }
        else
        {
            // Calculate node distances manually
            nodeDistances = new float[nodePositions.Length - 1];
            for (int i = 0; i < nodeDistances.Length; i++)
            {
                nodeDistances[i] = Vector3.Distance(nodePositions[i], nodePositions[i + 1]);
            }
            nodeDistancesCache[towerPathIndex] = nodeDistances;
        }

        // Filter enemies to only include those on the tower's path
        List<Enemy> validEnemies = new List<Enemy>();
        List<int> enemyIndices = new List<int>();
        
        for (int i = 0; i < EnemiesInRange.Length; i++)
        {
            Collider enemyCollider = EnemiesInRange[i];
            if (enemyCollider == null) continue;

            Enemy currentEnemy = enemyCollider.transform.parent?.GetComponent<Enemy>();
            if (currentEnemy == null)
            {
                currentEnemy = enemyCollider.GetComponent<Enemy>();
                if (currentEnemy == null)
                {
                    continue;
                }
            }

            // Only target enemies on the same path as the tower
            if (currentEnemy.PlayerPathIndex != towerPathIndex)
            {
                continue;
            }

            int enemyIndexInList = Entitysummoner.EnemiesInGame.FindIndex(x => x == currentEnemy);
            if (enemyIndexInList == -1)
            {
                // Enemy not found in the global list, might be newly spawned or removed
                continue;
            }

            validEnemies.Add(currentEnemy);
            enemyIndices.Add(enemyIndexInList);
        }

        // Skip if no valid enemies were found
        if (validEnemies.Count == 0)
        {
            return null;
        }

        // Create native arrays for the job system
        NativeArray<EnemyData> EnemiesToCalculate = new NativeArray<EnemyData>(validEnemies.Count, Allocator.TempJob);
        NativeArray<Vector3> NodePositionsNative = new NativeArray<Vector3>(nodePositions, Allocator.TempJob);
        NativeArray<float> NodeDistancesNative = new NativeArray<float>(nodeDistances, Allocator.TempJob);
        NativeArray<int> EnemyToIndex = new NativeArray<int>(new int[] { -1 }, Allocator.TempJob);
        
        // Fill the native array with data from valid enemies
        for (int i = 0; i < validEnemies.Count; i++)
        {
            Enemy currentEnemy = validEnemies[i];
            EnemiesToCalculate[i] = new EnemyData(
                currentEnemy.transform.position, 
                currentEnemy.NodeIndex, 
                currentEnemy.Health, 
                enemyIndices[i],
                currentEnemy.PlayerPathIndex
            );
        }

        // Set up job parameters based on targeting method
        SearchForEnemy EnemySearchJob = new SearchForEnemy
        {
            _EnemiesToCalculate = EnemiesToCalculate,
            _NodePositions = NodePositionsNative,
            _NodeDistances = NodeDistancesNative,
            _EnemyToIndex = EnemyToIndex,
            TargetingType = (int)targetMethod,
            TowerPosition = CurrentTower.transform.position,
            ValidEnemyCount = validEnemies.Count,
            TowerPathIndex = towerPathIndex
        };

        // Initialize compare value based on targeting method
        switch (targetMethod)
        {
            case TargetType.First:
                EnemySearchJob.CompareValue = Mathf.Infinity;
                break;
            case TargetType.Last:
                EnemySearchJob.CompareValue = Mathf.NegativeInfinity;
                break;
            case TargetType.Closest:
                EnemySearchJob.CompareValue = Mathf.Infinity;
                break;
            case TargetType.Strong:
                EnemySearchJob.CompareValue = Mathf.NegativeInfinity;
                break;
            case TargetType.Weak:
                EnemySearchJob.CompareValue = Mathf.Infinity;
                break;
        }

        // Execute the job
        JobHandle dependency = new JobHandle();
        JobHandle SearchJobHandle = EnemySearchJob.Schedule(validEnemies.Count, dependency);
        SearchJobHandle.Complete();

        // Get the result
        Enemy resultEnemy = null;
        if (EnemyToIndex[0] != -1)
        {
            int enemyIndexInGlobalList = EnemiesToCalculate[EnemyToIndex[0]].EnemyIndex;
            if (enemyIndexInGlobalList >= 0 && enemyIndexInGlobalList < Entitysummoner.EnemiesInGame.Count)
            {
                resultEnemy = Entitysummoner.EnemiesInGame[enemyIndexInGlobalList];
            }
        }

        // Dispose native arrays
        EnemyToIndex.Dispose();
        EnemiesToCalculate.Dispose();
        NodePositionsNative.Dispose();
        NodeDistancesNative.Dispose();

        return resultEnemy;
    }

    // Version of GetTarget that doesn't use jobs for simpler or single-enemy targeting
    public static Enemy GetTargetSimple(TowerBehaviour CurrentTower, TargetType targetMethod)
    {
        if (CurrentTower == null) return null;

        Collider[] EnemiesInRange = Physics.OverlapSphere(CurrentTower.transform.position, CurrentTower.Range, CurrentTower.EnemiesLayer);
        if (EnemiesInRange == null || EnemiesInRange.Length == 0) return null;

        int towerPathIndex = CurrentTower.ownerPathIndex;
        List<Enemy> validEnemies = new List<Enemy>();

        // Find all valid enemies on the same path
        foreach (Collider col in EnemiesInRange)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy == null) enemy = col.transform.parent?.GetComponent<Enemy>();
            if (enemy != null && enemy.PlayerPathIndex == towerPathIndex)
            {
                validEnemies.Add(enemy);
            }
        }

        if (validEnemies.Count == 0) return null;

        Enemy targetEnemy = null;
        
        switch (targetMethod)
        {
            case TargetType.First:
                // Find enemy closest to the end
                float minDistanceToEnd = Mathf.Infinity;
                foreach (Enemy enemy in validEnemies)
                {
                    float distToEnd = CalculateDistanceToEnd(enemy, towerPathIndex);
                    if (distToEnd < minDistanceToEnd)
                    {
                        minDistanceToEnd = distToEnd;
                        targetEnemy = enemy;
                    }
                }
                break;
                
            case TargetType.Last:
                // Find enemy furthest from the end
                float maxDistanceToEnd = Mathf.NegativeInfinity;
                foreach (Enemy enemy in validEnemies)
                {
                    float distToEnd = CalculateDistanceToEnd(enemy, towerPathIndex);
                    if (distToEnd > maxDistanceToEnd)
                    {
                        maxDistanceToEnd = distToEnd;
                        targetEnemy = enemy;
                    }
                }
                break;
                
            case TargetType.Closest:
                // Find enemy closest to the tower
                float minDistance = Mathf.Infinity;
                foreach (Enemy enemy in validEnemies)
                {
                    float dist = Vector3.Distance(CurrentTower.transform.position, enemy.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        targetEnemy = enemy;
                    }
                }
                break;
                
            case TargetType.Strong:
                // Find enemy with highest health
                float maxHealth = 0;
                foreach (Enemy enemy in validEnemies)
                {
                    if (enemy.Health > maxHealth)
                    {
                        maxHealth = enemy.Health;
                        targetEnemy = enemy;
                    }
                }
                break;
                
            case TargetType.Weak:
                // Find enemy with lowest health
                float minHealth = Mathf.Infinity;
                foreach (Enemy enemy in validEnemies)
                {
                    if (enemy.Health < minHealth)
                    {
                        minHealth = enemy.Health;
                        targetEnemy = enemy;
                    }
                }
                break;
        }
        
        return targetEnemy;
    }

    // Helper method to calculate distance to the end of path
    private static float CalculateDistanceToEnd(Enemy enemy, int pathIndex)
    {
        Vector3[] nodePositions;
        if (nodePositionsCache.ContainsKey(pathIndex))
        {
            nodePositions = nodePositionsCache[pathIndex];
        }
        else
        {
            nodePositions = GameloopManager.GetNodePositionsForPlayer(pathIndex);
            if (nodePositions != null && nodePositions.Length > 0)
            {
                nodePositionsCache[pathIndex] = nodePositions;
            }
            else
            {
                return Mathf.Infinity;
            }
        }

        float[] nodeDistances;
        if (nodeDistancesCache.ContainsKey(pathIndex))
        {
            nodeDistances = nodeDistancesCache[pathIndex];
        }
        else if (GameloopManager.NodeDistances != null && GameloopManager.NodeDistances.Length > 0)
        {
            nodeDistances = GameloopManager.NodeDistances;
            nodeDistancesCache[pathIndex] = nodeDistances;
        }
        else
        {
            nodeDistances = new float[nodePositions.Length - 1];
            for (int i = 0; i < nodeDistances.Length; i++)
            {
                nodeDistances[i] = Vector3.Distance(nodePositions[i], nodePositions[i + 1]);
            }
            nodeDistancesCache[pathIndex] = nodeDistances;
        }

        if (enemy.NodeIndex < 0 || enemy.NodeIndex >= nodePositions.Length)
        {
            return Mathf.Infinity;
        }

        float distance = Vector3.Distance(enemy.transform.position, nodePositions[enemy.NodeIndex]);
        for (int i = enemy.NodeIndex; i < nodeDistances.Length; i++)
        {
            distance += nodeDistances[i];
        }

        return distance;
    }

    struct EnemyData
    {
        public EnemyData(Vector3 position, int nodeIndex, float hp, int enemyIndex, int pathIndex = 0)
        {
            EnemyPosition = position;
            NodeIndex = nodeIndex;
            EnemyIndex = enemyIndex;
            Health = hp;
            PathIndex = pathIndex;
        }
        public Vector3 EnemyPosition;
        public int EnemyIndex;
        public int NodeIndex;
        public float Health;
        public int PathIndex;
    }

    struct SearchForEnemy : IJobFor
    {
        [ReadOnly]public NativeArray<EnemyData> _EnemiesToCalculate;
        [ReadOnly]public NativeArray<Vector3> _NodePositions;
        [ReadOnly]public NativeArray<float> _NodeDistances;
        [NativeDisableParallelForRestriction]public NativeArray<int> _EnemyToIndex;
        public Vector3 TowerPosition;
        public float CompareValue;
        public int TargetingType;
        public int ValidEnemyCount;
        public int TowerPathIndex;

        public void Execute(int index)
        {
            // Skip if index is beyond valid enemies
            if (index >= ValidEnemyCount) return;
            
            // Only target enemies on the same path as the tower
            if (_EnemiesToCalculate[index].PathIndex != TowerPathIndex) return;

            float CurrentEnemyDistanceToEnd;
            float DistanceToEnemy;
            switch ((TargetType)TargetingType)
            {
                case TargetType.First:
                {
                    CurrentEnemyDistanceToEnd = GetDistanceToEnd(_EnemiesToCalculate[index]);
                    if (CurrentEnemyDistanceToEnd < CompareValue)
                    {
                        _EnemyToIndex[0] = index;
                        CompareValue = CurrentEnemyDistanceToEnd;
                    }
                    break;
                }
                case TargetType.Last:
                {
                    CurrentEnemyDistanceToEnd = GetDistanceToEnd(_EnemiesToCalculate[index]);
                    if (CurrentEnemyDistanceToEnd > CompareValue)
                    {
                        _EnemyToIndex[0] = index;
                        CompareValue = CurrentEnemyDistanceToEnd;
                    }
                    break;
                }
                case TargetType.Closest:
                {
                    DistanceToEnemy = Vector3.Distance(TowerPosition, _EnemiesToCalculate[index].EnemyPosition);
                    if (DistanceToEnemy < CompareValue)  // Fixed this - should be less than for closest
                    {
                        _EnemyToIndex[0] = index;
                        CompareValue = DistanceToEnemy;
                    }
                    break;
                }
                case TargetType.Strong:
                {
                    if (_EnemiesToCalculate[index].Health > CompareValue)
                    {
                        _EnemyToIndex[0] = index;
                        CompareValue = _EnemiesToCalculate[index].Health;
                    }
                    break;
                }
                case TargetType.Weak:
                {
                    if (_EnemiesToCalculate[index].Health < CompareValue)
                    {
                        _EnemyToIndex[0] = index;
                        CompareValue = _EnemiesToCalculate[index].Health;
                    }
                    break;
                }
            }
        }

        private float GetDistanceToEnd(EnemyData EnemyToEvaluate)
        {
            // Safety check for node index
            if (EnemyToEvaluate.NodeIndex < 0 || EnemyToEvaluate.NodeIndex >= _NodePositions.Length) 
            {
                return Mathf.Infinity; // Return a large value to avoid selecting this enemy
            }

            float FinalDistance = Vector3.Distance(EnemyToEvaluate.EnemyPosition, _NodePositions[EnemyToEvaluate.NodeIndex]);

            for (int i = EnemyToEvaluate.NodeIndex; i < _NodeDistances.Length; i++)
            {
                FinalDistance += _NodeDistances[i];
            }

            return FinalDistance;
        }
    }
}