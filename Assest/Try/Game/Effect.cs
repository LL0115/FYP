using UnityEngine;
public class Effects
    {
        public Effects(string effectName, float damage, float damageRate, float expireTime)
        {
            EffectName = effectName;
            Damage = damage;
            DamageRate = damageRate;
            ExpireTime = expireTime;
        }

        public string EffectName;
        public float Damage;
        public float DamageRate;
        public float DamageDelay;

        public float ExpireTime;

    }