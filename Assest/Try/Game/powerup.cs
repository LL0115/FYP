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
        if (PhotonNetwork.IsConnected)
        {
            // Find GameloopManager to communicate with
            GameloopManager manager = GameloopManager.Instance;
            if (manager != null && manager.photonView != null)
            {
                // Send RPC through the GameloopManager
                manager.photonView.RPC("ApplyPowerUpRPC", RpcTarget.All, 
                    (int)type, targetPlayerPathIndex, effectValue, duration);
            }
            else
            {
                Debug.LogError("Cannot find GameloopManager with PhotonView to send power-up RPC");
            }
        }
        else
        {
            // In single player, directly apply the effect
            ApplyPowerUpEffect(type, targetPlayerPathIndex, effectValue, duration);
        }
    }

    // Static method to apply power-up effects
    public static void ApplyPowerUpEffect(PowerUpType type, int targetPlayerPathIndex, float effectValue, float duration)
    {
        switch (type)
        {
            case PowerUpType.ExtraLife:
                // Get player stat for target path
                PlayerStat playerStat = GameloopManager.GetPlayerStatForPath(targetPlayerPathIndex);
                if (playerStat != null)
                {
                    playerStat.IncreaseLives(Mathf.RoundToInt(effectValue));
                    Debug.Log($"Added {effectValue} lives to player on path {targetPlayerPathIndex}");
                }
                break;

            case PowerUpType.EnemySpeedDebuff:
                // Apply speed increase to enemies on target path
                ApplyEnemyDebuff(targetPlayerPathIndex, true, effectValue, duration);
                break;

            case PowerUpType.EnemyHealthDebuff:
                // Apply health increase to enemies on target path
                ApplyEnemyDebuff(targetPlayerPathIndex, false, effectValue, duration);
                break;
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