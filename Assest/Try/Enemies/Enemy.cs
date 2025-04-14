using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Enemy : MonoBehaviourPun, DamagableItem
{
    public float MaxHealth;
    public float Health;
    public float DamageResistance = 1f;
    public int DamageToBase = 1; 
    public float Speed;
    public int ID;
    
    public HealthBarPrefab HPBarPrefab;
    public HPBar HPBar;
    public event System.Action<int> DamageDealt;
    public event System.Action<Vector3> DeathEvent;
    public int MaxHP => Mathf.RoundToInt(MaxHealth);
    public int CurrentHP => Mathf.RoundToInt(Health);

    public List<Effects> ActiveEffects;
    public Transform RootPart;
    public int NodeIndex;

    // The player/path index this enemy belongs to (0, 1, etc.)
    [Tooltip("The player/path index this enemy belongs to (0, 1, etc.)")]
    public int PlayerPathIndex = 0;

    // Reference to GameManager for handling multiplayer
    private GameManager gameManager;
    private new PhotonView photonView;
    
    // Track if we've been properly initialized from network data
    private bool initializedFromNetwork = false;

    protected virtual void Awake()
    {
        gameManager = GameManager.Instance;
        photonView = GetComponent<PhotonView>();
        
        // If in a networked game and this is a freshly instantiated enemy,
        // initialize some values immediately
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            Debug.Log($"Enemy Awake with PhotonView ID: {photonView.ViewID}");
            
            // Get the instantiation data right away if available
            if (photonView.InstantiationData != null && photonView.InstantiationData.Length > 0)
            {
                try {
                    // The first parameter is the path index
                    PlayerPathIndex = (int)photonView.InstantiationData[0];
                    initializedFromNetwork = true;
                    Debug.Log($"Enemy initialized from instantiation data with path index {PlayerPathIndex}");
                }
                catch (System.Exception e) {
                    Debug.LogError($"Error reading instantiation data: {e.Message}");
                }
            }
        }
    }
    
    private void OnEnable()
    {
        if (PhotonNetwork.IsConnected)
        {
            // If this is a network-instantiated enemy and we're not the master client
            if (photonView != null && !PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"Non-master client registering network enemy: {photonView.ViewID}, PathIndex: {PlayerPathIndex}");
                // Register this enemy with the Entitysummoner
                Entitysummoner.RegisterNetworkSpawnedEnemy(this);
                
                // Initialize health bar now
                if (HPBarPrefab != null && HPBar == null)
                {
                    CreateHPBar();
                }
            }
        }
    }
    
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // This method is called automatically when the object is network instantiated
        if (photonView.InstantiationData != null && photonView.InstantiationData.Length > 0)
        {
            try {
                // The first parameter is the path index
                PlayerPathIndex = (int)photonView.InstantiationData[0];
                initializedFromNetwork = true;
                Debug.Log($"Enemy instantiated with path index {PlayerPathIndex} from network data");
            }
            catch (System.Exception e) {
                Debug.LogError($"Error processing instantiation data: {e.Message}");
            }
        }
    }

    public virtual void Init()
    {
        try
        {
            if (this == null || gameObject == null) return;

            // Initialize the effects list if needed
            if (ActiveEffects == null)
            {
                ActiveEffects = new List<Effects>();
            }
            else
            {
                ActiveEffects.Clear(); // Clear any existing effects
            }
            
            // Set health to max
            Health = MaxHealth;

            // Important: Only set position if we're the master client or in single player
            // Non-master clients shouldn't override position that will be synced from master
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) 
            {
                // Get the correct node positions array for this enemy's player path
                Vector3[] nodePositions = GameloopManager.GetNodePositionsForPlayer(PlayerPathIndex);
                
                if (nodePositions != null && nodePositions.Length > 0)
                {
                    transform.position = nodePositions[0];
                    Debug.Log($"Setting enemy position to {nodePositions[0]} (first node of path {PlayerPathIndex})");
                }
                else
                {
                    Debug.LogError($"No node positions found for player path index {PlayerPathIndex}");
                }
            }

            // Initialize node index - but don't override if already set by Entitysummoner
            if (NodeIndex == 0)
            {
                NodeIndex = 1; // Start at first node (Entitysummoner already sets this)
            }

            // Create HP bar if needed
            if (HPBarPrefab != null && HPBar == null)
            {
                CreateHPBar();
            }
            
            // Update health bar
            if (HPBar != null)
            {
                HPBar.UpdateHealthBar(MaxHP, CurrentHP);
            }
            
            Debug.Log($"Enemy initialized: Position={transform.position}, NodeIndex={NodeIndex}, Health={Health}, PathIndex={PlayerPathIndex}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Enemy.Init: {e.Message}");
        }
    }

    public float GetHealth()
    {
        return CurrentHP; // or whatever property stores health
    }
    
    // Get the correct path node positions for this enemy
    private Vector3[] GetMyPathNodes()
    {
        Vector3[] nodes = GameloopManager.GetNodePositionsForPlayer(PlayerPathIndex);
        if (nodes == null || nodes.Length == 0)
        {
            Debug.LogError($"Failed to get node positions for path {PlayerPathIndex}");
            // Fallback to default nodes - use the static method
            nodes = GameloopManager.GetNodePositionsForPlayer(0); // Try path 0 as fallback
            
            if (nodes == null || nodes.Length == 0)
            {
                Debug.LogError("Could not get any node positions! Enemy movement will fail.");
                return new Vector3[0]; // Return empty array as last resort
            }
        }
        return nodes;
    }
    
    public virtual void ResetEnemy()
    {
        try
        {
            if (this == null || gameObject == null) return;
            
            Health = MaxHealth;
            NodeIndex = 1; // Start at first node
            
            // Only master client sets position
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
            {
                // Get the correct node positions for this enemy's path
                Vector3[] nodePositions = GetMyPathNodes();
                
                if (nodePositions != null && nodePositions.Length > 0)
                {
                    transform.position = nodePositions[0];
                }
            }
            
            if (ActiveEffects != null)
            {
                ActiveEffects.Clear();
            }
            
            if (HPBar != null)
            {
                HPBar.UpdateHealthBar(Mathf.RoundToInt(MaxHealth), Mathf.RoundToInt(Health));
            }
            
            Debug.Log($"Enemy reset: Position={transform.position}, NodeIndex={NodeIndex}, Health={Health}, PathIndex={PlayerPathIndex}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Enemy.ResetEnemy: {e.Message}");
        }
    }

    public void Tick()
    {
        try
        {
            // Only process effects on the master client
            if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
                return;
                
            if (ActiveEffects == null)
                return;
                
            for(int i = 0; i < ActiveEffects.Count; i++)
            {
                if(ActiveEffects[i].ExpireTime > 0f)
                {
                    if(ActiveEffects[i].DamageDelay > 0f)
                    {
                        ActiveEffects[i].DamageDelay -= Time.deltaTime;
                    }
                    else
                    {
                        float damage = ActiveEffects[i].Damage;
                        GameloopManager.EnqueueDamageData(new GameData.EnemyDamageData(this, damage, 1f));
                        ActiveEffects[i].DamageDelay = 1f / ActiveEffects[i].DamageRate;
                    }
                }
                ActiveEffects[i].ExpireTime -= Time.deltaTime;
            }
            ActiveEffects.RemoveAll(effect => effect.ExpireTime <= 0f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Enemy.Tick: {e.Message}");
        }
    }

    public void ReachedEnd()
    {
        try
        {
            // In multiplayer, only the master client should process enemy reaching the end
            if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
                return;
                    
            Debug.Log($"Enemy {gameObject.name} reached the end of path {PlayerPathIndex}. Damaging player base.");
            
            // FIND ALL PLAYER STATS IN THE GAME
            PlayerStat[] allPlayerStats = GameObject.FindObjectsOfType<PlayerStat>();
            
            // Log all PlayerStats and their path indices for debugging
            foreach (PlayerStat stat in allPlayerStats)
            {
                PhotonView pv = stat.GetComponent<PhotonView>();
                string ownerName = (pv != null && pv.Owner != null) ? pv.Owner.NickName : "No Owner";
                bool isOwner = (pv != null) ? pv.IsMine : false;
                
                Debug.Log($"Found PlayerStat: Path={stat.PathIndex}, Owner={ownerName}, IsMine={isOwner}");
            }
            
            // Find the player stat component for the correct player - MATCHING THE PATH INDEX
            PlayerStat targetPlayerStat = null;
            foreach (PlayerStat stat in allPlayerStats)
            {
                if (stat.PathIndex == PlayerPathIndex)
                {
                    targetPlayerStat = stat;
                    PhotonView pv = stat.GetComponent<PhotonView>();
                    string ownerName = (pv != null && pv.Owner != null) ? pv.Owner.NickName : "No Owner";
                    Debug.Log($"Found matching PlayerStat for path {PlayerPathIndex}. Owner: {ownerName}");
                    break;
                }
            }
            
            if (targetPlayerStat != null)
            {
                // In multiplayer, we need to use RPCs to damage the correct player
                if (PhotonNetwork.IsConnected)
                {
                    PhotonView playerView = targetPlayerStat.GetComponent<PhotonView>();
                    
                    if (playerView != null && playerView.Owner != null)
                    {
                        Debug.Log($"Sending damage RPC to {playerView.Owner.NickName} for {DamageToBase} damage on path {PlayerPathIndex}");
                        
                        // Option 1: Send RPC to the specific owner
                        playerView.RPC("RemoteTakeDamage", playerView.Owner, DamageToBase);
                        
                        // Option 2: Send RPC to all clients - the owner will process it
                        // playerView.RPC("RemoteTakeDamage", RpcTarget.All, DamageToBase);
                    }
                    else
                    {
                        Debug.LogError($"PlayerStat on path {PlayerPathIndex} doesn't have a valid PhotonView or Owner!");
                        
                        // Try direct damage as fallback
                        targetPlayerStat.TakeDamage(DamageToBase);
                    }
                }
                else
                {
                    // Single player - directly damage the player
                    targetPlayerStat.TakeDamage(DamageToBase);
                }
            }
            else
            {
                Debug.LogWarning($"No PlayerStat found with path index {PlayerPathIndex} when enemy reached end!");
                
                // Fallback - find ANY player stat as a last resort
                if (allPlayerStats.Length > 0)
                {
                    Debug.Log($"Using fallback PlayerStat (found {allPlayerStats.Length} in total)");
                    // Choose the first one found
                    allPlayerStats[0].TakeDamage(DamageToBase);
                }
                else
                {
                    Debug.LogError("No PlayerStat components found in scene!");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Enemy.ReachedEnd: {e.Message}\n{e.StackTrace}");
        }
    }

    public void TakeDamage(int damage)
    {
        try
        {
            // Check if we're in a networked game
            if (PhotonNetwork.IsConnected)
            {
                // Only allow damage if this is the local player causing it
                if (!PhotonNetwork.LocalPlayer.IsMasterClient)
                {
                    // Non-master clients should send damage request to master
                    if (photonView != null)
                    {
                        photonView.RPC("RPC_TakeDamage", RpcTarget.MasterClient, damage);
                        return;
                    }
                }
                else
                {
                    // Master client processes damage and then tells others
                    ProcessDamage(damage);

                    // Broadcast the health update to others
                    if (photonView != null)
                    {
                        photonView.RPC("RPC_SyncHealth", RpcTarget.Others, Health);
                    }
                    return;
                }
            }
            // Single player, just process damage locally
            ProcessDamage(damage);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Enemy.TakeDamage: {e.Message}");
        }
    }

    // Add a new RPC to sync health directly
    [PunRPC]
    public void RPC_SyncHealth(float newHealth)
    {
        Health = newHealth;
        ForceUpdateHealthBar();
    }

    [PunRPC]
    public void RPC_TakeDamage(int damage)
    {
        ProcessDamage(damage);
    }

    public void ForceUpdateHealthBar()
    {
        if (HPBar != null)
        {
            HPBar.UpdateHealthBar(MaxHP, CurrentHP);
        }
    }

    protected virtual void ProcessDamage(int damage)
    {
        try
        {
            if (damage <= 0) return; // Avoid processing zero or negative damage

            float previousHealth = Health;
            Health -= damage;

            // More detailed debugging
            Debug.Log($"Enemy {gameObject.name} taking {damage} damage. Health: {previousHealth} -> {Health}. Stack: {new System.Diagnostics.StackTrace()}");

            // Ensure we invoke the event for health bar updates
            DamageDealt?.Invoke(damage);

            // Directly update the health bar if available
            if (HPBar != null)
            {
                HPBar.UpdateHealthBar(MaxHP, CurrentHP);
            }

            if (Health <= 0 && previousHealth > 0)
            {
                Die();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Enemy.ProcessDamage: {e.Message}");
        }
    }

    protected virtual void Die()
    {
        try
        {
            // Invoke the death event for effects, etc.
            DeathEvent?.Invoke(transform.position);
            
            // Handle scoring and rewards - only master client awards money
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
            {
                // Instead of using FindObjectOfType, use the player stat for this path
                PlayerStat playerStat = GameloopManager.GetPlayerStatForPath(PlayerPathIndex);
                if (playerStat != null)
                {
                    // Award kill score/money
                    int reward = Mathf.RoundToInt(MaxHealth / 10f); // Simple example reward calculation
                    playerStat.AddMoney(reward);
                }
            }
            
            // Master client handles despawning in multiplayer
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
            {
                // Remove the enemy from the game
                Entitysummoner.DespawnEnemy(this);
            }
            else
            {
                // Non-master clients just unregister from tracking lists
                Entitysummoner.RemoveEnemyFromTrackingLists(this);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Enemy.Die: {e.Message}");
        }
    }
    
    private void CreateHPBar()
    {
        try
        {
            if (HPBarPrefab != null)
            {
                GameObject healthBarObject = HPBarPrefab.CreateHealthBar(transform, this);
                if (healthBarObject != null)
                {
                    HPBar = healthBarObject.GetComponent<HPBar>();
                    if (HPBar != null)
                    {
                        HPBar.UpdateHealthBar(MaxHP, CurrentHP);
                    }
                    else
                    {
                        Debug.LogError("Failed to get HPBar component from health bar object");
                    }
                }
                else
                {
                    Debug.LogError("Failed to create health bar object");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in Enemy.CreateHPBar: {e.Message}");
        }
    }

}