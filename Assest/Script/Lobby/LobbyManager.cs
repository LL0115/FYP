using UnityEngine;
using UnityEngine.UIElements;
using Photon.Pun;
using UnityEngine.SceneManagement;
using System.Text;
using Photon.Realtime;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    private VisualElement root;
    private TextField playerNameInput;
    private TextField roomNameInput;
    private Button createRoomButton;
    private Button refreshButton;
    private Button returnButton;
    private ScrollView roomListView;
    private string currentPlayerName = "";
    private string currentRoomName = "";

    // Add room options configuration
    private RoomOptions roomOptions = new RoomOptions
    {
        MaxPlayers = 5, // Set your desired max players
        IsVisible = true,
        IsOpen = true
    };

    void Start()
    {
        if (!PhotonNetwork.IsConnected)
        {
            // Ensure we connect to Photon if not already connected
            PhotonNetwork.ConnectUsingSettings();
            SceneManager.LoadScene("Startgame");
            return;
        }

        InitializeUI();
        PhotonNetwork.AutomaticallySyncScene = true; // Make sure this is enabled
        PhotonNetwork.JoinLobby();
    }
    
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master Server");
        PhotonNetwork.JoinLobby();
    }

    private void InitializeUI()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        
        playerNameInput = root.Q<TextField>("player-name-input");
        roomNameInput = root.Q<TextField>("room-name-input");
        createRoomButton = root.Q<Button>("create-room-button");
        refreshButton = root.Q<Button>("refresh-button");
        returnButton = root.Q<Button>("return-button");
        roomListView = root.Q<ScrollView>("room-list");

        SetupInputHandlers();
        SetupButtonListeners();
    }

    private void SetupInputHandlers()
    {
        playerNameInput.RegisterValueChangedCallback(evt => 
        {
            currentPlayerName = evt.newValue;
            playerNameInput.value = currentPlayerName;
        });

        roomNameInput.RegisterValueChangedCallback(evt => 
        {
            currentRoomName = evt.newValue;
            roomNameInput.value = currentRoomName;
        });

        if (!string.IsNullOrEmpty(currentPlayerName))
        {
            playerNameInput.value = currentPlayerName;
        }
        if (!string.IsNullOrEmpty(currentRoomName))
        {
            roomNameInput.value = currentRoomName;
        }
    }

    private void SetupButtonListeners()
    {
        createRoomButton.clicked += OnClickCreateRoom;
        refreshButton.clicked += () => PhotonNetwork.JoinLobby();
        returnButton.clicked += OnClickReturn;
    }

    private void OnClickReturn()
    {
        PhotonNetwork.Disconnect();
        SceneManager.LoadScene("Startgame");
    }

    private string GetRoomName()
    {
        return currentRoomName.Trim();
    }

    private string GetPlayerName()
    {
        return currentPlayerName.Trim();
    }

    private void OnClickCreateRoom()
    {
        string playerName = GetPlayerName();
        string roomName = GetRoomName();

        if (!string.IsNullOrEmpty(roomName) && !string.IsNullOrEmpty(playerName))
        {
            PhotonNetwork.LocalPlayer.NickName = playerName;
            PhotonNetwork.CreateRoom(roomName, roomOptions);
            Debug.Log($"Creating room: {roomName}");
        }
        else
        {
            Debug.LogWarning("Room Name is Empty or Player Name is Invalid");
        }
    }

    private void JoinRoom(string roomName)
    {
        string playerName = GetPlayerName();
        
        if (!string.IsNullOrEmpty(roomName) && !string.IsNullOrEmpty(playerName))
        {
            PhotonNetwork.LocalPlayer.NickName = playerName;
            PhotonNetwork.JoinRoom(roomName);
            Debug.Log($"Joining room: {roomName}");
        }
        else
        {
            Debug.LogWarning("Room Name is Empty or Player Name is Invalid");
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Successfully joined room: " + PhotonNetwork.CurrentRoom.Name);
        // Make sure AutomaticallySyncScene is enabled
        PhotonNetwork.AutomaticallySyncScene = true;
        SceneManager.LoadScene("NewRoomScene");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to join room. Error: {message}");
        // You could display this error in the UI
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Failed to create room. Error: {message}");
        // You could display this error in the UI
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        roomListView.Clear();

        foreach (RoomInfo roomInfo in roomList)
        {
            if (!roomInfo.RemovedFromList && roomInfo.PlayerCount > 0)
            {
                CreateRoomListItem(roomInfo);
            }
        }
    }

    private void CreateRoomListItem(RoomInfo roomInfo)
    {
        VisualElement roomItem = new VisualElement();
        roomItem.AddToClassList("room-item");

        Label roomNameLabel = new Label(roomInfo.Name);
        roomNameLabel.AddToClassList("room-name");
        
        Label playerCountLabel = new Label($"{roomInfo.PlayerCount}/{roomInfo.MaxPlayers}");
        playerCountLabel.AddToClassList("players-count");
        
        Label statusLabel = new Label(roomInfo.PlayerCount >= roomInfo.MaxPlayers ? "Full" : "Waiting");
        statusLabel.AddToClassList("room-status");
        
        Button joinButton = new Button(() => JoinRoom(roomInfo.Name)) { text = "Join" };
        joinButton.AddToClassList("button");
        joinButton.AddToClassList("join-button");

        // Disable join button if room is full
        if (roomInfo.PlayerCount >= roomInfo.MaxPlayers)
        {
            joinButton.SetEnabled(false);
            joinButton.AddToClassList("disabled");
        }

        roomItem.Add(roomNameLabel);
        roomItem.Add(playerCountLabel);
        roomItem.Add(statusLabel);
        roomItem.Add(joinButton);

        roomListView.Add(roomItem);
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Successfully joined lobby");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"Disconnected from server. Cause: {cause}");
        SceneManager.LoadScene("Startgame");
    }
}