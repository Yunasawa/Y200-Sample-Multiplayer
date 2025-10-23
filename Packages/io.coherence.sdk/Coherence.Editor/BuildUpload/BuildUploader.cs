// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using Portal;
    using UnityEditor;
    using UnityEngine;

    internal abstract class BuildUploader
    {
        private class BuildUploadEventProperties : Analytics.BaseProperties
        {
            public string platform;
        }

        protected internal virtual string DialogTitle => "Compress and upload?";
        protected internal virtual string GetMessage(string buildPath) => $"Contents in the following path will be compressed and uploaded into the selected project. Are you sure?\n\n{buildPath}";
        protected internal virtual string OkButton => "Compress and upload";
        protected internal virtual string CancelButton => "Cancel";

        internal abstract bool Upload(AvailablePlatforms platform, string buildPath, [MaybeNull] ProjectInfo project);

        internal bool AllowUpload(string buildPath)
        {
            return EditorUtility.DisplayDialog(DialogTitle, GetMessage(buildPath), OkButton, CancelButton);
        }

        protected string GetZipTempPath()
        {
            return Path.Combine(Application.temporaryCachePath, Paths.gameZipFile);
        }

        protected void OnUploadStart(string platform)
        {
            Analytics.Capture(new Analytics.Event<BuildUploadEventProperties>(
                Analytics.Events.UploadStart,
                new BuildUploadEventProperties {
                    platform = platform
                }
            ));
        }

        protected void OnUploadEnd(string platform)
        {
            Analytics.Capture(new Analytics.Event<BuildUploadEventProperties>(
                Analytics.Events.UploadEnd,
                new BuildUploadEventProperties {
                    platform = platform
                }
            ));
        }
    }
}
