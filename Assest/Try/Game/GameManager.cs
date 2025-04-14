using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections;

public class GameManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static GameManager Instance { get; private set; }

    // Game State
    public enum GameState
    {
        MainMenu,
        Lobby,
        Playing,
        Paused,
        GameOver,
        Victory
    }

    // Custom event codes for Photon network events
    private const byte PLAYER_DIED_EVENT = 1;
    private const byte GAME_OVER_EVENT = 2;
    private const byte PLAYER_NOTIFICATION_EVENT = 3;
    private const byte CHECK_LAST_PLAYER_EVENT = 4; // New event for checking last player

    // Notification types
    public enum NotificationType
    {
        Join,
        Leave,
        Death,
        Achievement
    }

    public GameState CurrentGameState { get; private set; }

    // Game Settings
    [Serializable]
    public class GameSettings
    {
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        public int difficulty = 1;
        public bool tutorialCompleted = false;
    }

    // Game Progress
    [Serializable]
    public class GameProgress
    {
        public int currentLevel = 0;
        public float gameTime = 0f;
        public int score = 0;
    }

    public GameSettings Settings { get; private set; }
    public GameProgress Progress { get; private set; }

    public event Action<GameState> OnGameStateChanged;
    public event Action<int> OnScoreChanged;
    public event Action<int> OnMoneyChanged;
    public event Action<int> OnLivesChanged;
    public Action<Player> OnPlayerWon;
    public event Action<Player, NotificationType, string> OnPlayerNotification;
    private LevelData levelData;

    // Track which players are still alive
    private HashSet<int> alivePlayers = new HashSet<int>();
    
    // Debug flag to show detailed logging
    [SerializeField] private bool showDebugLogs = true;

    // PhotonView reference
    private new PhotonView photonView;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGame();

            // Get the PhotonView component
            photonView = GetComponent<PhotonView>();
            if (photonView == null && PhotonNetwork.IsConnected)
            {
                Debug.LogWarning("GameManager has no PhotonView! Adding one dynamically.");
                photonView = gameObject.AddComponent<PhotonView>();
                photonView.ViewID = 999; // Use a high number to avoid conflicts
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeGame()
    {
        Settings = LoadSettings();
        Progress = new GameProgress();
        CurrentGameState = GameState.MainMenu;
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Initialize alive players list if we're already in a room
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            alivePlayers.Clear();
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                // Check if player has IsDead property and if not, add them to alive list
                if (!player.CustomProperties.TryGetValue("IsDead", out object isDead) || !(bool)isDead)
                {
                    alivePlayers.Add(player.ActorNumber);
                }
            }
            DebugLog($"Initialized with {alivePlayers.Count} alive players");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string sceneName = scene.name;
        if (scene.name == "MainMenu" || scene.name == "Startgame")
        {
            SetGameState(GameState.MainMenu);
        }
        else if (IsGameplayScene(sceneName))
        {
            SetGameState(GameState.Playing);
            
            // Initialize alive players when game starts
            if (PhotonNetwork.IsConnected)
            {
                alivePlayers.Clear();
                foreach (Player player in PhotonNetwork.PlayerList)
                {
                    // Reset IsDead property when joining new game
                    if (player.CustomProperties.TryGetValue("IsDead", out object isDead) && (bool)isDead)
                    {
                        // Clear the IsDead property for all players in a new game
                        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                        props.Remove("IsDead");
                        player.SetCustomProperties(props);
                    }
                    
                    alivePlayers.Add(player.ActorNumber);
                }
                DebugLog($"Game started with {alivePlayers.Count} players");
            }
        }
        else if (scene.name == "NewLobbyScene" || scene.name == "NewLobbyUI")
        {
            SetGameState(GameState.Lobby);
        }
    }

    // Helper method for conditional debugging
    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[GameManager] {message}");
        }
    }

    private bool IsGameplayScene(string sceneName)
    {
        string[] gameplayScenes = new string[] 
        { 
            "TDgameScene", 
            "Map_Forest", 
            "Map_Desert", 
            "Map_Winter",
        };
        
        return Array.Exists(gameplayScenes, scene => scene == sceneName);
    }

    private GameSettings LoadSettings()
    {
        GameSettings settings = new GameSettings();
        settings.musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        settings.sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
        return settings;
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MusicVolume", Settings.musicVolume);
        PlayerPrefs.SetFloat("SFXVolume", Settings.sfxVolume);
        PlayerPrefs.Save();
    }

    public void StartNewGame()
    {
        // Reset all necessary values
        Progress = new GameProgress();
        Time.timeScale = 1f;

        SetGameState(GameState.Lobby);

        // Connect to the server
        PhotonNetwork.ConnectUsingSettings();
        DebugLog("Connecting to server...");
    }

    public override void OnConnectedToMaster()
    {
        DebugLog("Connected to server");
        SceneManager.LoadScene("NewLobbyUI");
    }


    public void StartGameformMainMenu()
    {
        SceneManager.LoadScene("TDgameScene");
    }

  
    public void PauseGame()
    {
        if (CurrentGameState == GameState.Playing)
        {
            SetGameState(GameState.Paused);
            Time.timeScale = 0f;
        }
    }

    public void ResumeGame()
    {
        if (CurrentGameState == GameState.Paused)
        {
            SetGameState(GameState.Playing);
            Time.timeScale = 1f;
        }
    }

    // Called when local player runs out of lives
    public void PlayerDied()
    {
        DebugLog($"PlayerDied() called for {(PhotonNetwork.LocalPlayer?.NickName ?? "local player")}");
        
        if (PhotonNetwork.IsConnected)
        {
            // Remove player from alive players list
            if (alivePlayers.Contains(PhotonNetwork.LocalPlayer.ActorNumber))
            {
                alivePlayers.Remove(PhotonNetwork.LocalPlayer.ActorNumber);
            }
            
            // Set custom property to mark player as dead
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props.Add("IsDead", true);
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            
            // Send event to all players that this player died
            object[] content = new object[] { PhotonNetwork.LocalPlayer.ActorNumber };
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(PLAYER_DIED_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
            
            // Show game over UI for the local player
            SetGameState(GameState.GameOver);
            
            // If master client, immediately check if game is over
            if (PhotonNetwork.IsMasterClient)
            {
                CheckLastPlayerStanding();
            }
            else
            {
                // Request master client to check game state
                object[] checkContent = new object[] { };
                RaiseEventOptions checkOptions = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
                PhotonNetwork.RaiseEvent(CHECK_LAST_PLAYER_EVENT, checkContent, checkOptions, SendOptions.SendReliable);
            }
        }
        else
        {
            // Single player mode
            GameOver();
        }
    }

    // New method to notify other players about a player's death (for UI notifications)
    public void NotifyPlayerDeath(Player player)
    {
        if (!PhotonNetwork.IsConnected) return;
        
        // Send notification to all players
        object[] content = new object[] 
        { 
            player.ActorNumber, 
            (int)NotificationType.Death,
            "was defeated!"
        };
        
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(PLAYER_NOTIFICATION_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
    }

    // New method to send custom player notifications
    public void SendPlayerNotification(Player player, NotificationType type, string message)
    {
        if (!PhotonNetwork.IsConnected) return;
        
        object[] content = new object[] { player.ActorNumber, (int)type, message };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(PLAYER_NOTIFICATION_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
    }

    // Achievement notification for individual players
    public void AwardAchievement(string achievementName)
    {
        if (!PhotonNetwork.IsConnected) return;
        
        // Only send achievement notifications for local player
        SendPlayerNotification(PhotonNetwork.LocalPlayer, NotificationType.Achievement, $"earned the {achievementName} achievement!");
    }

    // Check if this player is the last one alive
    private void CheckLastPlayerStanding()
    {
        if (!PhotonNetwork.IsConnected) return;
        
        DebugLog($"Checking last player standing. Alive players: {alivePlayers.Count}");

        // If we're already in Victory state, don't check again
        if (CurrentGameState == GameState.Victory)
        {
            DebugLog("Not checking last player standing because already in Victory state");
            return;
        }
        
        // If only one player is left alive
        if (alivePlayers.Count == 1)
        {
            // Find the remaining player
            int lastPlayerActorNumber = -1;
            foreach (int actorNumber in alivePlayers)
            {
                lastPlayerActorNumber = actorNumber;
                break;
            }
            
            DebugLog($"Last player standing: {lastPlayerActorNumber}");
            
            // Find the player object
            Player winnerPlayer = null;
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.ActorNumber == lastPlayerActorNumber)
                {
                    winnerPlayer = player;
                    break;
                }
            }
            
            // Send event to declare the winner
            object[] content = new object[] { lastPlayerActorNumber };
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(GAME_OVER_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
            
            // Also trigger the event locally if this is the winner
            if (lastPlayerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                Victory();
                
                if (winnerPlayer != null && OnPlayerWon != null)
                {
                    OnPlayerWon(winnerPlayer);
                }
            }
        }
        // If all players are dead
        else if (alivePlayers.Count == 0)
        {
            // Everyone lost
            DebugLog("All players are defeated!");
            object[] content = new object[] { -1 }; // -1 indicates no winner
            RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(GAME_OVER_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        try
        {         
            if (photonEvent.Code == PLAYER_DIED_EVENT)
            {
                // Safely extract data
                if (photonEvent.CustomData is object[] data && data.Length > 0)
                {
                    // Make sure data[0] is an integer or can be converted to one
                    int deadPlayerActorNumber;
                    if (data[0] is int intValue)
                    {
                        deadPlayerActorNumber = intValue;
                    }
                    else if (data[0] is byte byteValue)
                    {
                        deadPlayerActorNumber = (int)byteValue;
                    }
                    else if (data[0] is short shortValue)
                    {
                        deadPlayerActorNumber = (int)shortValue;
                    }
                    else
                    {
                        Debug.LogError($"Expected integer for player ActorNumber, but got {data[0]?.GetType().Name ?? "null"}");
                        return;
                    }
                    
                    // Remove from alive players
                    if (alivePlayers.Contains(deadPlayerActorNumber))
                    {
                        alivePlayers.Remove(deadPlayerActorNumber);
                        DebugLog($"Player #{deadPlayerActorNumber} died. Remaining players: {alivePlayers.Count}");
                        
                        // Find the corresponding player
                        Player deadPlayer = null;
                        foreach (Player player in PhotonNetwork.PlayerList)
                        {
                            if (player.ActorNumber == deadPlayerActorNumber)
                            {
                                deadPlayer = player;
                                break;
                            }
                        }
                        
                        if (deadPlayer != null)
                        {
                            // Notify all players of the death
                            NotifyPlayerDeath(deadPlayer);
                        }
                        
                        // If local player died, show game over UI
                        if (deadPlayerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                        {
                            GameOver();
                        }
                        
                        // If master client, check if game is over
                        if (PhotonNetwork.IsMasterClient)
                        {
                            CheckLastPlayerStanding();
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Invalid data format for PLAYER_DIED_EVENT: {photonEvent.CustomData?.GetType().Name ?? "null"}");
                }
            }
            else if (photonEvent.Code == GAME_OVER_EVENT)
            {
                // Safely extract data
                if (photonEvent.CustomData is object[] data && data.Length > 0)
                {
                    // Make sure data[0] is an integer or can be converted to one
                    int winnerActorNumber;
                    if (data[0] is int intValue)
                    {
                        winnerActorNumber = intValue;
                    }
                    else if (data[0] is byte byteValue)
                    {
                        winnerActorNumber = (int)byteValue;
                    }
                    else if (data[0] is short shortValue)
                    {
                        winnerActorNumber = (int)shortValue;
                    }
                    else
                    {
                        Debug.LogError($"Expected integer for winner ActorNumber, but got {data[0]?.GetType().Name ?? "null"}");
                        return;
                    }
                    
                    DebugLog($"Game over event received. Winner: {winnerActorNumber}");
                    
                    // Skip processing if already in Victory state, unless I'm the winner
                    if (CurrentGameState == GameState.Victory && winnerActorNumber != PhotonNetwork.LocalPlayer.ActorNumber)
                    {
                        DebugLog("Ignoring game over event because already in Victory state");
                        return;
                    }
                    
                    // -1 means no winner (everyone lost)
                    if (winnerActorNumber == -1)
                    {
                        // Only process GameOver if not already in Victory state
                        if (CurrentGameState != GameState.Victory)
                        {
                            GameOver();
                        }
                    }
                    // If I'm the winner
                    else if (winnerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                    {
                        DebugLog("You win!");
                        
                        // Make sure we're not already in victory state
                        if (CurrentGameState != GameState.Victory)
                        {
                            Victory();
                            
                            // Find the player object
                            Player winnerPlayer = null;
                            foreach (Player player in PhotonNetwork.PlayerList)
                            {
                                if (player.ActorNumber == winnerActorNumber)
                                {
                                    winnerPlayer = player;
                                    break;
                                }
                            }
                            
                            if (winnerPlayer != null && OnPlayerWon != null)
                            {
                                OnPlayerWon(winnerPlayer);
                            }
                        }
                    }
                    // If someone else won
                    else
                    {
                        DebugLog("You lost!");
                        // Only go to game over if we're not already there
                        if (CurrentGameState != GameState.GameOver && CurrentGameState != GameState.Victory)
                        {
                            GameOver();
                        }
                        
                        // Find the winner player object to notify UI
                        Player winnerPlayer = null;
                        foreach (Player player in PhotonNetwork.PlayerList)
                        {
                            if (player.ActorNumber == winnerActorNumber)
                            {
                                winnerPlayer = player;
                                break;
                            }
                        }
                        
                        if (winnerPlayer != null && OnPlayerWon != null)
                        {
                            OnPlayerWon(winnerPlayer);
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Invalid data format for GAME_OVER_EVENT: {photonEvent.CustomData?.GetType().Name ?? "null"}");
                }
            }
            else if (photonEvent.Code == PLAYER_NOTIFICATION_EVENT)
            {
                // Safely extract data
                if (photonEvent.CustomData is object[] data && data.Length > 2)
                {
                    // Safe extraction of player actor number
                    int playerActorNumber;
                    if (data[0] is int intValue)
                    {
                        playerActorNumber = intValue;
                    }
                    else if (data[0] is byte byteValue)
                    {
                        playerActorNumber = (int)byteValue;
                    }
                    else if (data[0] is short shortValue)
                    {
                        playerActorNumber = (int)shortValue;
                    }
                    else
                    {
                        Debug.LogError($"Expected integer for player ActorNumber, but got {data[0]?.GetType().Name ?? "null"}");
                        return;
                    }
                    
                    // Safe extraction of notification type
                    NotificationType notificationType;
                    if (data[1] is int notificationTypeInt)
                    {
                        notificationType = (NotificationType)notificationTypeInt;
                    }
                    else if (data[1] is byte notificationTypeByte)
                    {
                        notificationType = (NotificationType)(int)notificationTypeByte;
                    }
                    else
                    {
                        Debug.LogError($"Expected integer for notification type, but got {data[1]?.GetType().Name ?? "null"}");
                        return;
                    }
                    
                    // Safe extraction of message
                    string message = data[2] as string;
                    if (message == null)
                    {
                        Debug.LogWarning($"Expected string for message, but got {data[2]?.GetType().Name ?? "null"}");
                        message = ""; // Provide a default empty message
                    }
                    
                    // Find the player
                    Player notifiedPlayer = null;
                    foreach (Player player in PhotonNetwork.PlayerList)
                    {
                        if (player.ActorNumber == playerActorNumber)
                        {
                            notifiedPlayer = player;
                            break;
                        }
                    }
                    
                    // Trigger notification event
                    if (notifiedPlayer != null)
                    {
                        DebugLog($"Notification: Player {notifiedPlayer.NickName} {message} (type: {notificationType})");
                        OnPlayerNotification?.Invoke(notifiedPlayer, notificationType, message);
                    }
                }
                else
                {
                    Debug.LogError($"Invalid data format for PLAYER_NOTIFICATION_EVENT: {photonEvent.CustomData?.GetType().Name ?? "null"}");
                }
            }
            else if (photonEvent.Code == CHECK_LAST_PLAYER_EVENT)
            {
                // Only master client should handle this
                if (PhotonNetwork.IsMasterClient)
                {
                    CheckLastPlayerStanding();
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception for debugging
            Debug.LogError($"Error in OnEvent: {ex.Message}\nStackTrace: {ex.StackTrace}");
        }
    }

    public void GameOver()
    {
        // Don't change to GameOver if already in Victory state
        if (CurrentGameState != GameState.GameOver && CurrentGameState != GameState.Victory)
        {
            DebugLog("Game Over!");
            SetGameState(GameState.GameOver);
            
            // Mark player as dead in multiplayer if not already
            if (PhotonNetwork.IsConnected)
            {
                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                props.Add("IsDead", true);
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            }
        }
        else if (CurrentGameState == GameState.Victory)
        {
            DebugLog("Ignoring GameOver because player is already in Victory state");
        }
    }

    public void Victory()
    {
        if (CurrentGameState != GameState.Victory && CurrentGameState != GameState.GameOver)
        {
            DebugLog("You Win!");
            SetGameState(GameState.Victory);
            
            // Ensure we're counted as alive
            if (PhotonNetwork.IsConnected && !alivePlayers.Contains(PhotonNetwork.LocalPlayer.ActorNumber))
            {
                alivePlayers.Add(PhotonNetwork.LocalPlayer.ActorNumber);
            }
            
            // Mark this player as a winner with custom property
            if (PhotonNetwork.IsConnected)
            {
                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                props.Add("IsWinner", true);
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            }
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SetGameState(GameState.MainMenu);
        SceneManager.LoadScene("Startgame");
    }

    public void SetGameState(GameState newState)
    {
        if (CurrentGameState != newState)
        {
            DebugLog($"Game state changing from {CurrentGameState} to {newState}");
            CurrentGameState = newState;
            OnGameStateChanged?.Invoke(newState);
        }
    }

    // Progress Update Methods
    public void UpdateMoney(int amount)
    {
        // Don't track money internally anymore
        OnMoneyChanged?.Invoke(amount);
    }

    public void UpdateLives(int amount)
    {
        // Don't track lives internally anymore
        OnLivesChanged?.Invoke(amount);
        
        // Still handle game over logic if needed
        if (amount < 0 && PlayerIsOutOfLives())
        {
            PlayerDied();
        }
    }

    private bool PlayerIsOutOfLives()
    {
        // Get this from PlayerStat instead
        // This could be a direct reference or you could find the local player's stat
        PlayerStat localPlayerStat = FindLocalPlayerStat();
        return localPlayerStat != null && localPlayerStat.GetLives() <= 0;
    }

    private PlayerStat FindLocalPlayerStat()
    {
        // Find the PlayerStat component that belongs to the local player
        PlayerStat[] allPlayerStats = FindObjectsOfType<PlayerStat>();
        foreach (PlayerStat stat in allPlayerStats)
        {
            if (!PhotonNetwork.IsConnected || stat.photonView.IsMine)
            {
                return stat;
            }
        }
        return null;
    }

    public void UpdateScore(int points)
    {
        Progress.score += points;
        OnScoreChanged?.Invoke(Progress.score);
    }

    // Check if a player is still alive
    public bool IsPlayerAlive(Player player)
    {
        return alivePlayers.Contains(player.ActorNumber);
    }

    // Get the number of alive players
    public int GetAlivePlayerCount()
    {
        return alivePlayers.Count;
    }

    private void Update()
    {
        if (CurrentGameState == GameState.Playing)
        {
            Progress.gameTime += Time.deltaTime;
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        
        // Add them to alive players list
        alivePlayers.Add(newPlayer.ActorNumber);
        
        // Send notification
        DebugLog($"Player {newPlayer.NickName} entered the room");
        OnPlayerNotification?.Invoke(newPlayer, NotificationType.Join, "joined the game");
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        
        // Remove from alive players
        alivePlayers.Remove(otherPlayer.ActorNumber);
        
        // Send notification
        DebugLog($"Player {otherPlayer.NickName} left the room");
        OnPlayerNotification?.Invoke(otherPlayer, NotificationType.Leave, "left the game");
        
        // Update game state if needed (like if there's only one player left)
        if (PhotonNetwork.IsMasterClient && alivePlayers.Count == 1 && 
            PhotonNetwork.IsConnected && CurrentGameState == GameState.Playing)
        {
            // Check if game is over
            CheckLastPlayerStanding();
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        // Check if the IsDead property changed
        if (changedProps.ContainsKey("IsDead"))
        {
            bool isDead = (bool)changedProps["IsDead"];
            DebugLog($"Player {targetPlayer.NickName} IsDead property changed to: {isDead}");
            
            // Update our tracking of alive players
            if (isDead)
            {
                if (alivePlayers.Contains(targetPlayer.ActorNumber))
                {
                    alivePlayers.Remove(targetPlayer.ActorNumber);
                    DebugLog($"Removed player {targetPlayer.NickName} from alive list. Alive count: {alivePlayers.Count}");
                    
                    // If this player just died, send a notification
                    if (targetPlayer != PhotonNetwork.LocalPlayer)
                    {
                        OnPlayerNotification?.Invoke(targetPlayer, NotificationType.Death, "was defeated!");
                    }
                    
                    // If master client, check if game is over
                    if (PhotonNetwork.IsMasterClient)
                    {
                        CheckLastPlayerStanding();
                    }
                }
            }
            else // Player came back to life
            {
                if (!alivePlayers.Contains(targetPlayer.ActorNumber))
                {
                    alivePlayers.Add(targetPlayer.ActorNumber);
                    DebugLog($"Added player {targetPlayer.NickName} to alive list. Alive count: {alivePlayers.Count}");
                }
            }
        }
    }

    public void ResetGameState()
    {
        // Reset game state for a new game
        alivePlayers.Clear();
        
        // Clear custom properties
        if (PhotonNetwork.IsConnected)
        {
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props.Remove("IsDead");
            props.Remove("IsWinner");
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private new void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private new void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }
}