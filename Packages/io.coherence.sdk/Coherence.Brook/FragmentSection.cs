// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Brook
{
    using System;

    /// <summary>
    /// Represents a number of fragments as a section with a start index and a count.
    /// </summary>
    public struct FragmentSection
    {
        public uint Index;
        public uint Count;

        /// <summary>
        /// The index of the last fragment in this section.
        /// </summary>
        public readonly uint LastIndex => Index + Count - 1;

        public FragmentSection(uint index, uint count)
        {
            if (count == 0)
            {
                throw new ArgumentException("Count must be greater than zero.", nameof(count));
            }

            Index = index;
            Count = count;
        }

        public override readonly string ToString()
        {
            return $"FragmentSection(Index: {Index}, Count: {Count})";
        }
    }
}
