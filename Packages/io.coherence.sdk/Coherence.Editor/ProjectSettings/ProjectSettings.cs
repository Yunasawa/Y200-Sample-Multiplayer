// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using UnityEditor;
    using UnityEngine;
    using System.Linq;
    using System.IO;
    using System.Collections.Generic;
    using Toolkit;
    using Common;
    using UnityEngine.Serialization;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Build;
    using Log;
    using Portal;
#if UNITY_2023_1_OR_NEWER
    using UnityEditor.Build;
#endif

    [FilePath(Paths.projectSettingsPath, FilePathAttribute.Location.ProjectFolder)]
    public class ProjectSettings : ScriptableSingleton<ProjectSettings>
    {
        private string cachedToken;
        private const string hashKey = "Coherence.Settings.ActiveSchemas.Hash";
        private const string encryptedLoginTokenKey = "Coherence.Settings.UserLoginEncryptedToken";
        private const string userIdKey = "Coherence.Settings.UserID";
        private const string emailKey = "Coherence.Settings.Email";
        private const string customToolsPathKey = "Coherence.Settings.CustomToolsPath";
        private const string useCustomToolsKey = "Coherence.Settings.UseCustomTools";
        private const string useCustomEndpointsKey = "Coherence.Settings.UseCustomEndpoints";
        private const string customAPIDomainKey = "Coherence.Settings.CustomAPIDomain";
        private const string projectNameKey = "Coherence.Settings.ProjectName";
        private const string useCustomToolsValue = "use";
        private const string useCustomEndpointsValue = "yes";

        public string PortalToken => Environment.GetEnvironmentVariable("COHERENCE_PORTAL_TOKEN") ?? string.Empty;

        public string LoginToken
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(cachedToken))
                    {
                        return cachedToken;
                    }

                    var configValue = EditorUserSettings.GetConfigValue(encryptedLoginTokenKey);
                    if (string.IsNullOrEmpty(configValue))
                    {
                        return null;
                    }

                    var decrypted = Encryption.Decrypt(Convert.FromBase64String(configValue));
                    //If we failed to decrypt the value of token in settings
                    if (string.IsNullOrEmpty(decrypted))
                    {
                        cachedToken = String.Empty;
                        EditorUserSettings.SetConfigValue(encryptedLoginTokenKey, "");
                        return null;
                    }

                    return cachedToken = decrypted;
                }
                catch
                {
                    EditorUserSettings.SetConfigValue(encryptedLoginTokenKey, String.Empty);
                    Debug.LogWarning("Failed to decrypt portal token. Please login again");
                    return cachedToken = String.Empty;
                }
            }
            set
            {
                cachedToken = value;
                EditorUserSettings.SetConfigValue(encryptedLoginTokenKey, Convert.ToBase64String(Encryption.Encrypt(value)));
            }
        }

        public string UserID
        {
            get => EditorUserSettings.GetConfigValue(userIdKey);
            set => EditorUserSettings.SetConfigValue(userIdKey, value);
        }

        public string Email
        {
            get => EditorUserSettings.GetConfigValue(emailKey);
            set => EditorUserSettings.SetConfigValue(emailKey, value);
        }

        public string ProjectName
        {
            get => EditorUserSettings.GetConfigValue(projectNameKey);
            set => EditorUserSettings.SetConfigValue(projectNameKey, value);
        }

        public string CustomToolsPath
        {
            get => EditorUserSettings.GetConfigValue(customToolsPathKey);
            set => EditorUserSettings.SetConfigValue(customToolsPathKey, value);
        }

        public bool UseCustomTools
        {
            get => EditorUserSettings.GetConfigValue(useCustomToolsKey) == useCustomToolsValue;
            set => EditorUserSettings.SetConfigValue(useCustomToolsKey, value ? useCustomToolsValue : null);
        }

        public string CustomAPIDomain
        {
            get => EditorUserSettings.GetConfigValue(customAPIDomainKey);
            set
            {
                EditorUserSettings.SetConfigValue(customAPIDomainKey, value);
                RuntimeSettings.Instance.SetApiEndpoint(Endpoints.Play);
            }
        }

        public bool UseCustomEndpoints
        {
            get => EditorUserSettings.GetConfigValue(useCustomEndpointsKey) == useCustomEndpointsValue;
            set
            {
                EditorUserSettings.SetConfigValue(useCustomEndpointsKey, value ? useCustomEndpointsValue : null);
                RuntimeSettings.Instance.SetApiEndpoint(Endpoints.Play);
            }
        }

        /// <summary>
        /// Returns identifiers of all projects that are currently selected.
        /// </summary>
        /// <remarks>
        /// If <see cref="UsingMultipleProjects"/> is enabled returns the ids of both release and development projects;
        /// otherwise, returns <see cref="RuntimeSettings.ProjectID"/>.
        /// </remarks>
        internal IEnumerable<string> ProjectIds
        {
            get
            {
                if (!UsingMultipleProjects)
                {
                    // We're using ProjectIds on an AssetPostprocessor (Postprocessor class).
                    // Post-processor can get called before RuntimeSettings (PreloadedSingleton) is created.
                    if (RuntimeSettings.TryGet(out var runtimeSettings))
                    {
                        yield return runtimeSettings.ProjectID;
                    }

                    yield break;
                }

                foreach (var project in multipleProjects)
                {
                    yield return project?.Project.id;
                }
            }
        }

        internal bool UsingMultipleProjects
        {
            get => multipleProjects.Length > 0;

            set
            {
                if (value == UsingMultipleProjects)
                {
                    return;
                }

                if (value)
                {
                    var selectedOrganizationId = Organization.Id;
                    var selectedOrganization = PortalLogin.organizations.FirstOrDefault(x => x.Id == selectedOrganizationId);
                    var organizationProjects = selectedOrganization?.projects ?? Array.Empty<ProjectInfo>();
                    var selectedProjectId = RuntimeSettings.Instance.ProjectID;
                    var selectedProject = organizationProjects.FirstOrDefault(x => x.id == selectedProjectId) ?? new();

                    multipleProjects = new[]
                    {
                        ConditionalProject.ForDevelopment(selectedProject),
                        ConditionalProject.ForRelease(selectedProject)
                    };
                }
                else
                {
                    multipleProjects = Array.Empty<ConditionalProject>();
                }

                Schemas.UpdateSyncState();
            }
        }

        internal IReadOnlyList<ConditionalProject> MultipleProjects => multipleProjects;

        internal Organization Organization
        {
            get
            {
#pragma warning disable CS0618
                // Handle migrating legacy data from RuntimeSettings.
                if (RuntimeSettings.TryGet(out var runtimeSettings)
                    && string.IsNullOrEmpty(instance.organization.Id)
                    && !string.IsNullOrEmpty(runtimeSettings.OrganizationID))
                {
                    instance.organization = new(id: runtimeSettings.OrganizationID, name: runtimeSettings.OrganizationName, "", Array.Empty<ProjectInfo>());
                }
#pragma warning restore CS0618

                return organization;
            }

            set
            {
                if (organization == value)
                {
                    return;
                }

                organization = value;

#pragma warning disable CS0618
                // Ensure new organization value won't get overridden by legacy data from RuntimeSettings when the getter is called the next time.
                if (RuntimeSettings.TryGet(out var runtimeSettings))
                {
                    runtimeSettings.OrganizationName = "";
                    runtimeSettings.OrganizationID = "";
                }
#pragma warning restore CS0618
            }
        }

        public string OrganizationId => Organization.Id;
        public string OrganizationName => Organization.Name;

        public bool showHubModuleQuickHelp = true;

        // old portal token, stored in ProjectSettings,
        // kept so we can migrate it to UserSettings smoothly
        [SerializeField] internal string portalToken;

        [FormerlySerializedAs("port")]
        [Tooltip("Port at which the Replication Server will listen for world.")]
        public int worldUDPPort = Constants.defaultWorldUDPPort;

        [Tooltip("Port at which the Replication Server will listen for world in web builds.")]
        public int worldWebPort = Constants.defaultWorldWebPort;

        [Tooltip("Port at which the Replication Server will listen for rooms.")]
        public int roomsUDPPort = Constants.defaultRoomsUDPPort;

        [Tooltip("Port at which the Replication Server will listen for rooms in web builds.")]
        public int roomsWebPort = Constants.defaultRoomsWebPort;

        [Tooltip("Rate at which the Replication Server will send packets to clients.")]
        public int sendFrequency = Constants.defaultSendFrequency;

        [Tooltip("Rate at which the Replication Server wants to receive packets from any client. Packets received faster will be dropped and the connection throttled.")]
        public int recvFrequency = Constants.defaultRecvFrequency;

        [Tooltip("Duration in which the Replication Server waits before attempting to clean up empty rooms.")]
        public int localRoomsCleanupTimeSeconds = Mathf.RoundToInt((float)Constants.localRoomsCleanupTime.TotalSeconds + 0.5f);

        [Tooltip("When starting a local World, apply restrictions so that only Simulators and Hosts are allowed to perform the specified actions.")]
        public HostAuthority localWorldHostAuthority = 0;

        [Tooltip("Log level at which the Replication Server will log to the console.")]
        public LogLevel rsConsoleLogLevel = LogLevel.Info;

        [Tooltip("Will the Replication Server also log to the file.")]
        public bool rsLogToFile;

        [Tooltip("File to which the Replication Server will log (relative to the project root).")]
        public string rsLogFilePath = Constants.defaultRSLogFilePath;

        [Tooltip("Log level at which the Replication Server will log to the file.")]
        public LogLevel rsFileLogLevel = LogLevel.Debug;

        public bool reportAnalytics = true;

        private string hash;

        [SerializeField]
        public bool RSBundlingEnabled;

        [Tooltip("If checked, the replication server is started without a connection timeout. This makes it possible to use the editor without causing a disconnect while playing the game.")]
        public bool keepConnectionAlive;

        public string GetSchemaBakeFolderPath()
        {
            return Paths.defaultSchemaBakePath;
        }

        public SchemaAsset[] activeSchemas = { };

        [SerializeField] private Organization organization = new();
        [SerializeField] private ConditionalProject[] multipleProjects = Array.Empty<ConditionalProject>();

        [MaybeNull]
        public RuntimeSettings RuntimeSettings => RuntimeSettings.InstanceUnsafe;

        /// <summary>
        /// Gets projects currently selected in coherence Hub. Excludes duplicates and unselected projects.
        /// </summary>
        internal IEnumerable<ProjectInfo> GetValidAndDistinctProjects()
        {
            if (UsingMultipleProjects)
            {
                var addedIds = new HashSet<string>();
                foreach (var conditionalProject in multipleProjects)
                {
                    if (conditionalProject.Project is { id: { Length: > 0 } } project && addedIds.Add(project.id))
                    {
                        yield return project;
                    }
                }
            }
            else
            {
                if (GetActiveProject() is { id: { Length: > 0 } } activeProject)
                {
                    yield return activeProject;
                }
            }
        }

        internal void UpdateProjectInRuntimeSettings() => UpdateProjectInRuntimeSettings(GetIsDevelopmentMode());

        internal void UpdateProjectInRuntimeSettings(bool isDevelopmentMode)
        {
            if (GetActiveConditionalProject(isDevelopmentMode)?.Project is { } setProject)
            {
                var runtimeSettings = RuntimeSettings.Instance;
                runtimeSettings.ProjectID = setProject.id;
                runtimeSettings.ProjectName = setProject.name;
                runtimeSettings.RuntimeKey = setProject.runtime_key;
                runtimeSettings.SimulatorSlug = ProjectSimulatorSlugStore.Get(setProject.id);
            }
        }

        [return: MaybeNull]
        internal ConditionalProject GetActiveConditionalProject(bool isDevelopmentMode)
        {
            if (!UsingMultipleProjects)
            {
                return null;
            }

            var projects = multipleProjects;
            return projects.FirstOrDefault(x => x.AreConditionsMet(isDevelopmentMode, false))
                   ?? projects.FirstOrDefault(x => !x.HasConditions);
        }

        internal string GetActiveProjectId() => GetActiveProjectId(GetIsDevelopmentMode());

        internal string GetActiveProjectId(bool isDevelopmentMode)
        {
            if (GetActiveConditionalProject(isDevelopmentMode)?.Project.id is { } activeConditionalProjectId)
            {
                return activeConditionalProjectId;
            }

            if (RuntimeSettings.TryGet(out var runtimeSettings))
            {
                return runtimeSettings.ProjectID;
            }

            return "";
        }

        [return: MaybeNull]
        internal ProjectInfo GetActiveProject(bool isDevelopmentMode)
        {
            if (GetActiveConditionalProject(isDevelopmentMode) is { } activeConditionalProject)
            {
                return activeConditionalProject.Project;
            }

            if (!RuntimeSettings.TryGet(out var runtimeSettings) || runtimeSettings.ProjectID is not { Length: > 0 } projectId)
            {
                return null;
            }

            return new(){ id = projectId, name = runtimeSettings.ProjectName, runtime_key = runtimeSettings.RuntimeKey };
        }

        [return: MaybeNull]
        internal ProjectInfo GetActiveProject() => GetActiveProject(GetIsDevelopmentMode());

        [return: MaybeNull]
        internal ConditionalProject GetConditionalProject(string projectId)
        {
            foreach (var conditionalProject in multipleProjects)
            {
                if (string.Equals(conditionalProject.Project.id, projectId))
                {
                    return conditionalProject;
                }
            }

            return null;
        }

        [return: MaybeNull]
        internal ProjectInfo GetProject(string projectId)
        {
            if (GetConditionalProject(projectId) is { } conditionalProject)
            {
                return conditionalProject.Project;
            }

            if (RuntimeSettings.TryGet(out var runtimeSettings) || string.Equals(runtimeSettings.ProjectID, projectId))
            {
                return new() { id = projectId, name = runtimeSettings.ProjectName, runtime_key = runtimeSettings.RuntimeKey, portal_token = PortalToken };
            }

            return null;
        }

        private void Awake()
        {
            hash = EditorUserSettings.GetConfigValue(hashKey);
            if (string.IsNullOrEmpty(hash))
            {
                if (TryGetActiveSchemasHash(out var h))
                {
                    hash = h.ToString();
                    SessionState.SetString(hashKey, hash);
                }
            }

            UpdateProjectInRuntimeSettings();
        }

        private void OnEnable()
        {
            hideFlags &= ~HideFlags.NotEditable;
            EditorApplication.quitting += OnQuit;
        }

        private void OnDisable() => EditorApplication.quitting -= OnQuit;

        private void OnQuit() => EditorUserSettings.SetConfigValue(hashKey, hash);

        public bool TryGetActiveSchemasHash(out Hash128 hash)
        {
            hash = default;

            try
            {
                var raws = new List<string>();
                raws.Add(File.ReadAllText(Paths.toolkitSchemaPath));

                if (File.Exists(Paths.gatherSchemaPath))
                {
                    raws.Add(File.ReadAllText(Paths.gatherSchemaPath));
                }

                hash = Hash128.Compute(string.Join(string.Empty, raws));

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ActiveSchemasChanged => TryGetActiveSchemasHash(out var newHash) ? hash != newHash.ToString() : false;

        internal IEnumerable<ProjectInfo> Projects
        {
            get
            {
                if (UsingMultipleProjects)
                {
                    foreach (var conditionalProject in multipleProjects)
                    {
                        if (conditionalProject.Project is { } project)
                        {
                            yield return project;
                        }
                    }
                }
                else
                {
                    if (GetActiveProject() is { } activeProject)
                    {
                        yield return activeProject;
                    }
                }
            }
        }

        public bool RehashActiveSchemas()
        {
            if (!TryGetActiveSchemasHash(out var newHash))
            {
                return false;
            }

            var newHashString = newHash.ToString();
            bool changed = hash != newHashString;
            hash = newHashString;
            SessionState.SetString(hashKey, hash);
            return changed;
        }

        public void PruneSchemas()
        {
            string[] guids = AssetDatabase.FindAssets("a:assets t:Coherence.SchemaAsset");

            bool pruned = false;
            for (int i = 0; i < activeSchemas.Length; i++)
            {
                // for unknown reasons, upon opening the Unity project (fresh start):
                //   at static constructor time (InitializeOnLoad), the instance is valid i.e. activeSchemas[i] != null
                //   after the first unity editor tick (EditorApplication.delayCall) the instance is invalid i.e. activeSchemas[i] == null
                //
                // however, the unmanaged (native) type is still tracked by unity, so instead of checking in managed land (C#) we pass the reference
                // for the Unity AssetDatabase (native) to resolve the asset reference, which is valid.
                //
                // TL;DR checking for null (activeSchemas[i] == null) can fail! Hence schemas can be deleted from the active list erroneously!
                // instead, we check for a valid asset path (AssetDatabase API), which resolves the instance natively and successfully.
                //
                // worth noting, triggering an assembly reload will not cause this inconsistency (it actually fixes the reference).
                //
                // this might be an underlying Unity issue.

                string path = AssetDatabase.GetAssetPath(activeSchemas[i]);
                if (string.IsNullOrEmpty(path) || !guids.Contains(AssetDatabase.AssetPathToGUID(path)))
                {
                    ArrayUtility.RemoveAt(ref activeSchemas, i);
                    i--;
                    pruned = true;
                }
            }

            if (pruned)
            {
#if COHERENCE_REBUILD_SYMBOLS_ON_EDIT
                RebuildDefineSymbols();
#endif
                Save();
            }
        }

        public void AddSchema(SchemaAsset asset)
        {
            if (HasSchema(asset))
            {
                return;
            }

            ArrayUtility.Add(ref activeSchemas, asset);
            Array.Sort(activeSchemas);
#if COHERENCE_REBUILD_SYMBOLS_ON_EDIT
            if (asset.defines.Count > 0)
            {
                RebuildDefineSymbols();
            }
#endif
            Save();
        }

        public void RemoveSchema(SchemaAsset asset)
        {
            if (!HasSchema(asset))
            {
                return;
            }

            ArrayUtility.Remove(ref activeSchemas, asset);
#if COHERENCE_REBUILD_SYMBOLS_ON_EDIT
            if (asset.defines.Count > 0)
            {
                RebuildDefineSymbols();
            }
#endif
            Save();
        }

        public bool HasSchema(SchemaAsset schema)
        {
            if (!schema)
            {
                return false;
            }

            return ArrayUtility.Contains(activeSchemas, schema);
        }

        public HostAuthority GetHostAuthority()
        {
            return localWorldHostAuthority;
        }

        public void Save()
        {
            Save(true);
        }

        private static bool GetIsDevelopmentMode() => BuildPreprocessor.GetIsDevelopmentMode();
    }
}
