// Copyright (c) coherence ApS.
// See license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Log;
    using Portal;
    using UnityEngine;
    using Logger = Log.Logger;

    /// <summary>
    /// Utility class for uploading game builds to Coherence.
    /// </summary>
    /// <remarks>
    /// Supports uploading builds for Windows, WebGL, Linux, and macOS platforms.
    /// </remarks>
    [HelpURL("https://docs.coherence.io/v/1.8/manual/advanced-topics/team-workflows/continuous-integration-setup#game-build-pipeline")]
    public class UploadBuildToCoherence
    {
        private static readonly Logger logger = Log.GetLogger<UploadBuildToCoherence>();

        /// <summary>
        /// Uploads a Windows build to the specified project in coherence Cloud.
        /// </summary>
        /// <param name="path">The path to the directory containing the build.</param>
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
        public static bool UploadWindowsBuild(string path, string projectId, string projectToken) => UploadWindowsBuild(path, new() { id = projectId, portal_token = projectToken });

        /// <summary>
        /// Uploads a WebGL build to the specified project in coherence Cloud.
        /// </summary>
        /// <param name="path">The path to the directory containing the build.</param>
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
        public static bool UploadWebGlBuild(string path, string projectId, string projectToken) => UploadWebGlBuild(path, new() { id = projectId, portal_token = projectToken });

        /// <summary>
        /// Uploads a Linux build to the specified project in coherence Cloud.
        /// </summary>
        /// <param name="path">The path to the directory containing the build.</param>
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
        public static bool UploadLinuxBuild(string path, string projectId, string projectToken) => UploadLinuxBuild(path, new() { id = projectId, portal_token = projectToken });

        /// <summary>
        /// Uploads a macOS build to the specified project in coherence Cloud.
        /// </summary>
        /// <param name="path">The path to the directory containing the build.</param>
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
        public static bool UploadMacOsBuild(string path, string projectId, string projectToken) => UploadMacOsBuild(path, new() { id = projectId, portal_token = projectToken });

        /// <inheritdoc cref="UploadWindowsBuild(string, string, string)"/>
        public static bool UploadWindowsBuild(string path) => UploadBuildForPlatform(AvailablePlatforms.Windows, path);

        /// <inheritdoc cref="UploadWebGlBuild(string, string, string)"/>
        public static bool UploadWebGlBuild(string path) => UploadBuildForPlatform(AvailablePlatforms.WebGL, path);

        /// <inheritdoc cref="UploadLinuxBuild(string, string, string)"/>
        public static bool UploadLinuxBuild(string path) => UploadBuildForPlatform(AvailablePlatforms.Linux, path);

        /// <inheritdoc cref="UploadMacOsBuild(string, string, string)"/>
        public static bool UploadMacOsBuild(string path) => UploadBuildForPlatform(AvailablePlatforms.macOS, path);

        internal static bool UploadWindowsBuild(string path, ProjectInfo project) => UploadBuildForPlatform(AvailablePlatforms.Windows, path, project);
        internal static bool UploadWebGlBuild(string path, ProjectInfo project) => UploadBuildForPlatform(AvailablePlatforms.WebGL, path, project);
        internal static bool UploadLinuxBuild(string path, ProjectInfo project) => UploadBuildForPlatform(AvailablePlatforms.Linux, path, project);
        internal static bool UploadMacOsBuild(string path, ProjectInfo project) => UploadBuildForPlatform(AvailablePlatforms.macOS, path, project);

        private static bool UploadBuildForPlatform(AvailablePlatforms platform, string path)
        {
            ProjectInfo[] projects;
            if (ProjectSelectDialog.GetSelectedProjectIds() is { Length: > 0 } selectedProjectIds)
            {
                projects = selectedProjectIds.Select(ProjectSettings.instance.GetProject).ToArray();
            }
            else
            {
                projects = ProjectSettings.instance.Projects.ToArray();
            }

            return UploadBuildForPlatform(platform, path, projects);
        }

        private static bool UploadBuildForPlatform(AvailablePlatforms platform, string path, params ProjectInfo[] projects)
        {
            var validator = GetValidator(platform);
            if (!validator.Validate(path))
            {
                var message = validator.GetInfoString();
                logger.Warning(Warning.EditorBuildUploadValidator, message);
                return false;
            }

            var uploader = GetUploader(platform);
            var result = true;
            if (projects is { Length: > 0 })
            {
                foreach (var project in projects)
                {
                    result &= uploader.Upload(platform, path, project);
                }
            }
            else
            {
                result &= uploader.Upload(platform, path, null);
            }

            return result;
        }

        private static BuildPathValidator GetValidator(AvailablePlatforms platform) => platform switch
        {
            AvailablePlatforms.Windows => new WindowsPathValidator(),
            AvailablePlatforms.Linux => new LinuxPathValidator(),
            AvailablePlatforms.WebGL => new WebGLPathValidator(),
            AvailablePlatforms.macOS => new MacOSPathValidator(),
            _ => null
        };

        private static BuildUploader GetUploader(AvailablePlatforms platform) => platform switch
        {
            AvailablePlatforms.Linux or AvailablePlatforms.Windows => new DefaultUploader(),
            AvailablePlatforms.WebGL => new WebGLUploader(),
            AvailablePlatforms.macOS => new MacOSUploader(),
            _ => null
        };
    }
}
