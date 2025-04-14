using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameData : MonoBehaviour
{

    public struct ApplyEffectData
    {
        public ApplyEffectData(Enemy enemytoAffect, Effects effectToApply)
        {
            EnemytoAffect = enemytoAffect;
            EffectToApply = effectToApply;
        }

        public Enemy EnemytoAffect;
        public Effects EffectToApply;
    }

    public struct EnemyDamageData
    {
        public EnemyDamageData(Enemy enemy, float damage, float resistance, int ownerPathIndex = 0)
        {
            targetedEnemy = enemy;
            TotalDamage = damage;
            Resistance = resistance;
            OwnerPathIndex = ownerPathIndex;
        }

        public Enemy targetedEnemy;
        public float TotalDamage;
        public float Resistance;
        public int OwnerPathIndex;
    }
}
