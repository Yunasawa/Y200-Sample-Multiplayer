// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer.Tests
{
    using Brook;
    using Brook.Octet;
    using NUnit.Framework;
    using Serializer;
    using OutOctetStream = Brook.Octet.OutOctetStream;
    using Coherence.Tests;
    using System;

    public class SerializeToolsTests : CoherenceTest
    {
        [TestCase(0, 0, 5)]
        [TestCase(1, 1, 5)]
        [TestCase(8, 8, 11)]
        [TestCase(255, 255, 11)]
        [TestCase(256, 256, 20)]
        [TestCase(16000, 16000, 20)]
        [TestCase(short.MaxValue, short.MaxValue, 20)]
        [TestCase(-1, -1, 5)]
        [TestCase(-8, -8, 11)]
        [TestCase(-255, -255, 11)]
        [TestCase(-256, -256, 20)]
        [TestCase(-16000, -16000, 20)]
        [TestCase(short.MinValue, short.MinValue, 20)]
        public void ShortVarIntSigned_Works(short value, short exp, int bitsUsed)
        {
            // Arrange
            var packetStream = new OutOctetStream(256);
            var outBitStream = (IOutBitStream)new OutBitStream(packetStream);

            // Act
            SerializeTools.WriteShortVarIntSigned(outBitStream, value);
            outBitStream.Flush();

            var actualBitCount = outBitStream.Position;
            var calculatedBitCount = SerializeTools.WriteVarIntSize((ushort)(value < 0 ? -value : value), SerializeTools.Default16bitVarIntDefinition) + 1;

            var octetReader = new InOctetStream(packetStream.Close().ToArray());
            var inBitStream = (IInBitStream)new InBitStream(octetReader, (int)outBitStream.Position);

            var deserialized = DeserializerTools.ReadShortVarIntSigned(inBitStream);

            // Assert
            Assert.That(actualBitCount, Is.EqualTo(bitsUsed));
            Assert.That(calculatedBitCount, Is.EqualTo(bitsUsed));
            Assert.That(deserialized, Is.EqualTo(exp));
        }

        [TestCase(0, 0, 4)]
        [TestCase(1, 1, 4)]
        [TestCase(8, 8, 10)]
        [TestCase(255, 255, 10)]
        [TestCase(256, 256, 19)]
        [TestCase(16000, 16000, 19)]
        [TestCase(60000, 60000, 19)]
        [TestCase(ushort.MaxValue, ushort.MaxValue, 3)]
        public void ShortVarInt_Works(int value, int exp, int bitsUsed)
        {
            // Arrange
            var packetStream = new OutOctetStream(256);
            var outBitStream = (IOutBitStream)new OutBitStream(packetStream);

            // Act
            SerializeTools.WriteShortVarInt(outBitStream, (ushort)value);
            outBitStream.Flush();

            var actualBitCount = outBitStream.Position;
            var calculatedBitCount = SerializeTools.WriteVarIntSize((ushort)value, SerializeTools.Default16bitVarIntDefinition);

            var octetReader = new InOctetStream(packetStream.Close().ToArray());
            var inBitStream = (IInBitStream)new InBitStream(octetReader, (int)outBitStream.Position);

            var deserialized = DeserializerTools.ReadShortVarInt(inBitStream);

            // Assert
            Assert.That(actualBitCount, Is.EqualTo(bitsUsed));
            Assert.That(calculatedBitCount, Is.EqualTo(bitsUsed));
            Assert.That(deserialized, Is.EqualTo((ushort)exp));
        }

        [TestCase(0, 1)]
        [TestCase(1, 4)]
        [TestCase(2, 4)]
        [TestCase(3, 4)]
        [TestCase(4, 7)]
        [TestCase(5, 7)]
        [TestCase(9, 7)]
        [TestCase(15, 7)]
        [TestCase(16, 11)]
        [TestCase(17, 11)]
        [TestCase(123, 11)]
        [TestCase(254, 11)]
        [TestCase(255, 11)]
        public void FieldSimFrameDelta_Works(byte value, int bitsUsed)
        {
            // Arrange
            var packetStream = new OutOctetStream(256);
            var outBitStream = (IOutBitStream)new OutBitStream(packetStream);
            var outProtocolBitStream = new OutProtocolBitStream(outBitStream, null);

            // Act
            SerializeTools.WriteFieldSimFrameDelta(outProtocolBitStream, value);
            outBitStream.Flush();

            var actualBitCount = outBitStream.Position;

            var octetReader = new InOctetStream(packetStream.Close().ToArray());
            var inBitStream = (IInBitStream)new InBitStream(octetReader, (int)outBitStream.Position);
            var inProtocolBitStream = new InProtocolBitStream(inBitStream);

            var deserialized = DeserializerTools.ReadFieldSimFrameDelta(inProtocolBitStream);

            // Assert
            Assert.That(actualBitCount, Is.EqualTo(bitsUsed));
            Assert.That(deserialized, Is.EqualTo(value));
        }

        public struct VarIntTestCase
        {
            public uint Value;
            public byte[] PartSizes;
            public uint ExpectedBitCount;

            public VarIntTestCase(uint value, byte[] partSizes, uint expectedBitCount)
            {
                Value = value;
                PartSizes = partSizes;
                ExpectedBitCount = expectedBitCount;
            }
        }

        public static readonly VarIntTestCase[] VarIntTestCases = new[]
        {
            new VarIntTestCase(0, new byte[] { 0, 2 }, 1),
            new VarIntTestCase(1, new byte[] { 0, 2 }, 3),
            new VarIntTestCase(2, new byte[] { 0, 2 }, 3),
            new VarIntTestCase(0, new byte[] { 0, 2, 2 }, 1),
            new VarIntTestCase(1, new byte[] { 0, 2, 2 }, 4),
            new VarIntTestCase(3, new byte[] { 0, 2, 2 }, 4),
            new VarIntTestCase(4, new byte[] { 0, 2, 2 }, 6),
            new VarIntTestCase(0, new byte[] { 0, 2, 2, 4 }, 1),
            new VarIntTestCase(1, new byte[] { 0, 2, 2, 4 }, 4),
            new VarIntTestCase(2, new byte[] { 0, 2, 2, 4 }, 4),
            new VarIntTestCase(3, new byte[] { 0, 2, 2, 4 }, 4),
            new VarIntTestCase(4, new byte[] { 0, 2, 2, 4 }, 7),
            new VarIntTestCase(5, new byte[] { 0, 2, 2, 4 }, 7),
            new VarIntTestCase(14, new byte[] { 0, 2, 2, 4 }, 7),
            new VarIntTestCase(15, new byte[] { 0, 2, 2, 4 }, 7),
            new VarIntTestCase(16, new byte[] { 0, 2, 2, 4 }, 11),
            new VarIntTestCase(255, new byte[] { 0, 2, 2, 4 }, 11),
            new VarIntTestCase(0, new byte[] { 2, 2 }, 3),
            new VarIntTestCase(1, new byte[] { 2, 2 }, 3),
            new VarIntTestCase(2, new byte[] { 2, 2 }, 3),
            new VarIntTestCase(3, new byte[] { 2, 2 }, 3),
            new VarIntTestCase(4, new byte[] { 2, 2 }, 5),
            new VarIntTestCase(15, new byte[] { 2, 2 }, 5),
            new VarIntTestCase(15, new byte[] { 30 }, 30),
            new VarIntTestCase(15, new byte[] { 31 }, 31),
            new VarIntTestCase(15, new byte[] { 32 }, 32),
        };

        [TestCaseSource(nameof(VarIntTestCases))]
        public void VarInt_Works(VarIntTestCase testCase)
        {
            // Arrange
            var varIntDefinition = new VarIntDefinition(testCase.PartSizes);
            var packetStream = new OutOctetStream(256);
            var outBitStream = (IOutBitStream)new OutBitStream(packetStream);

            // Act
            _ = SerializeTools.WriteVarInt(outBitStream, testCase.Value, varIntDefinition);
            outBitStream.Flush();

            var actualBitCount = outBitStream.Position;
            var calculatedBitCount = SerializeTools.WriteVarIntSize(testCase.Value, varIntDefinition);

            var octetReader = new InOctetStream(packetStream.Close().ToArray());
            var inBitStream = (IInBitStream)new InBitStream(octetReader, (int)outBitStream.Position);

            var deserialized = DeserializerTools.ReadVarInt(inBitStream, varIntDefinition);

            // Assert
            Assert.That(actualBitCount, Is.EqualTo(testCase.ExpectedBitCount));
            Assert.That(calculatedBitCount, Is.EqualTo(testCase.ExpectedBitCount));
            Assert.That(deserialized, Is.EqualTo(testCase.Value));
        }

        [Test]
        public void VarInt_EmptyParts_Throws()
        {
            var emptyOutStream = new OutBitStream(new OutOctetStream(256));
            var emptyInStream = new InBitStream(new InOctetStream(new byte[] { }), 256);

            _ = Assert.Throws<ArgumentException>(() => SerializeTools.WriteVarInt(emptyOutStream, 0, new VarIntDefinition(new byte[] { })));
            _ = Assert.Throws<ArgumentException>(() => DeserializerTools.ReadVarInt(emptyInStream, new VarIntDefinition(new byte[] { })));
        }

        [Test]
        public void VarInt_OnlyZeroPart_Throws()
        {
            var emptyOutStream = new OutBitStream(new OutOctetStream(256));
            var emptyInStream = new InBitStream(new InOctetStream(new byte[] { }), 256);

            _ = Assert.Throws<ArgumentException>(() => SerializeTools.WriteVarInt(emptyOutStream, 0, new VarIntDefinition(new byte[] { 0 })));
            _ = Assert.Throws<ArgumentException>(() => DeserializerTools.ReadVarInt(emptyInStream, new VarIntDefinition(new byte[] { 0 })));
        }

        [Test]
        public void VarInt_NonFirstZeroPart_Throws()
        {
            var emptyOutStream = new OutBitStream(new OutOctetStream(256));
            var emptyInStream = new InBitStream(new InOctetStream(new byte[] { 0xFF, 0xFF }), 16);

            _ = Assert.Throws<ArgumentException>(() => SerializeTools.WriteVarInt(emptyOutStream, 3, new VarIntDefinition(new byte[] { 1, 0, 10 })));
            _ = Assert.Throws<ArgumentException>(() => DeserializerTools.ReadVarInt(emptyInStream, new VarIntDefinition(new byte[] { 1, 0, 10 })));
        }

        [Test]
        public void VarInt_MoreThan32Part_Throws()
        {
            var emptyOutStream = new OutBitStream(new OutOctetStream(256));
            var emptyInStream = new InBitStream(new InOctetStream(new byte[] { }), 0);

            _ = Assert.Throws<ArgumentException>(() => SerializeTools.WriteVarInt(emptyOutStream, 1, new VarIntDefinition(new byte[] { 33 })));
            _ = Assert.Throws<ArgumentException>(() => DeserializerTools.ReadVarInt(emptyInStream, new VarIntDefinition(new byte[] { 33 })));
        }

        [Test]
        public void VarInt_MoreThan32PartsSum_Throws()
        {
            var emptyOutStream = new OutBitStream(new OutOctetStream(256));
            var emptyInStream = new InBitStream(new InOctetStream(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }), 40);

            _ = Assert.Throws<ArgumentException>(() => SerializeTools.WriteVarInt(emptyOutStream, 1 << 20, new VarIntDefinition(new byte[] { 17, 17 })));
            _ = Assert.Throws<ArgumentException>(() => DeserializerTools.ReadVarInt(emptyInStream, new VarIntDefinition(new byte[] { 17, 17 })));
        }

        [Test]
        public void WriteVarInt_TooBigValue_Throws()
        {
            var emptyOutStream = new OutBitStream(new OutOctetStream(256));

            _ = Assert.Throws<Exception>(() => SerializeTools.WriteVarInt(emptyOutStream, 16, new VarIntDefinition(new byte[] { 2, 2 })));
        }
    }
}
