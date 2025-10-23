// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Represents an object that can provide <see langword="string"/> input.
    /// </summary>
    internal interface ITextInput
    {
        /// <summary>
        /// The provided text input.
        /// </summary>
        /// <remarks>
        /// <see langword="null"/> if can not acquire the text input at this time.
        /// </remarks>
        [MaybeNull]
        string Text { get; set; }
    }
}
