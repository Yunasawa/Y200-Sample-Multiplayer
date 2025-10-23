// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using Portal;
    using UnityEditor;
    using UnityEngine;

    internal class MacOSUploader : BuildUploader
    {
        protected internal override string GetMessage(string buildPath) => $"The Application Bundle in the following path will be compressed and uploaded. Are you sure?\n\n{buildPath}";

        internal override bool Upload(AvailablePlatforms platform, string buildPath, [MaybeNull] ProjectInfo project)
        {
            string path = GetZipTempPath();
            string platformAsString = platform.ToString().ToLowerInvariant();

            OnUploadStart(platformAsString);

            try
            {
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayProgressBar("Game", "Compressing game build path...", 1f);
                }

                File.Delete(path);

                ZipUtils.Zip(buildPath, path, false);
            }
            catch (System.Exception e)
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
                long size = new FileInfo(path).Length;

                // request a valid upload endpoint
                var uurl = UploadURL.GetGame(size, platformAsString, project);
                if (uurl == null)
                {
                    return false;
                }

                // upload the game (zipfile)
                if (!uurl.Upload(path, size))
                {
                    return false;
                }

                _ = Portal.UploadURL.RegisterBuild(platform: platformAsString, filename: "", project);

                OnUploadEnd(platformAsString);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            return true;
        }
    }
}
