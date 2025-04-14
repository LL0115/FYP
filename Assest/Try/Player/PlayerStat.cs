using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerStat : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField] private int startingLives = 3;
    [SerializeField] private int startingMoney = 1000;
    
    private int lives;
    private int money;
    private GameManager gameManager;
    private bool hasNotifiedGameOver = false;
    
    // UI event callbacks
    public event System.Action<int> OnLivesChanged;
    public event System.Action<int> OnMoneyChanged;
    
    [SerializeField] private int pathIndex = 0;
    
    // Property with network synchronization - only PathIndex needs to be synced
    public int PathIndex 
    { 
        get { return pathIndex; }
        set 
        { 
            pathIndex = value;
            // Only sync PathIndex as it affects gameplay for all players
            if (PhotonNetwork.IsConnected && photonView != null && photonView.IsMine)
            {
                photonView.RPC("SyncPathIndex", RpcTarget.Others, pathIndex);
            }
            Debug.Log($"PlayerStat PathIndex set to {pathIndex} for {(photonView?.Owner?.NickName ?? "local player")}");
        }
    }
    
    void Start()
    {
        gameManager = GameManager.Instance;
        
        // Initialize stats - only for our own player
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            lives = startingLives;
            money = startingMoney;
            
            // Trigger initial UI updates
            OnLivesChanged?.Invoke(lives);
            OnMoneyChanged?.Invoke(money);
        }
        
        // Register with GameManager if available
        if (gameManager != null)
        {
            Debug.Log($"PlayerStat registering with GameManager, PathIndex={PathIndex}");
            if (gameManager.GetType().GetMethod("RegisterPlayerStat") != null)
            {
                gameManager.SendMessage("RegisterPlayerStat", this, SendMessageOptions.DontRequireReceiver);
            }
        }
        else
        {
            Debug.LogWarning("GameManager not found during PlayerStat Start!");
        }
    }
    
    private void Awake()
    {
        // In multiplayer, determine which player this PlayerStat belongs to
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            // If this is our local player's PlayerStat
            if (photonView.IsMine)
            {
                // Set path index based on actor number
                PathIndex = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % 2;
                Debug.Log($"Local player stats using path index {PathIndex} (Actor: {PhotonNetwork.LocalPlayer.ActorNumber})");
                
                // Broadcast path index only (not stats)
                photonView.RPC("SyncPathIndex", RpcTarget.OthersBuffered, PathIndex);
            }
        }
    }
    
    [PunRPC]
    public void SyncPathIndex(int newPathIndex)
    {
        // Only update if we don't own this object
        if (!photonView.IsMine)
        {
            pathIndex = newPathIndex;
            Debug.Log($"Received path index {pathIndex} for player {photonView.Owner.NickName}");
        }
    }
    
    public int GetLives()
    {
        return lives;
    }
    
    public int GetMoney()
    {
        return money;
    }

    // Add this new RPC method
    [PunRPC]
    public void RemoteTakeDamage(int damage)
    {
        Debug.Log($"[RPC] Player on path {PathIndex} received remote damage request for {damage} damage. IsMine={photonView.IsMine}");
        
        // Only the owner should process this
        if (photonView.IsMine)
        {
            Debug.Log($"Processing remote damage request - I am the owner of this PlayerStat (Path: {PathIndex})");
            TakeDamage(damage);
        }
        else
        {
            Debug.Log($"Ignoring remote damage request - not the owner of this PlayerStat (Path: {PathIndex})");
        }
    }
    
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;
        
        // Only modify our own player's stats
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            Debug.Log($"[DAMAGE] Player on path {PathIndex} taking {damage} damage. Current lives: {lives}, reducing to {lives - damage}");
            lives -= damage;
            
            // Ensure lives don't go below 0
            if (lives < 0) lives = 0;
            
            // Update UI
            OnLivesChanged?.Invoke(lives);
            
            // Update GameManager
            if (gameManager != null)
            {
                gameManager.UpdateLives(-damage); // Negative because it's damage
            }
            
            // Check for player death
            if (lives <= 0 && !hasNotifiedGameOver)
            {
                Debug.Log($"Player on path {PathIndex} lost all lives!");
                hasNotifiedGameOver = true;
                
                // Notify all players of death, but don't sync stats
                if (PhotonNetwork.IsConnected)
                {
                    photonView.RPC("NotifyPlayerDeath", RpcTarget.All);
                    
                    // Notify GameManager of player death
                    if (gameManager != null)
                    {
                        Debug.Log("Notifying GameManager of player death");
                        gameManager.PlayerDied();
                    }
                }
                else
                {
                    // Single player mode
                    if (gameManager != null)
                    {
                        gameManager.GameOver();
                    }
                }
            }
        }
        else
        {
            Debug.Log($"Ignoring damage request - not owner of this PlayerStat (Path: {PathIndex})");
        }
    }
    
    [PunRPC]
    public void NotifyPlayerDeath()
    {
        Debug.Log($"Received death notification for player on path {PathIndex}");
        
        // If this is OUR player death (we own this object), update UI
        if (photonView.IsMine)
        {
            lives = 0;
            OnLivesChanged?.Invoke(lives);
            hasNotifiedGameOver = true;
            
            // Notify our GameManager
            if (GameManager.Instance != null)
            {
                Debug.Log("Notifying GameManager of local player death");
                GameManager.Instance.PlayerDied();
            }
        }
        
        // Master client needs to know about all player deaths for game logic
        if (PhotonNetwork.IsMasterClient && GameManager.Instance != null && !photonView.IsMine) 
        {
            Debug.Log($"Master client notifying GameManager about remote player death (path: {PathIndex})");
            GameManager.Instance.SendMessage("OnPlayerDeathReceived", photonView.Owner, SendMessageOptions.DontRequireReceiver);
        }
    }
    
    public void DecreaseLives(int amount)
    {
        TakeDamage(amount);
    }
    
    public void IncreaseLives(int amount)
    {
        if (amount <= 0) return;
        
        // Only local player can modify their own stats
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            lives += amount;
            
            // Update UI
            OnLivesChanged?.Invoke(lives);
            
            // Update GameManager
            if (gameManager != null)
            {
                gameManager.UpdateLives(amount);
            }
            
            // No need to sync lives anymore
        }
    }
    
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        
        // Only local player can modify their own stats
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            money += amount;
            
            // Update UI
            OnMoneyChanged?.Invoke(money);
            
            // Update GameManager
            if (gameManager != null)
            {
                gameManager.UpdateMoney(amount);
            }
            
            // No need to sync money anymore
        }
    }
    
    // Removed SyncMoney RPC as each player manages their own money
    
    public bool SpendMoney(int amount)
    {
        // Only local player can spend their own money
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return false;
        
        if (money >= amount)
        {
            money -= amount;
            Debug.Log($"Money spent successfully. New balance: {money}");
            
            // Update UI
            OnMoneyChanged?.Invoke(money);
            
            // Update GameManager
            if (gameManager != null)
            {
                gameManager.UpdateMoney(-amount); // Negative because it's spending
            }
            
            // No need to sync money anymore
            
            return true;
        }
        
        return false;
    }

    // Modified to only sync PathIndex and death notification status
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send only necessary data
            stream.SendNext(pathIndex);
            stream.SendNext(hasNotifiedGameOver);
        }
        else
        {
            // Network player, receive data
            pathIndex = (int)stream.ReceiveNext();
            hasNotifiedGameOver = (bool)stream.ReceiveNext();
            
            // No need to update UI for stats that aren't being synced
        }
    }

    public void ResetStats()
    {
        // Only reset our own player's stats
        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            lives = startingLives;
            money = startingMoney;
            hasNotifiedGameOver = false;
            
            // Update UI
            OnLivesChanged?.Invoke(lives);
            OnMoneyChanged?.Invoke(money);
            
            // No need to sync reset anymore
        }
    }

    [PunRPC]
    public void RPC_SpendMoney(int amount)
    {
        Debug.Log($"[{gameObject.name}] RPC_SpendMoney called: amount={amount}, money={money}, IsMine={photonView.IsMine}");
        
        // This RPC is called directly on the owner, so we don't need ownership checks
        if (money >= amount)
        {
            money -= amount;
            Debug.Log($"[{gameObject.name}] Money spent via RPC. New balance: {money}");
            
            // Update UI
            OnMoneyChanged?.Invoke(money);
            
            // Update GameManager
            if (gameManager != null)
            {
                gameManager.UpdateMoney(-amount);
            }
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] RPC_SpendMoney failed: Not enough money. Have {money}, need {amount}");
        }
    }
    
    // Debug helper method
    public void LogDebugInfo()
    {
        string ownerInfo = photonView != null ? 
            (photonView.Owner != null ? photonView.Owner.NickName : "No Owner") : "No PhotonView";
            
        Debug.Log($"PlayerStat Debug Info:\n" +
                  $"  Path Index: {PathIndex}\n" +
                  $"  Lives: {lives}\n" +
                  $"  Money: {money}\n" +
                  $"  Is Local Player: {(photonView?.IsMine ?? false)}\n" +
                  $"  Owner: {ownerInfo}\n" +
                  $"  Has Notified Game Over: {hasNotifiedGameOver}");
    }
    
    // Add this to be able to call debug from inspector or event system
    public void DebugButtonPressed()
    {
        LogDebugInfo();
    }
  
    [PunRPC]
    public void AddMoneyRPC(int amount)
    {
        Debug.Log($"[RPC] Player on path {PathIndex} received money reward: {amount}");

        // Only process if this is our player
        if (photonView.IsMine)
        {
            Debug.Log($"Processing money reward: Adding {amount} to player on path {PathIndex}");
            AddMoney(amount);
        }
        else
        {
            Debug.Log($"Ignoring money reward - not owner of this PlayerStat (Path: {PathIndex})");
        }
    }
    
}