// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System.Runtime.CompilerServices;

    internal static class RichText
    {
        /// <summary>
        /// Highlights the specified text with the coherence blue color (#29abe2) using rich text tags.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Highlight(string text) => $"<color=#29abe2>{text ?? "n/a"}</color>";

        /// <summary>
        /// Highlights the specified text with the coherence blue color (#29abe2) using rich text tags.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Highlight(object @object) => Highlight(@object?.ToString());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Bold(string text) => $"<b>{text}</b>";
    }
}
