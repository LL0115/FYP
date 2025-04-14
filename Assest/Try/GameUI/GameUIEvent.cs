using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting;
using WebSocketSharp;
using System.Linq;

public class GameUIEvent : MonoBehaviourPunCallbacks
{
    private UIDocument _document;
    private Button _shopButton;
    private Button _doorButton;
    //Door Function
    private GameObject _door;
    private PhotonView _doorView;
    private bool _isDoorOpen = false;  
    private Vector3 _lastDoorPosition = Vector3.zero;
    private Quaternion _lastDoorRotation = Quaternion.identity;
    private bool _doorButtonCoolDown = false;
    private float _doorButtonCooldown = 5f;
    private float _doorButtonCooldownTimer = 0f;
    private VisualElement _doorButtonCooldownLayer;

    private Button _closeShopButton;
    private Label _moneyLabel;
    private Label _livesLabel;
    private Label _waveLabel;
    public VisualElement _shopUI;
    private VisualElement _itemsGrid;
    private PlayerStat _playerStat;
    private GameloopManager _gameloopManager;
    private VisualElement _gameOverUI;
    private Button _retryButton;
    private Button _mainMenuButton;
    private Label _wavesSurvivedLabel;
    // Add new variables for Victory UI
    private VisualElement _victoryUI;
    private Button _playAgainButton;
    private Button _victoryMainMenuButton;
    private Label _wavesCompletedLabel;
    private GameManager gameManager;

    // Add new UI elements for multiplayer
    private VisualElement _multiplayerInfoPanel;
    private Label _playersAliveLabel;
    private Label _winnerLabel;
    private VisualElement _waitingForPlayersPanel;
    private VisualElement _playerStatusNotifications;
    private VisualElement _spinnerAnimation;
    
    //Update tower
    private VisualElement _towerUpgradeUI;
    private Label _towerStats;
    private Label _upgradeCost;
    private Button _upgradeButton;
    private Button _closeTowerInfoButton;
    private TowerBehaviour _selectedTower;

    // Audio for notifications
    [SerializeField] private AudioClip playerJoinedSound;
    [SerializeField] private AudioClip playerLeftSound;
    [SerializeField] private AudioClip playerDefeatedSound;

    [SerializeField] private TowerPlacement towerPlacement;

    [System.Serializable]
    public class TowerShopItem
    {
        public GameObject towerPrefab;
        public Sprite towerIcon;
    }

    [SerializeField] private TowerShopItem[] shopTowers;

    [SerializeField] private PowerUpShopManager powerUpShopManager;
    
    // Track whether this UI belongs to the local player
    private bool isLocalPlayerUI = false;

    private void Start()
    {
        Transform playerTransform = transform.parent;
        
        // Check if this UI belongs to the local player
        if (PhotonNetwork.IsConnected)
        {
            if (playerTransform != null)
            {
                PhotonView playerView = playerTransform.GetComponent<PhotonView>();
                if (playerView != null)
                {
                    isLocalPlayerUI = playerView.IsMine;
                    
                    // If this isn't our UI, disable it completely
                    if (!isLocalPlayerUI)
                    {
                        gameObject.SetActive(false);
                        return; // Exit early - we don't want to setup UI for remote players
                    }
                }
            }
        }
        else
        {
            // In single player, we always show the UI
            isLocalPlayerUI = true;
        }
        
        gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("GameManager instance not found!");
            return;
        }
        if (gameManager != null)
        {
            // Make sure to unsubscribe first to prevent duplicate subscriptions
            gameManager.OnPlayerNotification -= HandlePlayerNotification;
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
            if (gameManager.OnPlayerWon != null)
            {
                gameManager.OnPlayerWon -= HandlePlayerWon;
            }
            
            // Then subscribe to events
            gameManager.OnPlayerNotification += HandlePlayerNotification;
            gameManager.OnGameStateChanged += HandleGameStateChanged;
            if (gameManager.OnPlayerWon != null)
            {
                gameManager.OnPlayerWon += HandlePlayerWon;
            }
            
            Debug.Log("Successfully connected to GameManager events");
        }

        _playerStat = playerTransform.GetComponent<PlayerStat>();
        if (_playerStat == null)
        {
            Debug.LogError("PlayerStat not found!");
            return;
        }
        
        // Try finding GameloopManager in multiple ways
        _gameloopManager = playerTransform.GetComponent<GameloopManager>();
        if (_gameloopManager == null)
        {
            // Try finding it in the scene if not found on parent
            _gameloopManager = FindObjectOfType<GameloopManager>();
            if (_gameloopManager == null)
            {
                Debug.LogError("GameloopManager not found!");
                return;
            }
        }

        if (playerTransform != null)
        {
            towerPlacement = playerTransform.GetComponentInChildren<TowerPlacement>();

            if (towerPlacement == null)
            {
                Debug.LogError("TowerPlacement script not found in Player's children!");
            }
        }
        else
        {
            Debug.LogError("Cannot find Player GameObject!");
        }

        _document = GetComponent<UIDocument>();
        var root = _document.rootVisualElement;

