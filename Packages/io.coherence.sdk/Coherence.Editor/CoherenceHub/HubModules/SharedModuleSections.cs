// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Linq;
    using Portal;
    using Toolkit;
    using UnityEditor;
    using UnityEngine;

    internal static class SharedModuleSections
    {
        private static class GUIContents
        {
            public static readonly GUIContent schemaInCloud = EditorGUIUtility.TrTextContent("Schema in Cloud");
            public static readonly GUIContent schemasInCloud = EditorGUIUtility.TrTextContent("Schemas in Cloud");
            public static readonly GUIContent localSchemaId = new("Local Schema ID");
            public static readonly GUIContent link = Icons.GetContent("IconLink");
        }

        internal static void DrawSchemasInPortal(EditorWindow window)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var text = BakeUtil.HasSchemaID ? BakeUtil.SchemaID.Substring(0, 5) : "No Schema";
                var localSchemaContent = new GUIContent(text);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(GUIContents.localSchemaId);
                    GUILayout.Label(localSchemaContent, GUILayout.ExpandWidth(false));
                    if (GUILayout.Button(ContentUtils.GUIContents.clipboard, ContentUtils.GUIStyles.iconButton))
                    {
                        EditorGUIUtility.systemCopyBuffer = BakeUtil.SchemaID;
                        EditorWindow.focusedWindow.ShowNotification(new GUIContent("Schema ID copied to clipboard"));
                    }
                }
            }

            GUILayout.Space(5f);

            var projectSettings = ProjectSettings.instance;
            if (PortalUtil.OrgIsSet && projectSettings.GetValidAndDistinctProjects().ToArray() is { Length: > 0 } projects)
            {
                if (projectSettings.UsingMultipleProjects)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel(GUIContents.schemasInCloud);
                        GUILayout.FlexibleSpace();
                        CoherenceHubLayout.DrawCloudDependantButton(CoherenceHubLayout.GUIContents.refresh, Schemas.UpdateSyncState, string.Empty);
                    }

                    EditorGUI.indentLevel++;

                    foreach (var project in projects)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var state = Schemas.GetState(project.id);
                            var stateContent = Schemas.GetStateContent(state);

                            CoherenceHubLayout.DrawLabel(new(project.name));

                            GUILayout.FlexibleSpace();

                            CoherenceHubLayout.DrawLabel(stateContent, GUILayout.ExpandWidth(false));
                            DrawDashboardSchemasLink(project.name);
                        }
                    }

                    EditorGUI.indentLevel--;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var tooltip = PortalUtil.CanCommunicateWithPortal ? "" : "You need to log in to sync schemas.";
                        CoherenceHubLayout.DrawCloudDependantButton(CloudModule.ModuleGUIContents.Upload,
                            () => Schemas.UploadActive(InteractionMode.UserAction, window),
                            tooltip,
                            null,
                            ContentUtils.GUIStyles.bigButton);
                    }
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var content = PortalUtil.OrgAndProjectIsSet ? Schemas.StateContent : new GUIContent();
                        var prefixLabel = GUIContents.schemaInCloud;
                        CoherenceHubLayout.DrawLabel(prefixLabel, content, options: GUILayout.ExpandWidth(true));

                        DrawDashboardSchemasLink();

                        GUILayout.FlexibleSpace();

                        CoherenceHubLayout.DrawCloudDependantButton(CoherenceHubLayout.GUIContents.refresh, Schemas.UpdateSyncState, string.Empty);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        CoherenceHubLayout.DrawCloudDependantButton(CloudModule.ModuleGUIContents.Upload, () =>
                            {
                                Schemas.UploadActive(InteractionMode.UserAction);
                            }, "You need to login to sync schemas.",
                            () => !PortalUtil.OrgAndProjectIsSet,
                            ContentUtils.GUIStyles.bigButton);
                    }
                }

                if (PortalUtil.OrgAndProjectIsSet)
                {
                    GUILayout.Label("Worlds must be edited from the Dashboard to use a different schema.", ContentUtils.GUIStyles.miniLabelGreyWrap);
                }
            }

            static void DrawDashboardSchemasLink(string projectName = null)
            {
                var url = GetDashboardSchemasUrl(projectName ?? RuntimeSettings.Instance.ProjectName, ProjectSettings.instance.Organization.slug);
                var linkLabel = new GUIContent(GUIContents.link);
                linkLabel.tooltip = url;
                if (GUILayout.Button(linkLabel, EditorStyles.label))
                {
                    Application.OpenURL(url);
                }
            }
        }

        internal static string GetDashboardUrl(string organizationSlug)
        {
            var org = organizationSlug ?? string.Empty;
            var url = $"{ExternalLinks.PortalUrl}/{NameToSlug(org)}";
            return url;
        }

        internal static string GetOrganizationUsageUrl(string organizationSlug)
            => $"{GetDashboardUrl(organizationSlug)}/usage";

        internal static string GetOrganizationBillingUrl(string organizationSlug)
            => $"{GetDashboardUrl(organizationSlug)}/billing";

        internal static string GetDashboardWorldsUrl(string projectName, string organizationSlug)
        {
            var proj = projectName ?? string.Empty;
            var org = organizationSlug ?? string.Empty;
            string url = projectName == PortalLoginDrawer.NoneProjectName ?
                Endpoints.portalUrl :
                $"{ExternalLinks.PortalUrl}/{NameToSlug(org)}/{NameToSlug(proj)}/worlds";

            return url;
        }

        internal static string GetDashboardProjectUrl(string projectName, string organizationSlug)
        {
            var proj = projectName ?? string.Empty;
            var org = organizationSlug ?? string.Empty;
            string url = projectName == PortalLoginDrawer.NoneProjectName ?
                Endpoints.portalUrl :
                $"{ExternalLinks.PortalUrl}/{NameToSlug(org)}/{NameToSlug(proj)}";

            return url;
        }

        internal static string GetDashboardSchemasUrl(string projectName, string organizationSlug)
            => $"{GetDashboardProjectUrl(projectName, organizationSlug)}/schemas";

        /// <summary>
        /// Converts project name to a URL slug.
        /// </summary>
        /// <example> "A - - - B" => "a-b" </example>
        /// <param name="projectName"> Project name. </param>
        /// <returns> Url slug. </returns>
        private static string NameToSlug(string projectName)
        {
            var slug = projectName.ToLower().Replace(' ', '-');
            while (slug.Contains("--"))
            {
                slug = slug.Replace("--", "-");
            }

            return slug;
        }
    }
}
