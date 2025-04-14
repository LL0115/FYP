/*
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyDy : MonoBehaviour
{
    public EnemyHP enemyHP;
    public DeathEffect deathEffect;
    public Transform player; // 角色的 Transform
    public float chaseSpeed = 2.0f; // 追逐速度
    public float attackRange = 1.5f; // 攻擊範圍
    public int damagePerSecond = 10; // 每秒造成的傷害

    [SerializeField] private HealthBarPrefab healthBarPrefab; // Added this line for health bar

    private PlayerData playerData; // 參考玩家的 PlayerData 組件
    private float attackCooldown = 1.0f; // 攻擊冷卻時間
    private float lastAttackTime; // 上次攻擊的時間
    private GameObject healthBarInstance; // Added this line to track health bar instance
    private HPBar healthBar; // Reference to the HPBar component

    private void Awake()
    {
        // Get the EnemyHP and DeathEffect components attached to the same GameObject
        enemyHP = GetComponent<EnemyHP>();
        deathEffect = GetComponent<DeathEffect>();

        if (enemyHP == null)
        {
            Debug.LogError("EnemyHP component is missing on this GameObject.");
        }

        if (deathEffect == null)
        {
            Debug.LogError("DeathEffect component is missing on this GameObject.");
        }

        // 獲取玩家的 PlayerData 組件
        if (player != null)
        {
            playerData = player.GetComponent<PlayerData>();
            if (playerData == null)
            {
                Debug.LogError("PlayerData component is missing on the player GameObject.");
            }
        }
    }

    private void Start()
    {
        // Create health bar
        if (healthBarPrefab != null && enemyHP != null)
        {
            healthBarInstance = healthBarPrefab.CreateHealthBar(transform, enemyHP);
            healthBar = healthBarInstance.GetComponent<HPBar>();
        }
        else
        {
            Debug.LogError("HealthBarPrefab or EnemyHP is not assigned!");
        }
    }

    private void OnEnable()
    {
        // Subscribe to the DamageDealt, DeathEvent, and HealthUpdated events
        if (enemyHP != null)
        {
            enemyHP.DamageDealt += OnDamageDealt;
            enemyHP.DeathEvent += OnDeath;
            enemyHP.HealthUpdated += OnHealthUpdated;
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from the DamageDealt, DeathEvent, and HealthUpdated events
        if (enemyHP != null)
        {
            enemyHP.DamageDealt -= OnDamageDealt;
            enemyHP.DeathEvent -= OnDeath;
            enemyHP.HealthUpdated -= OnHealthUpdated;
        }
    }

    private void Update()
    {
        // 簡單的追逐邏輯
        if (player != null)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            transform.position += direction * chaseSpeed * Time.deltaTime;

            // 檢查敵人和玩家之間的距離
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
            {
                // 對玩家造成傷害
                AttackPlayer();
                lastAttackTime = Time.time;
            }
        }
    }

    // Method to handle the DamageDealt event
    private void OnDamageDealt(int damage)
    {
        Debug.Log($"Enemy took {damage} damage. Current HP: {enemyHP.CurrentHP}");
    }

    // Method to handle the DeathEvent event
    private void OnDeath(Vector3 position)
    {
        Debug.Log("Enemy died.");
        // Play the death effect
        if (deathEffect != null)
        {
            deathEffect.DamagableItem_DeathEvent(position);
        }

        // The health bar will be automatically destroyed by its own script
        // when receiving the death event, but we can also destroy it here
        if (healthBarInstance != null)
        {
            Destroy(healthBarInstance);
        }

        // 銷毀敵人
        Destroy(gameObject);
    }

    // Method to handle the HealthUpdated event
    private void OnHealthUpdated()
    {
        if (healthBar != null)
        {
            healthBar.UpdateHealthBar(0);
        }
    }

    // Method to attack the player
    private void AttackPlayer()
    {
        if (playerData != null)
        {
            playerData.TakeDamage(damagePerSecond);
            Debug.Log($"Player took {damagePerSecond} damage from enemy. Current HP: {playerData.CurrentHP}");
        }
    }
}
*/