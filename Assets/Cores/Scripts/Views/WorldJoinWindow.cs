using Coherence.Cloud;
using Coherence.Connection;
using Coherence.Samples.RoomsDialog;
using Coherence.Toolkit;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace Y200.ProjectMultiplayer
{
    public class WorldJoinWindow : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform _window;
        [SerializeField] private RectTransform _worldListPanel;
        [SerializeField] private RectTransform _worldListViewParent;
        [SerializeField] private ConnectDialogRoomView _worldItemTemplate;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _createWorldButton;
        [SerializeField] private Button _joinWorldButton;
        [SerializeField] private Button _disconnectButton;

        [Header("Create World UI")]
        [SerializeField] private RectTransform _createWorldPanel;
        [SerializeField] private InputField _worldNameInput;
        [SerializeField] private InputField _maxPlayerInput;
        [SerializeField] private Button _createAndJoinButton;
        [SerializeField] private Button _cancelCreateButton;

        [Header("Misc")]
        [SerializeField] private CoherenceBridge _bridge;
        [SerializeField] private GameObject _loadingSpinner;
        [SerializeField] private Text _statusText;

        private CloudRooms _cloudRooms;
        private CloudRoomsService _cloudRoomsService;
        private IRoomsService _activeRoomsService;

        private CancellationTokenSource _cancellationTokenSource;
        private List<ConnectDialogRoomView> _worldViews = new();
        private ConnectDialogRoomView _selectedWorld;
        private ulong _lastCreatedRoomUid;
        private bool _joinNextCreatedRoom;
        private IReadOnlyList<string> _regions = Array.Empty<string>();

        private void Awake()
        {
            _worldItemTemplate.gameObject.SetActive(false);
            _createWorldPanel.gameObject.SetActive(false);
            _loadingSpinner.SetActive(false);
            _disconnectButton.gameObject.SetActive(false);

            _createWorldButton.onClick.AddListener(OnCreateWorldButtonClicked);
            _createAndJoinButton.onClick.AddListener(OnCreateAndJoinButtonClicked);
            _cancelCreateButton.onClick.AddListener(OnCancelButtonClicked);
            _refreshButton.onClick.AddListener(RefreshRooms);
            _joinWorldButton.onClick.AddListener(() =>
            {
                if (_selectedWorld != null)
                    JoinRoom(_selectedWorld.RoomData);
            });

            _bridge.onConnected.AddListener(OnBridgeConnected);
            _bridge.onDisconnected.AddListener(OnBridgeDisconnected);
            _bridge.onConnectionError.AddListener(OnConnectionError);

            _disconnectButton.onClick.AddListener(_bridge.Disconnect);
        }

        private async void Start()
        {
            if (!_bridge)
            {
                Debug.LogError("CoherenceBridge not assigned.");
                return;
            }

            _statusText.text = "Logging in...";
            EnterLoadingState();

            var cloudLogin = FindAnyObjectByType<CoherenceCloudLogin>(FindObjectsInactive.Exclude);
            if (cloudLogin == null)
            {
                _statusText.text = "No CoherenceCloudLogin found in scene.";
                return;
            }

            if (!cloudLogin.IsLoggedIn)
            {
                var result = await cloudLogin.LogInAsync(GetOrCreateActiveCancellationToken());
                if (!result.IsCompletedSuccessfully)
                {
                    _statusText.text = "Login failed: " + result.Error.Message;
                    Debug.LogError(result.Error.Message);
                    return;
                }

                _cloudRooms = result.Result.Services.Rooms;
            }
            else
            {
                _cloudRooms = cloudLogin.Services.Rooms;
            }

            _statusText.text = "Fetching regions...";
            _cloudRooms.RefreshRegions(OnCloudRoomsRegionsChanged, GetOrCreateActiveCancellationToken());
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            if (_bridge)
            {
                _bridge.onConnected.RemoveListener(OnBridgeConnected);
                _bridge.onDisconnected.RemoveListener(OnBridgeDisconnected);
                _bridge.onConnectionError.RemoveListener(OnConnectionError);
            }
        }

        // -------------------- CLOUD REGION HANDLING --------------------

        private void OnCloudRoomsRegionsChanged(RequestResponse<IReadOnlyList<string>> requestResponse)
        {
            if (requestResponse.Status != RequestStatus.Success)
            {
                _statusText.text = "Error fetching regions.";
                Debug.LogError(requestResponse.Exception);
                ExitLoadingState();
                return;
            }

            _regions = requestResponse.Result;
            if (_regions.Count == 0)
            {
                _statusText.text = "No Cloud regions available.";
                ExitLoadingState();
                return;
            }

            // Use first region by default
            string region = _regions[0];
            _cloudRoomsService = _cloudRooms.GetRoomServiceForRegion(region);
            _activeRoomsService = _cloudRoomsService;

            _statusText.text = $"Connected to region: {region}. Fetching worlds...";
            RefreshRooms();
        }

        // -------------------- ROOM HANDLING --------------------

        private void RefreshRooms()
        {
            if (_activeRoomsService == null)
                return;

            EnterLoadingState();
            _activeRoomsService.FetchRooms(OnRoomsFetched, null, GetOrCreateActiveCancellationToken());
        }

        private void OnRoomsFetched(RequestResponse<IReadOnlyList<RoomData>> response)
        {
            ExitLoadingState();

            foreach (var view in _worldViews)
                Destroy(view.gameObject);
            _worldViews.Clear();
            _selectedWorld = null;

            if (response.Status != RequestStatus.Success)
            {
                _statusText.text = "Error fetching worlds.";
                Debug.LogError(response.Exception);
                return;
            }

            var rooms = response.Result;
            if (rooms.Count == 0)
            {
                _statusText.text = "No worlds available.";
                return;
            }

            _statusText.text = $"Found {rooms.Count} worlds.";

            foreach (var room in rooms)
            {
                var view = Instantiate(_worldItemTemplate, _worldListViewParent);
                view.RoomData = room;
                view.gameObject.SetActive(true);

                view.OnClick = () =>
                {
                    foreach (var v in _worldViews)
                        v.IsSelected = false;

                    view.IsSelected = true;
                    _selectedWorld = view;
                    _joinWorldButton.interactable = true;
                };

                _worldViews.Add(view);
            }
        }

        private void OnCreateWorldButtonClicked()
        {
            _createWorldPanel.gameObject.SetActive(true);
            _worldListPanel.gameObject.SetActive(false);
        }

        private void OnCreateAndJoinButtonClicked()
        {
            _joinNextCreatedRoom = true;

            var options = RoomCreationOptions.Default;
            options.KeyValues.Add(RoomData.RoomNameKey, _worldNameInput.text);
            options.MaxClients = int.TryParse(_maxPlayerInput.text, out var limit) ? limit : 10;

            EnterLoadingState();
            _activeRoomsService?.CreateRoom(OnRoomCreated, options, GetOrCreateActiveCancellationToken());
            _createWorldPanel.gameObject.SetActive(false);

            _worldListPanel.gameObject.SetActive(true);
            _createWorldPanel.gameObject.SetActive(false);

            RefreshRooms();
        }

        private void OnCancelButtonClicked()
        {
            _worldListPanel.gameObject.SetActive(true);
            _createWorldPanel.gameObject.SetActive(false);
        }

        private void OnBridgeConnected(CoherenceBridge _)
        {
            UpdateDialogsVisibility();

            Debug.Log("[CoherenceBridge] Connected successfully.");
            _statusText.text = "Joined world!";
            ExitLoadingState();
        }

        private void OnBridgeDisconnected(CoherenceBridge _, ConnectionCloseReason reason)
        { 
            UpdateDialogsVisibility();
        }
        private void UpdateDialogsVisibility()
        {
            Debug.Log("[CoherenceBridge] Disconnected from server.");
            _statusText.text = "Disconnected.";
            ExitLoadingState();

            _window.gameObject.SetActive(!_bridge.IsConnected);
            _disconnectButton.gameObject.SetActive(_bridge.IsConnected);
        }

        private void OnConnectionError(CoherenceBridge _, ConnectionException exception)
        {
            ExitLoadingState();
            RefreshRooms();

            var (title, message) = exception.GetPrettyMessage();

            Debug.LogError(message, this);
            _statusText.text = message;
        }

        private void OnRoomCreated(RequestResponse<RoomData> response)
        {
            if (response.Status != RequestStatus.Success)
            {
                _statusText.text = "Error creating world.";
                Debug.LogError(response.Exception);
                ExitLoadingState();
                return;
            }

            var createdRoom = response.Result;
            _lastCreatedRoomUid = createdRoom.UniqueId;

            if (_joinNextCreatedRoom)
            {
                _joinNextCreatedRoom = false;
                JoinRoom(createdRoom);
            }
            else
            {
                RefreshRooms();
            }
        }

        private void JoinRoom(RoomData roomData)
        {
            _statusText.text = "Joining world...";
            EnterLoadingState();
            _bridge.JoinRoom(roomData);
        }

        // -------------------- HELPERS --------------------

        private void EnterLoadingState()
        {
            _loadingSpinner.SetActive(true);
            _refreshButton.interactable = false;
            _createWorldButton.interactable = false;
            _joinWorldButton.interactable = false;
        }

        private void ExitLoadingState()
        {
            _loadingSpinner.SetActive(false);
            _refreshButton.interactable = true;
            _createWorldButton.interactable = true;
            _joinWorldButton.interactable = _selectedWorld != null;
        }

        private CancellationToken GetOrCreateActiveCancellationToken() => GetOrCreateActiveCancellationTokenSource().Token;
        private CancellationTokenSource GetOrCreateActiveCancellationTokenSource() => _cancellationTokenSource ??= new();
    }
}
