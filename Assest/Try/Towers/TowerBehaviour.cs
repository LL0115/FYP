using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class TowerBehaviour : MonoBehaviourPun , DamagableItem
{
    [Header("Tower Configuration")]
    public LayerMask EnemiesLayer;
    public Transform TowerPivot;
    public Sprite towerIcon;
    public string towerName;
    public TowerType TowerType;
    public TowerTargetting.TargetType targetingType = TowerTargetting.TargetType.First;
    
    [Header("Tower Stats")]
    public float Damage = 10f;
    public float Range = 10f;
    public float Firerate = 1f;
    public int SummonCost = 100;

    [Header("Tower HP")]
    public int MaxHealth = 100;
    public int CurrentHealth;
    public int CurrentHP => CurrentHealth;
    public int MaxHP => MaxHealth;
    public event System.Action<int> DamageDealt;
    public event System.Action<Vector3> DeathEvent;
    public GameObject HPBarPrefab;

    [Header("Tower Upgrades")]
    public int maxUpgradeLevel = 3;
    public float damageIncreasePerLevel = 5f;
    public float firerateIncreasePerLevel = 0.2f;
    public int baseUpgradeCost = 50;
    private int currentUpgradeLevel = 0;
 

    [Header("Tower Ownership")]
    public int ownerPathIndex = 0; // Path index of the player who placed this tower
    
    [Header("Debug Visualization")]
    public bool showRangeGizmo = true;
    public Color rangeGizmoColor = new Color(0.5f, 0.5f, 1f, 0.3f);
    
    // Current target enemy
    [HideInInspector] public Enemy Target;
    
    // Internal variables
    private float Delay;
    private float lastTargetSearchTime = 0f;
    private float targetSearchInterval = 0.2f; // Search for target every 0.2 seconds
    private IDamageMethod CurrentDamageMethodClass;
    private bool isInitialized = false;

    void Start()
    {
        Initialize();
    }
    
    private void Initialize()
    {
        if (isInitialized) return;

        //Set Hp
        CurrentHealth = MaxHealth;

        // Initialize HP bar
        InitializeHPBar();

        // Get the damage method component
        CurrentDamageMethodClass = GetComponent<IDamageMethod>();

        if (CurrentDamageMethodClass == null)
        {
            Debug.LogError($"Towers: No damage class attached to tower {gameObject.name}");
        }
        else
        {
            CurrentDamageMethodClass.Init(Damage, Firerate);
        }

        // Set tower pivot if not assigned
        if (TowerPivot == null)
        {
            // Try to find a child named "Pivot" or "TowerPivot"
            Transform pivot = transform.Find("Pivot");
            if (pivot == null) pivot = transform.Find("TowerPivot");
            if (pivot == null && transform.childCount > 0) pivot = transform.GetChild(0);
            
            if (pivot != null)
            {
                TowerPivot = pivot;
            }
            else
            {
                Debug.LogWarning($"Tower {gameObject.name} has no pivot assigned. Using main transform.");
                TowerPivot = transform;
            }
        }

        Delay = 1f / Firerate;
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized)
        {
            Initialize();
        }
        
        // Handle visuals on all clients 
        if (Target != null && TowerPivot != null)
        {
            // Always rotate towards target on all clients for visual consistency
            RotateTowardsTarget();
        }
    }

    // Called regularly from GameloopManager
    public void Tick()
    {
        if (!isInitialized)
        {
            Initialize();
        }
        
        // Only process game logic on master client in multiplayer
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return;
            
        // Search for target periodically
        if (Time.time - lastTargetSearchTime > targetSearchInterval || Target == null)
        {
            FindTarget();
            lastTargetSearchTime = Time.time;
            
            // Sync the current target to all clients
            if (photonView != null && Target != null)
            {
                int targetViewID = Target.GetComponent<PhotonView>().ViewID;
                photonView.RPC("RPC_SyncTarget", RpcTarget.Others, targetViewID);
            }
            else if (photonView != null && Target == null)
            {
                photonView.RPC("RPC_ClearTarget", RpcTarget.Others);
            }
        }
        
        // Validate current target
        if (Target != null)
        {
            // Check if target is still valid (not destroyed and in range)
            if (Target.Health <= 0 || Vector3.Distance(transform.position, Target.transform.position) > Range)
            {
                Target = null;
                
                // Notify clients that target is cleared
                if (photonView != null)
                {
                    photonView.RPC("RPC_ClearTarget", RpcTarget.Others);
                }
                return;
            }
            
            // Apply damage
            if (CurrentDamageMethodClass != null)
            {
                CurrentDamageMethodClass.DamageTick(Target);
            }
            else
            {
                Debug.LogError("Towers: No damage class attached to given tower");
            }
        }
    }

    public void TakeDamage(int damage)
    {
        // Check if this is a local player trying to damage the tower
        if (photonView.IsMine && PhotonNetwork.IsConnected)
        {
            Debug.Log("Local player cannot damage their own towers");
            // Let the owner handle damage
            return;
        }

        CurrentHealth -= damage;
        DamageDealt?.Invoke(damage);

        // Sync health in multiplayer
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            // Sync to everyone including the owner
            photonView.RPC("RPC_SyncHealth", RpcTarget.All, CurrentHealth);
        }

        // Check if tower is destroyed
        if (CurrentHealth <= 0)
        {
            DeathEvent?.Invoke(transform.position);

            // If we're the master client, destroy the tower network-wide
            if (PhotonNetwork.IsMasterClient && PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Destroy(gameObject);
            }
            // In single player or if somehow we're not connected to Photon
            else if (!PhotonNetwork.IsConnected)
            {
                Destroy(gameObject);
            }
        }
    }

    [PunRPC]
    private void RPC_SyncHealth(int newHealth)
    {
        // Update the current health
        CurrentHealth = newHealth;
        DamageDealt?.Invoke(0);

        // If health reaches zero and we're not already handling destruction
        // in the TakeDamage method, trigger the destruction locally
        if (CurrentHealth <= 0)
        {
            DeathEvent?.Invoke(transform.position);
            Destroy(gameObject);
            
        }


    }

    private void InitializeHPBar()
    {
        // Check if HP bar prefab exists in resources
        GameObject hpBarPrefab = Resources.Load<GameObject>("UI/HPBar");

        if (hpBarPrefab != null)
        {
            // Instantiate HP bar
            GameObject hpBarObj = Instantiate(hpBarPrefab, transform.position + Vector3.up * 2f, Quaternion.identity);
            hpBarObj.transform.SetParent(transform);

            // Initialize the HP bar
            HPBar hpBar = hpBarObj.GetComponent<HPBar>();
            if (hpBar != null)
            {
                hpBar.Initialize(this);
            }
            else
            {
                Debug.LogError("HPBar component not found on HPBar prefab");
            }
        }
        else
        {
            Debug.LogError("HPBar prefab not found in Resources/UI/HPBar");
        }
    }

    private void FindTarget()
    {
        // Use the improved TowerTargetting system
        Target = TowerTargetting.GetTarget(this, targetingType);
    }
    
    private void RotateTowardsTarget()
    {
        if (Target == null || TowerPivot == null) return;
        
        // Calculate the direction to the target
        Vector3 direction = Target.transform.position - transform.position;
        // Optionally keep rotation only on horizontal plane
        direction.y = 0;
        
        // Only calculate rotation if we have a valid direction
        if (direction != Vector3.zero)
        {
            // Calculate the target rotation
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            // Smoothly rotate towards the target rotation
            TowerPivot.rotation = Quaternion.Slerp(TowerPivot.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
    
    // For UI element to show information about the tower
    public string GetTowerInfo()
    {
        string info = $"{towerName}\nDamage: {Damage:F1}\nRate: {Firerate:F1}/s\nRange: {Range:F1}m";
        if (CanUpgrade())
        {
            info += $"\nLevel: {currentUpgradeLevel + 1}/{maxUpgradeLevel + 1}";
            info += $"\nUpgrade Cost: {GetUpgradeCost()}";
        }
        else
        {
            info += "\nMAX LEVEL";
        }
        return info;
    }
    
    // Visualize tower range in editor and game
    private void OnDrawGizmosSelected()
    {
        if (showRangeGizmo)
        {
            Gizmos.color = rangeGizmoColor;
            Gizmos.DrawWireSphere(transform.position, Range);
        }
    }
    
    // Upgrade tower stats
    public void UpgradeTower(float damageBuff, float firerateBuff)
    {
        if (currentUpgradeLevel >= maxUpgradeLevel) return;
        
        Damage += damageBuff;
        Firerate += firerateBuff;
        currentUpgradeLevel++;
        
        if (CurrentDamageMethodClass != null)
        {
            CurrentDamageMethodClass.Init(Damage, Firerate);
        }
        
        // Sync the upgrade in multiplayer
        if (PhotonNetwork.IsConnected && photonView != null)
        {
            photonView.RPC("RPC_SyncUpgrade", RpcTarget.All, Damage, Firerate, currentUpgradeLevel);
        }
    }

    public int GetUpgradeCost()
    {
        if (currentUpgradeLevel >= maxUpgradeLevel) return -1;
        // Cost increases with each level
        return baseUpgradeCost * (currentUpgradeLevel + 1);
    }

    public bool CanUpgrade()
    {
        return currentUpgradeLevel < maxUpgradeLevel;
    }

    public int GetCurrentLevel()
    {
        return currentUpgradeLevel;
    }

    // Add these RPCs to synchronize targeting
    [PunRPC]
    private void RPC_SyncTarget(int targetViewID)
    {
        // Find the target enemy by its PhotonView ID
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView != null)
        {
            Target = targetView.GetComponent<Enemy>();
            if (Target == null)
            {
                Debug.LogError($"Found PhotonView {targetViewID} but it has no Enemy component");
            }
        }
        else
        {
            Debug.LogError($"Could not find PhotonView with ID {targetViewID}");
        }
    }

    [PunRPC]
    private void RPC_ClearTarget()
    {
        Target = null;
    }
    
    [PunRPC]
    private void RPC_SyncUpgrade(float newDamage, float newFirerate, int newLevel)
    {
        Damage = newDamage;
        Firerate = newFirerate;
        currentUpgradeLevel = newLevel;
        
        if (CurrentDamageMethodClass != null)
        {
            CurrentDamageMethodClass.Init(Damage, Firerate);
        }
    }

    [PunRPC]
    public void RPC_SetupTower(int ownerPathIndex)
    {
        Debug.Log($"RPC_SetupTower called with ownerPathIndex={ownerPathIndex}");
        
        try 
        {
            // Store the ownerPathIndex
            this.ownerPathIndex = ownerPathIndex;
            
            // Add to the GameloopManager if it exists
            if (GameloopManager.Instance != null && GameloopManager.TowersInGame != null)
            {
                if (!GameloopManager.TowersInGame.Contains(this))
                {
                    GameloopManager.TowersInGame.Add(this);
                    Debug.Log($"Added tower to GameloopManager.TowersInGame");
                }
            }
            else
            {
                Debug.LogWarning("GameloopManager.Instance or TowersInGame is null!");
            }
            
            // Fix collider if needed
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.isTrigger = false;
                Debug.Log("Set collider.isTrigger to false");
            }
            
            // Any other setup needed for this tower
            // ...
            
            Debug.Log($"Tower setup completed successfully for pathIndex {ownerPathIndex}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in RPC_SetupTower: {e.Message}\nStackTrace: {e.StackTrace}");
        }
    }

    [PunRPC]
    public void RPC_ShowAttackEffect(int targetViewID)
    {
        // Find the target
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView != null)
        {
            Transform targetTransform = targetView.transform;
            
            // Show visual effect (like a muzzle flash, laser beam, etc.)
            PlayAttackVisualEffect(targetTransform);
        }
    }

    private void PlayAttackVisualEffect(Transform target)
    {
        // Example: Create a line renderer to show a laser attack
        // Or instantiate particle effects
        // This is just a placeholder - implement based on your tower type
        Debug.Log($"Tower {gameObject.name} attack visual effect played");
    }
}

// Define this enum if you haven't already
public enum TowerType
{
    Basic,
    Sniper,
    Machine,
    Splash,
    Laser,
    Slow,
    Buff,
    Special,
    Missile,
    Flamethrower
}