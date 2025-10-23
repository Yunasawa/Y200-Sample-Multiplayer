using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Coherence.Cloud;
using Coherence.Connection;
using Coherence.Runtime;
using Coherence.Toolkit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Coherence.Samples.LobbiesDialog
{
    public class LobbiesDialogUI : MonoBehaviour
    {
        #region References
        [Header("References")]
        public GameObject sampleUi;
        public GameObject connectDialog;
        public GameObject disconnectDialog;
        public GameObject createRoomPanel;
        public GameObject noCloudPlaceholder;
        public GameObject noLobbiesAvailable;
        public GameObject loadingSpinner;
        public LobbySessionUI lobbySessionUI;
        public Text joinLobbyTitleText;
        public ConnectDialogLobbyView templateLobbyView;
        public InputField lobbyNameInputField;
        public InputField lobbyLimitInputField;
        public Dropdown regionDropdown;
        public Button refreshRegionsButton;
        public Button refreshLobbiesButton;
        public Button joinLobbyButton;
        public Button showCreateLobbyPanelButton;
        public Button hideCreateLobbyPanelButton;
        public Button createAndJoinLobbyButton;
        public Button disconnectButton;
        public GameObject popupDialog;
        public Text popupText;
        public Text popupTitleText;
        public Button popupDismissButton;
        public InputField nameText;
        public GameObject matchmakingRegionsContainer;
        public Toggle matchmakingRegionsTemplate;
        public Text matchmakingTag;
        public Button matchmakingButton;
        public GameObject matchmakingCreateRegionsContainer;
        public Toggle matchmakingCreateRegionsTemplate;
        public ToggleGroup matchmakingCreateRegionToggleGroup;
        #endregion

        private int PlayerLobbyLimit => int.TryParse(lobbyLimitInputField.text, out var limit) ? limit : 10;

        private string initialJoinLobbyTitle;
        private ListView lobbiesListView;
        private string lastCreatedLobbyId;
        private Coroutine cloudServiceReady;
        private CoherenceBridge bridge;
        private CoherenceCloudLogin cloudLogin;

        private IReadOnlyList<string> regionOptions = Array.Empty<string>();
        private string selectedRegion;

        private readonly List<Toggle> instantiatedRegionToggles = new();
        private readonly List<Toggle> instantiatedCreateRegionToggles = new();

        private PlayerAccount PlayerAccount => cloudLogin ? cloudLogin.PlayerAccount : null;
        private CloudRooms CloudRooms => PlayerAccount?.Services?.Rooms;
        private bool IsLoggedIn => PlayerAccount is { IsLoggedIn: true };

        #region Unity Events
        private void OnEnable()
        {
            var eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (eventSystems.Length == 0)
            {
                var eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                Debug.LogWarning("EventSystem not found on the scene. Adding one now.\nConsider creating an EventSystem yourself to forward UI input.", eventSystem);
            }

            if (!bridge && !CoherenceBridgeStore.TryGetBridge(gameObject.scene, out bridge))
            {
                Debug.LogError($"Couldn't find a {nameof(CoherenceBridge)} in your scene. This dialog will not function properly.", this);
                return;
            }

            bridge.onConnected.AddListener(OnBridgeConnected);
            bridge.onDisconnected.AddListener(OnBridgeDisconnected);
            bridge.onConnectionError.AddListener(OnConnectionError);

            UpdateDialogsVisibility();
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (!string.IsNullOrEmpty(RuntimeSettings.Instance.ProjectID))
            {
                cloudServiceReady = StartCoroutine(WaitForCloudService());
            }
            else if (regionDropdown.gameObject.activeInHierarchy)
            {
                noCloudPlaceholder.SetActive(true);
            }
        }

        private async void OnLogin(IReadOnlyList<string> lobbyIds)
        {
            if (lobbyIds?.FirstOrDefault() is { } lobbyId)
            {
                try
                {
                    var session = await CloudRooms.LobbyService.GetActiveLobbySessionForLobbyId(lobbyId);

                    OnJoinedLobby(new()
                    {
                        Status = RequestStatus.Success,
                        Exception = null,
                        Result = session
                    });
                }
                catch (Exception e)
                {
                    OnJoinedLobby(new()
                    {
                        Status = RequestStatus.Fail,
                        Exception = e,
                        Result = null
                    });
                }
            }
        }

        private void OnDisable()
        {
            if (bridge)
            {
                bridge.onConnected.RemoveListener(OnBridgeConnected);
                bridge.onDisconnected.RemoveListener(OnBridgeDisconnected);
                bridge.onConnectionError.RemoveListener(OnConnectionError);
            }

            if (cloudServiceReady != null)
            {
                StopCoroutine(cloudServiceReady);
                cloudServiceReady = null;
            }
        }

        void Start()
        {
            matchmakingRegionsTemplate.gameObject.SetActive(false);
            matchmakingCreateRegionsTemplate.gameObject.SetActive(false);
            nameText.text = Environment.UserName;
            joinLobbyButton.onClick.AddListener(() => JoinLobby(lobbiesListView.Selection.LobbyData));
            showCreateLobbyPanelButton.onClick.AddListener(ShowCreateRoomPanel);
            hideCreateLobbyPanelButton.onClick.AddListener(HideCreateRoomPanel);
            createAndJoinLobbyButton.onClick.AddListener(CreateLobbyAndJoin);
            regionDropdown.onValueChanged.AddListener(OnSelectedRegionChanged);
            refreshRegionsButton.onClick.AddListener(RefreshRegions);
            refreshLobbiesButton.onClick.AddListener(RefreshLobbies);
            disconnectButton.onClick.AddListener(bridge.Disconnect);
            popupDismissButton.onClick.AddListener(HideError);
            matchmakingButton.onClick.AddListener(MatchmakingLobby);

            popupDialog.SetActive(false);
            noLobbiesAvailable.SetActive(false);
            joinLobbyButton.interactable = false;
            showCreateLobbyPanelButton.interactable = false;
            templateLobbyView.gameObject.SetActive(false);
            lobbyNameInputField.text = "My Lobby";

            lobbiesListView = new()
            {
                Template = templateLobbyView,
                onSelectionChange = view =>
                {
                    joinLobbyButton.interactable = view != default && view.LobbyData.Id != default(LobbyData).Id;
                }
            };

            initialJoinLobbyTitle = joinLobbyTitleText.text;

            LogInToCoherenceCloud();
        }

        private void OnDestroy()
        {
            if (CloudRooms?.LobbyService is { } lobbyService)
            {
                lobbyService.OnPlaySessionStarted -= JoinRoom;
            }
        }

        #endregion

        #region Cloud & Replication Server Requests
        private void LogInToCoherenceCloud()
        {
            if (!cloudLogin && !TryGetComponent(out cloudLogin) && !(cloudLogin = FindAnyObjectByType<CoherenceCloudLogin>()))
            {
                cloudLogin = gameObject.AddComponent<CoherenceCloudLogin>();
            }

            cloudLogin.LogInAsync().OnSuccess(loginOperation =>
            {
                OnLogin(loginOperation.LobbyIds);
                RefreshLobbies();
            })
            .OnFail(error =>
            {
                var errorMessage = error.Type switch
                {
                    LoginErrorType.SchemaNotFound => "Logging in failed because local schema has not been uploaded to the Cloud.\n\nYou can upload local schema via <b>coherence > Upload Schema</b>.",
                    LoginErrorType.NoProjectSelected => "Logging in failed because no project was selected.\n\nYou can select a project via <b>coherence > Hub > Cloud</b>.",
                    LoginErrorType.ServerError => "Logging in failed because of a server error.",
                    LoginErrorType.InvalidCredentials => "Logging in failed because invalid credentials were provided.",
                    LoginErrorType.InvalidResponse => "Logging in failed because was unable to deserialize the response from the server.",
                    LoginErrorType.TooManyRequests => "Logging in failed because too many requests have been sent within a short amount of time.\n\nPlease slow down the rate of sending requests, and try again later.",
                    LoginErrorType.ConnectionError => "Logging in failed because of connection failure.",
                    LoginErrorType.AlreadyLoggedIn => $"The cloud services are already connected to a player account. You have to call {nameof(PlayerAccount)}.{nameof(Cloud.PlayerAccount.Logout)}. before attempting to log in again.",
                    LoginErrorType.ConcurrentConnection
                        => "We have received a concurrent connection for your Player Account. Your current credentials will be invalidated.\n\n" +
                        "Usually this happens when a concurrent connection is detected, e.g. running multiple game clients for the same player.\n\n" +
                        "When this happens the game should present a prompt to the player to inform them that there is another instance of the game running. " +
                        "The game should wait for player input and never try to reconnect on its own or else the two game clients would disconnect each other indefinitely.",
                    LoginErrorType.InvalidConfig => "Logging in failed because of invalid configuration in Online Dashboard." +
                                               "\nMake sure that the authentication method has been enabled and all required configuration has been provided in Project Settings." +
                                               "\nOnline Dashboard can found be found at: https://coherence.io/dashboard",
                    LoginErrorType.OneTimeCodeExpired => "Logging in failed because the provided ticket has expired.",
                    LoginErrorType.OneTimeCodeNotFound => "Logging in failed because no account has been linked to the authentication method in question. Pass an 'autoSignup' value of 'true' to automatically create a new account if one does not exist yet.",
                    LoginErrorType.IdentityLimit => "Logging in failed because identity limit has been reached.",
                    LoginErrorType.IdentityNotFound => "Logging in failed because provided identity not found",
                    LoginErrorType.IdentityTaken => "Logging in failed because the identity is already linked to another account. Pass a 'force' value of 'true' to automatically unlink the authentication method from the other player account.",
                    LoginErrorType.IdentityTotalLimit => "Logging in failed because maximum allowed number of identities has been reached.",
                    LoginErrorType.InvalidInput => "Logging in failed due to invalid input.",
                    LoginErrorType.PasswordNotSet => "Logging in failed because password has not been set for the player account.",
                    LoginErrorType.UsernameNotAvailable => "Logging in failed because the provided username is already taken by another player account.",
                    LoginErrorType.InternalException => "Logging in failed because of an internal exception.",
                    _ => error.Message,
                };

                ShowError("Logging in Failed", errorMessage);
                Debug.LogError(errorMessage, this);
            });

            CloudRooms.LobbyService.OnPlaySessionStarted += JoinRoom;
        }

        private IEnumerator WaitForCloudService()
        {
            ShowLoadingState();

            while (CloudRooms is not { IsLoggedIn : true })
            {
                yield return null;
            }

            HideLoadingState();

            RefreshRegions();
            cloudServiceReady = null;
        }

        // Join an active Game Session via a coherence Room
        private void JoinRoom(string lobbyId, RoomData room)
        {
            // The Lobby we were a part of has started a Game Session and we join the provided Room
            bridge.JoinRoom(room);
        }

        private void RefreshLobbies()
        {
            if (!IsLoggedIn)
            {
                return;
            }

            if (!regionOptions.Contains(selectedRegion))
            {
                if (regionOptions.Count is 0)
                {
                    return;
                }

                SetSelectedRegion(0);
            }

            ShowLoadingState();
            noLobbiesAvailable.SetActive(false);
            refreshLobbiesButton.interactable = false;

            var fetchLobbyFilter = new LobbyFilter()
                .WithRegion(FilterOperator.Any, new List<string> { selectedRegion });

            var lobbyFilters = new List<LobbyFilter> { fetchLobbyFilter };
            CloudRooms.LobbyService.FindLobbies(OnFetchLobbies, new() { LobbyFilters = lobbyFilters });
        }

        private void CreateLobby()
        {
            if (CloudRooms is not { IsLoggedIn : true })
            {
                return;
            }

            ShowLoadingState();

            var playerAttribute = GetPlayerNameAttribute();

            var createOptions = new CreateLobbyOptions()
            {
                Region = matchmakingCreateRegionToggleGroup.GetFirstActiveToggle().GetComponentInChildren<Text>().text.ToLowerInvariant(),
                MaxPlayers = PlayerLobbyLimit,
                Name = lobbyNameInputField.text,
                PlayerAttributes = playerAttribute
            };

            CloudRooms.LobbyService.CreateLobby(createOptions, OnCreatedLobby);
            HideCreateRoomPanel();
        }

        private List<CloudAttribute> GetPlayerNameAttribute()
        {
            var playerAttribute = new List<CloudAttribute> { new("player_name", nameText.text, true) };
            return playerAttribute;
        }

        private void JoinLobby(LobbyData lobbyData)
        {
            if (CloudRooms is not { IsLoggedIn : true })
            {
                return;
            }

            ShowLoadingState();

            var playerAttribute = GetPlayerNameAttribute();

            CloudRooms.LobbyService.JoinLobby(lobbyData, OnJoinedLobby, playerAttribute);
        }

        private void CreateLobbyAndJoin() => CreateLobby();

        private void RefreshRegions()
        {
            if (CloudRooms is { IsLoggedIn : true })
            {
                CloudRooms.RefreshRegions(OnRegionsChanged);
            }
        }

        private void MatchmakingLobby()
        {
            if (CloudRooms is not { IsLoggedIn : true })
            {
                return;
            }

            if (regionOptions.Count is 0)
            {
                return;
            }

            ShowLoadingState();

            var selectedRegions = new List<string>(regionOptions.Count);
            foreach (var regionToggle in instantiatedRegionToggles)
            {
                if (regionToggle.isOn)
                {
                    var addRegion = regionToggle.GetComponentInChildren<Text>().text.ToLowerInvariant();
                    selectedRegions.Add(addRegion);
                }
            }

            var filter = new LobbyFilter()
                .WithAnd()
                .WithRegion(FilterOperator.Any, selectedRegions)
                .WithTag(FilterOperator.Any, new() { matchmakingTag.text });

            var findOptions = new FindLobbyOptions { LobbyFilters = new() { filter } };
            var playerNameAttribute = GetPlayerNameAttribute();
            var region = matchmakingCreateRegionToggleGroup.GetFirstActiveToggle().GetComponentInChildren<Text>().text.ToLowerInvariant();

            var createOptions = new CreateLobbyOptions
            {
                Tag = matchmakingTag.text,
                Region = region,
                MaxPlayers = PlayerLobbyLimit,
                PlayerAttributes = playerNameAttribute
            };

            CloudRooms.LobbyService.FindOrCreateLobby(findOptions, createOptions, OnJoinedLobby);
            HideCreateRoomPanel();
        }

        #endregion

        #region Request Callbacks
        private void OnRegionsChanged(RequestResponse<IReadOnlyList<string>> requestResponse)
        {
            HideLoadingState();

            if (!AssertRequestResponse("Error while fetching room regions", requestResponse.Status, requestResponse.Exception))
            {
                return;
            }

            regionOptions = requestResponse.Result;
            if (regionOptions.Count is 0)
            {
                ShowError("No regions available", "There are no regions available for this project. Please enable some regions in the Project Settings of your Online Dashboard settings at https://coherence.io/dashboard/.");
                return;
            }

            regionDropdown.options = regionOptions.Select(region => new Dropdown.OptionData(region)).ToList();
            if (!regionOptions.Contains(selectedRegion))
            {
                SetSelectedRegion(0);
            }
        }

        private void OnFetchLobbies(RequestResponse<IReadOnlyList<LobbyData>> requestResponse)
        {
            var lobbies = requestResponse.Result;

            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(ReactivateRefreshButton());
            }
            else
            {
                refreshLobbiesButton.interactable = true;
            }

            loadingSpinner.SetActive(false);
            HideLoadingState();

            joinLobbyTitleText.text = initialJoinLobbyTitle + " (0)";
            noLobbiesAvailable.SetActive(requestResponse.Status != RequestStatus.Success || requestResponse.Result.Count == 0);

            if (!AssertRequestResponse("Error while fetching available lobbies", requestResponse.Status, requestResponse.Exception))
            {
                lobbiesListView.Clear();
                return;
            }

            if (lobbies.Count == 0)
            {
                lobbiesListView.Clear();
                return;
            }

            lobbiesListView.SetSource(lobbies, lastCreatedLobbyId);
            lastCreatedLobbyId = default; // selection was already set.
            joinLobbyTitleText.text = $"{initialJoinLobbyTitle} ({lobbies.Count})";

            joinLobbyButton.interactable = lobbiesListView.Selection != default;
        }

        private IEnumerator ReactivateRefreshButton()
        {
            while (CloudRooms.LobbyService.GetFindLobbiesCooldown() > TimeSpan.Zero)
            {
                yield return null;
            }

            refreshLobbiesButton.interactable = true;
        }

        private void OnCreatedLobby(RequestResponse<LobbySession> response)
        {
            HideLoadingState();

            if (!AssertRequestResponse("Error while creating and joining lobby", response.Status, response.Exception))
            {
                return;
            }

            ActivateLobbySessionUI(response);
        }

        private void ActivateLobbySessionUI(RequestResponse<LobbySession> response)
        {
            connectDialog.SetActive(false);
            lobbySessionUI.gameObject.SetActive(true);
            lobbySessionUI.Initialize(response.Result);
        }

        private void OnJoinedLobby(RequestResponse<LobbySession> response)
        {
            HideLoadingState();

            if (!AssertRequestResponse("Error while joining lobby", response.Status, response.Exception))
            {
                return;
            }

            ActivateLobbySessionUI(response);
        }
        #endregion

        #region Error Handling
        private void ShowError(string title, string message = "Unknown Error")
        {
            popupDialog.SetActive(true);
            popupTitleText.text = title;
            popupText.text = message;
            Debug.LogError(message, this);
        }

        private void HideError() => popupDialog.SetActive(false);

        private bool AssertRequestResponse(string title, RequestStatus status, Exception exception)
        {
            if (status == RequestStatus.Success)
            {
                return true;
            }

            var message = exception?.Message;

            if (exception is RequestException requestEx && requestEx.ErrorCode == ErrorCode.FeatureDisabled)
            {
                message += "\n\nMake sure Persisted Accounts is enabled in your coherence Dashboard.";
            }

            ShowError(title, message);

            return false;
        }

        private void OnConnectionError(CoherenceBridge bridge, ConnectionException exception)
        {
            HideLoadingState();
            RefreshLobbies();
            ShowError("Error connecting to Room", exception?.Message);
        }

        private void OnBridgeDisconnected(CoherenceBridge _, ConnectionCloseReason reason) => UpdateDialogsVisibility();
        private void OnBridgeConnected(CoherenceBridge _) => UpdateDialogsVisibility();
        #endregion

        #region Update UI
        private void ShowCreateRoomPanel()
        {
            createRoomPanel.SetActive(true);

            InstantiateRegionToggles(instantiatedRegionToggles, matchmakingRegionsTemplate, matchmakingRegionsContainer.transform);
            InstantiateRegionToggles(instantiatedCreateRegionToggles, matchmakingCreateRegionsTemplate, matchmakingCreateRegionsContainer.transform);
        }

        private void InstantiateRegionToggles(List<Toggle> instantiatedToggles, Toggle template, Transform parent)
        {
            foreach (var toggle in instantiatedToggles)
            {
                Destroy(toggle.gameObject);
            }

            instantiatedToggles.Clear();

            foreach (var region in regionDropdown.options)
            {
                var instantiatedToggle = Instantiate(template, parent);
                instantiatedToggle.gameObject.SetActive(true);
                instantiatedToggle.GetComponentInChildren<Text>().text = region.text.ToUpperInvariant();
                instantiatedToggles.Add(instantiatedToggle);
            }
        }

        private void HideCreateRoomPanel() => createRoomPanel.SetActive(false);

        private void UpdateDialogsVisibility()
        {
            var isConnected = bridge.IsConnected;
            sampleUi.SetActive(!isConnected);
            disconnectDialog.SetActive(isConnected);

            if (!isConnected)
            {
                RefreshLobbies();
            }
        }

        private void HideLoadingState()
        {
            loadingSpinner.SetActive(false);
            showCreateLobbyPanelButton.interactable = true;
            joinLobbyButton.interactable = lobbiesListView != null && lobbiesListView.Selection != default
                                                                && lobbiesListView.Selection.LobbyData.Id != default(LobbyData).Id;
        }

        private void ShowLoadingState()
        {
            loadingSpinner.SetActive(true);
            showCreateLobbyPanelButton.interactable = false;
            joinLobbyButton.interactable = false;
        }

        private void OnSelectedRegionChanged(int index)
        {
            if (CloudRooms is not { IsLoggedIn : true })
            {
                return;
            }

            SetSelectedRegion(index);
        }
        #endregion

        private void SetSelectedRegion(int index)
        {
            if (regionOptions.Count is 0)
            {
                regionDropdown.value = 0;
                selectedRegion = "";
                regionDropdown.captionText.text = "";
                return;
            }

            index = Mathf.Clamp(index, 0, regionOptions.Count - 1);
            regionDropdown.value = index;
            selectedRegion = regionOptions[index];
            RefreshLobbies();
        }
    }

    internal class ListView
    {
        public ConnectDialogLobbyView Template;
        public Action<ConnectDialogLobbyView> onSelectionChange;

        public ConnectDialogLobbyView Selection
        {
            get => selection;
            set
            {
                if (selection != value)
                {
                    selection = value;
                    lastSelectedId = selection == default ? default : selection.LobbyData.Id;
                    onSelectionChange?.Invoke(Selection);
                    foreach (var viewRow in Views)
                    {
                        viewRow.IsSelected = selection == viewRow;
                    }
                }
            }
        }

        public List<ConnectDialogLobbyView> Views { get; }
        private ConnectDialogLobbyView selection;
        private string lastSelectedId;

        public ListView(int capacity = 50) => Views = new List<ConnectDialogLobbyView>(capacity);

        public void SetSource(IReadOnlyList<LobbyData> dataSource, string idToSelect = default)
        {
            Clear();

            if (dataSource.Count <= 0)
            {
                return;
            }

            var sortedData = dataSource.ToList();
            sortedData.Sort((lobbyA, lobbyB) => String.CompareOrdinal(lobbyA.Name, lobbyA.Name));

            if (idToSelect == default && lastSelectedId != default)
            {
                idToSelect = lastSelectedId;
            }

            foreach (var data in sortedData)
            {
                var view = MakeViewItem(data);
                Views.Add(view);
                if (data.Id == idToSelect)
                {
                    Selection = view;
                }
            }
        }

        private ConnectDialogLobbyView MakeViewItem(LobbyData data, bool isSelected = false)
        {
            ConnectDialogLobbyView view = Object.Instantiate(Template, Template.transform.parent);
            view.LobbyData = data;
            view.IsSelected = isSelected;
            view.OnClick = () => Selection = view;
            view.gameObject.SetActive(true);
            return view;
        }

        public void Clear()
        {
            Selection = default;
            foreach (var view in Views)
            {
                Object.Destroy(view.gameObject);
            }
            Views.Clear();
        }
    }
}
