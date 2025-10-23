namespace Coherence.Toolkit
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cloud;
    using Common;
    using Log;
    using Runtime;
    using UnityEngine;
    using UnityEngine.Events;
    using static Utils.FlagsValues;
    using Logger = Log.Logger;

    /// <summary>
    /// A component that can be used to log in to <see cref="CoherenceCloud">coherence Cloud</see>.
    /// </summary>
    [AddComponentMenu("coherence/Coherence Cloud Login")]
    [NonBindable]
    [DefaultExecutionOrder(ScriptExecutionOrder.CoherenceCloudLogin)]
    [HelpURL("https://docs.coherence.io/v/1.8/manual/components/coherence-cloud-login")]
    public partial class CoherenceCloudLogin : CoherenceBehaviour, IPlayerAccountProvider
    {
        internal const int MaxRetries = 3;

        /// <summary>
        /// Event that is raised when this component has successfully logged in to coherence Cloud.
        /// </summary>
        public event UnityAction<LoginOperation> OnLoggedIn
        {
            add
            {
                if (playerAccount?.IsLoggedIn ?? false)
                {
                    value?.Invoke(new(Task.FromResult(playerAccount)));
                    return;
                }

                onLoggedIn.AddListener(value);
            }

            remove => onLoggedIn.RemoveListener(value);
        }

        /// <summary>
        /// Event that is raised when this component has failed to log in to coherence Cloud.
        /// </summary>
        public event UnityAction<LoginOperationError> OnLoginFailed
        {
            add
            {
                if (error is not null)
                {
                    value?.Invoke(error);
                    return;
                }

                onLoginFailed.AddListener(value);
            }

            remove => onLoginFailed.RemoveListener(value);
        }

        [SerializeField] private Login login = Login.AsGuest;
        [SerializeReference] private ITextInput usernameCloudUniqueIdOrIdentity = new StringTextInput();
        [SerializeReference] private ITextInput passwordTokenTicketOrCode = new StringTextInput();
        [SerializeField] private bool autoSignup = true;

        [SerializeField, Tooltip("Automatically log in to coherence Cloud when the component is loaded?\n\n" +
                                 "If set to 'False', then 'LogInAsync' can be called to start the login process manually.")]
        private bool logInOnLoad = true;

        [SerializeField, Tooltip("Log in Simulators to coherence Cloud?\n\n" +
                                 "If set to 'True', then simulators will be logged in using a simulator session token that is provided as a command line argument when 'Log In On Load' is enabled or 'LogInAsync' is called.\n\n" +
                                 "If set to 'False', then enabling 'Log In On Load' or calling 'LogInAsync' will have no effect in simulator builds.")]
        private bool logInSimulators = true;

        [Space]
        [SerializeField, Tooltip("Event that is raised when this player successfully logs in to coherence Cloud.")]
        private UnityEvent<LoginOperation> onLoggedIn = new();

        [SerializeField, Tooltip("Event that is raised when this player has failed to log in to coherence Cloud.")]
        private UnityEvent<LoginOperationError> onLoginFailed = new();

        [MaybeNull] private PlayerAccount playerAccount;
        [MaybeNull] private CloudService services;
        [MaybeNull] private Logger logger;
        private int failedLoginAttempts;
        private CancellationTokenSource loginCancellationTokenSource;
        private LoginOperationError error;
        private LoginOperation operation;
        [MaybeNull] private Validator validator;

        /// <summary>
        /// Automatically create a new Player Account with the given username and password if one does not exist already?
        /// </summary>
        /// <remarks>
        /// Ignored unless 'Login' has been set to 'With Password'.
        /// </remarks>
        public bool AutoSignup
        {
            get => autoSignup;
            set => autoSignup = value;
        }

        /// <summary>
        /// Username used to <see cref="CoherenceCloud.LoginWithPassword">log in to coherence Cloud</see>.
        /// </summary>
        public string Username
        {
            [return: MaybeNull] get => login is Login.WithPassword ? usernameCloudUniqueIdOrIdentity?.Text : null;

            set
            {
                usernameCloudUniqueIdOrIdentity ??= new StringTextInput();
                usernameCloudUniqueIdOrIdentity.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithPassword;
                }
            }
        }

        /// <summary>
        /// Password used to <see cref="CoherenceCloud.LoginWithPassword">log in to coherence Cloud</see>.
        /// </summary>
        public string Password
        {
            [return: MaybeNull] get => login is Login.WithPassword ? passwordTokenTicketOrCode?.Text : null;

            set
            {
                passwordTokenTicketOrCode ??= new StringTextInput();
                passwordTokenTicketOrCode.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithPassword;
                }
            }
        }

        /// <summary>
        /// PlayStation Network account token used to <see cref="CoherenceCloud.LoginWithPlayStation">log in to coherence Cloud</see>.
        /// </summary>
        public string PlayStationToken
        {
            [return: MaybeNull] get => login is Login.WithPlayStation ? passwordTokenTicketOrCode?.Text : null;

            set
            {
                passwordTokenTicketOrCode ??= new StringTextInput();
                passwordTokenTicketOrCode.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithPlayStation;
                }
            }
        }

        /// <summary>
        /// Xbox Live token used to <see cref="CoherenceCloud.LoginWithXbox">log in to coherence Cloud</see>.
        /// </summary>
        public string XboxToken
        {
            [return: MaybeNull] get => login is Login.WithXbox ? passwordTokenTicketOrCode?.Text : null;

            set
            {
                passwordTokenTicketOrCode ??= new StringTextInput();
                passwordTokenTicketOrCode.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithXbox;
                }
            }
        }

        /// <summary>
        /// Nintendo Account ID as a JSON Web Token to <see cref="CoherenceCloud.LoginWithNintendo">log in to coherence Cloud</see>.
        /// </summary>
        public string NintendoToken
        {
            [return: MaybeNull] get => login is Login.WithNintendo ? passwordTokenTicketOrCode?.Text : null;

            set
            {
                passwordTokenTicketOrCode ??= new StringTextInput();
                passwordTokenTicketOrCode.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithNintendo;
                }
            }
        }

        /// <summary>
        /// Epic Online Services authentication token used to <see cref="CoherenceCloud.LoginWithEpicGames">log in to coherence Cloud</see>.
        /// </summary>
        public string EpicGamesToken
        {
            [return: MaybeNull] get => login is Login.WithEpicGames ? passwordTokenTicketOrCode?.Text : null;

            set
            {
                passwordTokenTicketOrCode ??= new StringTextInput();
                passwordTokenTicketOrCode.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithEpicGames;
                }
            }
        }

        /// <summary>
        /// JSON Web Token used to <see cref="CoherenceCloud.LoginWithJwt">log in to coherence Cloud</see>.
        /// </summary>
        public string JwtToken
        {
            [return: MaybeNull] get => login is Login.WithJwt ? passwordTokenTicketOrCode?.Text : null;

            set
            {
                passwordTokenTicketOrCode ??= new StringTextInput();
                passwordTokenTicketOrCode.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithJwt;
                }
            }
        }

        /// <summary>
        /// Steam ticket used to <see cref="CoherenceCloud.LoginWithSteam">log in to coherence Cloud</see>.
        /// </summary>
        public string SteamTicket
        {
            [return: MaybeNull] get => login is Login.WithSteam ? passwordTokenTicketOrCode?.Text : null;

            set
            {
                passwordTokenTicketOrCode ??= new StringTextInput();
                passwordTokenTicketOrCode.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithSteam;
                }
            }
        }

        /// <summary>
        /// (Optional) Steam Identity used to <see cref="CoherenceCloud.LoginWithSteam">log in to coherence Cloud</see>.
        /// </summary>
        public string SteamIdentity
        {
            [return: MaybeNull] get => login is Login.WithSteam ? usernameCloudUniqueIdOrIdentity?.Text : null;

            set
            {
                usernameCloudUniqueIdOrIdentity ??= new StringTextInput();
                usernameCloudUniqueIdOrIdentity.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithSteam;
                }
            }
        }

        /// <summary>
        /// One-time code used to <see cref="CoherenceCloud.LoginWithOneTimeCode">log in to coherence Cloud</see>.
        /// </summary>
        public string OneTimeCode
        {
            [return: MaybeNull] get => login is Login.WithOneTimeCode ? passwordTokenTicketOrCode?.Text : null;

            set
            {
                passwordTokenTicketOrCode ??= new StringTextInput();
                passwordTokenTicketOrCode.Text = value ?? "";
                if (value is { Length : > 0 })
                {
                    login = Login.WithOneTimeCode;
                }
            }
        }

        /// <summary>
        /// (Optional) A <see cref="CloudUniqueId">locally unique identifier for a guest account</see> used to
        /// <see cref="CoherenceCloud.LoginAsGuest(LoginAsGuestOptions, CancellationToken)">log in to coherence Cloud</see>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Cloud Unique Ids can be used to create and log into multiple different guest player accounts on the same device.
        /// This might be useful for local multiplayer games, allowing each player to log into their own guest player account.
        /// </para>
        /// </remarks>
        public string CloudUniqueId
        {
            [return: MaybeNull] get => UseSharedCloudCredentials
                ? SimulatorPlayerAccountProvider.SimulatorInCloudUniqueId
                : login is Login.AsGuest ? usernameCloudUniqueIdOrIdentity?.Text : null;

            set
            {
                usernameCloudUniqueIdOrIdentity ??= new StringTextInput();
                usernameCloudUniqueIdOrIdentity.Text = value ?? "";
                login = Login.AsGuest;
            }
        }

        /// <summary>
        /// If logging in has completed, contains information about the login operation; otherwise contains <see langword="null"/>.
        /// </summary>
        [MaybeNull]
        public LoginOperation Operation => operation;

        /// <summary>
        /// If logging in has failed, contains information about the error that occurred; otherwise contains <see langword="null"/>.
        /// </summary>
        [MaybeNull]
        public LoginOperationError Error => error;

        /// <summary>
        /// Coherence cloud services for the <see cref="PlayerAccount"/>.
        /// </summary>
        /// <remarks>
        /// The cloud services will only become ready to be used once the component has successfully
        /// finished logging in, and <see cref="CloudService.IsLoggedIn"/> has become <see langword="true"/>.
        /// </remarks>
        public CloudService Services
        {
            get => services ??= (Application.isPlaying ? CreateServices() : null);
            internal set => services = value;
        }

        /// <inheritdoc cref="PlayerAccount.IsLoggedIn"/>
        public bool IsLoggedIn => PlayerAccount?.IsLoggedIn ?? false;

        internal bool LogsInAsSimulator => IsSimulator && logInSimulators;

        private string ProjectId => RuntimeSettings.Instance.ProjectID ?? "";
        internal bool IsSimulator => SimulatorUtility.IsSimulator;
        internal bool LogInSimulators => SimulatorUtility.IsSimulator;
        private bool UseSharedCloudCredentials => SimulatorUtility.UseSharedCloudCredentials;

        /// <summary>
        /// The <seealso cref="Coherence.Cloud.PlayerAccount"/> used to log in to coherence Cloud.
        /// </summary>
        [MaybeNull]
        public PlayerAccount PlayerAccount => playerAccount;

        private Logger Logger => logger ??= Log.GetLogger<CoherenceCloudLogin>(this);

        [NotNull]
        string IPlayerAccountProvider.ProjectId => ProjectId;

        CloudUniqueId IPlayerAccountProvider.CloudUniqueId => CloudUniqueId;

        // for components, we don't expose direct creation of instances - add as component instead
        private CoherenceCloudLogin() { }

        private async void Awake()
        {
            if (logInOnLoad)
            {
                await LogInAsync();
            }
        }

        [return: NotNull]
        private PlayerAccount GetOrCreatePlayerAccount(LoginInfo loginInfo)
        {
            if (playerAccount is not null)
            {
                return playerAccount;
            }

            playerAccount = new(loginInfo, CloudUniqueId, ProjectId, services ??= CreateServices());
            playerAccount.OnDisposed += OnPlayerAccountDisposed;
            PlayerAccount.Register(playerAccount);
            return playerAccount;
        }

        private LoginInfo GetLoginInfo() => login switch
        {
            _ when IsSimulator => LoginInfo.ForSimulator(Services.PlayerAccountProvider, preferLegacyLoginData: true),
            Login.AsGuest => LoginInfo.ForGuest(this, preferLegacyLoginData: true),
            Login.WithPassword => LoginInfo.WithPassword(Username, Password, autoSignup),
            Login.WithSteam => LoginInfo.WithSteam(SteamTicket, SteamIdentity),
            Login.WithEpicGames => LoginInfo.WithEpicGames(EpicGamesToken),
            Login.WithPlayStation => LoginInfo.WithPlayStation(PlayStationToken),
            Login.WithXbox => LoginInfo.WithXbox(XboxToken),
            Login.WithNintendo => LoginInfo.WithNintendo(NintendoToken),
            Login.WithOneTimeCode => LoginInfo.WithOneTimeCode(OneTimeCode),
            Login.WithJwt => LoginInfo.WithJwt(JwtToken),
            _ => throw new ArgumentOutOfRangeException(nameof(login), login, "Unsupported login type.")
        };

        [ContextMenu("Log In")]
        private void LogInFromContextMenu() => LogInAsync()
            .OnSuccess(operation => Debug.Log($"Logged in as {operation.Result}", this))
            .OnFail(error => Debug.LogError(error, this));

        /// <summary>
        /// Logs in to coherence Cloud using the configured credentials.
        /// </summary>
        public LoginOperation LogInAsync(CancellationToken cancellationToken = default)
        {
            var loginInfo = GetLoginInfo();

            if (playerAccount is not null)
            {
                if (playerAccount.LoginInfos.Contains(loginInfo))
                {
                    return operation;
                }

                playerAccount.OnDisposed -= OnPlayerAccountDisposed;
                playerAccount.Dispose();
                playerAccount = null;
            }

            if (IsSimulator && !logInSimulators)
            {
                return new(Task.FromCanceled<PlayerAccount>(new(true)));
            }

            var taskCompletionSource = new TaskCompletionSource<PlayerAccount>();
            operation = new(taskCompletionSource.Task);

            loginCancellationTokenSource?.Dispose();
            loginCancellationTokenSource = null;
            if (!cancellationToken.CanBeCanceled)
            {
                cancellationToken = (loginCancellationTokenSource = new()).Token;
            }

            failedLoginAttempts = 0;
            Services.AuthClient.Login(loginInfo, cancellationToken).Then(OnLoginAttemptCompleted());
            playerAccount ??= Services.AuthClient.PlayerAccount;
            return operation;

            Action<Task<LoginResult>> OnLoginAttemptCompleted() => task =>
            {
                if (cancellationToken.IsCancellationRequested || task.IsCanceled)
                {
                    taskCompletionSource.TrySetCanceled();
                    onLoginFailed.Invoke(operation.Error);
                    return;
                }

                if (task.IsFaulted)
                {
                    taskCompletionSource.TrySetException(task.Exception);
                    Logger.Warning(Warning.RuntimeCloudLoginFailedMsg, task.Exception.ToString());
                    this.error = operation.Error;
                    onLoginFailed.Invoke(operation.Error);
                    return;
                }

                var result = task.Result;
                if (result.Type is Result.Success)
                {
                    playerAccount.LoginResult = result;
                    playerAccount.shouldDisposeCloudService = !UseSharedCloudCredentials;
                    taskCompletionSource.TrySetResult(playerAccount);
                    onLoggedIn.Invoke(operation);
                    return;
                }

                if (result.Type is Result.TooManyRequests && ++failedLoginAttempts <= MaxRetries)
                {
                    Logger.Debug($"Login {login} failed because of too many requests. Retrying...");
                    Services.AuthClient.Login(loginInfo, cancellationToken).Then(OnLoginAttemptCompleted());
                    return;
                }

                var error = new LoginError(result.ErrorType, result.LoginErrorType, result.Error, result.ErrorMessage);
                taskCompletionSource.TrySetException(error);
                Logger.Warning(Warning.RuntimeCloudLoginFailedMsg, error.ToString());
                this.error = operation.Error;
                onLoginFailed.Invoke(operation.Error);
            };
        }

        /// <summary>
        /// Logs the Player Account associated with this component out from coherence Cloud.
        /// <remarks>
        /// If this component is not logged in, this method does nothing.
        /// </remarks>
        /// </summary>
        [ContextMenu("Log Out")]
        public void LogOut() => playerAccount?.Logout();

        public CredentialsIssue Validate() => (validator ??= new()).Validate(this);

        private void OnApplicationQuit() => ((IDisposable)this).Dispose();

        void IDisposable.Dispose()
        {
            if (playerAccount is not null)
            {
                playerAccount.OnDisposed -= OnPlayerAccountDisposed;
                playerAccount.Dispose();
                playerAccount = null;
            }

            if (services is not null)
            {
                services.Dispose();
                services = null;
            }

            DisposeShared();
        }

        internal async ValueTask DisposeAsync(bool waitForOngoingCloudOperationsToFinish)
        {
            if (playerAccount is { } playerAccountToDispose)
            {
                playerAccount = null;
                playerAccountToDispose.OnDisposed -= OnPlayerAccountDisposed;
                await playerAccountToDispose.DisposeAsync(waitForOngoingCloudOperationsToFinish);
            }

            if (services is not null)
            {
                await services.DisposeAsync(waitForOngoingCloudOperationsToFinish);
                services = null;
            }

            DisposeShared();
        }

        private void DisposeShared()
        {
            if (logger is not null)
            {
                logger.Dispose();
                logger = null;
            }
        }

        private async void OnDestroy() => await OnDestroyAsync(true);

        internal ValueTask OnDestroyAsync(bool waitForOngoingCloudOperationsToFinish)
        {
            loginCancellationTokenSource?.Cancel();
            return DisposeAsync(waitForOngoingCloudOperationsToFinish);
        }

        PlayerAccount IPlayerAccountProvider.GetPlayerAccount(LoginInfo loginInfo) => GetOrCreatePlayerAccount(loginInfo);

        /// <summary>
        /// Factory method for tests.
        /// </summary>
        internal static CoherenceCloudLogin Create(string username, string password, bool autoSignup = false, bool logInOnLoad = false)
            => Create(Login.WithPassword, username, password, autoSignup, logInOnLoad);

        /// <summary>
        /// Factory method for tests.
        /// </summary>
        internal static CoherenceCloudLogin Create(Login login, string tokenTicketOrCode = "", bool logInOnLoad = false) => Create(login, "", tokenTicketOrCode, autoSignup: false, logInOnLoad: logInOnLoad);

        /// <summary>
        /// Factory method for tests.
        /// </summary>
        private static CoherenceCloudLogin Create(Login login, string usernameCloudUniqueIdOrIdentity, string passwordTokenTicketOrCode, bool autoSignup, bool logInOnLoad)
        {
            var gameObject = new GameObject(nameof(CoherenceCloudLogin));
            gameObject.SetActive(false);
            var cloudLogin = gameObject.AddComponent<CoherenceCloudLogin>();
            cloudLogin.login = login;
            cloudLogin.usernameCloudUniqueIdOrIdentity = new StringTextInput { Text = usernameCloudUniqueIdOrIdentity};
            cloudLogin.passwordTokenTicketOrCode = new StringTextInput { Text = passwordTokenTicketOrCode };
            cloudLogin.autoSignup = autoSignup;
            cloudLogin.logInOnLoad = logInOnLoad;
            gameObject.SetActive(true);
            return cloudLogin;
        }

        private CloudService CreateServices() => UseSharedCloudCredentials ? CloudService.ForSimulator() : CloudService.ForClient(this, autoLoginAsGuest: false);

        private void OnPlayerAccountDisposed() => playerAccount = null;

        internal enum Login
        {
            [Tooltip("Log in to coherence Cloud as a guest.\n\n" +
                     "A new guest id is generated automatically when a player logs in to the project for the first. " +
                     "The guest id is then cached locally on the player's device and used to log them in again to the same guest account later on.")]
            AsGuest = _1,

            [Tooltip("Login to coherence Cloud using a username and password.")]
            WithPassword = _2,

            [InspectorName("With One-Time Code")]
            [Tooltip("Login to coherence Cloud using a one-time code.\n\n" +
                     "A temporary one-time code can be acquired using PlayerAccount.GetOneTimeCode.\n\n" +
                     "There are two use cases for one-time codes:\n" +
                     "transfer progress from one platform to another, and\n" +
                     "recover access to a lost account.\n\n")]
            WithOneTimeCode = _3,

            [InspectorName("With JWT")]
            [Tooltip("Login to coherence Cloud using a custom JSON Web Token (JWT).")]
            WithJwt = _4,

            [Tooltip("Login to coherence Cloud using a Steam account.\n\n" +
                     "The Steam ticket can be acquired using the Steamworks API.")]
            WithSteam = _5,

            [Tooltip("Login to coherence Cloud using an Epic Games account.\n\n" +
                     "The Epic Online Services authentication token can be acquired using the Epic Online Services API.")]
            WithEpicGames = _6,

            [InspectorName("With PlayStation")]
            [Tooltip("Login to coherence Cloud using a PlayStation Network account.\n\n" +
                     "The PlayStation Network account token can be acquired using the PlayStation Network API.")]
            WithPlayStation = _7,

            [Tooltip("Login to coherence Cloud using an Xbox profile.\n\n" +
                     "The Xbox Live token can be acquired using the Xbox Live API.")]
            WithXbox = _8,

            [Tooltip("Login to coherence Cloud using a Nintendo account.\n\n" +
                     "The Nintendo Account ID can be acquired using the Nintendo API.")]
            WithNintendo = _9
        }

#if UNITY_EDITOR
        /// <summary>
        /// Contains names of serialized properties that can be used in the editor with SerializedObject.FindProperty etc.
        /// </summary>
        internal static class Property
        {
            public const string login = nameof(CoherenceCloudLogin.login);
            public const string logInOnLoad = nameof(CoherenceCloudLogin.logInOnLoad);
            public const string logInSimulators = nameof(CoherenceCloudLogin.logInSimulators);

            public const string cloudUniqueId = nameof(usernameCloudUniqueIdOrIdentity);
            public const string username = nameof(usernameCloudUniqueIdOrIdentity);
            public const string password = nameof(passwordTokenTicketOrCode);
            public const string autoSignup = nameof(CoherenceCloudLogin.autoSignup);
            public const string ticket = nameof(passwordTokenTicketOrCode);
            public const string identity = nameof(usernameCloudUniqueIdOrIdentity);
            public const string token = nameof(passwordTokenTicketOrCode);
            public const string code = nameof(passwordTokenTicketOrCode);
        }
        #endif
    }
}
