using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;
using Photon.Pun;

public class GameLobbyUI : MonoBehaviour
{
    private UIDocument document;
    private GameManager gameManager;

    [Header("Main Menu Elements")]
    private Button startGameButton;
    private Button settingsButton;
    private Button quitButton;
    private Button creditsButton;

    [Header("Settings Elements")]
    private VisualElement settingsPanel;
    private Button closeSettingsButton;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private DropdownField difficultyDropdown;
    private Toggle tutorialToggle;

    [Header("Game State Elements")]
    private Label moneyLabel;
    private Label livesLabel;
    private Label scoreLabel;
    private Label gameTimeLabel;

    [Header("Multiplayer Settings")]
    [SerializeField] private string lobbySceneName = "NewLobbyUI";

    private void Awake()
    {
        document = GetComponent<UIDocument>();
        if (document == null)
        {
            Debug.LogError("UIDocument component not found!");
            return;
        }

        var root = document.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("Root VisualElement is null!");
            return;
        }

        // Add delay to ensure GameManager is initialized
        StartCoroutine(WaitForGameManager());
    }

    private IEnumerator WaitForGameManager()
    {
        int attempts = 0;
        while (GameManager.Instance == null && attempts < 10)
        {
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager instance not found after waiting!");
            yield break;
        }

        gameManager = GameManager.Instance;
        // Initialize UI elements that depend on GameManager
        var root = document.rootVisualElement;
        InitializeUIElements(root);
        RegisterCallbacks();
        LoadCurrentSettings();
    }

    private void InitializeUIElements(VisualElement root)
    {
        // Main Menu Elements
        startGameButton = root.Q<Button>("start-game-button");
        settingsButton = root.Q<Button>("settings-button");
        quitButton = root.Q<Button>("quit-button");
        creditsButton = root.Q<Button>("credits-button");

        // Settings Panel Elements
        settingsPanel = root.Q<VisualElement>("settings-panel");
        closeSettingsButton = root.Q<Button>("close-settings-button");
        musicVolumeSlider = root.Q<Slider>("music-volume-slider");
        sfxVolumeSlider = root.Q<Slider>("sfx-volume-slider");

        // Game State Elements
        moneyLabel = root.Q<Label>("money-label");
        livesLabel = root.Q<Label>("lives-label");

        // Initially hide settings panel
        settingsPanel.style.display = DisplayStyle.None;
    }

    private void RegisterCallbacks()
    {
        // Button callbacks
        startGameButton?.RegisterCallback<ClickEvent>(evt => GoToLobby());
        settingsButton?.RegisterCallback<ClickEvent>(evt => ShowSettings());
        quitButton?.RegisterCallback<ClickEvent>(evt => QuitGame());
        creditsButton?.RegisterCallback<ClickEvent>(evt => ShowCredits());
        closeSettingsButton?.RegisterCallback<ClickEvent>(evt => HideSettings());

        // Settings callbacks
        musicVolumeSlider?.RegisterValueChangedCallback(OnMusicVolumeChanged);
        sfxVolumeSlider?.RegisterValueChangedCallback(OnSFXVolumeChanged);

        // GameManager callbacks
        gameManager.OnGameStateChanged += OnGameStateChanged;
        gameManager.OnScoreChanged += UpdateScoreLabel;
        gameManager.OnMoneyChanged += UpdateMoneyLabel;
        gameManager.OnLivesChanged += UpdateLivesLabel;
    }

    private void LoadCurrentSettings()
    {
        if (gameManager.Settings != null)
        {
            musicVolumeSlider.value = gameManager.Settings.musicVolume;
            sfxVolumeSlider.value = gameManager.Settings.sfxVolume;
        }
    }

    private void GoToLobby()
    {
        if (PhotonNetwork.IsConnected)
            // This just takes us to the lobby scene
            SceneManager.LoadScene(lobbySceneName);
        else 
        {
            gameManager.StartNewGame();
        }
    }

    private void ShowSettings()
    {
        settingsPanel.style.display = DisplayStyle.Flex;
    }

    private void HideSettings()
    {
        settingsPanel.style.display = DisplayStyle.None;
        gameManager.SaveSettings();
    }

    private void QuitGame()
    {
        gameManager.SaveSettings();
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void ShowCredits()
    {
        gameManager.SaveSettings();
        SceneManager.LoadScene("CreditsScene");
    }

    private void OnMusicVolumeChanged(ChangeEvent<float> evt)
    {
        gameManager.Settings.musicVolume = evt.newValue;
    }

    private void OnSFXVolumeChanged(ChangeEvent<float> evt)
    {
        gameManager.Settings.sfxVolume = evt.newValue;
    }

    private void OnGameStateChanged(GameManager.GameState newState)
    {
        switch (newState)
        {
            case GameManager.GameState.MainMenu:
                settingsPanel.style.display = DisplayStyle.None;
                break;
            case GameManager.GameState.Playing:
                settingsPanel.style.display = DisplayStyle.None;
                break;
            case GameManager.GameState.Paused:
                break;
            case GameManager.GameState.GameOver:
                break;
            case GameManager.GameState.Victory:
                break;
        }
    }

    private void UpdateScoreLabel(int score)
    {
        if (scoreLabel != null)
            scoreLabel.text = $"Score: {score}";
    }

    private void UpdateMoneyLabel(int money)
    {
        if (moneyLabel != null)
            moneyLabel.text = $"Money: ${money}";
    }

    private void UpdateLivesLabel(int lives)
    {
        if (livesLabel != null)
            livesLabel.text = $"Lives: {lives}";
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnGameStateChanged -= OnGameStateChanged;
            gameManager.OnScoreChanged -= UpdateScoreLabel;
            gameManager.OnMoneyChanged -= UpdateMoneyLabel;
            gameManager.OnLivesChanged -= UpdateLivesLabel;
        }
    }

    private void Update()
    {
        // Update game time display if needed
        if (gameTimeLabel != null && gameManager.Progress != null)
        {
            float gameTime = gameManager.Progress.gameTime;
            int minutes = Mathf.FloorToInt(gameTime / 60);
            int seconds = Mathf.FloorToInt(gameTime % 60);
            gameTimeLabel.text = $"Time: {minutes:00}:{seconds:00}";
        }
    }
}