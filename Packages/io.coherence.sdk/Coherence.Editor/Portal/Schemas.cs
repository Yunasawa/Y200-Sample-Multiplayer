// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Log;
    using Newtonsoft.Json;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Networking;
    using Utils;
    using static SchemasUploadOperation;
    using Logger = Log.Logger;

    [Serializable]
    public class Schemas
    {
        public enum SyncState
        {
            Unknown,
            InProgress,
            InSync,
            OutOfSync,
        }

        public static event Action OnSchemaStateUpdate;

        private const string stateKey = "Coherence.Portal.SyncState";
        private const string initKey = "Coherence.Portal.InitializedSyncState";

        private static bool isUpdatingRemoteStates;
        private static SyncState syncState = SyncState.Unknown;
        private static string syncStateIsForSchemaId;
        private static string syncStateIsForProjectId;
        private static CancellationTokenSource updateStateCancellationTokenSource;
        internal static string localSchemaId;

        private static readonly Lazy<Logger> logger = new(() => Log.GetLogger(typeof(Schemas)));
        private static Logger Logger => logger.Value;

        /// <summary>
        /// Label representing the <see cref="SyncState"/> of the active project in single-project mode.
        /// </summary>
        private static readonly Dictionary<SyncState, GUIContent> stateContents = new()
        {
            { SyncState.Unknown, EditorGUIUtility.TrTextContent(
                text: ObjectNames.NicifyVariableName(SyncState.Unknown.ToString()),
                tooltip: "You need to log in and have a project selected, to be able to sync your local schema.") },
            { SyncState.InProgress, EditorGUIUtility.TrTextContent(
                text: ObjectNames.NicifyVariableName(SyncState.InProgress.ToString()),
                tooltip: string.Empty, "Warning") },
            { SyncState.OutOfSync, EditorGUIUtility.TrTextContent(
                text: ObjectNames.NicifyVariableName(SyncState.OutOfSync.ToString()),
                tooltip: "Your local schema hasn't been uploaded to your current project.", "Warning") },
            { SyncState.InSync, EditorGUIUtility.TrTextContent(
                text: ObjectNames.NicifyVariableName(SyncState.InSync.ToString())) },
        };

        /// <summary>
        /// Label representing the <see cref="SyncState"/> of a particular project in multi-project (Development/Release) mode.
        /// </summary>
        private static readonly Dictionary<SyncState, GUIContent> stateContentsMultipleProject = new()
        {
            { SyncState.Unknown, EditorGUIUtility.TrTextContent(
                text: ObjectNames.NicifyVariableName(SyncState.Unknown.ToString()),
                tooltip: "You need to log in and have a project selected, to be able to upload your local schema.") },
            { SyncState.InProgress, EditorGUIUtility.TrTextContent(
                text: ObjectNames.NicifyVariableName(SyncState.InProgress.ToString()),
                tooltip: string.Empty, "Warning") },
            { SyncState.OutOfSync, EditorGUIUtility.TrTextContent(
                text: ObjectNames.NicifyVariableName(SyncState.OutOfSync.ToString()),
                tooltip: "Your local schema hasn't been uploaded to this project.", "Warning") },
            { SyncState.InSync, EditorGUIUtility.TrTextContent(
                text: ObjectNames.NicifyVariableName(SyncState.InSync.ToString())) },
        };

        /// <summary>
        /// Identifiers of all schemas uploaded to the cloud for the active project.
        /// </summary>
        public static string[] RemoteSchemaIDs { get; private set; } = { };

        /// <summary>
        /// All schemas uploaded to the cloud for the active project.
        /// </summary>
        public static SchemaState[] RemoteSchemaStates { get; private set; } = { };

        /// <summary>
        /// All schemas uploaded to the cloud for different projects.
        /// </summary>
        private static readonly Dictionary<string, string> RemoteSchemaIdsByProjectId = new(2);

        public static SyncState state
        {
            get
            {
                if (isUpdatingRemoteStates)
                {
                    return SyncState.InProgress;
                }

                var activeProjectId = ProjectSettings.instance.GetActiveProjectId();
                if (!string.IsNullOrEmpty(syncStateIsForSchemaId)
                    && string.Equals(syncStateIsForSchemaId, GetLocalSchemaID())
                    && string.Equals(syncStateIsForProjectId, activeProjectId))
                {
                    return syncState;
                }

                if (LoadCachedStates() is not { } states)
                {
                    return state = SyncState.Unknown;
                }

                var setState = SyncState.InSync;
                foreach (var projectId in ProjectSettings.instance.ProjectIds)
                {
                    var projectState = states.States.FirstOrDefault(x => string.Equals(x.ProjectId, projectId));
                    if (projectState.IsUnknown)
                    {
                        setState = SyncState.Unknown;
                        continue;
                    }

                    if (!string.Equals(projectState.SchemaId, GetLocalSchemaID()))
                    {
                        setState = SyncState.OutOfSync;
                        break;
                    }
                }

                return state = setState;
            }

            internal set
            {
                if (value is SyncState.InProgress)
                {
                    isUpdatingRemoteStates = true;
                    return;
                }

                isUpdatingRemoteStates = false;
                var changed = false;
                if (!string.Equals(syncStateIsForSchemaId, GetLocalSchemaID()))
                {
                    syncStateIsForSchemaId = localSchemaId;
                    changed = true;
                }

                var activeProjectId = ProjectSettings.instance.GetActiveProjectId();
                if (!string.Equals(syncStateIsForProjectId, activeProjectId))
                {
                    syncStateIsForProjectId = activeProjectId;
                    changed = true;
                }

                if (syncState != value)
                {
                    syncState = value;
                    changed = true;
                }

                if (changed)
                {
                    EditorApplication.RepaintProjectWindow();
                    OnSchemaStateUpdate?.Invoke();
                }
            }
        }

        public static GUIContent StateContent => GetStateContent(state, false);
        internal static GUIContent GetStateContent(SyncState state, bool usingMultipleProjects = true) => (usingMultipleProjects ? stateContentsMultipleProject : stateContents)[state];

        public static void SetCachedSyncState(SyncState value) => state = value;

#pragma warning disable 649
        public Schema[] schemas;
        public string id;
#pragma warning restore 649

        static Schemas() => EditorApplication.delayCall += InitializeSyncState;

        private static void InitializeSyncState()
        {
            if (!SessionState.GetBool(initKey, false))
            {
                UpdateSyncState();
                SessionState.SetBool(initKey, true);
            }
        }

        internal Schemas(Schema[] schemas, string id)
        {
            this.schemas = schemas;
            this.id = id;
        }

        internal static SyncState GetState(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
            {
                return SyncState.Unknown;
            }

            if (isUpdatingRemoteStates)
            {
                return SyncState.InProgress;
            }

            GetLocalSchemaID();
            if (RemoteSchemaIdsByProjectId.TryGetValue(projectId, out var schemaId))
            {
                return schemaId is null ? SyncState.Unknown
                    : string.Equals(schemaId, localSchemaId) ? SyncState.InSync
                    : SyncState.OutOfSync;
            }

            if (LoadCachedStates() is not { } states)
            {
                RemoteSchemaIdsByProjectId[projectId] = null;
                return SyncState.Unknown;
            }

            var projectState = states.States.FirstOrDefault(x => string.Equals(x.ProjectId, projectId));
            if (projectState.IsUnknown)
            {
                RemoteSchemaIdsByProjectId[projectId] = null;
                return SyncState.Unknown;
            }

            if (!string.Equals(projectState.SchemaId, localSchemaId))
            {
                RemoteSchemaIdsByProjectId[projectId] = projectState.SchemaId;
                return SyncState.OutOfSync;
            }

            RemoteSchemaIdsByProjectId[projectId] = localSchemaId;
            return SyncState.InSync;
        }

        [return: MaybeNull]
        private static Schemas FromActiveSchemas()
        {
            var activeSchemas = ActiveSchemas();
            return activeSchemas is null ? null : new(activeSchemas, GetLocalSchemaID());
        }

        [return: MaybeNull]
        private static Schema[] ActiveSchemas()
        {
            var schemas = new List<Schema>();

            if (File.Exists(Paths.toolkitSchemaPath))
            {
                schemas.Add(Schema.GetFromString(File.ReadAllText(Paths.toolkitSchemaPath)));
            }

            if (File.Exists(Paths.gatherSchemaPath))
            {
                schemas.Add(Schema.GetFromString(File.ReadAllText(Paths.gatherSchemaPath)));
            }

            var activeSchemas = ProjectSettings.instance.activeSchemas.Select(Schema.GetFromSchemaAsset);
            schemas.AddRange(activeSchemas);

            if (!RuntimeSettings.TryGet(out var runtimeSettings))
            {
                return null;
            }

            if (runtimeSettings.extraSchemas is not null)
            {
                var extraSchemas = runtimeSettings.extraSchemas
                    .Where(asset => asset)
                    .Select(Schema.GetFromSchemaAsset);
                schemas.AddRange(extraSchemas);
            }

            return schemas.ToArray();
        }

        public static string GetLocalSchemaID()
        {
            if (!string.IsNullOrEmpty(localSchemaId))
            {
                return localSchemaId;
            }

            if (GetCombinedSchemaContents() is not { } combinedSchemaContents)
            {
                return "";
            }

            localSchemaId = HashCalc.SHA1Hash(combinedSchemaContents);
            return localSchemaId;
        }

        [return: MaybeNull]
        public static string GetCombinedSchemaContents()
        {
            var activeSchemas = ActiveSchemas();
            return activeSchemas is null ? null : string.Join("\n", activeSchemas.Select(s => s.Contents));
        }

        /// <summary>
        /// Uploads local schema to the currently active project in coherence Cloud.
        /// </summary>
        /// <remarks>
        /// You must have logged in to the coherence Cloud using the coherence Hub window,
        /// and selected organization and a project for the upload to succeed.
        /// </remarks>
        /// <param name="interactionMode">
        /// If set to <see cref="InteractionMode.UserAction"/>, the user will be prompted for confirmation before the schema is uploaded.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if uploading local schema completed successfully; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool UploadActive(InteractionMode interactionMode = InteractionMode.AutomatedAction, EditorWindow window = null)
        {
            var projectSettings = ProjectSettings.instance;
            var schemaIDShort = BakeUtil.SchemaIDShort;

            var selectableProjects = projectSettings.GetValidAndDistinctProjects().ToArray();
            Result result;
            if (!RuntimeSettings.InstanceUnsafe)
            {
                result = Result.Failed(FailReason.MissingRuntimeSettings);
            }
            else if (FromActiveSchemas() is not { } schemas)
            {
                result = Result.Failed(FailReason.MissingSchemaID);
            }
            else if (selectableProjects.Length <= 1 || interactionMode is InteractionMode.AutomatedAction)
            {
                var portalToken = projectSettings.PortalToken;
                var project = projectSettings.GetActiveProject() ?? new() { portal_token = portalToken };
                return Upload(project, interactionMode);
            }
            else
            {
                ProjectInfo[] selectedProjects;
                if (!Application.isBatchMode && interactionMode is InteractionMode.UserAction && selectableProjects.Length > 1)
                {
                    selectedProjects = ProjectSelectDialog.Open
                    (
                        "Upload Schema To Projects",
                        "Which projects should the local schema {0} be uploaded to?",
                        messageParams: $"({schemaIDShort})",
                        confirmButtonText: "Upload",
                        context: window
                    );
                    result = !selectedProjects.Any() ? Result.Failed(FailReason.AbortedByUser) : null;

                    // Don't prompt for any additional per-project confirmations further down the line.
                    interactionMode = InteractionMode.AutomatedAction;
                }
                else
                {
                    result = null;
                    selectedProjects = selectableProjects;
                }

                if (selectedProjects.Length > 0)
                {
                    Upload(selectedProjects, interactionMode);
                }
                else
                {
                    result ??= Result.Failed(FailReason.MissingProjectID);
                }
            }

            var failReason = result?.FailReason ?? FailReason.None;
            if (failReason is not FailReason.None)
            {
                if (failReason is FailReason.MissingRuntimeSettings or FailReason.MissingSchemaID && interactionMode is InteractionMode.UserAction)
                {
                    _ = EditorUtility.DisplayDialog("Can't Upload Schemas", "Either there's no codegen available, or you haven't set up a project.", "Ok");
                }
                else
                {
                    Logger.Info(GetUploadFailMessage(null, result));
                }
            }

            return false;
        }

        internal static bool Upload([DisallowNull] ProjectInfo[] uploadToProjects, InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            var schemas = FromActiveSchemas();
            var projectSettings = ProjectSettings.instance;

            bool? result = null;
            foreach (var project in uploadToProjects)
            {
                var portalToken = !string.IsNullOrEmpty(project.portal_token) ? project.portal_token : projectSettings.PortalToken;
                var uploadOperation = new SchemasUploadOperation
                (
                    schemas,
                    portalToken,
                    projectSettings.LoginToken,
                    projectSettings.OrganizationId,
                    project
                );

                var uploadResult = uploadOperation.Upload(interactionMode);
                if (uploadResult == Result.Success)
                {
                    result ??= true;
                    SetStateToInSync(project.id);
                }
                else
                {
                    result = false;
                    if (uploadResult.FailReason is FailReason.MissingRuntimeSettings or FailReason.MissingSchemaID && interactionMode == InteractionMode.UserAction)
                    {
                        _ = EditorUtility.DisplayDialog("Can't Upload Schemas", "Either there's no codegen available, or you haven't set up a project.", "Ok");
                        return false;
                    }

                    if (uploadResult.WebRequestError is not null)
                    {
                        Logger.Error(Error.EditorPortalSchemaUploadFail, GetUploadFailMessage(project, uploadResult));

                        if (!Application.isBatchMode && interactionMode == InteractionMode.UserAction)
                        {
                            _ = EditorUtility.DisplayDialog("Schemas upload", "Uploading schemas failed!", "OK");
                        }
                    }
                    else
                    {
                        Logger.Info(GetUploadFailMessage(project, uploadResult));
                    }
                }
            }

            if (result is not true)
            {
                return false;
            }

            LogUploadSuccess(uploadToProjects);

            if (uploadToProjects.Length == projectSettings.Projects.Count())
            {
                SetCachedStates(new(projectSettings.MultipleProjects.Select(x => new CachedState(x.Project.id, GetLocalSchemaID(), false)).ToArray()));
                state = SyncState.InSync;
            }
            else if (state is SyncState.InProgress)
            {
                state = SyncState.Unknown;
            }

            return true;
        }

        private static void LogUploadSuccess(params ProjectInfo[] targetedProjects) => Logger.Info(targetedProjects.Length > 1
            ? $"Uploaded schema {BakeUtil.SchemaIDShort} successfully to {(targetedProjects.Length is 2 ? "both" : "all")} projects:\n{string.Join("\n", targetedProjects.Select(x => $"- {x.name}"))}."
            : $"Uploaded schema {BakeUtil.SchemaIDShort} successfully to project '{targetedProjects.FirstOrDefault()?.name ?? ""}'.");

        private static string GetUploadFailMessage(ProjectInfo project, Result result) => result.FailReason switch
        {
            FailReason.MissingPortalAndLoginTokens => $"Attempting to upload Schema {BakeUtil.SchemaIDShort} but no Login Token or Portal Token has been found. Make sure you're logged in to the coherence Cloud in 'coherence > coherence Hub > coherence Cloud'.",
            FailReason.MissingOrganizationID => $"Attempting to upload Schema {BakeUtil.SchemaIDShort} but no Organization ID has been found. Make sure you've selected an Organization in 'coherence > coherence Hub > coherence Cloud > Account'.",
            FailReason.InvalidOrganizationID => $"Attempting to upload Schema {BakeUtil.SchemaIDShort} but selected Organization ID {ProjectSettings.instance.OrganizationId} is not recognized. Make sure you've selected a valid Project in 'coherence > coherence Hub > coherence Cloud > Account'.",
            FailReason.MissingProjectID => $"Attempting to upload Schema {BakeUtil.SchemaIDShort} but no Project ID has been found. Make sure you've selected a Project in 'coherence > coherence Hub > coherence Cloud > Account'.",
            FailReason.AbortedByUser => "Schema upload was aborted by the user.",
            FailReason.ProtocolError
                or FailReason.ConnectionError
                or FailReason.DataProcessingError
                or FailReason.RequestCreationFailed => $"Failed to upload Schema {BakeUtil.SchemaIDShort} to Project {GetNameOrId(project)}: {result.WebRequestError}.",
            FailReason.None
                or FailReason.MissingRuntimeSettings
                or FailReason.MissingSchemaID
                => throw new IndexOutOfRangeException($"Fail reason {result.FailReason} should have already been handled above."),
            _ => throw new IndexOutOfRangeException($"Fail reason {result.FailReason} unknown."),
        };

        private static string GetNameOrId(ProjectInfo project) => !string.IsNullOrEmpty(project.name) ? project.name : project.id;

        /// <summary>
        /// Uploads local schema to the specified project in coherence Cloud.
        /// </summary>
        /// <remarks>
        /// An organization must be selected in the coherence Hub for the upload to succeed.
        /// </remarks>
        /// <param name="projectId">
        /// <para>
        /// Identifier of the project into which local schema should be uploaded.
        /// </para>
        /// <para>
        /// You can find the Project ID using these steps:
        /// <list type="number">
        /// <item><description>
        /// Go to your Online Dashboard at https://coherence.io/dashboard.
        /// </description></item>
        /// <item><description>
        /// Select the project into which you want to upload the local schema.
        /// </description></item>
        /// <item><description>
        /// The Project ID will be displayed on the top right.
        /// </description></item>
        /// </list>
        /// </para>
        /// </param>
        /// <param name="projectToken">
        /// <para>
        /// Token for the project into which local schema should be uploaded.
        /// </para>
        /// <para>
        /// You can find the Project Token using these steps:
        /// <list type="number">
        /// <item><description>
        /// Go to your Online Dashboard at https://coherence.io/dashboard.
        /// </description></item>
        /// <item><description>
        /// Select the project into which you want to upload the local schema.
        /// </description></item>
        /// <item><description>
        /// Go to the 'Settings' page of the project and scroll down to the 'Project Tokens' section.
        /// </description></item>
        /// <item><description>
        /// Press the 'Copy' button next to the 'Project Token' field to copy it to your clipboard.
        /// </description></item>
        /// </list>
        /// </para>
        /// </param>
        /// <param name="interactionMode">
        /// If set to <see cref="InteractionMode.UserAction"/>, the user will be prompted for confirmation before the schema is uploaded.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if uploading local schema completed successfully; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool Upload(string projectId, string projectToken, InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            var project = new ProjectInfo { id = projectId, portal_token = projectToken };
            return Upload(project, interactionMode);
        }

        internal static bool Upload([DisallowNull] ProjectInfo project, InteractionMode interactionMode = InteractionMode.AutomatedAction)
            => Upload(new[] { project }, interactionMode);

        private static void SetStateToInSync(string projectId)
        {
            RemoteSchemaIdsByProjectId[projectId] = GetLocalSchemaID();
            var projectIds = ProjectSettings.instance.ProjectIds.ToArray();
            if (projectIds.All(x => GetState(x) == SyncState.InSync))
            {
                var cachedStates = new CachedStates(projectIds.Select(id => new CachedState(id, GetLocalSchemaID(), false)).ToArray());
                SetCachedStates(cachedStates);
                state = SyncState.InSync;
            }
        }

        /// <summary>
        /// Obsolete. Use <see cref="UploadActive"/> instead.
        /// </summary>
        public static bool UploadSchemas(bool warn = false) => UploadActive(warn ? InteractionMode.UserAction : InteractionMode.AutomatedAction);

        /// <summary>
        /// Obsolete. Use <see cref="UploadActive"/> instead.
        /// </summary>
        public static bool Upload() => UploadActive();

        public static Schemas Get()
        {
            // NOTE base64'd schema data is not retrieved
            var req = new PortalRequest(Endpoints.schemasPath, "GET");
            req.downloadHandler = new DownloadHandlerBuffer();
            _ = req.SendWebRequest();

            if (!Application.isBatchMode)
            {
                while (!req.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Portal", "Downloading schemas...",
                                                                   req.uploadProgress))
                    {
                        EditorUtility.ClearProgressBar();
                        req.Abort();
                        return null;
                    }
                }

                EditorUtility.ClearProgressBar();
            }

            switch (req.result)
            {
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Logger.Info($"Error getting schemas: {req.error}");
                    return null;
            }

            Logger.Info(req.downloadHandler.text);
            return JsonUtility.FromJson<Schemas>(req.downloadHandler.text);
        }

        private static UnityWebRequest GetAsyncRaw(Action<AsyncOperation> onCompleted = null, ProjectInfo project = null)
        {
            // NOTE base64'd schema data is not retrieved

            var req = new PortalRequest(Endpoints.schemasPath, method: "GET", project ?? ProjectSettings.instance.GetActiveProject());
            req.downloadHandler = new DownloadHandlerBuffer();

            req.SendWebRequest(onCompleted);

            return req;
        }

        public static void UpdateSyncState()
        {
            var updateTask = UpdateSyncStateAsync();
            if (Application.isBatchMode)
            {
                updateTask.Wait();
            }
        }

        public static async Task UpdateSyncStateAsync()
        {
            try
            {
                updateStateCancellationTokenSource?.Cancel();
                updateStateCancellationTokenSource = new();
                await UpdateSyncStatesAsync(updateStateCancellationTokenSource.Token);
            }
            catch
            {
                isUpdatingRemoteStates = false;
                state = SyncState.Unknown;
                throw;
            }

            static async Task UpdateSyncStatesAsync(CancellationToken cancellationToken)
            {
                state = SyncState.InProgress;
                RemoteSchemaIdsByProjectId.Clear();
                RemoteSchemaIDs = Array.Empty<string>();
                RemoteSchemaStates = Array.Empty<SchemaState>();

                var projects = ProjectSettings.instance.GetValidAndDistinctProjects().ToArray();
                var projectCount = projects.Length;
                if (projectCount is 0)
                {
                    SetCachedStates(new(Array.Empty<CachedState>()));
                    state = SyncState.Unknown;
                    return;
                }

                var activeProjectId = ProjectSettings.instance.GetActiveProjectId();
                var setSyncState = SyncState.InSync;
                var cachedStates = new CachedState[projectCount];

                for (var i = 0; i < projects.Length; i++)
                {
                    var project = projects[i];
                    var projectId = project.id;
                    var remoteSchemas = await GetRemoteSchemasAsync(project);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (remoteSchemas is null)
                    {
                        remoteSchemas = Array.Empty<SchemaState>();
                        if (setSyncState is not SyncState.OutOfSync)
                        {
                            setSyncState = SyncState.Unknown;
                        }

                        cachedStates[i] = new(projectId, "", true);
                    }
                    else if (remoteSchemas.Length is 0)
                    {
                        setSyncState = SyncState.OutOfSync;
                        cachedStates[i] = new(projectId, "", false);
                    }
                    else
                    {
                        var localSchemaID = GetLocalSchemaID();
                        if (remoteSchemas.Any(s => string.Equals(s.Id, localSchemaID)))
                        {
                            cachedStates[i] = new(projectId, localSchemaID, false);
                        }
                        else
                        {
                            setSyncState = SyncState.OutOfSync;
                            cachedStates[i] = new(projectId, remoteSchemas.OrderByDescending(x => x.Timestamp).First().Id, false);
                        }
                    }

                    if (string.Equals(projectId, activeProjectId))
                    {
                        RemoteSchemaStates = remoteSchemas;
                        RemoteSchemaIDs = remoteSchemas.Select(s => s.Id).ToArray();
                    }
                }

                SetCachedStates(new(cachedStates));
                state = setSyncState;
            }
        }

        private static void SetCachedStates(CachedStates value)
        {
            foreach (var state in value.States)
            {
                RemoteSchemaIdsByProjectId[state.ProjectId] = state.IsUnknown ? null : state.SchemaId;
            }

            UserSettings.SetString(stateKey, JsonUtility.ToJson(value));
        }

        private static CachedStates? LoadCachedStates()
        {
            var json = UserSettings.GetString(stateKey, "");
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                var states = JsonUtility.FromJson<CachedStates>(json);
                states.States ??= Array.Empty<CachedState>();
                return states;
            }
            catch (ArgumentException) // In versions 1.7.0 and before sync state was stored using just a single integer
            {
                if (!int.TryParse(json, out var integer) || ProjectSettings.instance.UsingMultipleProjects)
                {
                    return null;
                }

                var projectId = ProjectSettings.instance.GetActiveProjectId();
                switch ((SyncState)integer)
                {
                    case SyncState.Unknown:
                        return new CachedStates(new CachedState(projectId, "", true));
                    case SyncState.InSync:
                        return new CachedStates(new CachedState(projectId, GetLocalSchemaID(), false));
                    case SyncState.OutOfSync:
                        return new CachedStates(new CachedState(projectId, "", false));
                }

                return new CachedStates(Array.Empty<CachedState>());
            }
        }

        private static Task<SchemaState[]> GetRemoteSchemasAsync(ProjectInfo project)
        {
            if (!PortalUtil.CanCommunicateWithPortal || string.IsNullOrEmpty(project?.id))
            {
                return Task.FromResult<SchemaState[]>(null);
            }

            var taskCompletionSource = new TaskCompletionSource<SchemaState[]>();
            GetAsyncRaw(OnCompleted, project: project);
            return taskCompletionSource.Task;

            void OnCompleted(AsyncOperation asyncOperation)
            {
                var webRequest = ((UnityWebRequestAsyncOperation)asyncOperation).webRequest;
                switch (webRequest.result)
                {
                    case UnityWebRequest.Result.ProtocolError:
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        Logger.Info(webRequest.error);
                        taskCompletionSource.SetResult(null);
                        return;
                }

                var json = webRequest.downloadHandler.text;
                taskCompletionSource.SetResult(CoherenceJson.DeserializeObject<SchemaState[]>(json));
            }
        }

        internal static void InvalidateSchemaCache() => localSchemaId = null;

        internal static SyncState GetSyncStateForSchemaId(string schemaId)
            => RemoteSchemaIDs.Length is 0 ? SyncState.Unknown
            : RemoteSchemaIDs.All(s => s == schemaId)
                ? SyncState.InSync
                : SyncState.OutOfSync;

#pragma warning disable 649

        public struct SchemaState
        {
            [JsonProperty("schema_id")]
            public string Id;

            [JsonProperty("timestamp")]
            public string Timestamp;

            public override string ToString()
            {
                return $"{nameof(SchemaState.Id)}: {Id}," +
                       $"{nameof(SchemaState.Timestamp)}: {Timestamp}";
            }
        }
#pragma warning restore 649

        [Serializable]
        private struct CachedStates
        {
            public CachedState[] States;
            public CachedStates(params CachedState[] states) => States = states ?? Array.Empty<CachedState>();
        }

        [Serializable]
        private struct CachedState
        {
            public string ProjectId;
            public string SchemaId;
            public bool IsUnknown;

            public CachedState(string projectId, string schemaId, bool isUnknown)
            {
                ProjectId = projectId;
                SchemaId = schemaId;
                IsUnknown = isUnknown;
            }
        }
    }
}
