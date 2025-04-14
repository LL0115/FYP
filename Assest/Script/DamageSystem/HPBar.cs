using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class HPBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Canvas canvas;
    private Camera mainCamera;
    private DamagableItem target;
    private RectTransform fillRectTransform;
    private bool isDestroyed = false;
    private bool rewardGiven = false; // Track if reward has been given already

    // Only update health display every 0.1 sec For performance
    [SerializeField] private float updateInterval = 0.1f;
    private float updateTimer = 0f;

    public void Initialize(DamagableItem damagableTarget)
    {
        target = damagableTarget;
        mainCamera = Camera.main;

        if (fillImage != null)
        {
            fillRectTransform = fillImage.GetComponent<RectTransform>();
            fillRectTransform.pivot = new Vector2(0, 0.5f);
        }
       
        target.DamageDealt += OnDamageDealt;
        if (target is Enemy enemy)
        {
            // Got the dead enemy for reward
            enemy.DeathEvent += OnTargetDeath;
            UpdateHealthBar(target.CurrentHP, target.MaxHP);
            Debug.Log($"HPBar initialized for enemy. Current HP: {enemy.CurrentHP}, Max HP: {enemy.MaxHP}");
        }
        else
        {
            UpdateHealthBar(target.CurrentHP, target.MaxHP);
        }
    }

    private void OnDamageDealt(int damage)
    {
        if (!isDestroyed && target != null)
        {
            UpdateHealthBar(target.CurrentHP, target.MaxHP);
        }
    }

    private void Start()
    {
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera;
        }
    }

    private void Update()
    {
        // Rotate the health bar to face the camera
        if (mainCamera != null && !isDestroyed)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,mainCamera.transform.rotation * Vector3.up);
        }

        //Always Update health bar every 0.1 sec
        updateTimer += Time.deltaTime;

        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;

            // Check if we have a valid target and it's not destroyed
            if (!isDestroyed && target != null)
            {
                UpdateHealthBar(target.CurrentHP, target.MaxHP);

                if (target is Enemy enemy)
                {
                    // Check for health sync issues
                    int currentHP = enemy.CurrentHP;
                    float rawHealth = enemy.Health;
                    if (Mathf.Abs(currentHP - rawHealth) > 0.5f) // Make not too harsh on checking 
                    {
                        Debug.Log($"Health sync issue detected: CurrentHP={currentHP}, Raw Health={rawHealth}");
                        UpdateHealthBar(Mathf.RoundToInt(rawHealth), enemy.MaxHP);
                    }

                    // Check if enemy is dead but reward hasn't been given
                    if (currentHP <= 0 && !rewardGiven)
                    {
                        GiveRewardToPlayer(enemy);
                    }
                }
            }
        }
    }
    public void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (isDestroyed || fillRectTransform == null) 
            return;

        // Make sure that maxHealth is not zero that will make error
        if (maxHealth <= 0)
        {
            fillRectTransform.localScale = new Vector3(0, 1, 1);
            return;
        }

        float healthPercentage = Mathf.Clamp01((float)currentHealth / maxHealth);

        // Update the scale of the fill image
        Vector3 newScale = new Vector3(healthPercentage, 1, 1);
        fillRectTransform.localScale = newScale;

        // Update the color of the fill image based on health percentage
        if (healthPercentage <= 0)
        {
            Debug.Log($"Health bar empty. CurrentHealth: {currentHealth}, MaxHealth: {maxHealth}");
        }
    }

    private void GiveRewardToPlayer(Enemy enemy)
    {
        if (rewardGiven)
            return;

        try
        {
            // Get the player stat for this enemy path
            PlayerStat playerStat = GameloopManager.GetPlayerStatForPath(enemy.PlayerPathIndex);

       
            if (playerStat == null)
            {
                Debug.LogWarning($"Could not find PlayerStat for path {enemy.PlayerPathIndex}, attempting to use fallback");

                // find PlayerStat 
                PlayerStat[] allStats = GameObject.FindObjectsOfType<PlayerStat>();

                if (allStats != null && allStats.Length > 0)
                {
                    // Use the first one 
                    playerStat = allStats[0];
                    Debug.Log($"Using fallback PlayerStat: {playerStat.gameObject.name}");
                }
                else
                {
                    Debug.LogError("No PlayerStat components found in scene!");
                    return;
                }
            }

            if (playerStat != null)
            {
                int reward = Mathf.RoundToInt(enemy.MaxHealth / 10f);

                // in multiplayer send money thourhg RPC to ensure it goes to the right player
                if (PhotonNetwork.IsConnected && playerStat.photonView != null)
                {
                    Debug.Log($"HPBar sending AddMoneyRPC to player for {reward} gold");
                    playerStat.photonView.RPC("AddMoneyRPC", playerStat.photonView.Owner, reward);
                }
                else
                {
                    // In single player just add money directly
                    Debug.Log($"HPBar adding {reward} gold to player directly");
                    playerStat.AddMoney(reward);
                }

                rewardGiven = true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error giving reward in HPBar: {e.Message}");
        }
    }

    private void OnTargetDeath(Vector3 position)
    {
        // Final check to ensure reward is given before remobe
        if (target is Enemy enemy && !rewardGiven)
        {
            GiveRewardToPlayer(enemy);
        }

        Clean();
    }

    private void Clean()
    {
        // Before destroying, check if reward needs to be given
        if (!rewardGiven && target is Enemy enemy)
        {
            GiveRewardToPlayer(enemy);
        }

        isDestroyed = true;
     
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // Final check to ensure reward is given
        if (!rewardGiven && target is Enemy enemy)
        {
            GiveRewardToPlayer(enemy);
        }

        isDestroyed = true;
    }
}
