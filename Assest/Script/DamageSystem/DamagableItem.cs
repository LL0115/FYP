using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface DamagableItem
{
    int CurrentHP { get; }
    int MaxHP { get; }

    event System.Action<int> DamageDealt;
    event System.Action<Vector3> DeathEvent;

    void TakeDamage(int damage);
}
