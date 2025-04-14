using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Photon.Pun;

public class PowerUpShopManager : MonoBehaviour
{
    [System.Serializable]
    public class PowerUpShopItem
    {
        public PowerUpType type;
        public string name;
        public string description;
        public int cost;
        public Sprite icon;
        public float effectValue;
        public float duration;
    }

    [SerializeField] private PowerUpShopItem[] powerUpItems;
    
    private PlayerStat playerStat;
    private UIDocument uiDocument;
    private VisualElement itemsGrid;
    
    // Events for communication with main UI
    public System.Action<string> OnPurchaseNotification;
    public System.Action OnShopClosed;
    
    public void Initialize(UIDocument document, VisualElement grid, PlayerStat stat)
    {
        uiDocument = document;
        itemsGrid = grid;
        playerStat = stat;
        
        // Set default power-ups if none are defined
        if (powerUpItems == null || powerUpItems.Length == 0)
        {
            powerUpItems = CreateDefaultPowerUps();
        }
    }
    
    private PowerUpShopItem[] CreateDefaultPowerUps()
    {
        PowerUpShopItem[] defaults = new PowerUpShopItem[3];
        
        // Extra Life
        defaults[0] = new PowerUpShopItem
        {
            type = PowerUpType.ExtraLife,
            name = "Extra Life",
            description = "Gain an additional life",
            cost = 500,
            effectValue = 1,
            duration = 0 // Permanent
        };
        
        // Enemy Speed Debuff
        defaults[1] = new PowerUpShopItem
        {
            type = PowerUpType.EnemySpeedDebuff,
            name = "Speed Debuff",
            description = "Increase enemy speed by 20% for 30 seconds",
            cost = 300,
            effectValue = 20,
            duration = 30
        };
        
        // Enemy Health Debuff
        defaults[2] = new PowerUpShopItem
        {
            type = PowerUpType.EnemyHealthDebuff,
            name = "Resistance Debuff",
            description = "Make enemies 20% harder to kill for 30 seconds",
            cost = 350,
            effectValue = 20,
            duration = 30
        };
        
        return defaults;
    }
    
    public void CreatePowerUpItems()
    {
        Debug.Log("PowerUpShopManager.CreatePowerUpItems called");
        if (itemsGrid == null || playerStat == null) return;
        
        // Add a divider
        var divider = new VisualElement();
        divider.style.height = 2;
        divider.style.marginTop = 20;
        divider.style.marginBottom = 20;
        divider.style.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        divider.style.width = new StyleLength(Length.Percent(100));
        itemsGrid.Add(divider);
        
        // Add a section label
        var powerUpLabel = new Label("Power-ups & Debuffs");
        powerUpLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        powerUpLabel.style.fontSize = 20;
        powerUpLabel.style.marginBottom = 10;
        powerUpLabel.style.width = new StyleLength(Length.Percent(100));
        itemsGrid.Add(powerUpLabel);
        
        // Add power-up items to shop
        foreach (var powerUpItem in powerUpItems)
        {
            var itemContainer = CreatePowerUpShopItem(powerUpItem);
            itemsGrid.Add(itemContainer);
        }
    }
    
