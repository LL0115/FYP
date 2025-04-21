using UnityEngine;
using Photon.Pun;
using System.Collections;

public enum PowerUpType
{
    ExtraLife,
    EnemySpeedDebuff,
    EnemyHealthDebuff
}

public class PowerUp : MonoBehaviour
{
    [System.Serializable]
    public class PowerUpData
    {
        public PowerUpType type;
        public string name;
        public string description;
        public int cost;
        public Sprite icon;
        public float effectValue; // Amount of effect (e.g., +1 life, +20% speed)
        public float duration; // Duration in seconds for temporary effects (0 for permanent)
    }

    // Static method to apply a power-up through the GameManager
    public static void ApplyPowerUp(PowerUpType type, int targetPlayerPathIndex, float effectValue, float duration)
    {
        Debug.Log($"ApplyPowerUp called: Type={type}, TargetPath={targetPlayerPathIndex}, Value={effectValue}, Duration={duration}");
        
        // Find the GameloopManager for RPC
        GameloopManager gameloopManager = GameloopManager.Instance;
        
        if (gameloopManager != null && gameloopManager.photonView != null && PhotonNetwork.IsConnected)
        {
            // Send RPC to all clients
            gameloopManager.photonView.RPC("ApplyPowerUpRPC", RpcTarget.AllBuffered, 
                (int)type, targetPlayerPathIndex, effectValue, duration);
        }
        else
        {
            // Apply locally as fallback or in single player
            ApplyPowerUpEffect(type, targetPlayerPathIndex, effectValue, duration);
        }
    }

    // Static method to apply power-up effects
    public static void ApplyPowerUpEffect(PowerUpType type, int targetPlayerPathIndex, float effectValue, float duration)
    {
        Debug.Log($"Applying power-up effect: {type} to path {targetPlayerPathIndex}");
        
        switch (type)
        {
            case PowerUpType.ExtraLife:
                // Extra life code here
                PlayerStat playerStat = GameloopManager.GetPlayerStatForPath(targetPlayerPathIndex);
                if (playerStat != null)
                {
                    playerStat.IncreaseLives(Mathf.RoundToInt(effectValue));
                }
                break;
                
            case PowerUpType.EnemySpeedDebuff:
                ApplyEnemySpeedDebuff(targetPlayerPathIndex, effectValue, duration);
                break;
                
            case PowerUpType.EnemyHealthDebuff:
                // Health debuff code here 
                break;
        }
    }

    private static void ApplyEnemySpeedDebuff(int targetPathIndex, float effectValue, float duration)
    {
        Debug.Log($"Applying speed debuff to enemies on path {targetPathIndex}: {effectValue}% for {duration}s");
        
        // Find all enemies
        Enemy[] allEnemies = Object.FindObjectsOfType<Enemy>();
        Debug.Log($"Found {allEnemies.Length} total enemies");
        
        // Calculate multiplier (convert percentage to multiplier)
        // Positive effectValue means enemies move faster (like 20 = 20% faster = 1.2x)
        float speedMultiplier = 1f + (effectValue / 100f);
        
        int affectedCount = 0;
        
        // Apply to all enemies on the target path
        foreach (Enemy enemy in allEnemies)
        {
            if (enemy == null) continue;
            
            if (enemy.PlayerPathIndex == targetPathIndex)
            {
                affectedCount++;
                enemy.ApplySpeedDebuff(speedMultiplier, duration);
            }
        }
        
        Debug.Log($"Applied speed debuff to {affectedCount} enemies");
        
        // Register with enemy spawner for future enemies
        RegisterDebuffWithSpawners(targetPathIndex, PowerUpType.EnemySpeedDebuff, effectValue, duration);
    }

    private static void RegisterDebuffWithSpawners(int targetPathIndex, PowerUpType type, float value, float duration)
    {
        // Find all enemy spawners
        var spawners = Object.FindObjectsOfType<Entitysummoner>();
        
        foreach (var spawner in spawners)
        {
            // Check if this spawner belongs to the target path
            if (spawner.PlayerPathIndex == targetPathIndex)
            {
                // Register the debuff
                spawner.RegisterDebuff(type, value, duration, Time.time);
                Debug.Log($"Registered {type} debuff with spawner for path {targetPathIndex}");
            }
        }
    }

    private static void ApplyEnemyDebuff(int targetPathIndex, bool isSpeedDebuff, float effectValue, float duration)
    {
        // Find all enemies on the target path
        Enemy[] allEnemies = Object.FindObjectsOfType<Enemy>();
        
        foreach (Enemy enemy in allEnemies)
        {
            // Only affect enemies on the target path
            if (enemy.PlayerPathIndex == targetPathIndex)
            {
                if (isSpeedDebuff)
                {
                    // Increase enemy speed by effectValue percent
                    enemy.Speed *= (1 + effectValue/100f);
                    Debug.Log($"Increased speed of enemy on path {targetPathIndex} by {effectValue}%");
                }
                else
                {
                    // Increase enemy damage resistance (making them harder to kill)
                    enemy.DamageResistance *= (1 + effectValue/100f);
                    Debug.Log($"Increased damage resistance of enemy on path {targetPathIndex} by {effectValue}%");
                }
            }
        }

        // If we're in multiplayer and using a time-limited effect, register it for cleanup
        if (PhotonNetwork.IsConnected && duration > 0)
        {
            // Only the master client should track and revert time-based effects
            if (PhotonNetwork.IsMasterClient)
            {
                GameloopManager manager = GameloopManager.Instance;
                if (manager != null)
                {
                    manager.StartCoroutine(RevertDebuffAfterDelay(targetPathIndex, isSpeedDebuff, effectValue, duration));
                }
            }
        }
        else if (duration > 0)
        {
            // Single player mode
            MonoBehaviour coroutineRunner = GameloopManager.Instance as MonoBehaviour;
            if (coroutineRunner != null)
            {
                coroutineRunner.StartCoroutine(RevertDebuffAfterDelay(targetPathIndex, isSpeedDebuff, effectValue, duration));
            }
        }
    }

    private static IEnumerator RevertDebuffAfterDelay(int targetPathIndex, bool isSpeedDebuff, float effectValue, float duration)
    {
        Debug.Log($"Starting countdown to revert {(isSpeedDebuff ? "speed" : "health")} debuff on path {targetPathIndex} in {duration} seconds");
        yield return new WaitForSeconds(duration);

        // Find all enemies on the target path again (may include new enemies)
        Enemy[] allEnemies = Object.FindObjectsOfType<Enemy>();
        
        foreach (Enemy enemy in allEnemies)
        {
            // Only affect enemies on the target path
            if (enemy.PlayerPathIndex == targetPathIndex)
            {
                if (isSpeedDebuff)
                {
                    // Revert enemy speed
                    enemy.Speed /= (1 + effectValue/100f);
                    Debug.Log($"Reverted speed debuff of enemy on path {targetPathIndex}");
                }
                else
                {
                    // Revert enemy health/resistance
                    enemy.DamageResistance /= (1 + effectValue/100f);
                    Debug.Log($"Reverted resistance debuff of enemy on path {targetPathIndex}");
                }
            }
        }

        // Broadcast the effect ending to all players
        if (PhotonNetwork.IsConnected)
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                string effectName = isSpeedDebuff ? "Speed Debuff" : "Health Debuff";
                gameManager.SendPlayerNotification(PhotonNetwork.LocalPlayer, 
                    GameManager.NotificationType.Join, 
                    $"The {effectName} on path {targetPathIndex} has expired");
            }
        }
    }
}