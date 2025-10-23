// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if HAS_UI_TOOLKIT
namespace Coherence.Editor.Toolkit
{
    using System;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;
    using static Coherence.Toolkit.VisualElementTextInput;

    [CustomPropertyDrawer(typeof(VisualElementTextInput))]
    internal class VisualElementTextInputPropertyDrawer : TextInputPropertyDrawer
    {
        private const float PopupContentPadding = 3f;

        private readonly GUILayoutOption[] oneGuiLayoutOption = new GUILayoutOption[1];

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CorrectLayoutPosition();
            DrawTypeDropdown(property, label);
            EditorGUI.indentLevel++;
            using var uiDocumentProperty = property.FindPropertyRelative(Property.uiDocument);
            EditorGUILayout.PropertyField(uiDocumentProperty, GUIContents.UiDocument);
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.PrefixLabel(GUIContents.Element);
                EditorGUI.indentLevel--;

                using var requirementTypeProperty = property.FindPropertyRelative(Property.requirementType);
                var requirementType = (RequirementType)requirementTypeProperty.intValue;
                EditorGUILayout.PropertyField(requirementTypeProperty, GUIContent.none, GetPopupGuiLayoutOptions(requirementType));

                if (requirementType is not RequirementType.Unconstrained)
                {
                    using var nameProperty = property.FindPropertyRelative(Property.requiredValue);
                    EditorGUILayout.PropertyField(nameProperty, GUIContent.none);
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayoutOption[] GetPopupGuiLayoutOptions(RequirementType requirementType)
            {
                if (requirementType is RequirementType.Unconstrained)
                {
                    return Array.Empty<GUILayoutOption>();
                }

                var popupWidth = EditorStyles.popup.CalcSize(new(requirementType.ToString())).x + PopupContentPadding;
                oneGuiLayoutOption[0] = GUILayout.Width(popupWidth);
                return oneGuiLayoutOption;
            }
        }

        private static class GUIContents
        {
            public static readonly GUIContent UiDocument = new("UI Document", "The UI Document that contains the visual element to find.");
            public static readonly GUIContent Element = new("Text Field", "The visual element to find in the UI Document.\n\nThe visual element has to be of type TextField or some other type that implements the INotifyValueChanged<string> interface.");
        }
    }
}
#endif
