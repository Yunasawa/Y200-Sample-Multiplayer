// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using Portal;
    using System.Linq;
    using UnityEditor;

    public static class PortalUtil
    {
        static PortalUtil() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        private static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange is PlayModeStateChange.ExitingEditMode)
            {
                Postprocessor.UpdateRuntimeSettings();

                if (UploadOnEnterPlayMode
                    // Only auto-upload schemas to the Development project when testing in the Editor.
                    && ProjectSettings.instance.GetActiveProject(true) is { } activeProject)
                {
                    Schemas.Upload(activeProject);
                }
            }
        }

        public const string uploadOnEnterPlayModeKey = "Coherence.UploadOnEnterPlayMode";
        public const string uploadOnBuildKey = "Coherence.UploadOnBuild";
        public const string uploadAfterBakeKey = "Coherence.uploadAfterBake";

        /// <summary>
        /// Logs out the current user from the coherence Developer Portal.
        /// </summary>
        public static void Logout() => PortalLogin.Logout();

        /// <summary>
        /// Removes the data related to the organization and the project of the current user.
        /// </summary>
        public static void ClearOrganizationData() => PortalLogin.AssociateOrganization(null);

        public static bool UploadOnEnterPlayMode
        {
            get => EditorPrefs.GetBool(uploadOnEnterPlayModeKey, false);
            set => EditorPrefs.SetBool(uploadOnEnterPlayModeKey, value);
        }

        public static bool UploadAfterBake
        {
            get => EditorPrefs.GetBool(uploadAfterBakeKey, false);
            set => EditorPrefs.SetBool(uploadAfterBakeKey, value);
        }

        internal static UploadAfterBakeOptions UploadAfterBakeFlags
        {
            get => (UploadAfterBakeOptions)EditorPrefs.GetInt(uploadAfterBakeKey, 0);
            set => EditorPrefs.SetInt(uploadAfterBakeKey, (int)value);
        }

        public static bool UploadOnBuild
        {
            get => EditorPrefs.GetBool(uploadOnBuildKey, false);
            set => EditorPrefs.SetBool(uploadOnBuildKey, value);
        }
        /// <summary>
        /// Determines the state of synchronicity against the coherence Developer Portal.
        /// </summary>
        public static Schemas.SyncState SyncState => Schemas.state;

        /// <summary>
        /// List of schema states the portal knows about.
        /// </summary>
        public static Schemas.SchemaState[] RemoteSchemaStates => Schemas.RemoteSchemaStates;

        /// <summary>
        /// List of schema IDs the portal knows about.
        /// </summary>
        public static string[] RemoteSchemaIDs => Schemas.RemoteSchemaIDs;

        /// <summary>
        /// Determines if the schemas found in the project are in sync with the ones found in the coherence Developer Portal.
        /// </summary>
        public static bool InSync => SyncState == Schemas.SyncState.InSync;

        public static bool LocalSchemaFoundInPortal => BakeUtil.HasSchemaID && System.Array.IndexOf(RemoteSchemaIDs, BakeUtil.SchemaID) != -1;

        /// <summary>
        /// Uploads schemas to the portal.
        /// </summary>
        /// <note>
        /// This method communicates with the coherence Developer Portal through HTTP synchronously.
        /// </note>
        /// <returns>
        /// <see langword="true"/> if the operation succeeds, <see langword="false"/> otherwise.
        /// </returns>
        public static bool UploadSchemas()
        {
            return Schemas.UploadActive();
        }

        public static bool CanCommunicateWithPortal => !string.IsNullOrEmpty(ProjectSettings.instance.PortalToken) || !string.IsNullOrEmpty(ProjectSettings.instance.LoginToken);

        internal static bool OrgAndAtLeastOneProjectIsSet => OrgIsSet && AtLeastOneProjectIsSet;
        internal static bool OrgIsSet => PortalLogin.organizations.Any(x => x.id == ProjectSettings.instance.OrganizationId);
        internal static bool AtLeastOneProjectIsSet => ProjectSettings.instance.GetValidAndDistinctProjects().Any();

        /// <summary>
        /// returns <see cref="true"/> if <see cref="RuntimeSettings.ProjectID"/> has a value that is not null or empty,
        /// and <see cref="RuntimeSettings.OrganizationID"/> has a value that is found among <see cref="PortalLogin.organizations"/>;
        /// otherwise, <see cref="false"/>.
        /// <para>
        /// NOTE: Always returns <see cref="false"/>, unless <see cref="PortalLogin.FetchOrgs"/> has been called!
        /// This might not have happened unless the Coherence Sync window has been opened.
        /// </para>
        /// </summary>
        public static bool OrgAndProjectIsSet => OrgIsSet && !string.IsNullOrEmpty(ProjectSettings.instance.GetActiveProjectId());

        [Flags]
        internal enum UploadAfterBakeOptions
        {
            None = 0,
            Development = 1,
            Release = 2
        }
    }
}
