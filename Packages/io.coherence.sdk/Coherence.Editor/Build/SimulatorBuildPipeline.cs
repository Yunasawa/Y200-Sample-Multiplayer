// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Build
{
    using Editor;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Editor.Portal;
    using Editor.Toolkit;
    using Transport;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEditor.Callbacks;
    using UnityEditor.Compilation;
#if UNITY_EDITOR_OSX
    using UnityEditor.OSXStandalone;
#endif
    using UnityEngine;
    using Directory = UnityEngine.Windows.Directory;
    using File = UnityEngine.Windows.File;

    public static class SimulatorBuildPipeline
    {
        private static string DeferHeadlessBuildAfterCompilationKey => "io.coherence.deferheadlessbuild";
        private static string DeferLocalBuildAfterCompilationKey => "io.coherence.deferlocalbuild";
        private static string IsBuildingSimulatorKey => "io.coherence.isbuildingsim";
        private static string PreviousBuildTargetKey => "io.coherence.previousbuildtarget";
        private static string ProductNameKey => "io.coherence.productname";
        private static string ForceShowPromptKey => "io.coherence.forceshowprompt";
        private static string AppleSiliconWarningMessageKey => "io.coherence.applesiliconwarningmessage";
        private static string BuildTransportTypeKey => "io.coherence.buildtransporttype";
        internal static string PreviousLocalBuildPathKey => "io.coherence.previouslocalbuildpath";
        public static string ServerModuleMissingKey => "io.coherence.servermodulemissing";
        public static string UserConfirmedLocalServer = "io.coherence.userconfirmationoflocalserver";

        public static bool IsBuildingSimulator
        {
            get => SessionState.GetBool(IsBuildingSimulatorKey, false);
            internal set => SessionState.SetBool(IsBuildingSimulatorKey, value);
        }

        /// <summary>
        ///     This method must be called before BuildHeadlessLinuxClientAsync. The purpose of this method is to set the
        ///     required scripting symbol COHERENCE_SIMULATOR and the activeBuildSubTarget to Server, before building the Simulator itself.
        ///
        ///     It has to be done in two different method calls, because in Batch Mode, there is no Editor loop that ensures the code is recompiled
        ///     after setting the scripting symbols. See https://docs.unity3d.com/Manual/CustomScriptingSymbols.html
        /// </summary>
        public static void PrepareHeadlessBuild()
        {
            if (!IsInBatchMode())
            {
                return;
            }

            _ = ScriptingSymbolsChanger.ChangeScriptingSymbols(NamedBuildTarget.Server, false);

            var supportsHeadlessLinuxBuild =
                SimulatorEditorUtility.IsBuildTargetSupported(true,
                    SimulatorBuildOptions.Get().ScriptingImplementation);
            _ = ChangeBuildSubTarget(supportsHeadlessLinuxBuild);
            _ = ChangeBuildTargetToLinux();
        }

        /// <summary>
        /// Method to build a Headless Linux Coherence Simulator, to be uploaded to the developer portal. It handles all the pre-configuration needed to build the Simulator successfully.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The asynchronous nature is due to having to set the scripting symbol COHERENCE_SIMULATOR and the activeBuildSubTarget to Server
        /// before performing the build. This will trigger a full recompilation, unless the scripting symbol and the build sub target are already set.
        /// </para>
        /// <para>
        /// After compilation has finished, a build will be created, compressed and uploaded to the project in coherence Cloud.
        /// </para>
        /// <para>
        /// You must be logged in to coherence Cloud via coherence Hub and have an organization and project created.
        /// </para>
        /// <para>
        /// You also need to specify a simulator slug needs in coherence Hub > Simulators > Simulator Build > Simulator Slug,
        /// or pass it as the -simSlug command line argument.
        /// </para>
        /// </remarks>
        public static void BuildHeadlessLinuxClientAsync()
        {
            var projects = GetActiveProjects();
            var simulatorSlugs = GetSimulatorSlugs(projects);
            BuildHeadlessLinuxClientAsync(projects, simulatorSlugs);
        }

        internal static string[] GetSimulatorSlugs(ProjectInfo[] projects)
        {
            var count = projects.Length;
            var simulatorSlugs = new string[count];

            var simulatorSlug = GetCmdLineArg("-simSlug");
            if (!string.IsNullOrEmpty(simulatorSlug))
            {
                for (var i = 0; i < count; i++)
                {
                    simulatorSlugs[i] = simulatorSlug;
                }
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    var projectId = projects[i].id;
                    simulatorSlugs[i] =
                        RuntimeSettings.TryGet(out var runtimeSettings) && string.Equals(runtimeSettings.ProjectID, projectId)
                        ? runtimeSettings.SimulatorSlug
                        : ProjectSimulatorSlugStore.Get(projectId);
                }
            }

            return simulatorSlugs;
        }

        internal static string GetSimulatorSlug(ProjectInfo project)
        {
            var simulatorSlug = GetCmdLineArg("-simSlug");
            if (!string.IsNullOrEmpty(simulatorSlug))
            {
                return simulatorSlug;
            }

            return RuntimeSettings.TryGet(out var runtimeSettings) && string.Equals(runtimeSettings.ProjectID, project.id)
                    ? runtimeSettings.SimulatorSlug
                    : ProjectSimulatorSlugStore.Get(project.id);
        }

        /// <summary>
        /// Method to build a Headless Linux Coherence Simulator, to be uploaded to the developer portal. It handles all the pre-configuration needed to build the Simulator successfully.
        /// </summary>
        /// <param name="projectId">
        /// <para>
        /// Identifier of the project into which the simulator should be uploaded.
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
        /// Token for the project into which the simulator should be uploaded.
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
        /// <param name="simulatorSlug"> Unique identifier for the simulator. </param>
        /// <param name="interactionMode">
        /// If set to <see cref="InteractionMode.UserAction"/> errors will be displayed in dialog popups; otherwise, they will be logged to the console.
        /// </param>
        /// <remarks>
        /// <para>
        /// The asynchronous nature is due to having to set the scripting symbol COHERENCE_SIMULATOR and the activeBuildSubTarget to Server
        /// before performing the build. This will trigger a full recompilation, unless the scripting symbol and the build sub target are already set.
        /// </para>
        /// <para>
        /// After compilation has finished, a build will be created, compressed and uploaded to the project in coherence Cloud.
        /// </para>
        /// </remarks>
        public static void BuildHeadlessLinuxClientAsync(string projectId, string projectToken, string simulatorSlug, InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            var project = new ProjectInfo { id = projectId, portal_token = projectToken };
            BuildHeadlessLinuxClientAsync(project, simulatorSlug, interactionMode);
        }

        /// <summary>
        /// Method to build a Headless Linux Coherence Simulator, to be uploaded to the developer portal. It handles all the pre-configuration needed to build the Simulator successfully.
        /// </summary>
        /// <param name="projectId">
        /// <para>
        /// Identifier of the project into which the simulator should be uploaded.
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
        /// Token for the project into which the simulator should be uploaded.
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
        /// If set to <see cref="InteractionMode.UserAction"/> errors will be displayed in dialog popups; otherwise, they will be logged to the console.
        /// </param>
        /// <remarks>
        /// <para>
        /// The asynchronous nature is due to having to set the scripting symbol COHERENCE_SIMULATOR and the activeBuildSubTarget to Server
        /// before performing the build. This will trigger a full recompilation, unless the scripting symbol and the build sub target are already set.
        /// </para>
        /// <para>
        /// After compilation has finished, a build will be created, compressed and uploaded to the project in coherence Cloud.
        /// </para>
        /// <para>
        /// You need to specify a simulator slug needs in coherence Hub > Simulators > Simulator Build > Simulator Slug,
        /// or pass it as the -simSlug command line argument.
        /// </para>
        /// </remarks>
        public static void BuildHeadlessLinuxClientAsync(string projectId, string projectToken, InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            var projects = new ProjectInfo[] { new() { id = projectId, portal_token = projectToken } };
            BuildHeadlessLinuxClientAsync(projects, GetSimulatorSlugs(projects), interactionMode);
        }

        /// <summary>
        /// Method to build a Headless Linux Coherence Simulator, to be uploaded to the developer portal. It handles all the pre-configuration needed to build the Simulator successfully.
        /// </summary>
        /// <param name="project"> Project into which the simulator should be uploaded. </param>
        /// <param name="simulatorSlug"> Unique identifier for the simulator. </param>
        /// <param name="interactionMode">
        /// If set to <see cref="InteractionMode.UserAction"/> errors will be displayed in dialog popups; otherwise, they will be logged to the console.
        /// </param>
        /// <remarks>
        /// <para>
        /// The asynchronous nature is due to having to set the scripting symbol COHERENCE_SIMULATOR and the activeBuildSubTarget to Server
        /// before performing the build. This will trigger a full recompilation, unless the scripting symbol and the build sub target are already set.
        /// </para>
        /// <para>
        /// After compilation has finished, a build will be created, compressed and uploaded to the project in coherence Cloud.
        /// </para>
        /// </remarks>
        internal static void BuildHeadlessLinuxClientAsync(ProjectInfo project, string simulatorSlug, InteractionMode interactionMode = InteractionMode.AutomatedAction)
            => BuildHeadlessLinuxClientAsync(new[] { project }, new[] { simulatorSlug }, interactionMode);

        internal static void BuildHeadlessLinuxClientAsync(ProjectInfo[] projects, InteractionMode interactionMode = InteractionMode.AutomatedAction)
            => BuildHeadlessLinuxClientAsync(projects, GetSimulatorSlugs(projects), interactionMode);

        internal static void BuildHeadlessLinuxClientAsync(ProjectInfo[] projects, string[] simulatorSlugs, InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            if (simulatorSlugs.Length is 0 || simulatorSlugs.Any(string.IsNullOrEmpty))
            {
                DisplayError("Simulators slugs are required for building the headless linux client.", interactionMode);
                return;
            }

            if (projects.Length is 0 || projects.All(x => string.IsNullOrEmpty(x.id)))
            {
                DisplayError("Project not set.\n\n" +
                             "Open the 'Cloud' tab in coherence Hub and select a Project from the dropdown.", interactionMode);
                return;
            }

            for (var i = 0; i < projects.Length; i++)
            {
                var projectId = projects[i].id;
                var simulatorSlug = simulatorSlugs[i];
                ProjectSimulatorSlugStore.Set(projectId, simulatorSlug);

                if (RuntimeSettings.TryGet(out var runtimeSettings) && string.Equals(runtimeSettings.ProjectID, projectId))
                {
                    runtimeSettings.SimulatorSlug = simulatorSlug;
                }
            }

            ResetPrefs();

            DeleteFolder(SimulatorEditorUtility.FullBuildLocationPath);

            var options = SimulatorBuildOptions.Get();

            IsBuildingSimulator = true;

            var supportsHeadlessLinuxBuild =
                SimulatorEditorUtility.IsBuildTargetSupported(true, options.ScriptingImplementation);

            var changedScriptingSymbols = ScriptingSymbolsChanger.ChangeScriptingSymbols(
                supportsHeadlessLinuxBuild ? NamedBuildTarget.Server : NamedBuildTarget.Standalone, true);
            var changedBuildSubTarget = ChangeBuildSubTarget(supportsHeadlessLinuxBuild);
            var changedBuildTarget = ChangeBuildTargetToLinux();
            var changeRuntimeSettings = ChangeBuildToUdp();

            if (changedScriptingSymbols || changedBuildSubTarget || changedBuildTarget || changeRuntimeSettings)
            {
                SessionState.SetBool(DeferHeadlessBuildAfterCompilationKey, true);
                RequestCompilation();
            }
            else
            {
                PerformHeadlessBuild(simulatorSlugs, projects);
            }
        }

        private static bool ChangeBuildTargetToLinux()
        {
            bool changedBuildTarget = false;

            SessionState.SetInt(PreviousBuildTargetKey, (int)BuildTarget.NoTarget);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
            {
                changedBuildTarget = true;
                SessionState.SetInt(PreviousBuildTargetKey, (int)EditorUserBuildSettings.activeBuildTarget);
                _ = EditorUserBuildSettings.SwitchActiveBuildTarget(
                     BuildPipeline.GetBuildTargetGroup(BuildTarget.StandaloneLinux64), BuildTarget.StandaloneLinux64);
            }

            return changedBuildTarget;
        }

        private static bool ChangeBuildToUdp()
        {
            if (RuntimeSettings.Instance.TransportType != TransportType.UDPOnly)
            {
                SessionState.SetInt(BuildTransportTypeKey, (int)RuntimeSettings.Instance.TransportType);
                RuntimeSettings.Instance.TransportType = TransportType.UDPOnly;
                return true;
            }

            return false;
        }

        public static void RestoreTransportType()
        {
            RuntimeSettings.Instance.TransportType = (TransportType)SessionState.GetInt(BuildTransportTypeKey,
                (int)RuntimeSettings.Instance.TransportType);

            SessionState.EraseBool(DeferHeadlessBuildAfterCompilationKey);
        }

        /// <summary>
        ///     Method to build a Local Coherence Simulator, to be used locally. It handles all the pre-configuration needed to build the Simulator successfully.
        ///
        ///     The asynchronous nature is due to having to set the scripting symbol COHERENCE_SIMULATOR
        ///     before performing the build. This will trigger a full recompilation, unless the scripting symbol is already set.
        ///
        ///     The build will be performed after compilation finishes.
        /// </summary>
        /// <param name="forceShowPrompt">Force the dialog to appear, otherwise uses last-known good location from this session</param>
        public static void BuildLocalSimulator(bool forceShowPrompt = false)
        {
            ResetPrefs();
            SessionState.SetBool(ForceShowPromptKey, forceShowPrompt);
            var options = SimulatorBuildOptions.Get();

            IsBuildingSimulator = true;

            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);

            var namedBuildTarget = options.HeadlessMode ?
                NamedBuildTarget.Server : NamedBuildTarget.FromBuildTargetGroup(group);
            bool changedScriptingSymbols = ScriptingSymbolsChanger.ChangeScriptingSymbols(namedBuildTarget, false);
            bool changedBuildSubTarget = ChangeBuildSubTarget(options.HeadlessMode);

            if (changedScriptingSymbols || changedBuildSubTarget)
            {
                SessionState.SetBool(DeferLocalBuildAfterCompilationKey, true);
                RequestCompilation();
            }
            else
            {
                PerformLocalBuild();
            }
        }

        private static bool ChangeBuildSubTarget(bool useHeadlessMode)
        {
            if (EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Server && useHeadlessMode)
            {
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
                return true;
            }

            if (EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player && !useHeadlessMode)
            {
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
                return true;
            }

            return false;
        }

        [DidReloadScripts(0)]
        internal static void PerformHeadlessBuildAfterCompilation()
        {
            var hasToBuild = SessionState.GetBool(DeferHeadlessBuildAfterCompilationKey, false);

            if (!hasToBuild)
            {
                return;
            }

            SessionState.SetBool(DeferHeadlessBuildAfterCompilationKey, false);

            EditorApplication.delayCall += ()=>
            {
                var activeProjects = GetActiveProjects();
                if (activeProjects.Length is 0)
                {
                    DisplayError("Project not set.\n\n" +
                                 "Open the 'Cloud' tab in coherence Hub and select a Project from the dropdown.");
                    IsBuildingSimulator = false;
                    BuildSettingsRestorer.RestorePreviousBuildSettings(GetPreviousBuildTarget(), 0, 0);
                    return;
                }

                var simulatorSlugs = GetSimulatorSlugs(activeProjects);
                PerformHeadlessBuild(simulatorSlugs, activeProjects);
            };
        }

        private static ProjectInfo[] GetActiveProjects() => ProjectSelectDialog.GetSelectedProjectIds().Where(id => !string.IsNullOrEmpty(id)).Distinct().Select(id => ProjectSettings.instance.GetProject(id)).ToArray();

        [DidReloadScripts(0)]
        internal static void PerformLocalBuildAfterCompilation()
        {
            var hasToBuild = SessionState.GetBool(DeferLocalBuildAfterCompilationKey, false);

            if (!hasToBuild)
            {
                return;
            }

            SessionState.SetBool(DeferLocalBuildAfterCompilationKey, false);

            EditorApplication.delayCall += PerformLocalBuild;
        }

        private static void RequestCompilation()
        {
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }

        private static void ResetPrefs()
        {
            SessionState.SetBool(DeferHeadlessBuildAfterCompilationKey, false);
            SessionState.SetBool(DeferLocalBuildAfterCompilationKey, false);
            SessionState.SetInt(PreviousBuildTargetKey, (int)BuildTarget.NoTarget);
        }

        private static void DeleteFolder(string buildLocationPath)
        {
            if (Directory.Exists(buildLocationPath))
            {
                Directory.Delete(buildLocationPath);
            }
        }

        private static bool IsInBatchMode()
        {
            if (!Application.isBatchMode)
            {
                Debug.LogError("This method is meant to be called in batch mode.");
                return false;
            }

            return true;
        }

        private static async void PerformHeadlessBuild(string[] simulatorSlugs, ProjectInfo[] projects, InteractionMode interactionMode = InteractionMode.AutomatedAction)
        {
            var options = SimulatorBuildOptions.Get();

            var editorBuildSettingsScenes = GetEditorBuildSettingsScenes(options);

            if (editorBuildSettingsScenes.Length == 0)
            {
                Debug.LogError("No Scenes selected to be built in the Simulator");
                BuildSettingsRestorer.RestorePreviousBuildSettings(GetPreviousBuildTarget(), 0, projects.Length);
                IsBuildingSimulator = false;
                return;
            }

            var buildOptions = GetBuildOptions(options.DevBuild);

            ChangeProductName();

            ProjectSelectDialog.SaveSelectedProjectIds(projects.Select(x => x.id).ToArray());
            var runtimeSettings = RuntimeSettings.Instance;
            var projectIdWas = runtimeSettings.ProjectID;
            var projectNameWas = runtimeSettings.ProjectName;
            var runtimeKeyWas = runtimeSettings.RuntimeKey;
            var simulatorSlugWas = runtimeSettings.SimulatorSlug;
            var compressAndUploadTasks = new Task<bool>[projects.Length];
            try
            {
                for (var i = 0; i < projects.Length; i++)
                {
                    var project = projects[i];
                    var simulatorSlug = simulatorSlugs[i];
                    runtimeSettings.ProjectID = project.id;
                    runtimeSettings.ProjectName = project.name;
                    runtimeSettings.RuntimeKey = project.runtime_key;
                    runtimeSettings.SimulatorSlug = simulatorSlug;

                    if (string.IsNullOrEmpty(simulatorSlug))
                    {
                        DisplayError("Simulator slug is required for uploading the simulator.", interactionMode);
                        compressAndUploadTasks[i] = Task.FromResult(false);
                        continue;
                    }

                    if (string.IsNullOrEmpty(project.id))
                    {
                        DisplayError("Project not set.\n\n" +
                                     "Open the 'Cloud' tab in coherence Hub and select a Project from the dropdown.");
                        compressAndUploadTasks[i] = Task.FromResult(false);
                        continue;
                    }

                    var report = BuildPipeline.BuildPlayer(editorBuildSettingsScenes, SimulatorEditorUtility.ExecutablePath, BuildTarget.StandaloneLinux64, buildOptions);

                    if (report.summary.result == BuildResult.Succeeded)
                    {
                        if (!Application.isBatchMode)
                        {
                            var taskCompletionSource = new TaskCompletionSource<bool>();
                            compressAndUploadTasks[i] = taskCompletionSource.Task;
                            EditorApplication.delayCall += () => taskCompletionSource.SetResult(CompressAndUpload(simulatorSlug, project));
                        }
                        else
                        {
                            compressAndUploadTasks[i] = Task.FromResult(CompressAndUpload(simulatorSlug, project));
                        }
                    }
                    else
                    {
                        DisplayError($"Simulator build has finished with {report.summary.totalErrors} errors. Your build settings and scripting symbols will not be reverted to preserve logs.", interactionMode);
                        compressAndUploadTasks[i] = Task.FromResult(false);
                    }
                }

                await Task.WhenAll(compressAndUploadTasks);
            }
            finally
            {
                runtimeSettings.ProjectID = projectIdWas;
                runtimeSettings.ProjectName = projectNameWas;
                runtimeSettings.RuntimeKey = runtimeKeyWas;
                runtimeSettings.SimulatorSlug = simulatorSlugWas;

                var buildsSucceeded = compressAndUploadTasks.Count(x => x is { IsCompletedSuccessfully : true, Result: true });
                BuildSettingsRestorer.RestorePreviousBuildSettings(GetPreviousBuildTarget(), buildsSucceeded, projects.Length);
                IsBuildingSimulator = false;
                RestoreProductName();
            }
        }

        private static BuildTarget GetPreviousBuildTarget()
        {
            var previousBuildTargetInt = SessionState.GetInt(PreviousBuildTargetKey, (int)BuildTarget.NoTarget);

            return (BuildTarget)previousBuildTargetInt;
        }

        private static void PerformLocalBuild()
        {
            var options = SimulatorBuildOptions.Get();

            var sceneNames = GetSceneNames(options);

            if (sceneNames.Length == 0)
            {
                Debug.LogError("No Scenes selected to be built in the Simulator");
                return;
            }

#if UNITY_EDITOR_OSX
            var currentArchitecture = UserBuildSettings.architecture;
#if UNITY_2022_1_OR_NEWER
            const OSArchitecture architectureToUse = OSArchitecture.x64;
#else
            const MacOSArchitecture architectureToUse = MacOSArchitecture.x64;
#endif
            var changeArchitecture = currentArchitecture != architectureToUse;

            if (changeArchitecture && (RuntimeInformation.ProcessArchitecture == Architecture.Arm ||
                RuntimeInformation.ProcessArchitecture == Architecture.Arm64))
            {
                if (Application.isBatchMode)
                {
                    Debug.Log("Arm builds can be detected as malware on certain versions of MacOS. Switching to Intel to build local simulator.");
                }

                if (Application.isBatchMode || EditorUtility.DisplayDialog("Architecture Has Been Changed",
                        "Local simulator will use Intel builds for computers running MacOS on Apple Silicon.", "Ok",
                        DialogOptOutDecisionType.ForThisSession, AppleSiliconWarningMessageKey))
                {
#if UNITY_2022_1_OR_NEWER
                    Debug.Log(
                        $"Architecture for simulator build has been changed from {currentArchitecture} to {OSArchitecture.x64}.");
                    UserBuildSettings.architecture = OSArchitecture.x64;
#else
                    Debug.Log(
                        $"Architecture for simulator build has been changed from {currentArchitecture} to {MacOSArchitecture.x64}.");
                    UserBuildSettings.architecture = MacOSArchitecture.x64;
#endif
                }
                else
                {
                    return;
                }
            }
#endif

            var forcePrompt = SessionState.GetBool(ForceShowPromptKey, false);
            var buildPath = SessionState.GetString(PreviousLocalBuildPathKey, string.Empty);
            if (forcePrompt || string.IsNullOrEmpty(buildPath))
            {
                buildPath = GetBuildPathFromUser();
            }

            if (string.IsNullOrEmpty(buildPath))
            {
                return;
            }

            try
            {
                ChangeProductName();

                var buildOptions = GetBuildOptions(options.DevBuild);
                var report = BuildPipeline.BuildPlayer(sceneNames, buildPath,
                    EditorUserBuildSettings.activeBuildTarget, buildOptions);
                if (report.summary.result == BuildResult.Succeeded)
                {
                    SessionState.SetString(PreviousLocalBuildPathKey, buildPath);
                    SimulatorEditorUtility.LocalSimulatorExecutablePath = GetLocalSimulatorExecutablePath(buildPath);
                    Debug.Log($"Local Simulator Build succeeded! {buildPath}");
                }
                else if (report.summary.result == BuildResult.Failed)
                {
#if UNITY_2023_1_OR_NEWER
                    var errors = report.SummarizeErrors();
#else
                    var errors = report.steps.Select(s => s.messages)
                        .SelectMany(m => m)
                        .Where(m => m.type == LogType.Error)
                        .Select(m => m.content)
                        .Aggregate("", (current, message) => $"{current} {message} ");
#endif
                    var regex = new Regex("Dedicated Server support for .* is not installed");
                    if (regex.IsMatch(errors))
                    {
                        Debug.LogError($"Local Simulator Build failed with {report.summary.totalErrors} errors.");
                        SessionState.SetBool(ServerModuleMissingKey, true);
                    }
                    Debug.LogError($"Local Simulator Build failed with {report.summary.totalErrors} errors.");
                }
            }
            catch (BuildPlayerWindow.BuildMethodException buildMethodException)
            {
                if (!string.IsNullOrEmpty(buildMethodException.Message))
                {
                    Debug.LogError($"Message: {buildMethodException}. StackTrace: {buildMethodException.StackTrace}");
                }
            }
            finally
            {
#if UNITY_EDITOR_OSX
                UserBuildSettings.architecture = currentArchitecture;
#endif
                IsBuildingSimulator = false;
                RestoreProductName();
                EditorApplication.delayCall += BuildSettingsRestorer.RestorePreviousBuildSettingsForLocalBuild;
            }
        }

        private static string GetBuildPathFromUser()
        {
            var isWindows =
                EditorUserBuildSettings.activeBuildTarget is BuildTarget.StandaloneWindows
                    or BuildTarget.StandaloneWindows64;

            var isLinux =
                EditorUserBuildSettings.activeBuildTarget is BuildTarget.LinuxHeadlessSimulation
                    or BuildTarget.StandaloneLinux64;

            var buildPath = isWindows || isLinux
                ? EditorUtility.SaveFilePanel("Build Dedicated Server", "", "", "")
                : EditorUtility.SaveFolderPanel("Build Dedicated Server", "", "");

            if (string.IsNullOrEmpty(buildPath))
            {
                return null;
            }

            if (isWindows && !buildPath.EndsWith(SimulatorBuildOptions.BuildName + ".exe"))
            {
                var pathWithoutExtension = Path.Combine(Path.GetDirectoryName(buildPath),
                    Path.GetFileNameWithoutExtension(buildPath));
                if (!Directory.Exists(pathWithoutExtension))
                {
                    Directory.CreateDirectory(pathWithoutExtension);
                }

                buildPath = Path.Combine(pathWithoutExtension, SimulatorBuildOptions.BuildName + ".exe");
            }
            else if (isLinux && !buildPath.EndsWith(SimulatorBuildOptions.BuildName))
            {
                if (!Directory.Exists(buildPath))
                {
                    Directory.CreateDirectory(buildPath);
                }

                buildPath = Path.Combine(buildPath, SimulatorBuildOptions.BuildName);
            }

            return buildPath;
        }

        private static string GetLocalSimulatorExecutablePath(string filePath)
        {
            var isWindows =
                EditorUserBuildSettings.activeBuildTarget is BuildTarget.StandaloneWindows
                    or BuildTarget.StandaloneWindows64;
            var isMacOS = EditorUserBuildSettings.activeBuildTarget is BuildTarget.StandaloneOSX;

            if (!Path.HasExtension(filePath) || !isWindows)
            {
                return Path.Combine(filePath, isMacOS ? SimulatorBuildOptions.BuildName : "").Replace(
                    Path.DirectorySeparatorChar, '/');
            }

            var oldNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            if (string.Equals(oldNameWithoutExtension, SimulatorBuildOptions.BuildName))
            {
                return filePath;
            }

            var directory = Path.GetDirectoryName(filePath);
            var extension = Path.GetExtension(filePath);
            return (Path.Combine(directory, SimulatorBuildOptions.BuildName) + extension).Replace(
                Path.DirectorySeparatorChar, '/');
        }

        private static BuildPlayerOptions GetBuildPlayerOptions(bool devBuild, string[] scenes)
        {
            var defaultOptions = new BuildPlayerOptions();
            defaultOptions = BuildPlayerWindow.DefaultBuildMethods.GetBuildPlayerOptions(defaultOptions);

            if (devBuild)
            {
                defaultOptions.options |= BuildOptions.Development;
            }
            defaultOptions.scenes = scenes;
            return defaultOptions;
        }

        private static BuildOptions GetBuildOptions(bool devBuild)
        {
            BuildOptions buildOptions = BuildOptions.None;

            if (devBuild)
            {
                buildOptions |= BuildOptions.Development;
            }

            return buildOptions;
        }

        private static EditorBuildSettingsScene[] GetEditorBuildSettingsScenes(SimulatorBuildOptions options)
        {
            var editorBuildSettingsScenes = new List<EditorBuildSettingsScene>();
            foreach (var sceneAsset in options.ScenesToBuild)
            {
                string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
                if (!string.IsNullOrEmpty(scenePath))
                {
                    editorBuildSettingsScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                }
            }

            return editorBuildSettingsScenes.ToArray();
        }

        private static string[] GetSceneNames(SimulatorBuildOptions options)
        {
            var sceneNames = new List<string>();

            foreach (var scene in options.ScenesToBuild)
            {
                var scenePath = AssetDatabase.GetAssetPath(scene);
                sceneNames.Add(scenePath);
            }

            return sceneNames.ToArray();
        }

        private static bool CompressAndUpload(string simulatorSlug, ProjectInfo project)
        {
            EditorUtility.SetDirty(ProjectSettings.instance.RuntimeSettings);

            var path = Path.Combine(Application.temporaryCachePath, Paths.simulatorZipFile);

            Analytics.Capture(Analytics.Events.UploadSimStart);

            try
            {
                EditorUtility.DisplayProgressBar("Simulator", "Compressing simulator build path...", 1f);
                File.Delete(path);
                ZipUtils.Zip(Path.GetFullPath(SimulatorEditorUtility.FullBuildLocationPath), path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            try
            {
                var size = new FileInfo(path).Length;

                // request a valid upload endpoint
                var uurl = UploadURL.GetSimulator(size, project, simulatorSlug);
                if (uurl == null)
                {
                    return false;
                }

                // upload the simulator (zipfile)
                if (!uurl.Upload(path, size))
                {
                    return false;
                }

                // instruct the portal to deploy the uploaded simulator
                _ = uurl.RegisterSimulator();

                Analytics.Capture(Analytics.Events.UploadSimEnd);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            finally
            {
                File.Delete(path);
            }

            return true;
        }

        private static string GetCmdLineArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && args.Length > i + 1)
                {
                    return args[i + 1];
                }
            }
            return null;
        }


        private static void ChangeProductName()
        {
            EditorPrefs.SetString(ProductNameKey, PlayerSettings.productName);
            PlayerSettings.productName = SimulatorBuildOptions.BuildName;
        }

        private static void RestoreProductName()
        {
            var productName = EditorPrefs.GetString(ProductNameKey);
            PlayerSettings.productName = productName;
        }

        private static void DisplayError(string message, InteractionMode? interactionMode = null)
        {
            if (!Application.isBatchMode && interactionMode is InteractionMode.UserAction)
            {
                _ = EditorUtility.DisplayDialog("Simulator Error", message, "OK");
            }
            else
            {
                Debug.LogError(message);
            }
        }
    }
}
