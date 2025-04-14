using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "Trail Config", menuName = "Guns/Gun Trail Config", order = 2)]
public class TrailConfig : ScriptableObject
{
    public Material Material;
    public AnimationCurve WidthCurve;
    public float Duratime = 0.5f;   
    public float MinVertexDistance = 0.1f;
    public Gradient Color;

    public float DisappearDistance = 100f;
    public float ShootingSpeed = 100f;

    public GameObject HitEffectPrefab;
    public float HitEffectDuration = 2f;

}
