// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using Coherence.Toolkit;
    using Portal;
    using ReplicationServer;
    using UnityEditor;
    using UnityEngine;

    internal static class CoherenceMainMenu
    {
        // Order of sections. Any separation of more than 10 creates a separator
        private const int Section1 = 1;
        private const int Section2 = 100;
        private const int Section3 = 200;
        private const int Section4 = 300;
        private const int Section5 = 400;

        private static void Analytic(string menuItem)
        {
            Analytics.Capture(Analytics.Events.MenuItem,("menu", "main"), ("item", menuItem));
        }

        [MenuItem("coherence/Hub", false, Section1 + 1)]
        private static void OpenCoherenceHub()
        {
            Analytic("hub");
            CoherenceHub.Open();
        }

        // Quick Access
        [MenuItem("coherence/Networked Prefabs", false, Section2 + 1)]
        private static void OpenNetworkedPrefabs()
        {
            Analytic("csync_objects");
            CoherenceSyncObjectsStandaloneWindow.Open();
        }

        [MenuItem("coherence/Settings", false, Section2 + 2)]
        private static void OpenSettings()
        {
            Analytic("settings");
            _ = SettingsService.OpenProjectSettings(Paths.projectSettingsWindowPath);
        }

        // Baking and schemas
        [MenuItem("coherence/Bake %#&m", true, Section3 + 1)]
        private static bool BakeValidate()
        {
            return !CloneMode.Enabled;
        }

        [MenuItem("coherence/Bake %#&m", false, Section3 + 1)]
        private static void BakeSchemas()
        {
            Analytic("bake");
            BakeUtil.Bake();
        }

        [MenuItem("coherence/Upload Schema", false, Section3 + 2)]
        private static void UploadSchemas()
        {
            Analytic("upload_schemas");
            _ = Schemas.UploadActive(InteractionMode.UserAction);
        }

        [MenuItem("coherence/Upload Schema", true, Section3 + 2)]
        private static bool UploadSchemasValidate()
        {
            return !string.IsNullOrEmpty(ProjectSettings.instance.RuntimeSettings.ProjectID);
        }

        // Run RS
        [MenuItem("coherence/Run Replication Server for Rooms %#&r", false, Section4)]
        private static void RunRoomsReplicationServerInTerminal()
        {
            Analytic("run_local_rooms");
            EditorLauncher.RunRoomsReplicationServerInTerminal();
        }

        [MenuItem("coherence/Run Replication Server for Worlds %#&w", false, Section4 + 1)]
        private static void RunWorldsReplicationServerInTerminal()
        {
            Analytic("run_local_worlds");
            EditorLauncher.RunWorldsReplicationServerInTerminal();
        }

        // Links
        [MenuItem("coherence/Help/Documentation", false, Section5)]
        private static void OpenDocumentation()
        {
            Analytic("open_docs");
            UsefulLinks.Documentation();
        }

        [MenuItem("coherence/Help/Community", false, Section5 + 1)]
        private static void OpenCommunityForums()
        {
            Analytic("help_community");
            UsefulLinks.CommunityForum();
        }

        [MenuItem("coherence/Help/Discord", false, Section5 + 2)]
        private static void OpenDiscord()
        {
            Analytic("help_discord");
            UsefulLinks.Discord();
        }

        [MenuItem("coherence/Help/Support", false, Section5 + 3)]
        private static void OpenSupport()
        {
            Analytic("help_support");
            UsefulLinks.Support();
        }

        [MenuItem("coherence/Help/Report a Bug...", false, Section5 + 4)]
        private static void ReportABug()
        {
            Analytic("help_report_bug");
            BugReportHelper.DisplayReportBugDialogs();
        }

        [MenuItem("coherence/Help/Troubleshooting/Known Issues", false, Section5 + 20)]
        private static void OpenKnownIssues()
        {
            Application.OpenURL(DocumentationLinks.GetDocsUrl(DocumentationKeys.KnownIssues));
        }

        [MenuItem("coherence/Help/Troubleshooting/Update Bindings", true, Section5 + 21)]
        private static bool UpdateBindingsValidate()
        {
            return !CloneMode.Enabled;
        }

        [MenuItem("coherence/Help/Troubleshooting/Update Bindings", false, Section5 + 21)]
        private static void UpdateBindings()
        {
            Analytic("update_bindings");
            EditorCache.UpdateBindingsAndNotify();
            Debug.Log("Trying to update bindings from Networked Prefabs...\nIf there are bindings updated, you'll see a popup window with the modified Prefabs.");
        }

        [MenuItem("coherence/Help/Troubleshooting/Migrate Assets", true, Section5 + 22)]
        private static bool MenuMigrationValidate()
        {
            return !CloneMode.Enabled;
        }

        [MenuItem("coherence/Help/Troubleshooting/Migrate Assets", false, Section5 + 22)]
        private static void MenuMigration()
        {
            Analytic("migrate_assets");
            var (migratedAny, hashSet) = Migration.Migrate();
            if (!migratedAny)
            {
                Debug.Log("No asset migrations were necessary.");
                return;
            }

            Debug.Log(hashSet.Count + " migrations performed:\n" + string.Join("\n", hashSet));
        }
    }
}