        _doorButton = root.Q<Button>("DoorButton");
        _shopButton = root.Q<Button>("ShopButton");
        _shopUI = root.Q<VisualElement>("ShopUI");
        _closeShopButton = root.Q<Button>("CloseShopButton");
        _itemsGrid = root.Q<VisualElement>("ItemsGrid");
        _moneyLabel = root.Q<Label>("Money");
        _livesLabel = root.Q<Label>("Lives");
        // Initialize tower upgrade UI elements
        _towerUpgradeUI = root.Q<VisualElement>("TowerUpgradeUI");
        _towerStats = _towerUpgradeUI.Q<Label>("TowerStats");
        _upgradeCost = _towerUpgradeUI.Q<Label>("UpgradeCost");
        _upgradeButton = _towerUpgradeUI.Q<Button>("UpgradeButton");
        _closeTowerInfoButton = _towerUpgradeUI.Q<Button>("CloseTowerInfoButton");

        // Hide upgrade UI initially
        _towerUpgradeUI.style.display = DisplayStyle.None;

        // Register button callbacks
        _upgradeButton.clicked += OnUpgradeButtonClicked;
        _closeTowerInfoButton.clicked += () => HideTowerUpgradeUI();

        //Handle the button cooldown
        _doorButtonCooldownLayer = new VisualElement();
        _doorButtonCooldownLayer.name = "DoorButtonCooldown";
        _doorButtonCooldownLayer.style.position = Position.Absolute;
        _doorButtonCooldownLayer.style.top = 0;
        _doorButtonCooldownLayer.style.left = 0;
        _doorButtonCooldownLayer.style.right = 0;
        _doorButtonCooldownLayer.style.bottom = 0;
        _doorButtonCooldownLayer.style.backgroundColor = new Color(0, 0, 0, 0.7f);
        _doorButtonCooldownLayer.style.display = DisplayStyle.None;

        Label cooldownLabel = new Label();
        cooldownLabel.name = "CooldownTimer";
        cooldownLabel.style.position = Position.Absolute;
        cooldownLabel.style.top = 0;
        cooldownLabel.style.left = 0;
        cooldownLabel.style.right = 0;
        cooldownLabel.style.bottom = 0;
        cooldownLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        cooldownLabel.style.color = Color.white;
        cooldownLabel.style.fontSize = 28;
        _doorButtonCooldownLayer.Add(cooldownLabel);
        if (_doorButton != null)
        {
            _doorButton.Add(_doorButtonCooldownLayer);
            _doorButton.RegisterCallback<ClickEvent>(evt => OnDoorButtonClick(evt));
        }


        // Update wave UI
        _waveLabel = root.Q<Label>("Wave");

        // Add Game Over UI elements
        _gameOverUI = root.Q<VisualElement>("GameOverUI");
        _retryButton = root.Q<Button>("RetryButton");
        _mainMenuButton = root.Q<Button>("MainMenuButton");
        _wavesSurvivedLabel = root.Q<Label>("WavesSurvived");

        // Add Victory UI elements
        _victoryUI = root.Q<VisualElement>("VictoryUI");
        _playAgainButton = root.Q<Button>("PlayAgainButton");
        _victoryMainMenuButton = root.Q<Button>("VictoryMainMenuButton");
        _wavesCompletedLabel = root.Q<Label>("WavesCompleted");

        if (_doorButton != null)
        {
            _doorButton.RegisterCallback<ClickEvent>(evt => OnDoorButtonClick(evt));
            Debug.Log("Door button registered for door control");
        }

        CreateDoor();

        // Setup player status notifications panel
        _playerStatusNotifications = root.Q<VisualElement>("PlayerStatusNotifications");
        if (_playerStatusNotifications == null)
        {
            _playerStatusNotifications = new VisualElement();
            _playerStatusNotifications.name = "PlayerStatusNotifications";
            _playerStatusNotifications.style.position = Position.Absolute;
            _playerStatusNotifications.style.top = 70;
            _playerStatusNotifications.style.right = 10;
            _playerStatusNotifications.style.width = 250;
            root.Add(_playerStatusNotifications);
        }

        // Setup multiplayer UI elements (add these to your UI XML/USS)
        _multiplayerInfoPanel = root.Q<VisualElement>("MultiplayerInfoPanel");
        if (_multiplayerInfoPanel != null)
        {
            _playersAliveLabel = _multiplayerInfoPanel.Q<Label>("PlayersAliveLabel");
            _winnerLabel = _multiplayerInfoPanel.Q<Label>("WinnerLabel");
        }
        else
        {
            // Create multiplayer info panel dynamically if not in UXML
            _multiplayerInfoPanel = new VisualElement();
            _multiplayerInfoPanel.name = "MultiplayerInfoPanel";
            _multiplayerInfoPanel.style.position = Position.Absolute;
            _multiplayerInfoPanel.style.top = 10;
            _multiplayerInfoPanel.style.right = 10;
            _multiplayerInfoPanel.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            _multiplayerInfoPanel.style.paddingTop = 5;
            _multiplayerInfoPanel.style.paddingBottom = 5;
            _multiplayerInfoPanel.style.paddingLeft = 10;
            _multiplayerInfoPanel.style.paddingRight = 10;
            _multiplayerInfoPanel.style.borderTopLeftRadius = 5;
            _multiplayerInfoPanel.style.borderTopRightRadius = 5;
            _multiplayerInfoPanel.style.borderBottomLeftRadius = 5;
            _multiplayerInfoPanel.style.borderBottomRightRadius = 5;
            
            _playersAliveLabel = new Label("Players: 0/0");
            _playersAliveLabel.name = "PlayersAliveLabel";
            _playersAliveLabel.style.color = Color.white;
            
            _winnerLabel = new Label("");
            _winnerLabel.name = "WinnerLabel";
            _winnerLabel.style.color = Color.yellow;
            _winnerLabel.style.display = DisplayStyle.None;
            
            _multiplayerInfoPanel.Add(_playersAliveLabel);
            _multiplayerInfoPanel.Add(_winnerLabel);
            
            root.Add(_multiplayerInfoPanel);
        }
        
