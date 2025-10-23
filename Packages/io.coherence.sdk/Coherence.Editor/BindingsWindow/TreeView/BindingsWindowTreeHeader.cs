// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Collections.Generic;
    using System.Linq;
    using Coherence.Toolkit;
    using Coherence.Toolkit.Archetypes;
    using Toolkit;
    using UnityEditor;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;

    internal class BindingsWindowTreeHeader : MultiColumnHeader
    {
        internal class WidthSetting
        {
            public MinMaxFloat WidthLimits { get; }
            public float DefaultWidth { get; }

            public WidthSetting(float defaultWidth, float min, float max)
            {
                WidthLimits = new MinMaxFloat(min, max);
                DefaultWidth = defaultWidth;
            }
        }

        private readonly BindingsWindow window;
        private readonly CoherenceSync sync;

        public BindingsWindowTreeHeader(BindingsWindow window, CoherenceSync sync, MultiColumnHeaderState state) : base(state)
        {
            this.window = window;
            this.sync = sync;
            height = BindingsWindowSettings.HeaderHeight;

            SetMaxBandwidthVisible(BindingsWindowSettings.ShowStatistics);
        }

        public override void OnGUI(Rect rect, float xScroll)
        {
            base.OnGUI(rect, xScroll);

            // Draw fill of the entire bar
            float size = state.widthOfAllVisibleColumns;
            Rect fillerRect = new Rect(rect.x + size, rect.y, rect.width - size, rect.height);
            EditorGUI.DrawRect(fillerRect, BindingsWindowSettings.HeaderColor);
        }

        // Current Columns - LeftBar  | Config | Statistics | LODS | Add LOD button
        protected override void ColumnHeaderGUI(MultiColumnHeaderState.Column column, Rect rect, int columnIndex)
        {
            EditorGUI.DrawRect(rect, BindingsWindowSettings.HeaderColor);

            Rect verticalLineRect = new Rect(rect.xMax - 1, rect.y, 1, rect.height);
            EditorGUI.DrawRect(verticalLineRect, BindingsWindowSettings.VerticalLineColor);

            GUIStyle header = new GUIStyle(EditorStyles.boldLabel);
            header.fontSize = 14;
            header.alignment = TextAnchor.MiddleLeft;

            var columnType = window.StateController.GetColumnContent(columnIndex);
            window.StateController.SetColumnWidth(columnIndex, rect.width);

            if (columnType == BindingsWindowState.ColumnContent.Variables)
            {
                DrawBindingsHeader(column, rect, header);
                return;
            }

            RectOffset padding = new RectOffset(2, 2, 2, 2);
            Rect headerRect = padding.Remove(rect);

            if (columnType == BindingsWindowState.ColumnContent.Configuration)
            {
                DrawConfigHeader(column, rect, header);
                return;
            }

            if (columnType == BindingsWindowState.ColumnContent.LOD)
            {
                int lodStep = window.StateController.GetLODIndexFromColumnIndex(columnIndex);
                DrawLODHeader(column, rect, headerRect, header, lodStep);
                return;
            }

            DrawDefaultHeader(column, rect, header);
        }

        private void DrawBindingsHeader(MultiColumnHeaderState.Column column, Rect headerRect, GUIStyle headerStyle)
        {
            int headerSize = 100;

            // Rects
            Rect padded = new RectOffset(2, 2, 0, 2).Remove(headerRect);
            Rect labelRect = new Rect(padded.x, padded.y, headerSize, 20);

            // label
            string text = window.StateController.Methods ? "Methods" : "Variables";
            GUI.Label(labelRect, text, headerStyle);

            // Filter type buttons
            if (window.StateController.AllowUserToBind)
            {
                int buttonWidth = Mathf.FloorToInt(16);
                Rect selectAllRect = new Rect(padded.xMax - buttonWidth * 2, labelRect.yMax - buttonWidth, buttonWidth, buttonWidth);
                Rect deselectAllRect = new Rect(selectAllRect.xMax, selectAllRect.y, buttonWidth, buttonWidth);

                if (window.DrawSelectAllButton(selectAllRect))
                {
                    window.Tree.SetActiveStateOfAllUnfilteredBindings(true);
                }

                if (window.DrawSelectNoneButton(deselectAllRect))
                {
                    window.Tree.SetActiveStateOfAllUnfilteredBindings(false);
                }
            }
            else
            {
                GUIContent buttonContent = EditorGUIUtility.TrTextContentWithIcon("Configure", Icons.GetPath("Coherence.Wizard"));
                int buttonWidth = 100;
                Rect buttonRect = new Rect(padded.xMax - buttonWidth, padded.y + 2, buttonWidth, 20);
                if (GUI.Button(buttonRect, buttonContent, GUI.skin.button))
                {
                    CoherenceSyncBindingsWindow w = CoherenceSyncBindingsWindow.GetWindow(sync);
                    w.SetScope(CoherenceSyncBindingsWindow.Scope.Variables);
                }
            }
        }

        private void DrawConfigHeader(MultiColumnHeaderState.Column column, Rect headerRect, GUIStyle headerStyle)
        {
            Rect labelRect = new Rect(headerRect.x + 5, headerRect.y, headerRect.width - 10, 20);
            GUIContent headerText = window.StateController.Methods ? new GUIContent("Routing target") : new GUIContent("Interpolation");
            GUI.Label(labelRect, headerText, headerStyle);
        }

        private void DrawDefaultHeader(MultiColumnHeaderState.Column column, Rect headerRect, GUIStyle headerStyle)
        {
            Rect labelRect = new Rect(headerRect.x + 5, headerRect.y, headerRect.width - 10, 20);
            GUI.Label(labelRect, column.headerContent, headerStyle);
        }

        private void DrawLODHeader(MultiColumnHeaderState.Column column, Rect rect, Rect headerRect, GUIStyle headerStyle, int lodStep)
        {
            int dataSize = 70;
            int dataLabelSize = 50;
            // Rects
            Rect labelRect = new Rect(headerRect.x + 5, headerRect.y, headerRect.width - dataSize - dataLabelSize - 5, 20);
            Rect dataLabelRect = new Rect(labelRect.xMax, headerRect.y, dataLabelSize, headerRect.height);
            Rect dataRect = new Rect(dataLabelRect.xMax, headerRect.y, dataSize, headerRect.height);

            // Label
            headerStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Label(labelRect, $"{column.headerContent.text}", headerStyle);

            /*
            bool baseStep = lodStep == 0;
            Rect bitsRect = new Rect(headerRect.x + 5, headerRect.y, headerRect.width - (baseStep ? 5 : 32), 20);
            Rect deleteRect = new Rect(rect.xMax - 22, rect.y, 22, rect.height);

            // Get Data
            ArchetypeLODStep LodLevel = sync.Archetype.LODLevels[lodStep];
            int bits = sync.Archetype.GetTotalActiveBitsOfLOD(lodStep);
            float distance = LodLevel.Distance;

            // Bits
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.richText = true;
            labelStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(bitsRect, $"{bits} {(bits == 1 ? "Bit" : "Bits")}" , labelStyle);



            if (!baseStep)
            {
                // Delete Button

                GUIContent delete = EditorGUIUtility.IconContent("TreeEditor.Trash", "Delete");
                GUIStyle style = new GUIStyle(EditorStyles.toolbarButton);
                style.fixedHeight = 0;
                if (GUI.Button(deleteRect, delete, style))
                {
                    Undo.RecordObject(sync, "Added LOD");
                    sync.Archetype.RemoveLodLevel(lodStep);
                    window.StateController.RemoveLOD(lodStep);
                    window.CreateNewTreeView(true);
                    window.UpdateSerialization();
                }
            }
            */

            DrawLODLabels(dataLabelRect);
            DrawLODData(dataRect, lodStep);
        }


        private void DrawLODLabels(Rect rect)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel);
            style.alignment = TextAnchor.MiddleLeft;

            Rect topRect = new Rect(rect.x, rect.y, rect.width - 2, rect.height / 2);
            Rect bottom = new Rect(rect.x, topRect.yMax, rect.width - 2, rect.height / 2);

            GUI.Label(topRect, "Bits", style);
            GUI.Label(bottom, "Distance", style);
        }

        private void DrawLODData(Rect rect, int lodStep)
        {
            bool baseStep = lodStep == 0;
            //Color color = CoherenceArchetypeDrawer.GetLODColor((float) lodStep / Sync.Archetype.LODLevels.Count) * .5f;
            Color color = BindingsWindowSettings.HeaderColor;
            EditorGUI.DrawRect(rect, color);

            int buttonSize = 20;

            // Rects
            Rect topRect = new Rect(rect.x, rect.y, rect.width, rect.height / 2);
            Rect bottom = new Rect(rect.x, topRect.yMax, rect.width, rect.height / 2);
            Rect deleteRect = new Rect(topRect.xMax - buttonSize, topRect.y, buttonSize, buttonSize);

            // Get Data
            ArchetypeLODStep LodLevel = sync.Archetype.LODLevels[lodStep];
            int baseBits = sync.Archetype.GetTotalActiveBitsOfLOD(0);
            int bits = sync.Archetype.GetTotalActiveBitsOfLOD(lodStep);
            float percentage = (float)bits / baseBits * 100;
            float distance = LodLevel.Distance;

            // Label
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
            labelStyle.richText = true;
            labelStyle.alignment = TextAnchor.MiddleLeft;

            // Bits
            string highlightColor = ColorUtility.ToHtmlStringRGBA(BindingsWindowSettings.HighlightColor * .9f);
            GUI.Label(topRect, $"<b><color=#{highlightColor}>{bits}</color></b><size=9> ({percentage.ToString("N0")}%)</size>", labelStyle);
            //GUI.Label(topRect, $"<b><color=#ff0000ff>{bits}</color></b><size=9> ({percentage.ToString("N0")}%)</size>", labelStyle);

            if (!baseStep)
            {
                // Distance field
                EditorGUI.BeginChangeCheck();
                float newDistance = EditorGUI.DelayedFloatField(bottom, GUIContent.none, distance, labelStyle);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sync, "Set Distance on Lodstep");
                    SetLODDistance(sync.Archetype, lodStep, newDistance);
                    window.OnPropertyValueChanged();
                    window.UpdateSerialization();
                }

                // Delete Button
                GUIContent delete = EditorGUIUtility.IconContent("TreeEditor.Trash", "Delete");
                //GUIContent delete = EditorGUIUtility.IconContent("P4_DeletedLocal", "Delete");
                if (GUI.Button(deleteRect, delete, GUI.skin.label))
                {
                    Undo.RecordObject(sync, "Removed LOD");
                    sync.Archetype.RemoveLodLevel(lodStep);
                    window.StateController.RemoveLOD(lodStep);
                    window.CreateNewTreeView(true);
                    window.OnPropertyValueChanged();
                    window.UpdateSerialization();
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                GUI.Label(bottom, $"{distance}", labelStyle);
            }
        }


        private void SetLODDistance(ToolkitArchetype archetype, int lodStep, float distance)
        {
            for (int i = 0; i < archetype.LODLevels.Count; i++)
            {
                var lodLevel = archetype.LODLevels[i];
                if (i == 0)
                {
                    lodLevel.SetDistance(0);
                }
                else if (i == lodStep)
                {
                    lodLevel.SetDistance(distance);
                }
                else
                {
                    float current = lodLevel.Distance;
                    float newDistance = i >= lodStep ? Mathf.Max(distance, current) : Mathf.Min(distance, current);
                    lodLevel.SetDistance(newDistance);
                }
            }
        }

        public static MultiColumnHeaderState CreateColumns(CoherenceSync sync, BindingsWindowState currentState)
        {
            var columns = new List<MultiColumnHeaderState.Column>();
            foreach (var columnState in currentState.Columns.Where(x => !x.IsLOD))
            {
                var columnType = columnState.Type;
                if (columnType is BindingsWindowState.ColumnContent.LOD)
                {
                    continue;
                }

                var width = columnState.Width;
#pragma warning disable CS8524
                columns.Add(columnType switch
#pragma warning restore CS8524
                {
                    BindingsWindowState.ColumnContent.Variables => CreateColumn(new("Variables"), width, BindingsWindowSettings.LeftBarSettings),
                    BindingsWindowState.ColumnContent.Configuration => CreateColumn(new("Configuration", "Interpolation and Routing Target"), width, BindingsWindowSettings.BindingConfigSettings, false),
                    BindingsWindowState.ColumnContent.CompressionType => CreateColumn(new("Compression Type", "Type of compression used"), width, BindingsWindowSettings.TypeSettings),
                    BindingsWindowState.ColumnContent.ValueRange => CreateColumn(new("Value Range", "Limit the input range to make the syncing faster and cheaper"), width, BindingsWindowSettings.ValueRangeSettings),
                    BindingsWindowState.ColumnContent.SampleRate => CreateColumn(new("Sample Rate", "Limit the frequency at which data is sampled and synced to other clients"), width, BindingsWindowSettings.SampleRateSettings),
                    BindingsWindowState.ColumnContent.MaxBandwidth => CreateColumn(new("Max bandwidth", "Overview of all LODs bit costs"), width, BindingsWindowSettings.StatisticsSettings, true),
                    BindingsWindowState.ColumnContent.LOD => throw new(),
                });
            }

            var lodIndex = 0;
            foreach (var columnState in currentState.Columns.Where(x => x.IsLOD))
            {
                var header = lodIndex == 0 ? new[] { "Base", "Base" } : new[] { $"LOD {lodIndex}", "LOD of bindings" };
                columns.Add(CreateColumn(new(header[0], header[1]), columnState.Width, BindingsWindowSettings.LODSettings));
                lodIndex++;
            }

            var state = new MultiColumnHeaderState(columns.ToArray());
            return state;
        }

        private static MultiColumnHeaderState.Column CreateColumn(GUIContent header, float width, WidthSetting settings, bool canToggleVisibility = false) => new()
        {
            headerContent = header,
            headerTextAlignment = TextAlignment.Left,
            canSort = false,
            width = width,
            minWidth = settings.WidthLimits.MinValue,
            maxWidth = settings.WidthLimits.MaxValue,
            autoResize = true,
            allowToggleVisibility = canToggleVisibility
        };

        internal void SetMaxBandwidthVisible(bool visible)
        {
            var currentState = window.StateController.CurrentState;
            var columnIndex = currentState.GetColumnIndex(BindingsWindowState.ColumnContent.MaxBandwidth);
            if (IsColumnVisible(columnIndex) != visible)
            {
                ToggleVisibility(columnIndex);
            }
        }

        protected override void OnVisibleColumnsChanged()
        {
            base.OnVisibleColumnsChanged();
            var currentState = window.StateController.CurrentState;
            var columnIndex = currentState.GetColumnIndex(BindingsWindowState.ColumnContent.MaxBandwidth);
            BindingsWindowSettings.ShowStatistics.Value = IsColumnVisible(columnIndex);
        }
    }
}
