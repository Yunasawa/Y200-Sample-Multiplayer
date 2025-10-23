// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using System;
    using UnityEngine;

    [Serializable, DisplayName("String", "Constant string input.")]
    internal sealed class StringTextInput : ITextInput
    {
        [SerializeField] private string text = "";

        public string Text
        {
            get => text;
            set => text = value;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Contains names of serialized properties that can be used in the editor with SerializedObject.FindProperty etc.
        /// </summary>
        internal static class Property
        {
            public const string text = nameof(StringTextInput.text);
        }
#endif
    }
}
