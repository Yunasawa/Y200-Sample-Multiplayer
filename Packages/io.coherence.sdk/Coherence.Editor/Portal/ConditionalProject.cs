// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Represents a project that is only active when certain conditions.
    /// </summary>
    /// <remarks>
    /// Can be used to automatically have a different project be selected in release builds
    /// than the one that is selected in the editor and in development builds.
    /// </remarks>
    [Serializable]
    internal sealed class ConditionalProject
    {
        internal const string ReleaseLabelText = "Release";
        internal const string DevelopmentLabelText = "Development";

        [SerializeField] private string name;
        [SerializeField] private string tooltip;
        [SerializeField] private ProjectInfo project;

        [SerializeField, Tooltip("If any of the listed conditions is met, this project will be used automatically.")]
        private Condition[] conditions;

        public GUIContent Label => new(name, tooltip);

        public ProjectInfo Project
        {
            get => project ??= new();
            set => project = value;
        }

        public bool HasConditions => conditions.Length > 0;

        public ConditionalProject()
        {
            name = "";
            tooltip = "";
            project = new();
            conditions = Array.Empty<Condition>();
        }

        private ConditionalProject(GUIContent label, ProjectInfo project, params Condition[] conditions)
        {
            this.name = label.text;
            this.tooltip = label.tooltip;
            this.project = project ?? new();
            this.conditions = conditions ?? Array.Empty<Condition>();
        }

        public static ConditionalProject ForRelease(ProjectInfo project) =>
            new(new(ReleaseLabelText, "Project to use in non-development builds."), project);

        public static ConditionalProject ForDevelopment(ProjectInfo project) =>
            new(new(DevelopmentLabelText, "Project to use in the editor and in development builds."), project,
                new Condition(ConditionType.IsEditor, true),
                new Condition(ConditionType.IsDevelopmentBuild, true));

        public bool AreConditionsMet(bool? isDevelopmentMode, bool resultIfHasNoConditions = true)
        {
            if (conditions.Length == 0)
            {
                return resultIfHasNoConditions;
            }

            isDevelopmentMode ??= BuildPreprocessor.GetIsDevelopmentMode();

            foreach (var condition in conditions)
            {
                if (condition.IsConditionMet(isDevelopmentMode.Value))
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString() => $"{project?.name ?? "None"} ({project?.id ?? "n/a"})";

        internal enum ConditionType
        {
            IsEditor = 0,
            IsDevelopmentBuild = 1
        }

        [Serializable]
        private struct Condition
        {
            [SerializeField] private ConditionType type;
            [SerializeField] private string requiredValue;

            public bool HasRequiredValue(bool value) => string.Equals(requiredValue, bool.TrueString) == value;
            public bool HasRequiredValue(string value) => string.Equals(requiredValue, value);

#pragma warning disable CS8524
            public bool IsConditionMet(bool isDevelopmentMode) => type switch
#pragma warning restore CS8524
            {
                ConditionType.IsEditor => isDevelopmentMode,
                ConditionType.IsDevelopmentBuild => isDevelopmentMode,
            };

            internal Condition(ConditionType type, string requiredValue)
            {
                this.type = type;
                this.requiredValue = requiredValue;
            }

            internal Condition(ConditionType type, bool requiredValue)
            {
                this.type = type;
                this.requiredValue = requiredValue.ToString();
            }
        }
    }
}
