using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;

public class MultiShoot : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private GunBank gunBank;

    [SerializeField]
    private float shootingRate = 0.2f; 

    private float lastShootTime;

    private void Start()
    {
        // Get reference to the GunBank 
        if (gunBank == null)
        {
            gunBank = GetComponent<GunBank>();
        }
    }

    private void Update()
    {
        // Only process input for the local player
        if (!photonView.IsMine) return;

        // Handle shooting with rate limiting
        if (Mouse.current.leftButton.isPressed && Time.time > lastShootTime + shootingRate)
        {
            lastShootTime = Time.time;
            Shoot();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && gunBank != null && gunBank.ActiveGun != null)
        {
            ResetShoot();
        }
    }

    private void Shoot()
    {
        if (gunBank != null && gunBank.ActiveGun != null)
        {
            // Handle local effects
            ParticleSystem shootSystem = gunBank.ActiveGun.Model.GetComponentInChildren<ParticleSystem>();
            if (shootSystem != null)
            {
                shootSystem.Play();

                Vector3 shootPosition = shootSystem.transform.position;
                Vector3 shootDirection = Camera.main.transform.forward;

                // Synchronize the shot with other players
                photonView.RPC("RPCShoot", RpcTarget.Others, shootPosition, shootDirection);

                // Process the shot
                ProcessShot(shootPosition, shootDirection);
            }
        }
    }

    private void ProcessShot(Vector3 shootPosition, Vector3 shootDirection)
    {
        if (gunBank.ActiveGun.ShootingConfig != null)
        {
            Vector3 direction = shootDirection;

            // Apply spread 
            if (gunBank.ActiveGun.ShootingConfig.Spread != Vector3.zero)
            {
                direction += new Vector3(
                    Random.Range(-gunBank.ActiveGun.ShootingConfig.Spread.x, gunBank.ActiveGun.ShootingConfig.Spread.x),
                    Random.Range(-gunBank.ActiveGun.ShootingConfig.Spread.y, gunBank.ActiveGun.ShootingConfig.Spread.y),
                    Random.Range(-gunBank.ActiveGun.ShootingConfig.Spread.z, gunBank.ActiveGun.ShootingConfig.Spread.z)
                );
                direction.Normalize();
            }

            if (Physics.Raycast(shootPosition, direction, out RaycastHit hit, float.MaxValue, gunBank.ActiveGun.ShootingConfig.HitMask))
            {
                // Start trail effect
                StartCoroutine(gunBank.ActiveGun.ClientPlayTrail(shootPosition, hit.point, hit, true));

                // Handle damage for local player
                if (photonView.IsMine && hit.collider != null)
                {
                    // Check if we hit an enemy
                    Enemy enemy = hit.collider.GetComponent<Enemy>() ?? hit.collider.GetComponentInParent<Enemy>();

                    // Get the local player's path index
                    int playerPathIndex = -1;
                    PlayerStat playerStat = GetComponentInParent<PlayerStat>();

                    if (playerStat == null)
                    {
                        // If not directly attached, try to find it by ownership
                        PlayerStat[] allPlayerStats = GameObject.FindObjectsOfType<PlayerStat>();
                        foreach (PlayerStat stat in allPlayerStats)
                        {
                            PhotonView pv = stat.GetComponent<PhotonView>();
                            if (pv != null && pv.IsMine)
                            {
                                playerStat = stat;
                                break;
                            }
                        }
                    }
                   
                    if (playerStat != null)
                    {
                        playerPathIndex = playerStat.PathIndex;
                    }

                    // Determine if we can damage this target refer on path
                    bool canDamage = true;
                    if (enemy != null && playerPathIndex >= 0)
                    {
                        // Only allow damage to enemies on the player's path
                        canDamage = (enemy.PlayerPathIndex == playerPathIndex);

                        if (!canDamage)
                        {
                            Debug.Log($"Cannot damage enemy on path {enemy.PlayerPathIndex} - player controls path {playerPathIndex}");
                        }
                    }

                    if (canDamage)
                    {
                        int damage = gunBank.ActiveGun.DamageConfig.GetDamage();
                        Debug.Log($"Shot hit target with damage: {damage}");

                        // Apply damage to the enemy
                        MultiDamage networkDamagable = hit.collider.GetComponent<MultiDamage>() ?? hit.collider.GetComponentInParent<MultiDamage>();

                        if (networkDamagable != null)
                        {
                            // Apply damage over the network
                            networkDamagable.ApplyDamageNetwork(damage, playerPathIndex);

                            // Provide immediate visual feedback
                            if (enemy != null && enemy.HPBar != null)
                            {
                                float predictedHealth = Mathf.Max(0, enemy.Health - damage);
                                enemy.HPBar.UpdateHealthBar(Mathf.RoundToInt(predictedHealth), enemy.MaxHP);
                            }
                        }
                        else
                        {
                            // Handle damage for other types of objects
                            DamagableItem damagable = hit.collider.GetComponent<DamagableItem>() ?? hit.collider.GetComponentInParent<DamagableItem>();

                            if (damagable != null)
                            {
                                damagable.TakeDamage(damage);

                                // Provide immediate visual feedback
                                if (damagable is Enemy)
                                {
                                    GameloopManager.EnqueueDamageData(new GameData.EnemyDamageData(enemy, damage, 1f));
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // No hit, just show the trail effect
                StartCoroutine(gunBank.ActiveGun.ClientPlayTrail(shootPosition,
                    shootPosition + (direction * gunBank.ActiveGun.TrailConfiguration.DisappearDistance),
                    new RaycastHit(), false));
            }
        }
    }

    private void ResetShoot()
    {
        if (gunBank != null && gunBank.ActiveGun != null)
        {
            gunBank.ActiveGun.ResetShoot();
            photonView.RPC("RPCResetShoot", RpcTarget.Others);
        }
    }
   
    
    [PunRPC]
    // Remote clients to play the shoot effect
    private void RPCShoot(Vector3 shootPosition, Vector3 shootDirection)
    {
        // Play particle effect for remote players
        if (gunBank != null && gunBank.ActiveGun != null)
        {
            ParticleSystem shootSystem = gunBank.ActiveGun.Model.GetComponentInChildren<ParticleSystem>();
            if (shootSystem != null)
            {
                shootSystem.Play();
            }

            // Process visual effects for remote players
            if (gunBank.ActiveGun.ShootingConfig != null)
            {
                Vector3 direction = shootDirection;

                // Apply spread 
                if (gunBank.ActiveGun.ShootingConfig.Spread != Vector3.zero)
                {
                    direction += new Vector3(
                        Random.Range(-gunBank.ActiveGun.ShootingConfig.Spread.x, gunBank.ActiveGun.ShootingConfig.Spread.x),
                        Random.Range(-gunBank.ActiveGun.ShootingConfig.Spread.y, gunBank.ActiveGun.ShootingConfig.Spread.y),
                        Random.Range(-gunBank.ActiveGun.ShootingConfig.Spread.z, gunBank.ActiveGun.ShootingConfig.Spread.z)
                    );
                    direction.Normalize();
                }

                if (Physics.Raycast(shootPosition, direction, out RaycastHit hit, float.MaxValue, gunBank.ActiveGun.ShootingConfig.HitMask))
                {
                    StartCoroutine(gunBank.ActiveGun.ClientPlayTrail(shootPosition, hit.point, hit, true));
                }
                else
                {
                    StartCoroutine(gunBank.ActiveGun.ClientPlayTrail(shootPosition, shootPosition + (direction * gunBank.ActiveGun.TrailConfiguration.DisappearDistance), new RaycastHit(), false));
                }
            }
        }
    }

    [PunRPC]
    private void RPCResetShoot()
    {
        if (gunBank != null && gunBank.ActiveGun != null)
        {
            gunBank.ActiveGun.ResetShoot();
        }
    }
}