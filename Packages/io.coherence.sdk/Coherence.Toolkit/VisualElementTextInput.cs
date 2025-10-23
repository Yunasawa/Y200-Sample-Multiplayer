// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if HAS_UI_TOOLKIT
namespace Coherence.Toolkit
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Provides text input from a <see cref="TextField"/> in a <see cref="UIDocument"/>.
    /// </summary>
    [Serializable, DisplayName("From Text Field (UI Toolkit)", "Get text input from a Text Field in a UI Document.")]
    internal sealed class VisualElementTextInput : ITextInput
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField, HideInInspector] private RequirementType requirementType = RequirementType.ClassName;
        [SerializeField, HideInInspector] private string requiredValue = "unity-text-field";
        private INotifyValueChanged<string> cachedVisualElement;

        public string Text
        {
            get => GetVisualElement()?.value;

            set
            {
                if (GetVisualElement() is { } visualElement)
                {
                    visualElement.value = value ?? "";
                }
            }
        }

        private string RequiredName => requirementType is RequirementType.Name && string.IsNullOrEmpty(requiredValue) ? null : requiredValue;
        private string RequiredClassName => requirementType is RequirementType.ClassName && string.IsNullOrEmpty(requiredValue) ? null : requiredValue;

        [return: MaybeNull]
        private INotifyValueChanged<string> GetVisualElement()
        {
            if (cachedVisualElement is not null)
            {
#if UNITY_EDITOR
                if (cachedVisualElement is not VisualElement visualElement)
                {
                    cachedVisualElement = null;
                }
                else if (requirementType is RequirementType.Name && requiredValue is { Length: > 0 } && !string.Equals(visualElement.name, requiredValue))
                {
                    cachedVisualElement = null;
                }
                else if (requirementType is RequirementType.ClassName && requiredValue is { Length: > 0 } && !visualElement.GetClasses().Contains(requiredValue))
                {
                    cachedVisualElement = null;
                }
                else
#endif
                {
                    return cachedVisualElement;
                }
            }


            if (!uiDocument || uiDocument.rootVisualElement is null)
            {
                return cachedVisualElement;
            }

            if (TryFind<TextField>(out cachedVisualElement))
            {
                return cachedVisualElement;
            }

            if (TryFind<TextInputBaseField<string>>(out cachedVisualElement))
            {
                return cachedVisualElement;
            }

            if (TryFind<BaseField<string>>(out cachedVisualElement))
            {
                return cachedVisualElement;
            }

            if (TryFindVisualElement(out cachedVisualElement))
            {
                return cachedVisualElement;
            }

            return cachedVisualElement;

            bool TryFind<T>(out INotifyValueChanged<string> result) where T : VisualElement, INotifyValueChanged<string>
            {
                result = uiDocument.rootVisualElement.Q<T>(name: RequiredName, className: RequiredClassName);
                return result is not null;
            }

            bool TryFindVisualElement(out INotifyValueChanged<string> result)
            {
                result = uiDocument.rootVisualElement.Query<VisualElement>(name: RequiredName, className: RequiredClassName)
                    .Where(x => x is INotifyValueChanged<string>)
                    .First()?
                    .GetFirstOfType<INotifyValueChanged<string>>();
                return result is not null;
            }
        }

        internal enum RequirementType
        {
            [Tooltip("No name or USS class name required; find the first Text Field in the UI Document.")]
            Unconstrained,

            [Tooltip("Find the first Text Field with the given name in the UI Document.")]
            Name,

            [Tooltip("Find the first Text Field with the given USS class name in the UI Document.")]
            ClassName
        }

#if UNITY_EDITOR
        /// <summary>
        /// Contains names of serialized properties that can be used in the editor with SerializedObject.FindProperty etc.
        /// </summary>
        internal static class Property
        {
            public const string uiDocument = nameof(VisualElementTextInput.uiDocument);
            public const string requirementType = nameof(VisualElementTextInput.requirementType);
            public const string requiredValue = nameof(VisualElementTextInput.requiredValue);
        }
#endif
    }
}
#endif