    private VisualElement CreatePowerUpShopItem(PowerUpShopItem powerUpItem)
    {
        var itemContainer = new VisualElement();
        itemContainer.AddToClassList("shop-item");
        itemContainer.style.width = 150;
        itemContainer.style.height = 200;
        itemContainer.style.marginTop = 10;
        itemContainer.style.backgroundColor = new Color(0.24f, 0.24f, 0.24f);

        var imageContainer = new VisualElement();
        imageContainer.style.width = 100;
        imageContainer.style.height = 100;
        imageContainer.style.marginTop = 10;
        imageContainer.style.marginLeft = 25;
        
        // Set background color based on type
        if (powerUpItem.type == PowerUpType.ExtraLife)
        {
            imageContainer.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f); // Green for buffs
        }
        else
        {
            imageContainer.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f); // Red for debuffs
        }

        if (powerUpItem.icon != null)
        {
            imageContainer.style.backgroundImage = new StyleBackground(powerUpItem.icon);
        }

        var nameLabel = new Label(powerUpItem.name);
        nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        nameLabel.style.marginTop = 5;

        var costLabel = new Label($"{powerUpItem.cost} Gold");
        costLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        costLabel.style.marginTop = 5;

        var buyButton = new Button(() => PurchasePowerUp(powerUpItem));
        buyButton.AddToClassList("button");
        buyButton.style.width = new StyleLength(Length.Percent(40));
        buyButton.style.height = 50;
        buyButton.style.marginTop = 5;
        buyButton.style.marginLeft = new StyleLength(Length.Percent(30));
        
        // Change button color based on type
        if (powerUpItem.type != PowerUpType.ExtraLife)
        {
            buyButton.AddToClassList("debuff");
        }
        
        // Add a tooltip with description
        var tooltip = new Label(powerUpItem.description);
        tooltip.style.position = Position.Absolute;
        tooltip.style.backgroundColor = new Color(0, 0, 0, 0.8f);
        tooltip.style.color = Color.white;
        tooltip.style.paddingTop = 5;
        tooltip.style.paddingBottom = 5;
        tooltip.style.paddingLeft = 5;
        tooltip.style.paddingRight = 5;
        tooltip.style.fontSize = 12;
        tooltip.style.display = DisplayStyle.None;
        tooltip.style.width = 140;
        tooltip.style.whiteSpace = WhiteSpace.Normal;
        
        // Show/hide tooltip on hover
        itemContainer.RegisterCallback<MouseEnterEvent>(evt => tooltip.style.display = DisplayStyle.Flex);
        itemContainer.RegisterCallback<MouseLeaveEvent>(evt => tooltip.style.display = DisplayStyle.None);

        itemContainer.Add(imageContainer);
        itemContainer.Add(nameLabel);
        itemContainer.Add(costLabel);
        itemContainer.Add(buyButton);
        itemContainer.Add(tooltip);
        
        return itemContainer;
    }
    
    private void PurchasePowerUp(PowerUpShopItem powerUpItem)
    {
        if (playerStat == null) return;
        
        if (playerStat.SpendMoney(powerUpItem.cost))
        {
            // For extra life, directly increase lives
            if (powerUpItem.type == PowerUpType.ExtraLife)
            {
                // DIRECT approach - don't use PowerUp.ApplyPowerUp system yet
                int livesBeforePurchase = playerStat.GetLives();
                playerStat.IncreaseLives(Mathf.RoundToInt(powerUpItem.effectValue));
                int livesAfterPurchase = playerStat.GetLives();
                
                Debug.Log($"Extra Life purchased: Lives before={livesBeforePurchase}, Lives after={livesAfterPurchase}");
                
                // Still use PowerUp.ApplyPowerUp for network synchronization
                PowerUp.ApplyPowerUp(powerUpItem.type, playerStat.PathIndex, 
                    powerUpItem.effectValue, powerUpItem.duration);
                    
                OnPurchaseNotification?.Invoke($"Purchased {powerUpItem.name}! Lives: {livesAfterPurchase}");
            }
            else
            {
                // For debuffs, apply to opponents
                // First, find opponent path index
                int opponentPathIndex = (playerStat.PathIndex == 0) ? 1 : 0;
                
                // Make sure there is actually an opponent
                bool opponentExists = false;
                PlayerStat[] allStats = FindObjectsOfType<PlayerStat>();
                foreach (PlayerStat stat in allStats)
                {
                    if (stat.PathIndex == opponentPathIndex)
                    {
                        opponentExists = true;
                        break;
                    }
                }
                
                if (opponentExists)
                {
                    PowerUp.ApplyPowerUp(powerUpItem.type, opponentPathIndex, 
                        powerUpItem.effectValue, powerUpItem.duration);
                        
                    OnPurchaseNotification?.Invoke($"Purchased {powerUpItem.name} for opponent!");
                }
                else
                {
                    // Refund if no opponent found
                    playerStat.AddMoney(powerUpItem.cost);
                    OnPurchaseNotification?.Invoke("No opponent found to apply debuff!");
                }
            }
            
            OnShopClosed?.Invoke();
        }
        else
        {
            OnPurchaseNotification?.Invoke("Not enough money!");
        }
    }
}