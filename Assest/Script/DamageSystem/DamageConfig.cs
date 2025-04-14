using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Damage Config" , menuName = "Guns/Damage Config", order = 3)]
public class DamageConfig : ScriptableObject
{
    public int Damage;

    public int GetDamage()
    {
        return Damage;
    }

}
