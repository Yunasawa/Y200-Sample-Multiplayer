// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor.Portal
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Events;
    using Object = UnityEngine.Object;

    internal class PortalLoginDrawer
    {
        private static readonly int MaxPickListRows = 10;
        public static readonly GUIContent orgLabel = EditorGUIUtility.TrTextContent("Organization");
        public static readonly GUIContent projLabel = EditorGUIUtility.TrTextContent("Project");
        public static readonly GUIContent projectsLabel = EditorGUIUtility.TrTextContent("Projects");
        public static readonly GUIContent usage = EditorGUIUtility.TrTextContent("Usage");
        public static readonly GUIContent refresh = Icons.GetContent("Coherence.Sync", "Refresh");
        public static readonly string NoneProjectName = "None";

        /// <summary>
        /// The pending request to get <see cref="OrgSubscription"/> if any.
        /// </summary>
        public static PortalRequest GetSubscriptionDataRequest;

        public static OrganizationSubscription OrgSubscription;

        public static float DrawOrganizationOptions()
        {
            using var scope = new EditorGUILayout.HorizontalScope();

            var organizations = PortalLogin.organizations;
            EditorGUI.BeginChangeCheck();
            using var disabled = new EditorGUI.DisabledScope(string.IsNullOrEmpty(ProjectSettings.instance.LoginToken));
            var labelWidth = Mathf.Max(CoherenceHubLayout.Styles.Label.CalcSizeCeil(orgLabel).x, CoherenceHubLayout.Styles.Label.CalcSizeCeil(projLabel).x);
            var height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var refreshBtnWidth = CoherenceHubLayout.Styles.Button.CalcSize(refresh).x;

            CoherenceHubLayout.DrawLabel(orgLabel, GUILayout.Width(labelWidth), GUILayout.Height(height));

            var controlRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f, CoherenceHubLayout.Styles.PopupNonFixedHeight, GUILayout.MinWidth(100f), GUILayout.MaxWidth(2040f));
            if (EditorGUI.DropdownButton(controlRect, new GUIContent(GetSelectedOrganization()?.name ?? "None"), FocusType.Passive, CoherenceHubLayout.Styles.PopupNonFixedHeight))
            {
                var projectsPopup =
                    new PopupPicker<Organization>(organizations, Math.Min(organizations.Length + 1, MaxPickListRows),
                        controlRect.width, item => item.name);
                projectsPopup.ItemSelected += org =>
                {
                    var rt = ProjectSettings.instance.RuntimeSettings;
                    Undo.RecordObject(rt, "Set Organization");
                    PortalLogin.AssociateOrganization(org);
                    RefreshSubscriptionInfo();
                };
                PopupWindow.Show(controlRect, projectsPopup);
                GUIUtility.ExitGUI();
            }

            DrawOrganizationRefreshButton(refresh, refreshBtnWidth);

            return labelWidth;
        }

        public static void DrawOrganizationRefreshButton(GUIContent content, float width)
        {
            if (CoherenceHubLayout.DrawButton(content, true, null, GUILayout.Width(width)))
            {
                PortalLogin.FetchOrgs();
                RefreshSubscriptionInfo();
            }
        }

        public static void RefreshSubscriptionInfo()
        {
            GetSubscriptionDataRequest = OrganizationSubscription.FetchAsync(ProjectSettings.instance.OrganizationId,
                subscription =>
                {
                    OrgSubscription = subscription;
                    GetSubscriptionDataRequest = null;
                });
        }

        public static Organization GetSelectedOrganization()
        {
            foreach (var org in PortalLogin.organizations)
            {
                if (org.id == ProjectSettings.instance.OrganizationId)
                {
                    return org;
                }
            }

            return null;
        }

        public static string GetSelectedProjectName()
        {
            var selectedOrg = GetSelectedOrganization();
            if (selectedOrg == null)
            {
                return NoneProjectName;
            }

            foreach (var proj in selectedOrg.projects)
            {
                if (proj.id == ProjectSettings.instance.RuntimeSettings.ProjectID)
                {
                    return proj.name;
                }
            }

            return NoneProjectName;
        }

        private static int GetSelectedOrganizationContent(Organization[] organizations)
        {
            for (int i = 0; i < organizations.Length; i++)
            {
                var org = organizations[i];
                if (org.id == ProjectSettings.instance.OrganizationId)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        public static void DrawProjectOptions(float labelWidth)
        {
            EditorGUI.BeginChangeCheck();

            using var disabled = new EditorGUI.DisabledScope(string.IsNullOrEmpty(ProjectSettings.instance.LoginToken));

            var projectSettings = ProjectSettings.instance;
            if (!projectSettings.UsingMultipleProjects)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    CoherenceHubLayout.DrawLabel(new(projLabel), GUILayout.Width(labelWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight + 2f));

                    DrawProjectPopup(new(GetSelectedProjectName()), project =>
                    {
                        var rt = projectSettings.RuntimeSettings;
                        Undo.RecordObject(rt, "Set Project");
                        PortalLogin.AssociateProject(project, Schemas.UpdateSyncState);
                        projectSettings.Save();
                    });
                }
            }
            else
            {
                CoherenceHubLayout.DrawLabel(new(projectsLabel), GUILayout.Height(EditorGUIUtility.singleLineHeight + 2f));
                var items = projectSettings.MultipleProjects;
                foreach (var item in items)
                {
                    var itemLabelWidth = CoherenceHubLayout.Styles.Label.CalcSizeCeil(item.Label).x;
                    if (itemLabelWidth > labelWidth)
                    {
                        labelWidth = itemLabelWidth;
                    }
                }

                EditorGUI.indentLevel++;
                var layoutOptions = new[] {GUILayout.Width(labelWidth), GUILayout.Height(EditorGUIUtility.singleLineHeight + 2f) };
                foreach (var item in items)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        CoherenceHubLayout.DrawLabel(new(item.Label), layoutOptions);
                        DrawProjectPopup(new(item.Project.name), project =>
                        {
                            var runtimeSettings = projectSettings.RuntimeSettings;
                            Undo.RecordObjects(new Object[] { projectSettings, runtimeSettings }, "Set Project");
                            item.Project = project;
                            projectSettings.UpdateProjectInRuntimeSettings();

                            if (runtimeSettings && string.Equals(runtimeSettings.ProjectID, item.Project.id))
                            {
                                PortalLogin.AssociateProject(project, Schemas.UpdateSyncState);
                            }
                            else
                            {
                                SaveRuntimeSettings(projectSettings.RuntimeSettings);
                                Schemas.UpdateSyncState();
                            }

                            projectSettings.Save();
                        });
                    }
                }

                EditorGUI.indentLevel--;
            }
        }

        private static void DrawProjectPopup(GUIContent valueLabel, UnityAction<ProjectInfo> onSelectionChanged) => DrawProjectPopup(GUIContent.none, valueLabel, onSelectionChanged);

        private static void DrawProjectPopup(GUIContent prefixLabel, GUIContent valueLabel, UnityAction<ProjectInfo> onSelectionChanged)
        {
            var position = EditorGUILayout.GetControlRect(prefixLabel.text.Length > 0, EditorGUIUtility.singleLineHeight + 2f, CoherenceHubLayout.Styles.PopupNonFixedHeight, GUILayout.MinWidth(100f), GUILayout.MaxWidth(2040f));
            DrawProjectPopup(position, prefixLabel, valueLabel, onSelectionChanged);
        }

        private static void DrawProjectPopup(Rect position, GUIContent prefixLabel, GUIContent valueLabel, UnityAction<ProjectInfo> onSelectionChanged)
            => DrawProjectPopup(position, prefixLabel, valueLabel, GetAllProjectsForSelectedOrganization(), onSelectionChanged);

        internal static void DrawProjectPopup(GUIContent prefixLabel, GUIContent valueLabel, ProjectInfo[] options, UnityAction<ProjectInfo> onSelectionChanged, bool sortByName = true)
        {
            var position = EditorGUILayout.GetControlRect(prefixLabel.text.Length > 0, EditorGUIUtility.singleLineHeight + 2f, CoherenceHubLayout.Styles.PopupNonFixedHeight, GUILayout.MinWidth(100f), GUILayout.MaxWidth(2040f));
            DrawProjectPopup(position, prefixLabel, valueLabel, options, onSelectionChanged, sortByName);
        }

        private static void DrawProjectPopup(Rect position, GUIContent prefixLabel, GUIContent valueLabel, ProjectInfo[] options, UnityAction<ProjectInfo> onSelectionChanged, bool sortByName = true)
        {
            if (prefixLabel.text.Length > 0)
            {
                position = EditorGUI.PrefixLabel(position, prefixLabel);
            }

            if (EditorGUI.DropdownButton(position, valueLabel, FocusType.Passive, CoherenceHubLayout.Styles.PopupNonFixedHeight))
            {
                var projectsPopup = new PopupPicker<ProjectInfo>(options, Math.Min(options.Length + 1, MaxPickListRows), position.width, item => item.name, sortByName);
                projectsPopup.ItemSelected += onSelectionChanged;
                PopupWindow.Show(position, projectsPopup);
                GUIUtility.ExitGUI();
            }
        }

        private static ProjectInfo[] GetAllProjectsForSelectedOrganization()
        {
            var organizations = PortalLogin.organizations;
            var orgContent = GetSelectedOrganizationContent(organizations);
            return orgContent == 0 ? Array.Empty<ProjectInfo>() : organizations[orgContent - 1].projects;
        }

        private static void SaveRuntimeSettings(RuntimeSettings runtimeSettings)
        {
            EditorUtility.SetDirty(runtimeSettings);
            AssetDatabase.SaveAssetIfDirty(runtimeSettings);
        }
    }
}


