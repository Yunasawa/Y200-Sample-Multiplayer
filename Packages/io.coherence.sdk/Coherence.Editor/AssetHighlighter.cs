namespace Coherence.Editor
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Pings and selects an asset after recompilation
    /// </summary>
    internal static class AssetHighlighter
    {
        private const string KeyAssetHighlightPath = "UIUtils.AssetHightlightPath";

        static AssetHighlighter()
        {
            ResolveDelayedHighlightPath();
        }

        /// <remarks>
        /// Only one asset can be highlighted at a time.
        /// </remarks>
        /// <param name="path">AssetDatabase compatible path (e.g., "Assets/MyFolder/MyAsset.asset")</param>
        public static void HighlightAfterRecompile(string path)
        {
            SessionState.SetString(KeyAssetHighlightPath, path);
        }

        private static void ResolveDelayedHighlightPath()
        {
            var path = SessionState.GetString(KeyAssetHighlightPath, null);
            SessionState.EraseString(KeyAssetHighlightPath);
            Highlight(path);
        }

        /// <param name="path">AssetDatabase compatible path (e.g., "Assets/MyFolder/MyAsset.asset")</param>
        public static void Highlight(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            Highlight(obj);
        }

        public static void Highlight(Object obj)
        {
            if (!obj)
            {
                return;
            }

            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }
    }
}
