using UnityEngine;
using UnityEngine.UIElements;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Text;
using Photon.Realtime;
using System.Collections.Generic;
using ExitGames.Client.Photon;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private MapData mapData; // Reference to MapData asset
    [SerializeField] private string defaultGameScene = "TDgameScene"; // Fallback scene if map selection fails
    
    private VisualElement root;
    private Label roomNameLabel;
    private Label playerCountLabel;
    private ScrollView playersList;
    private ScrollView chatMessages;
    private TextField chatInput;
    private Button sendButton;
    private Button readyButton;
    private Button startButton;
    private Button exitButton;
    private DropdownField mapSelector;
    private Label selectedMapInfo;
    private VisualElement mapPreviewImage;
    private Label mapDescription;

    private Dictionary<int, bool> playerReadyStatus = new Dictionary<int, bool>();
    private const byte PLAYER_READY_EVENT = 1;
    private const byte CHAT_MESSAGE_EVENT = 2;
    private const byte MAP_SELECTION_EVENT = 3;

    private string selectedMap; // Just store the map key

    void Start()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogWarning("Not in a Photon room! Returning to lobby.");
            SceneManager.LoadScene("NewLobbyUI");
            return;
        }

        // Ensure AutomaticallySyncScene is enabled
        PhotonNetwork.AutomaticallySyncScene = true;

        InitializeUI();
        SetupEventHandlers();
        UpdateRoomInfo();
        UpdatePlayersList();
        UpdateStartButtonState();
        
        // Set default map selection if we're the host
        if (PhotonNetwork.IsMasterClient && string.IsNullOrEmpty(selectedMap) && mapData != null && mapData.maps.Count > 0)
        {
            selectedMap = mapData.maps[0].MapKey;
            SyncMapSelection(selectedMap);
            UpdateMapPreview(selectedMap);
        }
    }

    private void InitializeUI()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        
        roomNameLabel = root.Q<Label>("room-name");
        playerCountLabel = root.Q<Label>("player-count");
        playersList = root.Q<ScrollView>("players-list");
        chatMessages = root.Q<ScrollView>("chat-messages");
        chatInput = root.Q<TextField>("chat-input");
        sendButton = root.Q<Button>("send-button");
        readyButton = root.Q<Button>("ready-button");
        startButton = root.Q<Button>("start-button");
        exitButton = root.Q<Button>("exit-button");
        mapSelector = root.Q<DropdownField>("map-selector");
        selectedMapInfo = root.Q<Label>("selected-map-info");
        mapPreviewImage = root.Q<VisualElement>("map-preview-image");
        mapDescription = root.Q<Label>("map-description");

        // Show start button only for room host
        startButton.style.display = PhotonNetwork.IsMasterClient ? DisplayStyle.Flex : DisplayStyle.None;

        // Initially disable start button until all players are ready
        startButton.SetEnabled(false);
        
        // Setup map selector
        SetupMapSelector();
    }
    
    private void SetupMapSelector()
    {
        if (mapSelector == null || mapData == null || mapData.maps.Count == 0) 
        {
            Debug.LogError("Map selector, MapData asset, or maps list is null or empty!");
            return;
        }

        // Populate the dropdown with map display names
        List<string> mapNames = new List<string>();
        foreach (var map in mapData.maps)
        {
            mapNames.Add(map.DisplayName);
        }
        
        mapSelector.choices = mapNames;
        
        // Only enable the selector for the host
        bool isHost = PhotonNetwork.IsMasterClient;
        mapSelector.SetEnabled(isHost);
        
        // Set visibility based on whether user is host
        mapSelector.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
        selectedMapInfo.style.display = isHost ? DisplayStyle.None : DisplayStyle.Flex;
        
        // Set initial selection for host
        if (isHost && mapNames.Count > 0)
        {
            mapSelector.index = 0;
        }
    }

    private void UpdateStartButtonState()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            startButton.style.display = DisplayStyle.None;
            return;
        }

        startButton.style.display = DisplayStyle.Flex;
        bool allPlayersReady = AreAllPlayersReady();
        startButton.SetEnabled(allPlayersReady);

        // Update button appearance based on ready state
        if (allPlayersReady)
        {
            startButton.RemoveFromClassList("disabled");
            startButton.AddToClassList("enabled");
        }
        else
        {
            startButton.RemoveFromClassList("enabled");
            startButton.AddToClassList("disabled");
        }
    }

    private bool AreAllPlayersReady()
    {
        // Check if we have at least 2 players
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2)
            return false;

        // Check if all players (except host) are ready
        foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (!playerReadyStatus.TryGetValue(player.ActorNumber, out bool isReady) || !isReady)
            {    
                return false;
            }
        }
        return true;
    }

    private void SetupEventHandlers()
    {
        exitButton.clicked += OnClickLeaveRoom;
        sendButton.clicked += SendChatMessage;
        readyButton.clicked += ToggleReady;
        startButton.clicked += StartGame;

        chatInput.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                SendChatMessage();
            }
        });
        
        // Setup map selector event handler
        if (mapSelector != null)
        {
            mapSelector.RegisterValueChangedCallback(OnMapSelectionChanged);
        }
    }
    
    private void OnMapSelectionChanged(ChangeEvent<string> evt)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        string selectedMapDisplayName = evt.newValue;
        
        // Find the map key from display name
        foreach (var map in mapData.maps)
        {
            if (map.DisplayName == selectedMapDisplayName)
            {
                selectedMap = map.MapKey;
                
                // Synchronize map selection with all clients
                SyncMapSelection(selectedMap);
                
                // Update the map preview
                UpdateMapPreview(selectedMap);
                
                // Announce map selection in chat
                AddChatMessage($"System: Host selected map: {selectedMapDisplayName}");
                break;
            }
        }
    }
    
    private void SyncMapSelection(string mapKey)
    {
        object[] content = new object[] { mapKey };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(MAP_SELECTION_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
    }
    
    private void UpdateMapPreview(string mapKey)
    {
        var mapInfo = mapData.GetMap(mapKey);
        if (mapInfo != null)
        {
            // Update the description
            mapDescription.text = mapInfo.Description;
            
            // Update the preview image
            if (mapInfo.PreviewImage != null)
            {
                mapPreviewImage.style.backgroundImage = new StyleBackground(mapInfo.PreviewImage);
            }
            else
            {
                // Default background if no texture is available
                mapPreviewImage.style.backgroundImage = null;
                mapPreviewImage.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            }
        }
    }

    private void UpdateRoomInfo()
    {
        roomNameLabel.text = PhotonNetwork.CurrentRoom.Name;
        playerCountLabel.text = $"{PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}";
    }

    private void UpdatePlayersList()
    {
        playersList.Clear();

        foreach (var player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            var playerItem = new VisualElement();
            playerItem.AddToClassList("player-item");

            var playerIcon = new VisualElement();
            playerIcon.AddToClassList("player-icon");
            playerIcon.Add(new Label($"P{player.ActorNumber}"));

            var playerName = new Label(player.NickName);
            playerName.AddToClassList("player-name");

            playerItem.Add(playerIcon);
            playerItem.Add(playerName);

            if (player.IsMasterClient)
            {
                var hostLabel = new Label("Host");
                hostLabel.AddToClassList("player-status");
                hostLabel.AddToClassList("host");
                playerItem.Add(hostLabel);
            }

            if (playerReadyStatus.TryGetValue(player.ActorNumber, out bool isReady) && isReady)
            {
                var readyLabel = new Label("Ready");
                readyLabel.AddToClassList("player-status");
                readyLabel.AddToClassList("ready");
                playerItem.Add(readyLabel);
            }

            playersList.Add(playerItem);
        }
    }

    private void SendChatMessage()
    {
        if (string.IsNullOrWhiteSpace(chatInput.value)) return;

        object[] content = new object[] { PhotonNetwork.LocalPlayer.NickName, chatInput.value };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(CHAT_MESSAGE_EVENT, content, raiseEventOptions, SendOptions.SendReliable);

        chatInput.value = string.Empty;
    }

    private void ToggleReady()
    {
        bool currentStatus = playerReadyStatus.ContainsKey(PhotonNetwork.LocalPlayer.ActorNumber) &&
                            playerReadyStatus[PhotonNetwork.LocalPlayer.ActorNumber];
        
        object[] content = new object[] { PhotonNetwork.LocalPlayer.ActorNumber, !currentStatus };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(PLAYER_READY_EVENT, content, raiseEventOptions, SendOptions.SendReliable);

        // Update ready button appearance
        UpdateReadyButtonState(!currentStatus);
    }

    private void UpdateReadyButtonState(bool isReady)
    {
        if (isReady)
        {
            readyButton.AddToClassList("not-ready");
            readyButton.text = "Not Ready";
        }
        else
        {
            readyButton.RemoveFromClassList("not-ready");
            readyButton.text = "Ready";
        }
    }

    private void StartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (AreAllPlayersReady())
        {
            // Close the room to prevent new players from joining mid-game
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            
            string sceneName = defaultGameScene;
            
            // Try to get scene name from map selection
            if (!string.IsNullOrEmpty(selectedMap))
            {
                var mapInfo = mapData.GetMap(selectedMap);
                if (mapInfo != null && !string.IsNullOrEmpty(mapInfo.SceneName))
                {
                    sceneName = mapInfo.SceneName;
                }
                else
                {
                    Debug.LogWarning($"Invalid map selection or scene name. Using default scene: {defaultGameScene}");
                    AddChatMessage($"System: Map information incomplete. Using default game scene.");
                }
            }
            
            // Store the selected map in room properties for the game scene to access
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props.Add("currentMap", selectedMap);
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            
            LogAvailableScenes();
            
            // Load the game scene
            Debug.Log($"Starting game with scene: {sceneName}");
            AddChatMessage($"System: Starting game! Loading {sceneName}...");

            // Ensure all clients load the same scene
            PhotonNetwork.AutomaticallySyncScene = true;
            
            // Load the scene for all clients
            PhotonNetwork.LoadLevel(sceneName);
        }
        else
        {
            AddChatMessage("System: Cannot start game - not all players are ready!");
        }
    }
    
    // Helper method to log all available scenes
    private void LogAvailableScenes()
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        Debug.Log($"Total scenes in build settings: {sceneCount}");
        
        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            Debug.Log($"Scene {i}: {sceneName} (Path: {scenePath})");
        }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
    }

    private void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case PLAYER_READY_EVENT:
                object[] readyData = (object[])photonEvent.CustomData;
                int playerId = (int)readyData[0];
                bool readyStatus = (bool)readyData[1];
                playerReadyStatus[playerId] = readyStatus;
                UpdatePlayersList();
                UpdateStartButtonState();

                // Add system message for ready status change
                Player player = PhotonNetwork.CurrentRoom.GetPlayer(playerId);
                string statusMessage = readyStatus ? "is ready" : "is not ready";
                AddChatMessage($"System: Player {player.NickName} {statusMessage}");
                break;

            case CHAT_MESSAGE_EVENT:
                object[] chatData = (object[])photonEvent.CustomData;
                string playerName = (string)chatData[0];
                string message = (string)chatData[1];
                AddChatMessage($"{playerName}: {message}");
                break;
                
            case MAP_SELECTION_EVENT:
                object[] mapData = (object[])photonEvent.CustomData;
                string mapKey = (string)mapData[0];
                selectedMap = mapKey;
                
                var mapInfo = this.mapData.GetMap(mapKey);
                if (mapInfo != null)
                {
                    // Update UI to show selected map
                    if (mapSelector != null && PhotonNetwork.IsMasterClient)
                    {
                        mapSelector.value = mapInfo.DisplayName;
                    }
                    else if (selectedMapInfo != null)
                    {
                        // For non-host players, update the info label
                        selectedMapInfo.text = $"Selected Map: {mapInfo.DisplayName}";
                    }
                    
                    // Update preview for everyone
                    UpdateMapPreview(mapKey);
                }
                break;
        }
    }

    private void AddChatMessage(string message)
    {
        var messageLabel = new Label(message);
        messageLabel.AddToClassList("chat-message");
        chatMessages.Add(messageLabel);
        chatMessages.ScrollTo(messageLabel);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateRoomInfo();
        UpdatePlayersList();
        UpdateStartButtonState();
        AddChatMessage($"System: {newPlayer.NickName} joined the room");
        
        // If we're the host and have a map selected, sync it to the new player
        if (PhotonNetwork.IsMasterClient && !string.IsNullOrEmpty(selectedMap))
        {
            SyncMapSelection(selectedMap);
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateRoomInfo();
        playerReadyStatus.Remove(otherPlayer.ActorNumber);
        UpdatePlayersList();
        UpdateStartButtonState();
        AddChatMessage($"System: {otherPlayer.NickName} left the room");
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // Update UI controls for the new host
        if (PhotonNetwork.LocalPlayer.ActorNumber == newMasterClient.ActorNumber)
        {
            startButton.style.display = DisplayStyle.Flex;
            
            if (mapSelector != null)
            {
                mapSelector.SetEnabled(true);
                mapSelector.style.display = DisplayStyle.Flex;
                
                if (selectedMapInfo != null)
                {
                    selectedMapInfo.style.display = DisplayStyle.None;
                }
            }
        }
        
        UpdateStartButtonState();
        UpdatePlayersList();
        AddChatMessage($"System: {newMasterClient.NickName} is now the host");
    }

    private void OnClickLeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("NewLobbyUI");
    }
}