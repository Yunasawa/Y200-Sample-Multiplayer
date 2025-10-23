// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Toolkit.Tests
{
    using System;
    using NUnit.Framework;
    using Coherence.Tests;

    public class ArchetypeMathTests : CoherenceTest
    {
        enum TestEnum
        {
            Zero,
            One,
            Two,
            Three,
        }

        public enum LargeEnum
        {
            A = 1231,
            B = 555,
            C = 23423424,
            D = 10,
        }

        [Flags]
        enum TestFlags
        {
            Zero = 0,
            One = 1,
            Two = 2,
            Three = One | Two,
            Four = 4,
            Eight = 8,
        }

        [Flags]
        enum SmallFlags
        {
            Zero = 0,
            One = 1,
        }

        [Flags]
        enum ExtraTestFlags
        {
            Zero = 0,
            One = 1,
            Two = 2,
            Three = One | Two,
            Four = 4,
            Eight = 8,
            Thirteen = Eight | Four | One,
        }

        [TestCase(typeof(TestEnum), 2, TestEnum.Zero, TestEnum.Three)]
        [TestCase(typeof(LargeEnum), 25, LargeEnum.D, LargeEnum.C)]
        [TestCase(typeof(TestFlags), 4, TestFlags.Zero, TestFlags.One | TestFlags.Two | TestFlags.Four | TestFlags.Eight)]
        [TestCase(typeof(SmallFlags), 1, TestFlags.Zero, TestFlags.One)]
        [TestCase(typeof(ExtraTestFlags), 4, TestFlags.Zero, TestFlags.One | TestFlags.Two | TestFlags.Four | TestFlags.Eight)]
        public void GetRangeAndBitsForEnum_Works(Type testEnum, int expectedBits, int expectedMin, int expectedMax)
        {
            var (bits, min, max) = ArchetypeMath.GetRangeAndBitsForEnum(testEnum);
            Assert.That(bits, Is.EqualTo(expectedBits));
            Assert.That(min, Is.EqualTo(expectedMin));
            Assert.That(max, Is.EqualTo(expectedMax));
        }

        [TestCase(0, 1, 1)]
        [TestCase(0, 2, 2)]
        [TestCase(0, 3, 2)]
        [TestCase(0, 4, 3)]
        [TestCase(0, ushort.MaxValue, 16)]
        [TestCase(-1, ushort.MaxValue, 17)]
        [TestCase(0, uint.MaxValue, 32)]
        [TestCase(int.MinValue, int.MaxValue, 32)]
        public void GetBitsForIntValue_Works(long minRangeInclusive, long maxRangeInclusive, int expectedBits)
        {
            int bits = ArchetypeMath.GetBitsForIntValue(minRangeInclusive, maxRangeInclusive);
            Assert.That(bits, Is.EqualTo(expectedBits));
        }

        [TestCase(4, 0.1, 1u)]
        [TestCase(5, 0.1, 3u)]
        [TestCase(32, 0.001, 4294967u)]
        public void GetRangeByBitsAndPrecision_Works(int bits, double precision, ulong expectedRange)
        {
            ulong range = ArchetypeMath.GetTotalRangeByBitsAndPrecision(bits, precision);
            Assert.That(range, Is.EqualTo(expectedRange));
        }
    }
}
