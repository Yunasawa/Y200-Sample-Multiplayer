// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;
    using static Coherence.Toolkit.StringTextInput;

    [CustomPropertyDrawer(typeof(StringTextInput))]
    internal class StringTextInputPropertyDrawer : TextInputPropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CorrectLayoutPosition();
            EditorGUILayout.BeginHorizontal();
            {
                DrawTypeDropdown(property, label);
                using var textProperty = property.FindPropertyRelative(Property.text);
                if (!ReferenceEquals(label, CoherenceCloudLoginEditor.GUIContents.Password))
                {
                    EditorGUILayout.PropertyField(textProperty, GUIContent.none, false);
                }
                else
                {
                    var setPassword = EditorGUILayout.PasswordField(GUIContent.none, textProperty.stringValue);
                    if (setPassword != textProperty.stringValue)
                    {
                        textProperty.stringValue = setPassword;
                        textProperty.serializedObject.ApplyModifiedProperties();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
