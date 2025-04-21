using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EnemyWaveInfo
{
    [SerializeField]
    public Enemysummondata enemyData; // Enemy type
    [SerializeField]
    public int countPath0; // Number of enemies to spawn on path 0
    [SerializeField]
    public int countPath1; // Number of enemies to spawn on path 1
    [SerializeField]
    public float spawnInterval = 1f; // Time between spawns
}

[System.Serializable]
public class WaveData
{
    [SerializeField]
    public List<EnemyWaveInfo> enemies = new List<EnemyWaveInfo>();
    [SerializeField]
    public float delayAfterWave = 5f; // Delay after wave completes
}

[CreateAssetMenu(fileName = "New LevelData", menuName = "Create LevelData")]
public class LevelData : ScriptableObject
{
    [SerializeField]
    public List<WaveData> waves = new List<WaveData>();
}