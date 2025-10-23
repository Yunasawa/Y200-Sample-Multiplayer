// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using System;
    using System.Linq;
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(ITextInput), useForChildren: true)]
    internal class TextInputPropertyDrawer : PropertyDrawer
    {
        private readonly GUILayoutOption[] guiLayoutOption = new GUILayoutOption[1];
        private static GUIContent[] typeOptionsLabels;
        private static Type[] typeOptions;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CorrectLayoutPosition();
            DrawTypeDropdown(property, label);
            EditorGUI.indentLevel++;
            DrawProperties(property);
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Undo vertical space reserved for the property, so that EditorGUILayout draws the first element at the correct position.
        /// </summary>
        protected virtual void CorrectLayoutPosition() => GUILayout.Space(-20f);

        /// <summary>
        /// Draw popup that allows selecting the type of <see cref="ITextInput"/> to use for the property.
        /// </summary>
        protected virtual void DrawTypeDropdown(SerializedProperty property, GUIContent label)
        {
            if (typeOptions is null)
            {
                typeOptions = TypeCache.GetTypesDerivedFrom<ITextInput>().ToArray();
                Array.Sort(typeOptions, (x, y) => x == typeof(StringTextInput) ? -1 : y == typeof(StringTextInput) ? 1 : 0);
                var removeSuffix = nameof(ITextInput)[1..];
                typeOptionsLabels = typeOptions.Select(Selector).ToArray();
                GUIContent Selector(Type type) => DisplayNames.Get(type, removeSuffix);
            }

            var currentTypeIndex = property.managedReferenceValue is ITextInput textInput ? Array.IndexOf(typeOptions, textInput.GetType()) : -1;
            EditorGUILayout.PrefixLabel(label);
            guiLayoutOption[0] = GUILayout.Width(EditorStyles.popup.CalcSize(typeOptionsLabels[currentTypeIndex]).x + 2f);
            var setTypeIndex = EditorGUILayout.Popup(GUIContent.none, currentTypeIndex, typeOptionsLabels, guiLayoutOption);
            if (setTypeIndex != currentTypeIndex)
            {
                var setType = typeOptions[setTypeIndex];
                if (setType is null)
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    property.managedReferenceValue = Activator.CreateInstance(setType);
                }

                property.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        protected virtual void DrawProperties(SerializedProperty property)
        {
            var currentProperty = property.Copy();
            var nextSiblingProperty = property.Copy();
            nextSiblingProperty.Next(false);
            if (currentProperty.Next(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                    {
                        break;
                    }

                    EditorGUILayout.PropertyField(currentProperty, true);
                }
                while (currentProperty.Next(false));
            }
        }
    }
}
