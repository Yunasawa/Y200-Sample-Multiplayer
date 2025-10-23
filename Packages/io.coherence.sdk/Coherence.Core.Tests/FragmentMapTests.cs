// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Channels.Tests
{
    using System;
    using Coherence.Tests;
    using NUnit.Framework;

    public class FragmentMapTests : CoherenceTest
    {
        private FragmentMap fragmentMap;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            fragmentMap = new FragmentMap();
        }

        private void AssertSections(params (uint index, uint count)[] sections)
        {
            Assert.AreEqual(sections.Length, fragmentMap.FragmentSections.Count);
            for (var i = 0; i < sections.Length; i++)
            {
                Assert.AreEqual(sections[i].index, fragmentMap.FragmentSections[i].Index);
                Assert.AreEqual(sections[i].count, fragmentMap.FragmentSections[i].Count);
            }
        }

        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(1234u)]
        [Description("Tests that adding a section containing a single fragment works correctly")]
        public void Add_SingleFragment_AddsCorrectly(uint fragmentIndex)
        {
            fragmentMap.Add(fragmentIndex);
            AssertSections((fragmentIndex, 1));
        }

        [TestCase(0u, 1u)]
        [TestCase(0u, 100u)]
        [TestCase(1u, 1u)]
        [TestCase(1u, 100u)]
        [TestCase(1234u, 5u)]
        [TestCase(9999u, 10u)]
        [Description("Tests that adding a section containing multiple fragments works correctly")]
        public void Add_MultipleFragments_AddsCorrectly(uint fragmentIndex, uint fragmentCount)
        {
            fragmentMap.Add(fragmentIndex, fragmentCount);
            AssertSections((fragmentIndex, fragmentCount));
        }

        [Test]
        public void Add_ZeroFragmentCount_ThrowsArgumentException()
        {
            _ = Assert.Throws<ArgumentException>(() => fragmentMap.Add(1, 0));
        }

        [Test]
        [Description("Tests that adding a section before an existing section works correctly")]
        public void Add_BeforeExisting_AddsCorrectly()
        {
            // Arrange
            fragmentMap.Add(5, 3); // 5,6,7

            // Act
            fragmentMap.Add(2, 2); // 2,3

            // Assert
            AssertSections((2, 2), (5, 3));
        }

        [Test]
        [Description("Tests that adding a section before (but bordering) an existing section works correctly")]
        public void Add_BeforeExistingButBordering_AddsCorrectly()
        {
            // Arrange
            fragmentMap.Add(5, 3); // 5,6,7

            // Act
            fragmentMap.Add(1, 4); // 1,2,3,4

            // Assert
            AssertSections((1, 7));
        }

        [Test]
        [Description("Tests that adding a section after an existing section works correctly")]
        public void Add_AfterExisting_AddsCorrectly()
        {
            // Arrange
            fragmentMap.Add(2, 3); // 2,3,4

            // Act
            fragmentMap.Add(6, 2); // 6,7

            // Assert
            AssertSections((2, 3), (6, 2));
        }

        [Test]
        [Description("Tests that adding a section after (but bordering) an existing section works correctly")]
        public void Add_AfterExistingButBordering_AddsCorrectly()
        {
            // Arrange
            fragmentMap.Add(2, 3); // 2,3,4

            // Act
            fragmentMap.Add(5, 2); // 5,6

            // Assert
            AssertSections((2, 5));
        }

        [TestCase(5u, 1u)] // 5
        [TestCase(6u, 1u)] // 6
        [TestCase(7u, 1u)] // 7
        [TestCase(4u, 2u)] // 4,5
        [TestCase(5u, 2u)] // 5,6
        [TestCase(6u, 2u)] // 6,7
        [TestCase(7u, 2u)] // 7,8
        [TestCase(3u, 3u)] // 3,4,5
        [TestCase(4u, 3u)] // 4,5,6
        [TestCase(5u, 3u)] // 5,6,7
        [TestCase(6u, 3u)] // 6,7,8
        [TestCase(7u, 3u)] // 7,8,9
        [TestCase(3u, 5u)] // 3,4,5,6,7
        [TestCase(4u, 5u)] // 4,5,6,7,8
        [TestCase(5u, 5u)] // 5,6,7,8,9
        [TestCase(6u, 5u)] // 6,7,8,9,10
        [TestCase(7u, 5u)] // 7,8,9,10,11
        [Description("Tests that adding a section overlapping an existing section throws an exception")]
        public void Add_Overlapping_Throws(uint fragmentIndex, uint fragmentCount)
        {
            // Arrange
            fragmentMap.Add(5, 3); // 5,6,7

            // Act & Assert
            _ = Assert.Throws<InvalidOperationException>(() => fragmentMap.Add(fragmentIndex, fragmentCount));
        }

        [Test]
        [Description("Tests that merging two sections works correctly")]
        public void Add_Complex()
        {
            fragmentMap.Add(5, 3); // 5,6,7
            fragmentMap.Add(10, 2); // 10,11

            fragmentMap.Add(8, 2); // 8,9

            AssertSections((5, 7));
        }

        [Test]
        [Description("Tests that a complex overlapping scenario throws an exception")]
        public void Add_ComplexOverlap()
        {
            fragmentMap.Add(5, 3); // 5,6,7
            fragmentMap.Add(10, 2); // 10,11

            _ = Assert.Throws<InvalidOperationException>(() => fragmentMap.Add(8, 3)); // 8,9,10
        }

        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        [TestCase(4u)]
        [Description("Tests that removing a section at the front of existing section works correctly")]
        public void Remove_AtFront(uint count)
        {
            // Arrange
            fragmentMap.Add(5, 5); // 5,6,7,8,9

            // Act
            fragmentMap.Remove(5, count);

            // Assert
            AssertSections((5 + count, 5 - count));
        }

        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        [TestCase(4u)]
        [Description("Tests that removing a section at the end of existing section works correctly")]
        public void Remove_AtEnd(uint count)
        {
            // Arrange
            fragmentMap.Add(5, 5); // 5,6,7,8,9

            // Act
            fragmentMap.Remove(10 - count, count);

            // Assert
            AssertSections((5, 5 - count));
        }

        [TestCase(6u, 1u)]
        [TestCase(6u, 2u)]
        [TestCase(6u, 3u)]
        [TestCase(7u, 1u)]
        [TestCase(7u, 2u)]
        [TestCase(8u, 1u)]
        [Description("Tests that removing a section at the middle of existing section works correctly")]
        public void Remove_InMiddle(uint index, uint count)
        {
            // Arrange
            fragmentMap.Add(5, 5); // 5,6,7,8,9

            // Act
            fragmentMap.Remove(index, count);

            // Assert
            AssertSections((5, index-5), (index + count, 10 - (index + count)));
        }

        [Test]
        [Description("Tests that removing a section that is the full existing section works correctly")]
        public void Remove_Full()
        {
            // Arrange
            fragmentMap.Add(5, 5); // 5,6,7,8,9

            // Act
            fragmentMap.Remove(5, 5);

            // Assert
            AssertSections();
        }

        [Test]
        public void RemoveZeroFragmentCount_ThrowsArgumentException()
        {
            _ = Assert.Throws<ArgumentException>(() => fragmentMap.Remove(1, 0));
        }

        [TestCase(1u, 1u)]
        [TestCase(1u, 2u)]
        [TestCase(1u, 5u)]
        [TestCase(1u, 10u)]
        [TestCase(4u, 1u)]
        [TestCase(4u, 2u)]
        [TestCase(4u, 5u)]
        [TestCase(4u, 6u)]
        [TestCase(5u, 6u)]
        [TestCase(5u, 10u)]
        [TestCase(6u, 5u)]
        [TestCase(9u, 2u)]
        [TestCase(10u, 1u)]
        [Description("Tests that removing a fragment that does not exist throws an exception")]
        public void Remove_NonExisting_ThrowsInvalidOperationException(uint index, uint count)
        {
            // Arrange
            fragmentMap.Add(5, 5); // 5,6,7,8,9

            // Act & Assert
            _ = Assert.Throws<InvalidOperationException>(() => fragmentMap.Remove(index, count));
        }

        [Test]
        [Description("Complex case of adding and removing fragments to ensure the map maintains integrity")]
        public void AddRemoveComplex()
        {
            fragmentMap.Add(1, 1);
            fragmentMap.Add(3, 1);
            fragmentMap.Add(5, 1);
            AssertSections((1, 1), (3, 1), (5, 1));

            fragmentMap.Add(2, 1);
            AssertSections((1, 3), (5, 1));

            fragmentMap.Add(4, 1);
            AssertSections((1, 5));

            fragmentMap.Add(6, 1000);
            AssertSections((1, 1005));

            fragmentMap.Remove(2, 1003);
            AssertSections((1, 1), (1005, 1));

            fragmentMap.Add(2, 1003);
            AssertSections((1, 1005));

            fragmentMap.Remove(1, 1004);
            AssertSections((1005, 1));

            fragmentMap.Remove(1005, 1);
            AssertSections();
        }
    }
}
