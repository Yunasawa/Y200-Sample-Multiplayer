// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Portal;
    using UnityEditor;
    using UnityEngine;
    using Toolkit;

    internal class GameBuildWindow : EditorWindow
    {
        private static string buildPath;
        private static AvailablePlatforms platformSelected = 0;

        private static bool validBuildPath;

        private static string CompanyAndProductName => $"{PlayerSettings.companyName}.{PlayerSettings.productName}";
        private static string BuildPathPrefix => $"Coherence.{CompanyAndProductName}.Build.Path";
        private static string BuildPlatformKey => $"Coherence.{CompanyAndProductName}.Build.Platform";

        private class GUIContents
        {
            public static readonly GUIContent title = Icons.GetContentWithText("EditorWindow", "Build Upload");
            public static readonly GUIContent uploadButton = EditorGUIUtility.TrTextContent("Upload");

            public static readonly GUIContent[] toolbarButtons = new GUIContent[]
            {
                uploadButton,
            };
        }

        private int toolbarSelected;

        private System.Action[] toolbarFns;

        private readonly static Dictionary<AvailablePlatforms, BuildPathValidator> buildPathValidators = new Dictionary<AvailablePlatforms, BuildPathValidator>()
        {
            { AvailablePlatforms.Linux, new LinuxPathValidator() },
            { AvailablePlatforms.macOS, new MacOSPathValidator() },
            { AvailablePlatforms.Windows, new WindowsPathValidator() },
            { AvailablePlatforms.WebGL, new WebGLPathValidator() }
        };

        private readonly static Dictionary<AvailablePlatforms, BuildUploader> buildUploaders = new Dictionary<AvailablePlatforms, BuildUploader>()
        {
            { AvailablePlatforms.Linux, new DefaultUploader() },
            { AvailablePlatforms.macOS, new MacOSUploader() },
            { AvailablePlatforms.Windows, new DefaultUploader() },
            { AvailablePlatforms.WebGL, new WebGLUploader() }
        };

        private GameBuildWindow()
        {
            toolbarFns = new System.Action[]
            {
                OnUploadGUI,
            };
        }

        internal static void RestoreSavedBuildSettings()
        {
            platformSelected = LoadSelectedBuildPlatform();
            _ = ProjectSelectDialog.GetSelectedProjectIds();
            buildPath = GetBuildPathForSelectedPlatform();
            UpdateBuildPathValidity();
        }

        private Vector2 infoScroll;

        internal static void DrawShareBuildGUI(EditorWindow window)
        {
            var uploadType = platformSelected == AvailablePlatforms.macOS ? "'.app'" : "folder";

            CoherenceHubLayout.DrawInfoLabel((ProjectSettings.instance.GetValidAndDistinctProjects().Count() > 1
                ? $"The selected {uploadType} will be compressed and uploaded to the projects you choose in coherence Cloud."
                : $"The selected {uploadType} will be compressed and uploaded to your selected project in coherence Cloud.")
                + "\nDepending on the size of the build, this step might take a few minutes to complete.");

            using (var change = new EditorGUI.ChangeCheckScope())
            {
                platformSelected = (AvailablePlatforms)EditorGUILayout.Popup("Platform", (int)platformSelected, Enum.GetNames(typeof(AvailablePlatforms)));

                if (change.changed)
                {
                    SaveSelectedBuildPlatform(platformSelected);
                    buildPath = GetBuildPathForSelectedPlatform();
                }
            }

            CoherenceHubLayout.DrawDiskPath(buildPath, "Select path...", GetBuildSelector(), (newPath) =>
            {
                buildPath = newPath;
                SetBuildPathForSelectedPlatform(buildPath);
                GUIUtility.ExitGUI();
            });

            if (!buildPathValidators[platformSelected].Validate(buildPath))
            {
                var infoString = buildPathValidators[platformSelected].GetInfoString();
                CoherenceHubLayout.DrawMessageArea(infoString);
            }

            UpdateBuildPathValidity();
            var tooltip = string.Empty;

            if (!validBuildPath)
            {
                tooltip = "Build path is invalid.";
            }
            else if (!PortalUtil.CanCommunicateWithPortal)
            {
                tooltip = "You need to login to upload builds.";
            }

            if (!PortalUtil.OrgAndProjectIsSet)
            {
                tooltip = "Organization and project must be set to upload simulator builds to the cloud.";
            }

            CoherenceHubLayout.DrawCloudDependantButton(new("Upload Game Build to Cloud"), () =>
            {
                if (!validBuildPath)
                {
                    Debug.LogError("Build path is invalid.");
                    buildPath = string.Empty;
                    return;
                }

                var uploader = buildUploaders[platformSelected];

                var projectOptions = ProjectSettings.instance.GetValidAndDistinctProjects().ToArray();
                ProjectInfo[] selectedProjects;
                var interactionMode = InteractionMode.UserAction;
                if (projectOptions.Length > 1)
                {
                    selectedProjects = ProjectSelectDialog.Open
                    (
                        title: uploader.DialogTitle,
                        message: uploader.GetMessage(RichText.Bold(buildPath)),
                        confirmButtonText: uploader.OkButton,
                        cancelButtonText: uploader.CancelButton,
                        context: window,
                        allowMultiple: false
                    );

                    if (!selectedProjects.Any())
                    {
                        return;
                    }

                    // Don't display any additional confirmation dialogs to the user.
                    interactionMode = InteractionMode.AutomatedAction;
                }
                else
                {
                    selectedProjects = projectOptions;
                }

                if (interactionMode is not InteractionMode.AutomatedAction
                    && !uploader.AllowUpload(buildPath))
                {
                    return;
                }

                foreach (var project in selectedProjects)
                {
                    uploader.Upload(platformSelected, buildPath, project);
                }

                GUIUtility.ExitGUI();
            }, tooltip, DisableConditions, ContentUtils.GUIStyles.bigButton);
        }

        private static string BuildPathKeyForSelectedPlatform => $"{BuildPathPrefix}.{platformSelected}";

        private static string GetBuildPathForSelectedPlatform() => EditorPrefs.GetString(BuildPathKeyForSelectedPlatform, string.Empty);

        private static void SetBuildPathForSelectedPlatform(string value) => EditorPrefs.SetString(BuildPathKeyForSelectedPlatform, value);

        private static AvailablePlatforms LoadSelectedBuildPlatform()
        {
            var defaultPlatform = SystemInfo.operatingSystemFamily switch
            {
                OperatingSystemFamily.MacOSX => AvailablePlatforms.macOS,
                OperatingSystemFamily.Linux => AvailablePlatforms.Linux,
                _ => AvailablePlatforms.Windows
            };

            return (AvailablePlatforms)EditorPrefs.GetInt(BuildPlatformKey, (int)defaultPlatform);
        }

        private static void SaveSelectedBuildPlatform(AvailablePlatforms platform) => EditorPrefs.SetInt(BuildPlatformKey, (int)platform);

        private static Func<string> GetBuildSelector()
        {
#if UNITY_EDITOR_OSX
            return platformSelected == AvailablePlatforms.macOS ? (Func<string>)OpenFilePanel : OpenFolderPanel;
#else
            return OpenFolderPanel;
#endif
        }

        private static bool DisableConditions() => !PortalUtil.OrgAndAtLeastOneProjectIsSet || !validBuildPath;

        private static string OpenFolderPanel()
        {
            return EditorUtility.OpenFolderPanel("Select a game build folder", "Builds", string.Empty);
        }

        private static string OpenFilePanel()
        {
            return EditorUtility.OpenFilePanel("Select a macOS app", "Builds", ".app");
        }


        internal static void Init()
        {
            _ = GetWindow<GameBuildWindow>();
        }

        private void OnEnable()
        {
            titleContent = GUIContents.title;
        }

        private void OnFocus()
        {
            UpdateBuildPathValidity();
        }

        private void OnUploadGUI()
        {
            CoherenceHeader.OnSlimHeader(string.Empty);

            DrawShareBuildGUI(this);
        }

        private void OnGUI()
        {
            _ = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            GUILayout.Space(4);
            toolbarSelected = GUILayout.Toolbar(toolbarSelected, GUIContents.toolbarButtons, EditorStyles.toolbarButton, GUI.ToolbarButtonSize.FitToContents);
            if (EditorGUI.EndChangeCheck())
            {
            }
            EditorGUILayout.EndHorizontal();

            toolbarFns[toolbarSelected]();
        }

        private static void UpdateBuildPathValidity()
        {
            validBuildPath = buildPathValidators[platformSelected].Validate(buildPath);
        }
    }
}
