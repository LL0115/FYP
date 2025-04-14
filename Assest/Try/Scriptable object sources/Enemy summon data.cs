using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Enum to categorize different enemy types
public enum EnemyType
{
    Basic,
    Scout,
    Tank,
    Healer,
    Swarm,
    Explosive,
    Boss
}

[CreateAssetMenu(fileName = "New EnemySummonData", menuName = "Create EnemySummonData")]
public class Enemysummondata : ScriptableObject
{
    public GameObject EnemyPrefab;
    public int EnemyID;
    public EnemyType Type = EnemyType.Basic; // Default to basic enemy
}