        // Create "Waiting for players" panel if needed
        _waitingForPlayersPanel = root.Q<VisualElement>("WaitingForPlayersPanel");
        if (_waitingForPlayersPanel == null)
        {
            _waitingForPlayersPanel = new VisualElement();
            _waitingForPlayersPanel.name = "WaitingForPlayersPanel";
            _waitingForPlayersPanel.style.position = Position.Absolute;
            _waitingForPlayersPanel.style.top = 0;
            _waitingForPlayersPanel.style.left = 0;
            _waitingForPlayersPanel.style.right = 0;
            _waitingForPlayersPanel.style.bottom = 0;
            _waitingForPlayersPanel.style.backgroundColor = new Color(0, 0, 0, 0.8f);
            _waitingForPlayersPanel.style.alignItems = Align.Center;
            _waitingForPlayersPanel.style.justifyContent = Justify.Center;
            
            VisualElement container = new VisualElement();
            container.style.backgroundColor = new Color(0.24f, 0.24f, 0.24f, 1f);
            container.style.borderTopLeftRadius = 10;
            container.style.borderTopRightRadius = 10;
            container.style.borderBottomLeftRadius = 10;
            container.style.borderBottomRightRadius = 10;
            container.style.paddingTop = 30;
            container.style.paddingBottom = 30;
            container.style.paddingLeft = 30;
            container.style.paddingRight = 30;
            container.style.alignItems = Align.Center;
            
            Label waitingLabel = new Label("Waiting for other players...");
            waitingLabel.style.fontSize = 24;
            waitingLabel.style.color = Color.white;
            waitingLabel.style.marginBottom = 20;
            
            // Add spinner
            VisualElement spinner = new VisualElement();
            spinner.name = "LoadingSpinner";
            spinner.style.width = 50;
            spinner.style.height = 50;
            spinner.style.backgroundImage = Resources.Load<Texture2D>("UI/LoadingIcon");
            
            _spinnerAnimation = new VisualElement();
            _spinnerAnimation.name = "SpinnerAnimation";
            _spinnerAnimation.style.width = new StyleLength(Length.Percent(100));
            _spinnerAnimation.style.height = new StyleLength(Length.Percent(100));
            spinner.Add(_spinnerAnimation);
            
            Label infoLabel = new Label("The game will start when at least 2 players have joined");
            infoLabel.style.fontSize = 18;
            infoLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            infoLabel.style.marginTop = 20;
            infoLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            infoLabel.style.maxWidth = 400;
            
            container.Add(waitingLabel);
            container.Add(spinner);
            container.Add(infoLabel);
            _waitingForPlayersPanel.Add(container);
            
            root.Add(_waitingForPlayersPanel);
        }
        
        // Hide multiplayer elements by default
        _multiplayerInfoPanel.style.display = DisplayStyle.None;
        _waitingForPlayersPanel.style.display = DisplayStyle.None;

        // Register callbacks for game over buttons
        _retryButton.RegisterCallback<ClickEvent>(evt => OnRetryButtonClick());
        _mainMenuButton.RegisterCallback<ClickEvent>(evt => OnMainMenuButtonClick());
        // Register callbacks for victory buttons
        _playAgainButton.RegisterCallback<ClickEvent>(evt => OnRetryButtonClick());
        _victoryMainMenuButton.RegisterCallback<ClickEvent>(evt => OnMainMenuButtonClick());

        // Hide game over UI initially
        _gameOverUI.style.display = DisplayStyle.None;
        // Hide victory UI initially
        _victoryUI.style.display = DisplayStyle.None;

        _shopButton.RegisterCallback<ClickEvent>(evt => OnShopButtonClick(evt));
        _closeShopButton.RegisterCallback<ClickEvent>(evt => OnCloseShopButtonClick(evt));

        _itemsGrid.Clear();
        InitializePowerUpShop();
        CreateTowerButtons();

        // Show multiplayer info if we're in a networked game
        if (PhotonNetwork.IsConnected)
        {
            _multiplayerInfoPanel.style.display = DisplayStyle.Flex;
            UpdateMultiplayerInfo();
            
            // Show waiting panel until we have enough players
            if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
            {
                ShowWaitingForPlayersPanel(true);
            }
            else
            {
                ShowWaitingForPlayersPanel(false);
            }
        }

