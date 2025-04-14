using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[CreateAssetMenu(fileName = "Gun", menuName = "Guns/Gun", order = 0)]
public class ScirptableGun : ScriptableObject
{
    public GunType Type;
    public string Name;

    public GameObject ModelPrefab;
    public GameObject Model;
    public Vector3 SpawnPoint;
    public Vector3 SpawnRotation;

    public DamageConfig DamageConfig;
    public ShootConfig ShootingConfig;
    public TrailConfig TrailConfiguration;

    private MonoBehaviour ActiveMonoBehaviour;
    private float LastShootTime;
    private ParticleSystem ShootSystem;
    private ObjectPool<TrailRenderer> TrailPool;
    private bool canShoot = true; // New field for shooting control

    public void Spawn(Transform Parent, MonoBehaviour ActiveMonoBehaviour)
    {
        this.ActiveMonoBehaviour = ActiveMonoBehaviour;

        LastShootTime = 0;
        TrailPool = new ObjectPool<TrailRenderer>(CreateTrail);

        Model = Instantiate(ModelPrefab);
        Model.transform.SetParent(Parent, false);
        Model.transform.localPosition = SpawnPoint;
        Model.transform.localRotation = Quaternion.Euler(SpawnRotation);

        ShootSystem = Model.GetComponentInChildren<ParticleSystem>();
        if (ShootSystem == null)
        {
            Debug.LogError("ShootSystem is not found in the model. Please ensure that the ModelPrefab or one of its child objects contains a ParticleSystem component.");
        }
        else
        {
            Debug.Log("ShootSystem successfully found in the model.");
        }
    }

