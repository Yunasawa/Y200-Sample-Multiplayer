// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Editor
{
    using System.Collections.Generic;
    using static BindingsWindowSettings;

    internal class BindingsWindowState
    {
        internal sealed class ColumnState
        {
            public readonly ColumnContent Type;
            public float Width;
            public bool IsLOD => Type is ColumnContent.LOD;

            public ColumnState(ColumnContent type, float width)
            {
                Type = type;
                Width = width;
            }
        }

        internal enum State
        {
            Bindings,
            Methods,
            Interpolation,
            Lods
        }

        internal enum ColumnContent
        {
            Variables,
            Configuration,
            CompressionType,
            ValueRange,
            SampleRate,
            MaxBandwidth,
            LOD
        }

        internal readonly State Type;

        /// <summary>
        /// All columns, including hidden ones and LOD columns.
        /// </summary>
        internal readonly List<ColumnState> Columns = new();

        internal BindingsWindowState(State state) => Type = state;

        internal void SetToNewObject(int lodCount)
        {
            Columns.Clear();

            Add(ColumnContent.Variables, LeftBarSettings.DefaultWidth);
            Add(ColumnContent.CompressionType, TypeSettings.DefaultWidth);

            if (Type is State.Methods or State.Interpolation)
            {
                Add(ColumnContent.Configuration, BindingConfigSettings.DefaultWidth);
            }
            else if (Type is State.Lods)
            {
                Add(ColumnContent.ValueRange, ValueRangeSettings.DefaultWidth);
                Add(ColumnContent.SampleRate, SampleRateSettings.DefaultWidth);
                Add(ColumnContent.MaxBandwidth, StatisticsSettings.DefaultWidth);
            }

            if (Type is not State.Lods)
            {
                return;
            }

            for (var i = 0; i < lodCount; i++)
            {
                Add(ColumnContent.LOD, LODSettings.DefaultWidth);
            }

            void Add(ColumnContent content, float width) => Columns.Add(new(content, width));
        }

        internal void AddLOD()
        {
            if (Type != State.Lods)
            {
                return;
            }

            var index = Columns.FindIndex(col => col.IsLOD);
            if (index is -1)
            {
                index = Columns.Count;
            }

            Columns.Insert(index, new(ColumnContent.LOD, LODSettings.DefaultWidth));
        }

        /// <param name="nth"> Zero nth LOD column to remove. </param>
        internal void RemoveLOD(int nth)
        {
            if (Type != State.Lods)
            {
                return;
            }

            foreach (var column in Columns)
            {
                if (!column.IsLOD)
                {
                    continue;
                }

                if (nth > 0)
                {
                    nth--;
                    continue;
                }

                Columns.Remove(column);
                return;
            }
        }

        internal int GetLODIndexFromColumnIndex(int columnIndex)
        {
            var lodIndex = -1;
            foreach (var column in Columns)
            {
                if (column.IsLOD)
                {
                    lodIndex++;
                }

                if (columnIndex is 0)
                {
                    return lodIndex;
                }

                columnIndex--;
            }

            return -1;
        }

        internal int GetColumnIndex(ColumnContent type) => Columns.FindIndex(col => col.Type == type);

        internal ColumnState GetColumn(int index) => Columns[index];

        internal void SetColumnWidth(int columnIndex, float width) => GetColumn(columnIndex).Width = width;
    }
}
