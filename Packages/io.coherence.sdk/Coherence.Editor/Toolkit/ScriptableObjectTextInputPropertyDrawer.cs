// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Toolkit
{
    using Coherence.Toolkit;
    using UnityEditor;
    using UnityEditor.VersionControl;
    using UnityEngine;
    using static Coherence.Toolkit.ScriptableObjectTextInput;

    [CustomPropertyDrawer(typeof(ScriptableObjectTextInput))]
    internal class ScriptableObjectTextInputPropertyDrawer : TextInputPropertyDrawer
    {
        private static class GUIContents
        {
            public static readonly GUIContent Create = EditorGUIUtility.TrTextContent("Create");
        }

        private GUILayoutOption[] createButtonLayoutOptions;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            CorrectLayoutPosition();
            EditorGUILayout.BeginHorizontal();
            {
                DrawTypeDropdown(property, label);
                using var scriptableObjectProperty = property.FindPropertyRelative(Property.scriptableObject);
                EditorGUILayout.PropertyField(scriptableObjectProperty, GUIContent.none, false);

                if (!scriptableObjectProperty.objectReferenceValue)
                {
                    createButtonLayoutOptions ??= new[] { GUILayout.MaxWidth(EditorStyles.popup.CalcSize(GUIContents.Create).x) };
                    if (GUILayout.Button(GUIContents.Create, createButtonLayoutOptions))
                    {
                        var path = new AssetPath(EditorUtility.SaveFilePanelInProject(
                            "Create ScriptableObject Text",
                            "New ScriptableObject Text",
                            "asset",
                            "Please specify the path to save the ScriptableObject Text."));

                        if (path.IsPartOfAssetDatabase())
                        {
                            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ScriptableObjectText>(), path);
                            scriptableObjectProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObjectText>(path);
                            scriptableObjectProperty.serializedObject.ApplyModifiedProperties();
                        }

                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
