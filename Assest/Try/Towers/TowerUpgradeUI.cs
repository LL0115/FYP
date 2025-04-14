using UnityEngine;
using UnityEngine.UIElements;
using Photon.Pun;

public class TowerUpgradeUI : MonoBehaviourPun
{
    [Header("UI Configuration")]
    [SerializeField] private float animationDuration = 0.3f;
     [Header("UI Colors")]
    [SerializeField] private Color canAffordColor = new Color(0, 1, 0, 1); // Green
    [SerializeField] private Color cantAffordColor = new Color(1, 0, 0, 1); // Red
    [SerializeField] private Color maxLevelColor = new Color(0.7f, 0.7f, 0.7f, 1); // Gray

    // UI Elements
    private VisualElement _upgradePanel;
    private Label _towerInfoLabel;
    private Label _upgradeCostLabel;
    private Button _upgradeButton;
    private Button _closeButton;
    private PlayerStat _playerStat;

    private TowerBehaviour _selectedTower;
    private bool isLocalPlayerUI = false;

    private void Start()
    {
        // Get PlayerStat from parent
        _playerStat = transform.parent.GetComponent<PlayerStat>();

        // Check if this UI belongs to the local player
        if (PhotonNetwork.IsConnected)
        {
            PhotonView playerView = transform.parent.GetComponent<PhotonView>();
            if (playerView != null)
            {
                isLocalPlayerUI = playerView.IsMine;
                if (!isLocalPlayerUI)
                {
                    enabled = false;
                    return;
                }
            }
        }
        else
        {
            isLocalPlayerUI = true;
        }

        // Get UI Document
        UIDocument document = GetComponent<UIDocument>();
        if (document == null)
        {
            Debug.LogError("No UIDocument found on GameObject!");
            return;
        }

        // Get root element
        VisualElement root = document.rootVisualElement;

        // Initialize UI elements
        InitializeUIElements(root);

        // Setup event handlers
        SetupEventHandlers();

        // Hide panel initially
        HideUpgradeUI();
    }

    private void InitializeUIElements(VisualElement root)
    {
        _upgradePanel = root.Q<VisualElement>("TowerUpgradeUI");
        _towerInfoLabel = root.Q<Label>("TowerStats");
        _upgradeCostLabel = root.Q<Label>("UpgradeCost");
        _upgradeButton = root.Q<Button>("UpgradeButton");
        _closeButton = root.Q<Button>("CloseTowerInfoButton");

        if (_upgradePanel == null) Debug.LogError("TowerUpgradeUI panel not found!");
        if (_towerInfoLabel == null) Debug.LogError("TowerStats label not found!");
        if (_upgradeCostLabel == null) Debug.LogError("UpgradeCost label not found!");
        if (_upgradeButton == null) Debug.LogError("UpgradeButton not found!");
        if (_closeButton == null) Debug.LogError("CloseTowerInfoButton not found!");

        // Add hover effects to the upgrade button
        if (_upgradeButton != null)
        {
            _upgradeButton.AddToClassList("upgrade-button");
            _upgradeButton.style.fontSize = 18;
            _upgradeButton.style.paddingLeft = 10;
            _upgradeButton.style.paddingRight = 10;
            _upgradeButton.style.paddingTop = 10;
            _upgradeButton.style.paddingBottom = 10;
        }

        // Style the tower info
        if (_towerInfoLabel != null)
        {
            _towerInfoLabel.AddToClassList("tower-info");
        }

        // Setup event handlers directly here instead of in separate method
        if (_upgradeButton != null)
        {
            _upgradeButton.RegisterCallback<ClickEvent>(evt => 
            {
                Debug.Log("Upgrade button clicked");
                OnUpgradeButtonClicked();
                evt.StopPropagation();
            });
        }

        if (_closeButton != null)
        {
            _closeButton.RegisterCallback<ClickEvent>(evt => 
            {
                Debug.Log("Close button clicked");
                HideUpgradeUI();
                evt.StopPropagation();
            });
        }
    }

    private void SetupEventHandlers()
    {
        if (_upgradeButton != null)
        {
            _upgradeButton.clicked += OnUpgradeButtonClicked;
        }

        if (_closeButton != null)
        {
            _closeButton.clicked += () => HideUpgradeUI();
        }
    }

    public void ShowTowerInfo(TowerBehaviour tower)
    {
        if (!isLocalPlayerUI || tower == null) return;

        _selectedTower = tower;

        // Update tower info
        if (_towerInfoLabel != null)
        {
            _towerInfoLabel.text = tower.GetTowerInfo();
        }

        // Update upgrade cost and button state
        UpdateUpgradeUI();

        // Show panel with animation
        ShowUpgradeUI();
    }