        UpdateUI();
    }

    private void Update()
    {
        // Skip updates if this isn't our UI
        if (!isLocalPlayerUI) return;
        
        UpdateUI();

        if (_doorButtonCoolDown)
        {
            float remainingTime = _doorButtonCooldownTimer - Time.time;

            if (remainingTime <= 0)
            {
                // Cooldown finished
                _doorButtonCoolDown = false;
                _doorButtonCooldownLayer.style.display = DisplayStyle.None;
            }
            else
            {
                // Update the cooldown timer text
                Label cooldownLabel = _doorButtonCooldownLayer.Q<Label>("CooldownTimer");
                if (cooldownLabel != null)
                {
                    cooldownLabel.text = Mathf.CeilToInt(remainingTime).ToString();
                }
            }
        }

        // Update multiplayer info if connected
        if (PhotonNetwork.IsConnected && _multiplayerInfoPanel.style.display == DisplayStyle.Flex)
        {
            UpdateMultiplayerInfo();
        }
        
        // Manually rotate spinner (for browsers or platforms that don't support CSS animations)
        if (_spinnerAnimation != null && _waitingForPlayersPanel.style.display == DisplayStyle.Flex)
        {
            _spinnerAnimation.transform.rotation = Quaternion.Euler(0, 0, Time.unscaledTime * 180f);
        }
    }

    // Add this to the Start method in GameUIEvent
    private void InitializePowerUpShop()
    {
        // Create PowerUpShopManager if it doesn't exist
        if (powerUpShopManager == null)
        {
            powerUpShopManager = gameObject.AddComponent<PowerUpShopManager>();
        }
        
        // Initialize the PowerUpShopManager
        powerUpShopManager.Initialize(_document, _itemsGrid, _playerStat);
        
        // Subscribe to events
        powerUpShopManager.OnPurchaseNotification += ShowPurchaseNotification;
        powerUpShopManager.OnShopClosed += CloseShop;
    }

    // Add method for purchase notifications
    private void ShowPurchaseNotification(string message)
    {
        VisualElement notification = new VisualElement();
        notification.AddToClassList("purchase-notification");
        notification.style.backgroundColor = new Color(0, 0, 0, 0.8f);
        notification.style.paddingTop = 10;
        notification.style.paddingBottom = 10;
        notification.style.paddingLeft = 15;
        notification.style.paddingRight = 15;
        notification.style.position = Position.Absolute;
        notification.style.bottom = 20;
        notification.style.left = new StyleLength(Length.Percent(50));
        notification.style.translate = new StyleTranslate(new Translate(-50f, 0f));
        notification.style.borderTopLeftRadius = 5;
        notification.style.borderTopRightRadius = 5;
        notification.style.borderBottomLeftRadius = 5;
        notification.style.borderBottomRightRadius = 5;
        
        Label label = new Label(message);
        label.style.color = Color.white;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        
        notification.Add(label);
        _document.rootVisualElement.Add(notification);
        
        StartCoroutine(RemoveNotificationAfterDelay(notification, 2f));
    }

    public void ShowTowerUpgradeUI(TowerBehaviour tower)
    {
        if (!isLocalPlayerUI) return;

        _selectedTower = tower;
        _towerStats.text = tower.GetTowerInfo();
        
        int upgradeCost = tower.GetUpgradeCost();
        bool canAfford = upgradeCost > 0 && _playerStat.GetMoney() >= upgradeCost;
        
        _upgradeButton.SetEnabled(tower.CanUpgrade() && canAfford);
        _upgradeCost.text = upgradeCost > 0 ? $"Upgrade Cost: {upgradeCost}" : "MAX LEVEL";
        
        // Show the UI with animation
        _towerUpgradeUI.RemoveFromClassList("hide");
        _towerUpgradeUI.AddToClassList("show");
        _towerUpgradeUI.style.display = DisplayStyle.Flex;
    }

    public void HideTowerUpgradeUI()
    {
        if (!isLocalPlayerUI) return;
        
        _towerUpgradeUI.RemoveFromClassList("show");
        _towerUpgradeUI.AddToClassList("hide");
        _selectedTower = null;
        
        // Hide after animation
        _towerUpgradeUI.schedule.Execute(() => {
            _towerUpgradeUI.style.display = DisplayStyle.None;
        }).StartingIn(300);
    }

    private void OnUpgradeButtonClicked()
    {
        if (!isLocalPlayerUI || _selectedTower == null) return;
        
        int upgradeCost = _selectedTower.GetUpgradeCost();
        
        if (upgradeCost > 0 && _playerStat.SpendMoney(upgradeCost))
        {
            _selectedTower.UpgradeTower(
                _selectedTower.damageIncreasePerLevel,
                _selectedTower.firerateIncreasePerLevel
            );
            
            // Update UI after upgrade
            ShowTowerUpgradeUI(_selectedTower);
        }
    }

    private void CreateDoor()
    {
        if (!isLocalPlayerUI) return;

        // Find if a door already exists in the scene
        GameObject existingDoor = GameObject.Find("ToggleableDoor");

        if (existingDoor != null)
        {
            // Use the existing door
            _door = existingDoor;
            _doorView = existingDoor.GetComponent<PhotonView>();

            // Store the door's position and rotation for future reference
            _lastDoorPosition = existingDoor.transform.position;
            _lastDoorRotation = existingDoor.transform.rotation;

            if (_doorView == null)
            {
                Debug.LogError("Door doesn't have a PhotonView component!");
            }

            Debug.Log("Found existing door in scene with PhotonView ID: " +
                      (_doorView != null ? _doorView.ViewID.ToString() : "None"));
        }
        else
        {
            Debug.LogError("ToggleableDoor not found in scene!");
        }
    }
    private void OnDoorButtonClick(ClickEvent evt)
    {
        if (!isLocalPlayerUI) return;

        // Check if button is on cooldown
        if (_doorButtonCoolDown)
        {
            // Button is on cooldown, can't use it
            evt.StopPropagation();
            return;
        }

        ToggleableDoor[] doorComponents = FindObjectsOfType<ToggleableDoor>();
        GameObject[] allDoors = new GameObject[doorComponents.Length];

        // Find ALL doors in the scene by name (since we don't have tags)
        for (int i = 0; i < doorComponents.Length; i++)
        {
            allDoors[i] = doorComponents[i].gameObject;
        }

        Debug.Log($"Found {allDoors.Length} doors in the scene");

        // If there are multiple doors, clean them up
        if (allDoors.Length > 1)
        {
            Debug.LogWarning("Multiple doors found! Cleaning up extras...");

            // Keep only the first door
            for (int i = 1; i < allDoors.Length; i++)
            {
                if (PhotonNetwork.IsConnected)
                {
                    PhotonView view = allDoors[i].GetComponent<PhotonView>();
                    if (view != null && (view.IsMine || PhotonNetwork.IsMasterClient))
                    {
                        PhotonNetwork.Destroy(allDoors[i]);
                    }
                }
                else
                {
                    Destroy(allDoors[i]);
                }
            }

            // Set the door references to the first door
            _door = allDoors[0];
            _doorView = _door.GetComponent<PhotonView>();

            // Start cooldown to let cleanup happen
            StartDoorButtonCooldown();
            evt.StopPropagation();
            return;
        }

        // Regular door toggle logic
        if (allDoors.Length == 1)
        {
            // We have exactly one door - perfect!
            _door = allDoors[0];
            _doorView = _door.GetComponent<PhotonView>();

            // Store its position and rotation
            _lastDoorPosition = _door.transform.position;
            _lastDoorRotation = _door.transform.rotation;

            // Destroy it (open the doorway)
            if (PhotonNetwork.IsConnected && _doorView != null)
            {
                _doorView.RPC("ToggleDoorRPC", RpcTarget.All, true);
                ShowPlayerNotification("System", "Door has been opened", "join");
            }
            else
            {
                // In single player
                Destroy(_door);
                ShowPlayerNotification("System", "Door has been opened", "join");
            }
        }
        else
        {
            // No door exists - create one
            if (PhotonNetwork.IsConnected)
            {
                // Get the position/rotation from previous door or use defaults
                Vector3 doorPosition = _lastDoorPosition != Vector3.zero ? _lastDoorPosition : new Vector3(0, 1, 0);
                Quaternion doorRotation = _lastDoorRotation != Quaternion.identity ? _lastDoorRotation : Quaternion.identity;

                GameObject newDoor = PhotonNetwork.Instantiate("Prefabs/ToggleableDoor", doorPosition, doorRotation);
                if (newDoor != null)
                {
                    newDoor.name = "ToggleableDoor";
                    _door = newDoor;
                    _doorView = newDoor.GetComponent<PhotonView>();
                    ShowPlayerNotification("System", "Door has been closed", "join");
                }
            }
            else
            {
                // In single player
                GameObject doorPrefab = Resources.Load<GameObject>("Prefabs/ToggleableDoor");
                if (doorPrefab != null)
                {
                    Vector3 doorPosition = _lastDoorPosition != Vector3.zero ? _lastDoorPosition : new Vector3(0, 1, 0);
                    Quaternion doorRotation = _lastDoorRotation != Quaternion.identity ? _lastDoorRotation : Quaternion.identity;

                    GameObject newDoor = Instantiate(doorPrefab, doorPosition, doorRotation);
                    newDoor.name = "ToggleableDoor";
                    _door = newDoor;
                    ShowPlayerNotification("System", "Door has been closed", "join");
                }
                else
                {
                    Debug.LogError("Door prefab not found in Resources/Prefabs/ToggleableDoor");
                }
            }
        }

        // Set the button on cooldown
        StartDoorButtonCooldown();
        evt.StopPropagation();
    }

    private void StartDoorButtonCooldown()
    {
        _doorButtonCoolDown = true;
        _doorButtonCooldownTimer = Time.time + _doorButtonCooldown;
        _doorButtonCooldownLayer.style.display = DisplayStyle.Flex;

        // Set the initial cooldown text
        Label cooldownLabel = _doorButtonCooldownLayer.Q<Label>("CooldownTimer");
        if (cooldownLabel != null)
        {
            cooldownLabel.text = _doorButtonCooldown.ToString();
        }
    }
    private void ToggleDoor(bool isOpen)
    {
        if (_door != null)
        {
            _door.SetActive(!isOpen);
            Debug.Log($"Door is now {(isOpen ? "open" : "closed")}");
        }
        else
        {
            Debug.LogWarning("Door object is missing!");
        }
    }

    // Add this RPC method to synchronize door state in multiplayer
    [PunRPC]
    public void RPC_ToggleDoor(bool isOpen)
    {
        // Find the GameUIEvent component and call its method
        GameUIEvent uiEvent = GetComponentInChildren<GameUIEvent>();
        if (uiEvent != null)
        {
            uiEvent.ToggleDoorState(isOpen);
        }
    }

    public void ToggleDoorState(bool isOpen)
    {
        _isDoorOpen = isOpen;
        ToggleDoor(isOpen);
    }
    private void OnDestroy()
{
    // Only destroy the door if we're exiting the scene entirely
    // In multiplayer, we don't want to destroy objects that other clients might be using
    if (!PhotonNetwork.IsConnected && _door != null && gameObject.scene.isLoaded)
    {
        Destroy(_door);
    }
}

