// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Coherence.Debugging;
    using Coherence.Brook;

    /// <summary>
    /// Memory optimized data structure to keep track of which fragments were sent/acked/received.
    /// Each fragment can be either present or not present in the map. 
    /// </summary>
    public class FragmentMap
    {
        private List<FragmentSection> fragmentSections = new();

        /// <summary>
        /// List of present fragment sections.
        /// </summary>
        public IReadOnlyList<FragmentSection> FragmentSections => fragmentSections;

        /// <summary>
        /// Marks the given fragments as present in the map.
        /// </summary>
        /// <remarks>Throws an exception if any of the fragments are already present.</remarks>
        /// <param name="fragmentIndex">Index of the first fragment to add.</param>
        /// <param name="fragmentCount">Number of fragments to add. Must be greater than zero.</param>
        public void Add(uint fragmentIndex, uint fragmentCount = 1)
        {
            if (fragmentCount == 0)
            {
                throw new ArgumentException("Fragment count must be greater than zero.", nameof(fragmentCount));
            }

            var newFragmentSection = new FragmentSection(fragmentIndex, fragmentCount);

            var fragmentSectionIndex = FindSectionIndexContainingOrJustBefore(fragmentIndex, out var sectionFound);

            if (sectionFound)
            {
                // We have an existing section to merge with by adding the new fragment count to it.

                // Check if the new section overlaps with the existing section at the found index.
                var section = fragmentSections[fragmentSectionIndex];
                if (newFragmentSection.Overlaps(section))
                {
                    throw new InvalidOperationException($"New {newFragmentSection} overlaps with an existing {section}.");
                }

                var updatedSection = section.Add(fragmentCount);

                // Check if the updated section overlaps with the next section, if it exists.
                if (fragmentSectionIndex + 1 < fragmentSections.Count && updatedSection.Overlaps(fragmentSections[fragmentSectionIndex + 1]))
                {
                    throw new InvalidOperationException($"New {newFragmentSection} overlaps with an existing {fragmentSections[fragmentSectionIndex + 1]}.");
                }

                fragmentSections[fragmentSectionIndex] = updatedSection;
            }
            else
            {
                // We don't have an existing section, so we insert the new section at the correct index.

                // Check if the new section overlaps with the existing section at the found index.
                if (fragmentSectionIndex < fragmentSections.Count && newFragmentSection.Overlaps(fragmentSections[fragmentSectionIndex]))
                {
                    throw new InvalidOperationException($"New {newFragmentSection} overlaps with an existing {fragmentSections[fragmentSectionIndex]}.");
                }

                fragmentSections.Insert(fragmentSectionIndex, newFragmentSection);
            }

            // Merge with the next section if it borders the new section.
            if (fragmentSectionIndex + 1 < fragmentSections.Count &&
                   fragmentSections[fragmentSectionIndex + 1].Borders(newFragmentSection))
            {
                fragmentSections[fragmentSectionIndex] =
                    fragmentSections[fragmentSectionIndex].Add(fragmentSections[fragmentSectionIndex + 1].Count);

                fragmentSections.RemoveAt(fragmentSectionIndex + 1);
            }

            AssertFragmentSections();
        }

        /// <inheritdoc cref="Add(uint, uint)"/>
        public void Add(FragmentSection section) => Add(section.Index, section.Count);

        /// <summary>
        /// Adds multiple fragment sections to the map.
        /// See: <see cref="Add(FragmentSection)"/>
        /// </summary>
        public void AddRange(IEnumerable<FragmentSection> sections)
        {
            foreach (var section in sections)
            {
                Add(section);
            }
        }

        /// <summary>
        /// Marks the given fragments as not present in the map.
        /// </summary>
        /// <remarks>Throws an exception if any of the fragments are not present in the map.</remarks>
        /// <param name="fragmentIndex">Index of the first fragment to remove.</param>
        /// <param name="fragmentCount">Number of fragments to remove. Must be greater than zero.</param>
        public void Remove(uint fragmentIndex, uint fragmentCount = 1)
        {
            if (fragmentCount == 0)
            {
                throw new ArgumentException("Fragment count must be greater than zero.", nameof(fragmentCount));
            }

            var removeSection = new FragmentSection(fragmentIndex, fragmentCount);

            var fragmentSectionIndex = FindSectionIndexContainingOrJustBefore(fragmentIndex, out var sectionFound);

            if (!sectionFound)
            {
                throw new InvalidOperationException($"Trying to remove non-existing fragments. Fragment section for fragment index {fragmentIndex} not found.");
            }

            var section = fragmentSections[fragmentSectionIndex];

            // Check that the existing section fully contains the section to remove.
            if (!section.Contains(removeSection))
            {
                throw new InvalidOperationException($"Trying to remove non-existing fragments. Existing {section} doesn't contain {removeSection} to remove.");
            }

            if (fragmentIndex == section.Index)
            {
                if (fragmentCount == section.Count)
                {
                    // Fragment sections fully match (index and count)
                    fragmentSections.RemoveAt(fragmentSectionIndex);
                }
                else if (fragmentCount < section.Count)
                {
                    // Fragment sections overlap at the front
                    fragmentSections[fragmentSectionIndex] = section.RemoveAtFront(fragmentCount);
                }
                else
                {
                    // This shouldn't be hit because it's covered by the Contains check above, but who knows.
                    throw new Exception($"Cannot remove {fragmentCount} fragments from section {section} as it only has {section.Count} fragments.");
                }
            }
            else if (fragmentIndex > section.Index && fragmentIndex <= section.LastIndex)
            {
                if (removeSection.LastIndex == section.LastIndex)
                {
                    // Fragment sections overlap at the end
                    fragmentSections[fragmentSectionIndex] = section.RemoveAtBack(fragmentCount);
                }
                else if (removeSection.LastIndex < section.LastIndex)
                {
                    // Fragment section overlap in the middle
                    var (first, second) = section.RemoveInMiddle(fragmentIndex, fragmentCount);
                    fragmentSections[fragmentSectionIndex] = first;
                    fragmentSections.Insert(fragmentSectionIndex + 1, second);
                }
                else
                {
                    // This shouldn't be hit because it's covered by the Contains check above, but who knows.
                    throw new Exception($"Cannot remove {fragmentCount} fragments starting at {fragmentIndex} from section {section} as it only has {section.Count} fragments.");
                }
            }
            else
            {
                // This shouldn't be hit because it's covered by the Contains check above, but who knows.
                throw new Exception($"Cannot remove {fragmentCount} fragments starting at {fragmentIndex} from section {section} as it only has {section.Count} fragments.");
            }

            AssertFragmentSections();
        }

        /// <inheritdoc cref="Remove(uint, uint)"/>
        public void Remove(FragmentSection section) => Remove(section.Index, section.Count);

        /// <summary>
        /// Remove multiple fragment sections from the map.
        /// See: <see cref="Remove(FragmentSection)"/>
        /// </summary>
        public void RemoveRange(IEnumerable<FragmentSection> sections)
        {
            foreach (var section in sections)
            {
                Remove(section);
            }
        }

        /// <summary>
        /// Finds the section index for the given fragment index using a binary search.
        /// </summary>
        /// <returns>
        /// Returns index of the fragment section that contains (or is just before) the given <paramref name="fragmentIndex"/>
        /// with <paramref name="found"/> set to true.
        /// Or if the fragment section with the given index doesn't exist, it returns the index at which it should be added
        /// with <paramref name="found"/> set to false.</returns>
        private int FindSectionIndexContainingOrJustBefore(uint fragmentIndex, out bool found)
        {
            found = false;
            if (fragmentSections.Count == 0)
            {
                return 0;
            }

            var low = 0;
            var high = fragmentSections.Count - 1;
            while (low <= high)
            {
                var mid = low + (high - low) / 2;
                var midSection = fragmentSections[mid];

                if (fragmentIndex >= midSection.Index && fragmentIndex <= midSection.LastIndex + 1) // +1 to allow bordering sections
                {
                    found = true;
                    return mid;
                }
                else if (midSection.Index < fragmentIndex)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }

        /// <summary>
        /// Asserts that the list of fragments is ordered by the fragment index, that no fragment section has zero size
        /// and that the fragment sections don't overlap nor border each other.
        /// </summary>
        [Conditional(DbgAssert.ASSERTIONS_ENABLED)]
        private void AssertFragmentSections()
        {
            if (!DbgAssert.Enabled)
            {
                return;
            }

            for (var i = 0; i < fragmentSections.Count; i++)
            {
                var section = fragmentSections[i];

                DbgAssert.ThatFmt(section.Count > 0, "Fragment count must be greater than zero. {0}", section);

                if (i < fragmentSections.Count - 1)
                {
                    DbgAssert.ThatFmt(section.Index < fragmentSections[i + 1].Index,
                        "Fragment sections must be ordered by index. Current: {0}, Next: {1}",
                        section, fragmentSections[i + 1]);

                    DbgAssert.ThatFmt(!section.OverlapsOrBorders(fragmentSections[i + 1]),
                        "Fragment sections must not overlap or border each other. Current: {0}, Next: {1}",
                        section, fragmentSections[i + 1]);
                }
            }
        }
    }

    public static class FragmentSectionExtensions
    {
        /// <summary>
        /// Returns true if this fragment section overlaps with the given <paramref name="other"/> fragment section.
        /// Bordering (eg, 4,5,6 and 7,8) is not considered overlapping.
        /// </summary>
        public static bool Overlaps(this FragmentSection f, FragmentSection other)
        {
            return other.Index <= f.LastIndex && other.LastIndex >= f.Index;
        }

        /// <summary>
        /// Returns true if this fragment section borders the given <paramref name="other"/> fragment section.
        /// For example 7,8 and 4,5,6 would return true.
        /// </summary>
        public static bool Borders(this FragmentSection f, FragmentSection other)
        {
            return f.Index == other.LastIndex + 1 || other.Index == f.LastIndex + 1;
        }

        /// <summary>
        /// See <see cref="Overlaps"/> and <see cref="Borders"/>.
        /// </summary>
        public static bool OverlapsOrBorders(this FragmentSection f, FragmentSection other)
        {
            return f.Overlaps(other) || f.Borders(other);
        }

        /// <summary>
        /// Returns true if this fragment section contains the given <paramref name="other"/> fragment section.
        /// For example, 4,5,6 would contain 5,6 or 4,5,6 but not 3,4 or 7,8.
        /// </summary>
        public static bool Contains(this FragmentSection f, FragmentSection other)
        {
            return other.Index >= f.Index && other.LastIndex <= f.LastIndex;
        }

        /// <summary>
        /// Returns a new <see cref="FragmentSection"/> with the same index but increased count.
        /// </summary>
        public static FragmentSection Add(this FragmentSection f, uint count)
        {
            if (count == 0)
            {
                throw new ArgumentException("Count must be greater than zero.", nameof(count));
            }

            return new FragmentSection(f.Index, f.Count + count);
        }

        /// <summary>
        /// Returns a new <see cref="FragmentSection"/> with the same index but reduced count.
        /// </summary>
        public static FragmentSection RemoveAtBack(this FragmentSection f, uint count)
        {
            if (count == 0)
            {
                throw new ArgumentException("Count must be greater than zero.", nameof(count));
            }

            if (count > f.Count)
            {
                throw new InvalidOperationException($"Cannot remove {count} fragments from section {f} as it only has {f.Count} fragments.");
            }

            return new FragmentSection(f.Index, f.Count - count);
        }

        /// <summary>
        /// Returns a new <see cref="FragmentSection"/> with the index increased by the given count but the same ending index.
        /// </summary>
        public static FragmentSection RemoveAtFront(this FragmentSection f, uint count)
        {
            if (count == 0)
            {
                throw new ArgumentException("Count must be greater than zero.", nameof(count));
            }

            if (count > f.Count)
            {
                throw new InvalidOperationException($"Cannot remove {count} fragments from section {f} as it only has {f.Count} fragments.");
            }

            return new FragmentSection(f.Index + count, f.Count - count);
        }

        /// <summary>
        /// Returns a tuple of two new <see cref="FragmentSection"/>s that represent the sections before and after the given section to be removed.
        /// </summary>
        public static (FragmentSection first, FragmentSection second) RemoveInMiddle(this FragmentSection f, uint index, uint count)
        {
            if (count == 0)
            {
                throw new ArgumentException("Count must be greater than zero.", nameof(count));
            }

            if (index <= f.Index || index + count >= f.Index + f.Count)
            {
                throw new InvalidOperationException($"Cannot remove middle of {f}, because index {index} and count {count} aren't splitting it.");
            }

            var first = new FragmentSection(f.Index, index - f.Index);
            var second = new FragmentSection(index + count, f.Index + f.Count - (index + count));

            return (first, second);
        }
    }
}