    private IEnumerator PlayTrail(Vector3 Start, Vector3 End, RaycastHit Hit, bool useAlternative)
    {
        if (TrailConfiguration == null)
        {
            Debug.LogError("TrailConfiguration is not assigned.");
            yield break;
        }

        TrailRenderer Trail = TrailPool.Get();
        Trail.transform.position = Start;
        Trail.Clear();
        Trail.emitting = true;

        float distance = Vector3.Distance(Start, End);
        float time = distance / TrailConfiguration.ShootingSpeed;

        if (useAlternative)
        {
            Trail.gameObject.SetActive(true);
            yield return null;

            float remainingDistance = distance;

            while (remainingDistance > 0)
            {
                Trail.transform.position = Vector3.Lerp(Start, End, Mathf.Clamp01(1 - (remainingDistance / distance)));
                remainingDistance -= TrailConfiguration.ShootingSpeed * Time.deltaTime;

                yield return null;
            }

            Trail.transform.position = End;

            yield return new WaitForSeconds(TrailConfiguration.Duratime);
            Trail.emitting = false;
            Trail.gameObject.SetActive(false);
        }
        else
        {
            float elapsedTime = 0;
            while (elapsedTime < time)
            {
                Trail.transform.position = Vector3.Lerp(Start, End, elapsedTime / time);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            Trail.emitting = false;
        }

        if (Hit.collider != null)
        {
            if (TrailConfiguration.HitEffectPrefab != null)
            {
                GameObject hitEffect = Instantiate(TrailConfiguration.HitEffectPrefab, Hit.point, Quaternion.LookRotation(Hit.normal));
                Destroy(hitEffect, TrailConfiguration.HitEffectDuration);
            }
            else
            {
                Debug.LogError("HitEffectPrefab is not assigned in the TrailConfiguration.");
            }
        }

        if (Hit.collider != null && Hit.collider.TryGetComponent(out DamagableItem damagable))
        {
            int damage = DamageConfig.GetDamage();
            Debug.Log($"Dealing {damage} damage to {Hit.collider.name}");
            damagable.TakeDamage(damage);
            Enemy enemy = Hit.collider.GetComponent<Enemy>();
            if (enemy != null)
            {
                GameloopManager.EnqueueDamageData(new GameData.EnemyDamageData(enemy, damage, 1f));
            }
        }

        TrailPool.Release(Trail);
        LastShootTime = Time.time;
    }

    private TrailRenderer CreateTrail()
    {
        GameObject TrailObject = new GameObject("Bullet Trail");
        TrailRenderer Trail = TrailObject.AddComponent<TrailRenderer>();

        Trail.material = TrailConfiguration.Material;
        Trail.widthCurve = TrailConfiguration.WidthCurve;
        Trail.time = TrailConfiguration.Duratime;
        Trail.minVertexDistance = TrailConfiguration.MinVertexDistance;
        Trail.colorGradient = TrailConfiguration.Color;

        Trail.emitting = false;
        Trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return Trail;
    }

    public void Shoot()
    {
        if (!canShoot) return; // Check if we can shoot

        if (Time.time > ShootingConfig.FireRate + LastShootTime)
        {
            if (ShootSystem == null)
            {
                Debug.LogError("ShootSystem is not initialized. Please ensure that the Spawn method has been called and the ModelPrefab contains a ParticleSystem component.");
                return;
            }

            if (ActiveMonoBehaviour == null)
            {
                Debug.LogError("ActiveMonoBehaviour is not initialized.");
                return;
            }

            UpdateGunDirection();

            ShootSystem.Play();
            Vector3 shootDirection = ShootSystem.transform.forward;
            shootDirection.Normalize();

            if (Physics.Raycast(ShootSystem.transform.position, shootDirection, out RaycastHit hit, float.MaxValue, ShootingConfig.HitMask))
            {
                ActiveMonoBehaviour.StartCoroutine(PlayTrail(ShootSystem.transform.position, hit.point, hit, true));
            }
            else
            {
                ActiveMonoBehaviour.StartCoroutine(PlayTrail(ShootSystem.transform.position, ShootSystem.transform.position + (shootDirection * TrailConfiguration.DisappearDistance), new RaycastHit(), false));
            }

            canShoot = false; // Prevent shooting until reset
        }
    }

    public void ResetShoot()
    {
        canShoot = true;
    }

    private void UpdateGunDirection()
    {
        if (Camera.main != null)
        {
            Model.transform.forward = Camera.main.transform.forward;
        }
        else
        {
            Debug.LogError("Main camera not found. Please ensure there is a camera tagged as 'MainCamera' in the scene.");
        }
    }

    public IEnumerator ClientPlayTrail(Vector3 Start, Vector3 End, RaycastHit Hit, bool useAlternative)
    {
        if (TrailConfiguration == null)
        {
            Debug.LogError("TrailConfiguration is not assigned.");
            yield break;
        }

        TrailRenderer Trail = TrailPool.Get();
        Trail.transform.position = Start;
        Trail.Clear();
        Trail.emitting = true;

        float distance = Vector3.Distance(Start, End);
        float time = distance / TrailConfiguration.ShootingSpeed;

        if (useAlternative)
        {
            Trail.gameObject.SetActive(true);
            yield return null;

            float remainingDistance = distance;

            while (remainingDistance > 0)
            {
                Trail.transform.position = Vector3.Lerp(Start, End, Mathf.Clamp01(1 - (remainingDistance / distance)));
                remainingDistance -= TrailConfiguration.ShootingSpeed * Time.deltaTime;

                yield return null;
            }

            Trail.transform.position = End;

            yield return new WaitForSeconds(TrailConfiguration.Duratime);
            Trail.emitting = false;
            Trail.gameObject.SetActive(false);
        }
        else
        {
            float elapsedTime = 0;
            while (elapsedTime < time)
            {
                Trail.transform.position = Vector3.Lerp(Start, End, elapsedTime / time);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            Trail.emitting = false;
        }

        if (Hit.collider != null)
        {
            if (TrailConfiguration.HitEffectPrefab != null)
            {
                GameObject hitEffect = Instantiate(TrailConfiguration.HitEffectPrefab, Hit.point, Quaternion.LookRotation(Hit.normal));
                Destroy(hitEffect, TrailConfiguration.HitEffectDuration);
            }
        }

        TrailPool.Release(Trail);
        LastShootTime = Time.time;
    }
}