    private void UpdateUpgradeUI()
    {
        if (_selectedTower == null) return;

        int upgradeCost = _selectedTower.GetUpgradeCost();
        bool canUpgrade = _selectedTower.CanUpgrade();
        bool canAfford = upgradeCost > 0 && _playerStat.GetMoney() >= upgradeCost;

        if (_upgradeButton != null)
        {
            _upgradeButton.SetEnabled(canUpgrade && canAfford);
            
            // Update button appearance based on state
            if (!canUpgrade)
            {
                _upgradeButton.style.backgroundColor = maxLevelColor;
                _upgradeButton.text = "MAX LEVEL";
            }
            else if (!canAfford)
            {
                _upgradeButton.style.backgroundColor = cantAffordColor;
                _upgradeButton.text = "Can't Afford";
            }
            else
            {
                _upgradeButton.style.backgroundColor = canAffordColor;
                _upgradeButton.text = "UPGRADE";
            }
        }

        if (_upgradeCostLabel != null)
        {
            if (!canUpgrade)
            {
                _upgradeCostLabel.text = "MAX LEVEL";
                _upgradeCostLabel.style.color = maxLevelColor;
            }
            else
            {
                _upgradeCostLabel.text = $"Upgrade Cost: {upgradeCost}";
                _upgradeCostLabel.style.color = canAfford ? canAffordColor : cantAffordColor;
            }
        }
    }

    // Add method to check if mouse is over UI
    public bool IsMouseOverUI()
    {
        if (_upgradePanel == null || _upgradePanel.style.display == DisplayStyle.None)
            return false;

        Vector2 mousePosition = Input.mousePosition;
        return _upgradePanel.worldBound.Contains(mousePosition);
    }

    // Remove the separate SetupEventHandlers method since we're handling it in InitializeUIElements
    private void OnUpgradeButtonClicked()
    {
        Debug.Log("OnUpgradeButtonClicked called");
        
        if (!isLocalPlayerUI)
        {
            Debug.Log("Not local player UI");
            return;
        }
        
        if (_selectedTower == null)
        {
            Debug.LogWarning("No tower selected!");
            return;
        }

        if (_playerStat == null)
        {
            Debug.LogWarning("PlayerStat not found!");
            return;
        }

        int upgradeCost = _selectedTower.GetUpgradeCost();
        int currentMoney = _playerStat.GetMoney();
        
        Debug.Log($"Attempting upgrade - Cost: {upgradeCost}, Current Money: {currentMoney}");

        if (upgradeCost > 0 && currentMoney >= upgradeCost)
        {
            if (_playerStat.SpendMoney(upgradeCost))
            {
                Debug.Log("Money spent successfully, upgrading tower...");
                
                _selectedTower.UpgradeTower(
                    _selectedTower.damageIncreasePerLevel,
                    _selectedTower.firerateIncreasePerLevel
                );

                Debug.Log($"Tower upgraded! New stats: {_selectedTower.GetTowerInfo()}");
                
                // Update tower info display
                _towerInfoLabel.text = _selectedTower.GetTowerInfo();
                
                // Update the UI to show new stats and cost
                UpdateUpgradeUI();
            }
        }
        else
        {
            Debug.Log("Cannot afford upgrade or tower at max level");
        }
    }

    private void ShowUpgradeUI()
    {
        if (_upgradePanel == null) return;

        // Reset any existing animations
        _upgradePanel.RemoveFromClassList("hide");
        
        // Show the panel
        _upgradePanel.style.display = DisplayStyle.Flex;
        
        // Add animation class
        _upgradePanel.AddToClassList("show");
    }

    public void HideUpgradeUI()
    {
        if (!isLocalPlayerUI || _upgradePanel == null) return;

        // Start hide animation
        _upgradePanel.RemoveFromClassList("show");
        _upgradePanel.AddToClassList("hide");

        // Schedule actual hiding after animation
        _upgradePanel.schedule.Execute(() => {
            _upgradePanel.style.display = DisplayStyle.None;
            _selectedTower = null;
        }).StartingIn((long)(animationDuration * 1000));
    }

    private void OnDisable()
    {
        if (_upgradeButton != null)
        {
            _upgradeButton.UnregisterCallback<ClickEvent>(evt => OnUpgradeButtonClicked());
        }

        if (_closeButton != null)
        {
            _closeButton.UnregisterCallback<ClickEvent>(evt => HideUpgradeUI());
        }
    }
    // Public method to check if UI is currently visible
    public bool IsVisible()
    {
        return _upgradePanel != null && _upgradePanel.style.display == DisplayStyle.Flex;
    }

    // Public method to get currently selected tower
    public TowerBehaviour GetSelectedTower()
    {
        return _selectedTower;
    }
}