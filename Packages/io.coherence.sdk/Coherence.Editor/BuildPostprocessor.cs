// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEditor.Compilation;

    internal class BuildPostprocessor : IPostprocessBuildWithReport, IPreprocessBuildWithReport
    {
        private class BuildEventProperties : Analytics.BaseProperties
        {
            public string build_target;
            public bool development;
            public bool headless;
            public double build_time;
        }

        public int callbackOrder => 0;

        public async void OnPreprocessBuild(BuildReport report)
        {
            do
            {
                await Task.Yield();
            }
            while (BuildPipeline.isBuildingPlayer);

            if (report.summary.result is BuildResult.Failed or BuildResult.Cancelled)
            {
                OnBuildFailedOrWasCanceled(report);
            }
        }

        public void OnPostprocessBuild(BuildReport report) => OnBuildSucceeded(report);

        private void OnBuildSucceeded(BuildReport report)
        {
            var properties = new BuildEventProperties
            {
                build_target = report.summary.platform.ToString(),
                development = (report.summary.options & BuildOptions.Development) != 0,
                headless = IsHeadless(report),
                build_time = report.summary.totalTime.TotalSeconds,
            };

            Analytics.Capture(new Analytics.Event<BuildEventProperties>(Analytics.Events.Build, properties));

            OnBuildSucceededFailedOrWasCanceled(report);
        }

        private void OnBuildFailedOrWasCanceled(BuildReport report) => OnBuildSucceededFailedOrWasCanceled(report);

        private void OnBuildSucceededFailedOrWasCanceled(BuildReport report)
        {
            if (BakeUtil.BakeOnBuild)
            {
                // make sure changes to baked files are picked up by Unity
                // after the build is completed
                // NOTE triggered even when no changes to generated code
                EditorApplication.delayCall += Refresh;
            }

            DeleteCombinedSchema();
            DeleteRsBinary(report.summary.platform);
            DeleteStreamingAssetsIfEmpty();
            ManagedCodeStrippingUtils.DeleteLinkXmlFileUnderAssetsFolder();
            Postprocessor.UpdateRuntimeSettings(isDevelopmentMode: true);
        }

        [InitializeOnLoadMethod]
        internal static void CleanUpTempFiles()
        {
            DeleteCombinedSchema();

            foreach (var supportedBuildTarget in ReplicationServerBinaries.GetSupportedPlatforms())
            {
                DeleteRsBinary(supportedBuildTarget);
            }

            DeleteStreamingAssetsIfEmpty();
        }

        private static void DeleteRsBinary(BuildTarget platform)
        {
            if (ProjectSettings.instance.RSBundlingEnabled && ReplicationServerBinaries.IsSupportedPlatform(platform))
            {
                ReplicationServerBundler.DeleteRsFromStreamingAssets(platform);
            }
        }

        internal static void DeleteStreamingAssetsIfEmpty() => AssetUtils.DeleteFolderIfEmpty(Paths.streamingAssetsPath);
        private static void DeleteCombinedSchema() => AssetUtils.DeleteFile(Paths.streamingAssetsCombinedSchemaPath);

        private static bool IsHeadless(BuildReport report)
        {
            try
            {
                switch (report.summary.platform)
                {
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                    case BuildTarget.StandaloneOSX:
                    case BuildTarget.StandaloneLinux64:
                        return report.summary.GetSubtarget<StandaloneBuildSubtarget>() == StandaloneBuildSubtarget.Server;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void Refresh() => CompilationPipeline.RequestScriptCompilation();
    }
}
