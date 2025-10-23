// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Build
{
    using Editor;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Compilation;
    using UnityEngine;

    internal static class BuildSettingsRestorer
    {
        internal static void RestorePreviousBuildSettings(BuildTarget previousBuildTarget, int buildsSucceeded, int buildsAttempted)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (EditorUtility.DisplayDialog("Restore Previous Build Settings", buildsAttempted switch
            {
                    0 => "Headless Linux Client build failed. Do you wish to restore your previous build settings?",
                    1 when buildsSucceeded is 1 => "Headless Linux Client has been uploaded successfully. Do you wish to restore your previous build settings?",
                    1 => "Headless Linux Client build failed. Do you wish to restore your previous build settings?",
                    _ when buildsSucceeded is 0 => "All Headless Linux Client builds failed. Do you wish to restore your previous build settings?",
                    _ when buildsSucceeded == buildsAttempted => "Headless Linux Clients have been uploaded successfully. Do you wish to restore your previous build settings?",
                    _ => $"{buildsSucceeded}/{buildsAttempted} Headless Linux Clients have been uploaded successfully. Do you wish to restore your previous build settings?"
            }, "Ok! Go Ahead", "Cancel"))
            {
                var options = SimulatorBuildOptions.Get();
                var supportsHeadlessLinuxBuild =
                    SimulatorEditorUtility.IsBuildTargetSupported(true, options.ScriptingImplementation);

                ScriptingSymbolsChanger.RestoreScriptingSymbols(supportsHeadlessLinuxBuild ? NamedBuildTarget.Server : NamedBuildTarget.Standalone);
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

                if (previousBuildTarget != BuildTarget.NoTarget && EditorUserBuildSettings.activeBuildTarget != previousBuildTarget)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildPipeline.GetBuildTargetGroup(previousBuildTarget),
                        previousBuildTarget);;
                }

                CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
                SimulatorBuildPipeline.RestoreTransportType();
                AssetDatabase.SaveAssets();
            }
        }

        internal static void RestorePreviousBuildSettingsForLocalBuild()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (EditorUtility.DisplayDialog("Restore Previous Build Settings",
                    "Do you wish to remove the COHERENCE_SIMULATOR scripting symbol?",
                    "Ok! Go Ahead", "Cancel"))
            {
                ScriptingSymbolsChanger.RestoreScriptingSymbols(EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Server ? NamedBuildTarget.Server : NamedBuildTarget.Standalone);
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
            }
        }

    }
}


