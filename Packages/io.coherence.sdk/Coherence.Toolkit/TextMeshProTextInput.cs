// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if HAS_TEXT_MESH_PRO
namespace Coherence.Toolkit
{
    using System;
    using TMPro;
    using UnityEngine;

    /// <summary>
    /// Provides text input from a <see cref="TMP_InputField">TextMesh Pro Input Field</see>.
    /// </summary>
    [Serializable, DisplayName("From Input Field (TextMesh Pro)", "Get text input from a TextMesh Pro Text Field.")]
    internal sealed class TextMeshProTextInput : ITextInput
    {
        [SerializeField] private TMP_InputField inputField;

        public string Text
        {
            get => inputField ? inputField.text : null;

            set
            {
                if (inputField)
                {
                    inputField.text = value ?? "";
                }
            }
        }
    }
}
#endif
