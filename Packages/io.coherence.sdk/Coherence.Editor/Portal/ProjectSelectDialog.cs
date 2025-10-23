// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// The <see cref="Open"/> allows users to specify which projects should be targeted by project-specific
    /// operations when <see cref="ProjectSettings.UsingMultipleProjects"/> is enabled.
    /// </summary>
    internal class ProjectSelectDialog : EditorWindow
    {
        private const float Padding = 10f;
        private const float Width = 400f;
        private const float MinHeight = 130f;
        private static string[] selectedProjectIds;
        private const string SelectedProjectIdsSeparator = ";";

        private readonly GUILayoutOption[]  buttonHeight = { GUILayout.Height(30f) };
        private ProjectInfo[] projects;
        private GUIContent[] projectLabels;
        private GUIContent message;
        private GUIContent confirmButtonLabel;
        private GUIContent cancelButtonLabel;
        private bool allowMultiple;
        private bool[] selections;
        private bool wasConfirmed;
        [NonSerialized] private GUIStyle checkboxStyle;
        [NonSerialized] private GUIStyle radioButtonLabelStyle;
        [NonSerialized] private GUIStyle messageStyle;
        [NonSerialized] private GUIStyle buttonStyle;

        public static ProjectInfo[] Open(string title = "Select projects to target", string message = "Select target projects", EditorWindow context = null, Vector2? position = null, string confirmButtonText = "OK", string cancelButtonText = "Cancel", bool allowMultiple = true, params object[] messageParams)
        {
            if (!position.HasValue)
            {
                if (context)
                {
                    position = context.position.position + context.position.size * 0.5f - new Vector2(Width * 0.5f, MinHeight * 0.5f);
                }
                else
                {
                    var resolution = Screen.currentResolution;
                    position = new Vector2(resolution.width * 0.5f - Width * 0.5f, resolution.height * 0.5f - MinHeight * 0.5f);
                }
            }

            var projectSettings = ProjectSettings.instance;

            var projectsIds = projectSettings.MultipleProjects
                .Select(x => x.Project.id)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToArray();

            var conditionalProjects = projectsIds.Select(x => projectSettings.GetConditionalProject(x)).ToArray();
            var window = CreateInstance<ProjectSelectDialog>();
            window.titleContent = new(title);
            window.message = new(messageParams is { Length: > 0 } ? string.Format(message, messageParams.Select(RichText.Highlight).ToArray()) : message);
            window.projects = conditionalProjects.Select(x => x.Project).ToArray();
            window.projectLabels = conditionalProjects.Select(x => new GUIContent(GetDisplayName(x))).ToArray();
            window.selections = new bool[window.projectLabels.Length];
            foreach (var selectedProjectId in GetSelectedProjectIds())
            {
                var index = Array.IndexOf(projectsIds, selectedProjectId);
                if (index != -1)
                {
                    window.selections[index] = true;
                }
            }

            if (!allowMultiple)
            {
                // Make sure no more than one project is selected
                var anySelected = false;
                for (var i = 0; i < window.selections.Length; i++)
                {
                    if (anySelected)
                    {
                        window.selections[i] = false;
                    }
                    else
                    {
                        anySelected = window.selections[i];
                    }
                }

                // Make sure at least one project is selected
                if (!anySelected && window.selections.Length > 0)
                {
                    window.selections[0] = true;
                }
            }

            window.confirmButtonLabel = new($"<b>{confirmButtonText}</b>");
            window.cancelButtonLabel = new(cancelButtonText);
            window.allowMultiple = allowMultiple;

            var size = new Vector2(Width, MinHeight);
            window.minSize = size;
            window.maxSize = size;
            if (position.HasValue)
            {
                window.position = new(position: position.Value, size: size);
            }

            window.ShowModalUtility();
            return window.wasConfirmed ? window.projects.Where((_, index) => window.selections[index]).ToArray() : Array.Empty<ProjectInfo>();
        }

        internal static string[] GetSelectedProjectIds() => selectedProjectIds ??= LoadSelectedProjectIds();

        internal static void SaveSelectedProjectIds(string[] projectIds)
        {
            selectedProjectIds = projectIds;
            EditorPrefs.SetString(GetBuildProjectKey(), string.Join(SelectedProjectIdsSeparator, projectIds));
        }

        private static string[] LoadSelectedProjectIds()
        {
            var projectSettings = ProjectSettings.instance;
            if (EditorPrefs.GetString(GetBuildProjectKey(), "") is { Length: > 0 } projectIdsString
                && projectIdsString.Split(SelectedProjectIdsSeparator, StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } projectIds)
            {
                return projectIds.Where(x => projectSettings.ProjectIds.Contains(x)).ToArray();
            }

            return projectSettings.ProjectIds.ToArray();
        }

        private static string GetBuildProjectKey() => $"Coherence.{PlayerSettings.companyName}.{PlayerSettings.productName}.Build.Project";

        private void OnGUI()
        {
            DrawWithPadding(DrawInfoText);
            DrawWithPadding(allowMultiple ? DrawProjectCheckboxes : DrawProjectRadioButtons);
            DrawWithPadding(DrawConfirmAndCancelButtons, drawHorizontally: true);
            GUILayout.Space(Padding);
        }

        private void DrawWithPadding(Action drawAction, bool drawHorizontally = false)
        {
            GUILayout.Space(Padding);
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(Padding);

                if (!drawHorizontally)
                {
                    GUILayout.BeginVertical();
                }

                drawAction();

                if (!drawHorizontally)
                {
                    GUILayout.EndVertical();
                }

                GUILayout.Space(Padding);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawInfoText()
        {
            messageStyle ??= new(EditorStyles.label) { fontSize = 14, wordWrap = true, richText = true };
            GUILayout.Label(message, messageStyle);
            var messageHeight = messageStyle.CalcHeight(message, Width - Padding - Padding);
            var totalNeededHeight = MinHeight + messageHeight - EditorGUIUtility.singleLineHeight;
            if (position.height < totalNeededHeight)
            {
                var size = new Vector2(Width, totalNeededHeight);
                minSize = size;
                maxSize = size;
            }
        }

        private void DrawProjectCheckboxes()
        {
            checkboxStyle ??= new(EditorStyles.label) { fontSize = 14, richText = true };
            for (var i = 0; i < projectLabels.Length; i++)
            {
                var wasSelected = selections[i];
                var setSelected = EditorGUILayout.ToggleLeft(projectLabels[i], selections[i], checkboxStyle);
                if (wasSelected != setSelected)
                {
                    selections[i] = setSelected;
                }
            }
        }

        private void DrawProjectRadioButtons()
        {
            radioButtonLabelStyle ??= new(EditorStyles.label) { fontSize = 14, richText = true, alignment = TextAnchor.MiddleLeft };

            for (var i = 0; i < projectLabels.Length; i++)
            {
                GUILayout.BeginHorizontal();
                var wasSelected = selections[i];
                var isSelected = GUILayout.Toggle(wasSelected, GUIContent.none, EditorStyles.radioButton) || wasSelected;
                isSelected |= GUILayout.Button(projectLabels[i], radioButtonLabelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (isSelected && !wasSelected)
                {
                    Select(i);
                }
            }

            void Select(int selectedIndex)
            {
                for (var i = 0; i < selections.Length; i++)
                {
                    selections[i] = i == selectedIndex;
                }
            }
        }

        private void DrawConfirmAndCancelButtons()
        {
            GUI.enabled = selections.Any(x => x);

            buttonStyle ??= new(GUI.skin.button) { fontSize = 14, richText = true };
            if (DrawButton(confirmButtonLabel))
            {
                var selectedIds = projects.Where((_, index) => selections[index]).Select(x => x.id).ToArray();
                if (selectedIds.Length > 0)
                {
                    wasConfirmed = true;
                    SaveSelectedProjectIds(selectedIds);
                }

                Close();
            }

            GUILayout.Space(Padding);

            GUI.enabled = true;
            if (DrawButton(cancelButtonLabel))
            {
                Close();
            }
        }

        private bool DrawButton(GUIContent label) => GUILayout.Button(label, buttonStyle, buttonHeight);

        private static string GetDisplayName(ConditionalProject x)
        {
            if (x.Project.name is { Length: > 0 } projectName)
            {
                if (x.Label.text is { Length: > 0 } labelText)
                {
                    return $"{projectName} {RichText.Highlight($"({labelText})")}";
                }

                return projectName;
            }
            else if (x.Label.text is { Length: > 0 } labelText)
            {
                return labelText;
            }

            return x.Project.id ?? "n/a";
        }
    }
}
