using UnityEngine;  // Add this - required for ScriptableObject
using System.Collections.Generic;

[System.Serializable]
public class EnemyWaveInfo
{
    [SerializeField] 
    public Enemysummondata enemyData;
    [SerializeField] 
    public int count;
    [SerializeField] 
    public float spawnInterval = 1f;
}

[System.Serializable]
public class WaveData
{
    [SerializeField] 
    public List<EnemyWaveInfo> enemies = new List<EnemyWaveInfo>();
    [SerializeField] 
    public float delayAfterWave = 5f;
}

[CreateAssetMenu(fileName = "New LevelData", menuName = "Create LevelData")]
public class LevelData : ScriptableObject  // Must inherit from ScriptableObject
{
    [SerializeField] 
    public List<WaveData> waves = new List<WaveData>();
}