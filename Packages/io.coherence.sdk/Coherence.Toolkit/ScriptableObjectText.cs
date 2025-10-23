// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit
{
    using UnityEngine;

    /// <summary>
    /// ScriptableObject that holds a text string.
    /// </summary>
    public class ScriptableObjectText : ScriptableObject
    {
        [SerializeField] private string text;

        public string Text
        {
            get => text;
            set => text = value ?? string.Empty;
        }
    }
}
