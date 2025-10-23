// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using UI;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.PackageManager.UI;
    using UnityEngine;
    using PackageInfo = UnityEditor.PackageManager.PackageInfo;

    internal static class UIUtils
    {
        private static readonly string assetFilter = "t:" + typeof(SampleAsset).FullName;
        private static Sample[] samples;
        private static SampleAsset[] sampleAssets;

        public static Sample[] Samples => samples ?? CacheSamples();
        public static SampleAsset[] SampleAssets => sampleAssets ?? CacheSampleAssets();

        internal static void ClearSamplesCache()
        {
            samples = null;
        }

        internal static Sample[] CacheSamples()
        {
            var packageInfo = PackageInfo.FindForAssetPath(Paths.packageManifestPath);
            samples = Sample.FindByPackage(Paths.packageId, packageInfo.version).ToArray();
            return samples;
        }

        internal static SampleAsset[] CacheSampleAssets()
        {
            sampleAssets = AssetDatabase.FindAssets(assetFilter)
                .Select(guid => AssetDatabase.LoadAssetAtPath<SampleAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            return sampleAssets;
        }

        public static IEnumerable<string> GetSampleImports(Sample sample, params string[] alternativeFileNames)
        {
            if (IsUnityPackageSample(sample))
            {
                return Enumerable.Empty<string>();
            }

            var importPath = sample.importPath;
            if (string.IsNullOrEmpty(importPath))
            {
                return Enumerable.Empty<string>();
            }

            var parentDirectory = Directory.GetParent(importPath)?.Parent;
            if (parentDirectory is not { Exists: true, })
            {
                return Enumerable.Empty<string>();
            }

            var fileNames = (alternativeFileNames ?? Enumerable.Empty<string>())
                .Append(Path.GetFileName(importPath));

            return parentDirectory.GetDirectories()
                .SelectMany(dir => fileNames.Select(fileName => Path.Combine(dir.ToString(), fileName)))
                .Where(Directory.Exists);
        }

        public static bool IsUnityPackageSample(Sample sample)
        {
            return Directory.GetFiles(sample.resolvedPath, "*.unitypackage").Length == 1;
        }

        public static bool TryGetSample(string sampleDisplayName, out Sample sample)
        {
            try
            {
                sample = Samples.First(sample => sample.displayName == sampleDisplayName);
                return true;
            }
            catch
            {
                sample = default;
                return false;
            }
        }

        public static bool HasImportableSample(SampleAsset asset)
        {
            return asset && TryGetSample(asset.SampleDisplayName, out _);
        }

        public static bool ImportSample(SampleAsset asset)
        {
            return ImportSample(asset, out _);
        }

        public static bool ImportSample(SampleAsset asset, out Sample sample)
        {
            if (!TryGetSample(asset.SampleDisplayName, out sample))
            {
                return false;
            }

            var imported = ImportSample(sample, asset);
            if (imported)
            {
                if (IsUnityPackageSample(sample))
                {
                    AssetHighlighter.HighlightAfterRecompile(asset.AssetHighlightPath);
                }
                else
                {
                    var path = PathUtils.GetRelativePath(sample.importPath);
                    var assetHighlightPath = PathUtils.GetRelativePath(Path.Combine(sample.importPath, asset.AssetHighlightPath));
                    var hasAssetHighlight = File.Exists(assetHighlightPath);
                    AssetHighlighter.Highlight(hasAssetHighlight ? assetHighlightPath : path);
                }
            }

            return imported;
        }

        public static bool ImportSample(Sample sample, SampleAsset asset)
        {
            if (sample.isImported)
            {
                if (!EditorUtility.DisplayDialog("Update Sample?",
                        $"This sample is already imported at\n\n{PathUtils.GetRelativePath(sample.importPath)}\n\nImporting again will override all changes you have made to it. Are you sure you want to continue?",
                        "Yes", "No"))
                {
                    return false;
                }
            }
            else
            {
                var imports = GetSampleImports(sample, asset.PreviousSampleDisplayNames).ToArray();
                if (imports.Length > 0)
                {
                    if (!EditorUtility.DisplayDialog("Update Sample?",
                            $"A different version of the sample is already imported at\n\n{PathUtils.GetRelativePath(imports[0])}\n\nIt will be moved to the trash when you update. Are you sure you want to continue?",
                            "Yes", "No"))
                    {
                        return false;
                    }

                    AssetDatabase.StartAssetEditing();
                    foreach (var import in imports)
                    {
                        var path = PathUtils.GetRelativePath(import);
                        AssetDatabase.MoveAssetToTrash(path);

                        // Delete version folder (Samples/coherence/<version>) if empty
                        var parent = Directory.GetParent(path)?.FullName;
                        if (Directory.GetFileSystemEntries(parent).Length == 0)
                        {
                            var parentRel = PathUtils.GetRelativePath(parent);
                            AssetDatabase.DeleteAsset(parentRel);
                        }
                    }
                    AssetDatabase.StopAssetEditing();
                }
                else if (asset && !string.IsNullOrEmpty(asset.PreInstallMessage))
                {
                    if (!EditorUtility.DisplayDialog("Importing Sample: " + asset.Name, asset.PreInstallMessage + "\n\nDo you want to continue?", "Continue", "Cancel"))
                    {
                        return false;
                    }
                }
            }

            return sample.Import(Sample.ImportOptions.OverridePreviousImports);
        }

        public static string GetEnumLabel(Enum enumValue)
        {
            var type = enumValue.GetType();
            var name = enumValue.ToString();

            var field = type.GetField(name);
            if (field == null)
            {
                return ObjectNames.NicifyVariableName(name);
            }

            var inspectorNameAttr = field.GetCustomAttribute(typeof(InspectorNameAttribute), false) as InspectorNameAttribute;
            return inspectorNameAttr != null ? inspectorNameAttr.displayName : ObjectNames.NicifyVariableName(name);
        }

        public static void DrawCustomEnumPopup<T>(Rect rect, GUIContent label, SerializedProperty property, params (T value, string displayName)[] enumOptions)
            where T : Enum
        {
            var currentValue = (T)(object)property.intValue;

            var selectedIndex = Array.FindIndex(enumOptions, e => e.value.Equals(currentValue));

            var newIndex = EditorGUI.Popup(rect, label, selectedIndex, enumOptions.Select(e => new GUIContent(e.displayName)).ToArray());

            if (newIndex != selectedIndex)
            {
                property.intValue = (int)(object)enumOptions[newIndex].value;
            }
        }
    }
}
