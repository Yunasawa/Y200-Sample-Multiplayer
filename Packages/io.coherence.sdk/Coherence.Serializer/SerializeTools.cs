// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using System;
    using Brook;
    using Coherence.ProtocolDef;
    using Entities;

    public struct VarIntDefinition
    {
        public byte[] PartSizes { get; private set; }
        public bool FullyCompressMaxValue { get; private set; }

        /// <param name="partSizes">The number of value bits for each variable-length encoding part.
        /// The sum of all part sizes must not exceed 32 (since VarInt value is uint32).
        /// The first part size can be 0, which will serialize value 0 as a single bit.</param>
        /// <param name="fullyCompressMaxValue">If set to true, the max value will be serialized
        /// by setting all continuation bits to 1, at the expense of having an extra continuation bit
        /// if value used last given part for serialization.</param>
        public VarIntDefinition(byte[] partSizes, bool fullyCompressMaxValue = false)
        {
            PartSizes = partSizes ?? throw new ArgumentNullException(nameof(partSizes));
            FullyCompressMaxValue = fullyCompressMaxValue;
        }
    }

    public static class SerializeTools
    {
        public static readonly VarIntDefinition SimFrameDeltaVarIntDefinition = new(new byte[] { 0, 2, 2, 4 }, false);
        public static readonly VarIntDefinition Default16bitVarIntDefinition = new(new byte[] { 3, 5, 8 }, true);

        public static void WriteShortVarIntSigned(IOutBitStream stream, short v)
        {
            uint signBit = v < 0 ? 1u : 0u;
            stream.WriteBits(signBit, 1);
            WriteShortVarInt(stream, (ushort)(signBit == 0 ? v : -v));
        }

        public static void WriteShortVarInt(IOutBitStream stream, ushort v)
        {
            _ = WriteVarInt(stream, v, Default16bitVarIntDefinition);
        }

        public static void SerializeEntity(Entity entity, IOutBitStream outBitStream)
        {
            entity.AssertRelative();

            outBitStream.WriteBits((uint)entity.Index, Entity.NumIndexBits);
            outBitStream.WriteBits((uint)entity.Version, Entity.NumVersionBits);
        }

        public static void SerializeComponentTypeID(uint componentTypeId, IOutBitStream outBitStream)
        {
            outBitStream.WriteUint16((byte)componentTypeId);
        }

        public static void WriteFieldSimFrameDelta(IOutProtocolBitStream stream, byte delta)
        {
            _ = WriteVarInt(stream.BitStream, delta, SimFrameDeltaVarIntDefinition);
        }

        /// <summary>
        /// Serializes a given uint value using a variable-length integer encoding.
        /// </summary>
        /// <param name="stream">The stream it serializes into.</param>
        /// <param name="value">The value to serialize.</param>
        /// <param name="definition">The VarInt encoding definition.</param>
        /// <remarks>
        /// Example: value = 19 (10011) and PartSizes = [2, 3, 2] will serialize as 10_10011
        /// </remarks>
        /// <returns>Number of total written bits.</returns>
        public static uint WriteVarInt(IOutBitStream stream, uint value, VarIntDefinition definition)
        {
            var partSizes = definition.PartSizes;
            var fullyCompressMaxValue = definition.FullyCompressMaxValue;

            if (partSizes == null || partSizes.Length == 0)
            {
                throw new ArgumentException("Part sizes must not be null or empty.", nameof(partSizes));
            }

            if (partSizes.Length == 1 && partSizes[0] == 0)
            {
                throw new ArgumentException("Part sizes must not contain a single zero element.", nameof(partSizes));
            }

            byte valueBitCount = 0;
            var reachedMaxValue = false;
            var totalWrittenBits = 0u;

            for (var i = 0; i < partSizes.Length; i++)
            {
                if (i > 0 && partSizes[i] == 0)
                {
                    throw new ArgumentException("Part sizes must not contain zero elements after the first element.", nameof(partSizes));
                }

                if (partSizes[i] > 32)
                {
                    throw new ArgumentException("Part size must not exceed 32 bits.", nameof(partSizes));
                }

                valueBitCount += partSizes[i];
                if (valueBitCount > 32)
                {
                    throw new ArgumentException("Sum of part sizes must not exceed 32 bits.", nameof(partSizes));
                }

                var maxValue = (1u << valueBitCount) - 1;
                var isLastPart = i == partSizes.Length - 1;

                if (fullyCompressMaxValue || !isLastPart)
                {
                    if (value > maxValue || (fullyCompressMaxValue && isLastPart && value == maxValue))
                    {
                        totalWrittenBits++;
                        stream?.WriteBits(1, 1);
                    }
                    else
                    {
                        totalWrittenBits++;
                        stream?.WriteBits(0, 1);
                        break;
                    }

                    if (isLastPart)
                    {
                        reachedMaxValue = true;
                    }
                }
            }

            if (valueBitCount < 32 && value >= (1u << valueBitCount))
            {
                throw new Exception($"Value too large to serialize with the provided part sizes.");
            }

            if (fullyCompressMaxValue && reachedMaxValue)
            {
                return totalWrittenBits;
            }

            if (valueBitCount > 0)
            {
                totalWrittenBits += valueBitCount;
                stream?.WriteBits(value, valueBitCount);
            }

            return totalWrittenBits;
        }

        /// <summary>
        /// Returns the number of bits it would take to serialize the given value using VarInt encoding with the provided definition.
        /// See: <see cref="WriteVarInt(IOutBitStream, uint, VarIntDefinition)"/>
        /// </summary>
        public static uint WriteVarIntSize(uint value, VarIntDefinition definition)
        {
            return WriteVarInt(null, value, definition);
        }
    }
}