private void UpdateUI()
    {
        // Only update UI if this is our local player's UI
        if (!isLocalPlayerUI) return;
        
        if (_moneyLabel != null && _playerStat != null)
        {
            _moneyLabel.text = $"Money: {_playerStat.GetMoney()}";
        }

        if (_livesLabel != null && _playerStat != null)
        {
            _livesLabel.text = $"Lives: {_playerStat.GetLives()}";
        }

        if (_waveLabel != null && _gameloopManager != null)
        {
            _waveLabel.text = $"Wave: {_gameloopManager.currentWaveIndex + 1}";
        }
    }

    private void UpdateMultiplayerInfo()
    {
        if (!isLocalPlayerUI) return;
        
        if (_playersAliveLabel != null && PhotonNetwork.InRoom)
        {
            int totalPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
            int alivePlayers = 0;
            
            // Count players that are still alive
            foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                // Check if the player has a custom property indicating they're dead
                if (!player.CustomProperties.TryGetValue("IsDead", out object isDead) || !(bool)isDead)
                {
                    alivePlayers++;
                }
            }
            
            _playersAliveLabel.text = $"Players: {alivePlayers}/{totalPlayers}";
        }
    }

    public void ShowGameOver(int wavesSurvived)
    {
        if (!isLocalPlayerUI) return;
        
        Debug.Log($"Showing Game Over UI with {wavesSurvived} waves survived");
    
        _gameOverUI.style.display = DisplayStyle.Flex;
        _victoryUI.style.display = DisplayStyle.None;
        _wavesSurvivedLabel.text = wavesSurvived.ToString();

        // Show cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Disable player movement and other gameplay elements if needed
        PlayerMov playerMov = transform.parent.GetComponent<PlayerMov>();
        if (playerMov != null)
        {
            playerMov.enabled = false;
        }

        // Close shop if it's open
        CloseShop();

        // Make sure game is actually stopped
        if (_gameloopManager != null)
        {
            _gameloopManager.StopGame();
        }
        
        // In multiplayer, mark this player as dead if not already
        if (PhotonNetwork.IsConnected && (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("IsDead", out object isDead) || !(bool)isDead))
        {
            Debug.Log("Setting local player as dead");
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props.Add("IsDead", true);
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            
            // Broadcast death to other players
            if (gameManager != null)
            {
                gameManager.PlayerDied();
            }
        }
    }

    public void ShowVictory(int wavesCompleted)
    {
        if (!isLocalPlayerUI) return;
        
        Debug.Log($"Showing Victory UI with {wavesCompleted} waves completed");
    
        _victoryUI.style.display = DisplayStyle.Flex;
        _gameOverUI.style.display = DisplayStyle.None;
        _wavesCompletedLabel.text = wavesCompleted.ToString();

        // Show cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Disable player movement and other gameplay elements if needed
        PlayerMov playerMov = transform.parent.GetComponent<PlayerMov>();
        if (playerMov != null)
        {
            playerMov.enabled = false;
        }

        // Close shop if it's open
        CloseShop();
        
        // Stop the game loop
        if (_gameloopManager != null)
        {
            _gameloopManager.StopGame();
        }
    }

    private void OnRetryButtonClick()
    {
        if (!isLocalPlayerUI) 
            return;
        
        // Reset timescale first
        Time.timeScale = 1f;

        // Hide UIs first
        _gameOverUI.style.display = DisplayStyle.None;
        _victoryUI.style.display = DisplayStyle.None;

        // For multiplayer, return to lobby instead of restarting immediately
        if (PhotonNetwork.IsConnected)
        {
            // Clear the IsDead property before returning to lobby
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props.Remove("IsDead");
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);


            StartCoroutine(LeaveRoomAndLoadLobby());
            // Return to lobby scene
            //PhotonNetwork.LoadLevel("NewLobbyUI");
        }
        else
        {
            // Single player mode - reset game state before loading new scene
            if (gameManager != null)
            {
                gameManager.StartNewGame();
            }
            else
            {
                Debug.LogError("GameManager is null in OnRetryButtonClick!");
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }
    private IEnumerator LeaveRoomAndLoadLobby()
    {
        // Leave the current room
        PhotonNetwork.LeaveRoom();

        // Wait until we've successfully left the room
        while (PhotonNetwork.InRoom)
        {
            yield return null;
        }

        // Now load the lobby scene
        SceneManager.LoadScene("NewLobbyUI");
    }

    private void OnMainMenuButtonClick()
    {
        if (!isLocalPlayerUI) return;
        
        // Reset timescale
        Time.timeScale = 1f;

        PhotonNetwork.Disconnect();

        SceneManager.LoadScene("Startgame");
    }
    
    private IEnumerator DisconnectAndLoadMainMenu()
    {
        // Clear any player properties
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
        props.Remove("IsDead");
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        
        // Leave room
        PhotonNetwork.LeaveRoom();
        
        // Wait for disconnect
        while (PhotonNetwork.InRoom)
        {
            yield return null;
        }
        
        // Disconnect from Photon
        PhotonNetwork.Disconnect();
        
        // Wait for disconnect
        while (PhotonNetwork.IsConnected)
        {
            yield return null;
        }
        
        // Load main menu
        SceneManager.LoadScene("Startgame");
    }

    private void CreateTowerButtons()
    {
        if (!isLocalPlayerUI) return;

        // Clear existing items
        _itemsGrid.Clear();
        
        foreach (var towerItem in shopTowers)
        {
            var towerPrefab = towerItem.towerPrefab;
            var towerBehaviour = towerPrefab.GetComponent<TowerBehaviour>();

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
            imageContainer.style.backgroundColor = new Color(0.66f, 0.66f, 0.66f);

            if (towerBehaviour.towerIcon != null)
            {
                imageContainer.style.backgroundImage = new StyleBackground(towerBehaviour.towerIcon);
            }

            var nameLabel = new Label(towerBehaviour.towerName);
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            nameLabel.style.marginTop = 5;

            var costLabel = new Label($"{towerBehaviour.SummonCost} Gold");
            costLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            costLabel.style.marginTop = 5;

            var buyButton = new Button(() => SelectTower(towerPrefab));
            buyButton.AddToClassList("button");
            buyButton.style.width = new StyleLength(Length.Percent(40));
            buyButton.style.height = 50;
            buyButton.style.marginTop = 5;
            buyButton.style.marginLeft = new StyleLength(Length.Percent(30));

            itemContainer.Add(imageContainer);
            itemContainer.Add(nameLabel);
            itemContainer.Add(costLabel);
            itemContainer.Add(buyButton);

            _itemsGrid.Add(itemContainer);
        }

        // Add power-up items from the PowerUpShopManager
        powerUpShopManager.CreatePowerUpItems();
    }

    private void OnShopButtonClick(ClickEvent evt)
    {
        if (!isLocalPlayerUI) return;
        
        OpenShop();
        evt.StopPropagation();
    }

    private void OnCloseShopButtonClick(ClickEvent evt)
    {
        if (!isLocalPlayerUI) return;
        
        CloseShop();
        evt.StopPropagation();
    }

    private void SelectTower(GameObject towerPrefab)
    {
        if (!isLocalPlayerUI) return;
        
        if (towerPlacement != null)
        {
            towerPlacement.SetTowerToPlace(towerPrefab);
            CloseShop();
        }
    }

    private void OpenShop()
    {
        if (!isLocalPlayerUI) return;
        
        _shopUI.style.display = DisplayStyle.Flex;
        PlayerMov playerMov = transform.parent.GetComponent<PlayerMov>();
        if (playerMov != null)
        {
            playerMov.SetCursorState(true);
        }
    }

    private void CloseShop()
    {
        if (!isLocalPlayerUI) return;
        
        _shopUI.style.display = DisplayStyle.None;
        PlayerMov playerMov = transform.parent.GetComponent<PlayerMov>();
        if (playerMov != null && !Input.GetKey(KeyCode.LeftAlt))
        {
            playerMov.SetCursorState(false);
        }

        TowerPlacement towerPlacement = transform.parent.GetComponentInChildren<TowerPlacement>();
        if (towerPlacement != null && towerPlacement.CurrentPlacingTower == null && !Input.GetKey(KeyCode.LeftAlt))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
    
    // Add player notification methods
    private void ShowPlayerNotification(string playerName, string message, string notificationType)
    {
        if (!isLocalPlayerUI) return;
        if (_playerStatusNotifications == null) return;
        
        // Create notification container
        VisualElement notification = new VisualElement();
        notification.AddToClassList("player-notification");
        notification.AddToClassList($"player-notification-{notificationType}");
        
        // Set background color based on type
        switch (notificationType)
        {
            case "join":
                notification.style.backgroundColor = new Color(0, 0.7f, 0, 0.7f);
                break;
            case "leave":
                notification.style.backgroundColor = new Color(1f, 0.65f, 0, 0.7f);
                break;
            case "death":
                notification.style.backgroundColor = new Color(0.7f, 0, 0, 0.7f);
                break;
        }
        
        notification.style.paddingTop = 8;
        notification.style.paddingBottom = 8;
        notification.style.paddingLeft = 12;
        notification.style.paddingRight = 12;
        notification.style.marginBottom = 8;
        notification.style.borderTopLeftRadius = 5;
        notification.style.borderTopRightRadius = 5;
        notification.style.borderBottomLeftRadius = 5;
        notification.style.borderBottomRightRadius = 5;
        
        // Add text
        Label textLabel = new Label($"{playerName} {message}");
        textLabel.style.color = Color.white;
        textLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        
        notification.Add(textLabel);
        _playerStatusNotifications.Add(notification);
        
        // Play sound based on notification type
        PlayNotificationSound(notificationType);
        
        // Remove after delay
        StartCoroutine(RemoveNotificationAfterDelay(notification, 3f));
    }
    
    private IEnumerator RemoveNotificationAfterDelay(VisualElement notification, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Fade out
        float fadeTime = 0.5f;
        float startTime = Time.time;
        
        while (Time.time < startTime + fadeTime)
        {
            float t = (Time.time - startTime) / fadeTime;
            notification.style.opacity = Mathf.Lerp(1, 0, t);
            yield return null;
        }
        
        _document.rootVisualElement.Remove(notification);
    }
    
    private void PlayNotificationSound(string type)
    {
        if (!isLocalPlayerUI) return;
        
        AudioClip clipToPlay = null;
        
        switch (type)
        {
            case "join":
                clipToPlay = playerJoinedSound;
                break;
            case "leave":
                clipToPlay = playerLeftSound;
                break;
            case "death":
                clipToPlay = playerDefeatedSound;
                break;
        }
        
        if (clipToPlay != null)
        {
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            
            audioSource.PlayOneShot(clipToPlay);
        }
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!isLocalPlayerUI) return;
        
        // Show notification
        ShowPlayerNotification(newPlayer.NickName, "joined the game", "join");
        
        // Update UI
        UpdateMultiplayerInfo();
        
        // If we now have enough players, hide the waiting panel
        if (PhotonNetwork.CurrentRoom.PlayerCount >= 2)
        {
            ShowWaitingForPlayersPanel(false);
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!isLocalPlayerUI) return;
        
        // Show notification
        ShowPlayerNotification(otherPlayer.NickName, "left the game", "leave");
        
        // Update UI
        UpdateMultiplayerInfo();
        
        // If there's only one player left (us), we win!
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1 && gameManager != null)
        {
            gameManager.Victory();
        }
    }
    
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (!isLocalPlayerUI) return;
        
        // Check if player died
        if (changedProps.ContainsKey("IsDead") && (bool)changedProps["IsDead"] == true && targetPlayer != PhotonNetwork.LocalPlayer)
        {
            ShowPlayerNotification(targetPlayer.NickName, "was defeated!", "death");
        }
        
        // Update UI
        UpdateMultiplayerInfo();
    }
    
    private void HandleGameStateChanged(GameManager.GameState newState)
    {
        if (!isLocalPlayerUI) return;
        
        switch (newState)
        {
            case GameManager.GameState.Playing:
                // Game is starting
                _gameOverUI.style.display = DisplayStyle.None;
                _victoryUI.style.display = DisplayStyle.None;
                if (_winnerLabel != null)
                {
                    _winnerLabel.style.display = DisplayStyle.None;
                }
                break;
                
            case GameManager.GameState.GameOver:
                // Local player lost
                ShowGameOver(_gameloopManager.latestWaveIndex);
                break;
                
            case GameManager.GameState.Victory:
                // Local player won
                ShowVictory(_gameloopManager.latestWaveIndex);
                break;
        }
    }
    
    private void HandlePlayerWon(Player winningPlayer)
    {
        if (!isLocalPlayerUI) return;
        
        // Show who won the game
        if (_winnerLabel != null)
        {
            _winnerLabel.text = $"{winningPlayer.NickName} won the game!";
            _winnerLabel.style.display = DisplayStyle.Flex;
            
            // Also show a notification
            ShowPlayerNotification(winningPlayer.NickName, "is the last player standing and won the game!", "join");
        }
    }
    
    private void ShowWaitingForPlayersPanel(bool show)
    {
        if (!isLocalPlayerUI) return;
        
        if (_waitingForPlayersPanel != null)
        {
            _waitingForPlayersPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Pause/unpause the game while waiting
            Time.timeScale = show ? 0f : 1f;
            
            // Disable/enable player movement
            PlayerMov playerMov = transform.parent.GetComponent<PlayerMov>();
            if (playerMov != null)
            {
                playerMov.enabled = !show;
            }
        }
    }

    public override void OnDisable()
    {
        base.OnDisable();
    
        // Unsubscribe from PowerUpShopManager events
        if (powerUpShopManager != null)
        {
            powerUpShopManager.OnPurchaseNotification -= ShowPurchaseNotification;
            powerUpShopManager.OnShopClosed -= CloseShop;
        }
        if (!isLocalPlayerUI) return;

        if (_doorButton != null)
        {
            _doorButton.UnregisterCallback<ClickEvent>(evt => OnDoorButtonClick(evt));
        }

        if (_shopButton != null)
        {
            _shopButton.UnregisterCallback<ClickEvent>(evt => OnShopButtonClick(evt));
        }
        if (_closeShopButton != null)
        {
            _closeShopButton.UnregisterCallback<ClickEvent>(evt => OnCloseShopButtonClick(evt));
        }

        if (_retryButton != null)
        {
            _retryButton.UnregisterCallback<ClickEvent>(evt => OnRetryButtonClick());
        }
        if (_mainMenuButton != null)
        {
            _mainMenuButton.UnregisterCallback<ClickEvent>(evt => OnMainMenuButtonClick());
        }
        if (_playAgainButton != null)
        {
            _playAgainButton.UnregisterCallback<ClickEvent>(evt => OnRetryButtonClick());
        }
        if (_victoryMainMenuButton != null)
        {
            _victoryMainMenuButton.UnregisterCallback<ClickEvent>(evt => OnMainMenuButtonClick());
        }
        
        // Unsubscribe from GameManager events
        if (gameManager != null)
        {
            gameManager.OnPlayerNotification -= HandlePlayerNotification;
            gameManager.OnGameStateChanged -= HandleGameStateChanged;
            if (gameManager.OnPlayerWon != null)
            {
                gameManager.OnPlayerWon -= HandlePlayerWon;
            }
        }
    }

    private void HandlePlayerNotification(Player player, GameManager.NotificationType type, string message)
    {
        if (!isLocalPlayerUI) return;
        
        string notificationType = "join";
        
        switch (type)
        {
            case GameManager.NotificationType.Join:
                notificationType = "join";
                break;
            case GameManager.NotificationType.Leave:
                notificationType = "leave";
                break;
            case GameManager.NotificationType.Death:
                notificationType = "death";
                break;
            case GameManager.NotificationType.Achievement:
                notificationType = "join"; // Use green color for achievements
                break;
        }
        
        ShowPlayerNotification(player.NickName, message, notificationType);
    }
}