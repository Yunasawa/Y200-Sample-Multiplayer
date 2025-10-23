// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

// See SamplesModule.cs for an explanation on why we
// If this condition is changed, update SamplesModule.cs too.
#if UNITY_6000_1_OR_NEWER
#define USE_TABVIEW
#endif
namespace Coherence.UI
{
    using System;
    using UnityEngine;

    [Serializable]
    [ExcludeFromPreset]
    internal class SampleAsset : ScriptableObject, IGridItem
    {
        [Serializable]
        public struct Link
        {
            public string Name;
            [Tooltip("Standard URL or 'docs://{DocumentationKey}' for documentation links.\nExample: 'docs://DeveloperPortalOverview'.\nCheck DocumentationLinks.cs for available keys.")]
            public string Url;

            public string GetResolvedUrl()
            {
                const string prefix = "docs://";
                var idx = Url.IndexOf(prefix, StringComparison.InvariantCulture);
                if (idx == -1)
                {
                    return Url;
                }

                var keyString = Url[(idx + prefix.Length)..];
                if (Enum.TryParse(typeof(DocumentationKeys), keyString, out var val))
                {
                    var key = (DocumentationKeys)val;
                    return DocumentationLinks.GetDocsUrl(key);
                }

                return Url;
            }
        }

        public bool Enabled = true;
        public string Name;
        public string SampleDisplayName;
        public string[] PreviousSampleDisplayNames;
        public string AssetHighlightPath;
        public string OpenAssetPath;
        public string Category;
        public string Description;
        public Texture2D Thumbnail;
        public Link[] Links;
        public int MainLink;
        public string PreInstallMessage;

        public int Priority;

        public int Id => GetHashCode();
        public Texture2D Icon => Thumbnail;
        public string Label =>
#if USE_TABVIEW
            Name;
#else
            $"{Category}: {Name}";
#endif
    }
}
