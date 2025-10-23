// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using Brook;
    using ProtocolDef;
    using Entities;

    public static class DeserializerTools
    {
        public static short ReadShortVarIntSigned(IInBitStream stream)
        {
            var hasSign = stream.ReadBits(1) != 0;
            uint value = ReadShortVarInt(stream);
            return hasSign ? (short)(value * -1) : (short)value;
        }

        public static ushort ReadShortVarInt(IInBitStream stream)
        {
            return (ushort)ReadVarInt(stream, SerializeTools.Default16bitVarIntDefinition);
        }

        public static Entity DeserializeEntity(IInBitStream outBitStream)
        {
            Index rawIndex = (Index)outBitStream.ReadBits(Entity.NumIndexBits);
            byte rawVersion = (byte)outBitStream.ReadBits(Entity.NumVersionBits);

            Entity entityId = new Entity(rawIndex, rawVersion, Entity.Relative);

            return entityId;
        }

        public static uint DeserializeComponentTypeID(IInBitStream inBitStream)
        {
            return (uint)inBitStream.ReadUint16();
        }

        public static MessageTarget DeserializeMessageTarget(IInBitStream inBitStream)
        {
            return (MessageTarget)inBitStream.ReadBits(2);
        }

        public static RawCommandMeta? DeserializeCommandMeta(IInBitStream inBitStream)
        {
            uint senderID = 0;
            long frame = 0;

            var hasMeta = inBitStream.ReadBits(1) == 1;
            if (!hasMeta)
            {
                return null;
            }

            var isSenderIDPresent = inBitStream.ReadBits(1) == 1;
            if (isSenderIDPresent)
            {
                senderID = inBitStream.ReadUint32();
            }

            frame = (long)inBitStream.ReadUint64();

            return new RawCommandMeta
            {
                SenderID = senderID,
                Frame = frame
            };
        }

        public static byte ReadFieldSimFrameDelta(IInProtocolBitStream stream)
        {
            return (byte)ReadVarInt(stream.BitStream, SerializeTools.SimFrameDeltaVarIntDefinition);
        }

        /// <summary>
        /// See: <see cref="SerializeTools.WriteVarInt(IOutBitStream, uint, VarIntDefinition)"/>
        /// </summary>
        public static uint ReadVarInt(IInBitStream stream, VarIntDefinition varIntDefinition)
        {
            var partSizes = varIntDefinition.PartSizes;
            var fullyCompressMaxValue = varIntDefinition.FullyCompressMaxValue;

            if (partSizes == null || partSizes.Length == 0)
            {
                throw new System.ArgumentException("Part sizes must not be null or empty.", nameof(partSizes));
            }

            if (partSizes.Length == 1 && partSizes[0] == 0)
            {
                throw new System.ArgumentException("Part sizes must not contain a single zero element.", nameof(partSizes));
            }

            byte valueBitCount = 0;
            var reachedMaxValue = false;

            for (var i = 0; i < partSizes.Length; i++)
            {
                if (i > 0 && partSizes[i] == 0)
                {
                    throw new System.ArgumentException("Part sizes must not contain zero elements after the first element.", nameof(partSizes));
                }

                if (partSizes[i] > 32)
                {
                    throw new System.ArgumentException("Part size must not exceed 32 bits.", nameof(partSizes));
                }

                valueBitCount += partSizes[i];
                if (valueBitCount > 32)
                {
                    throw new System.ArgumentException("Sum of part sizes must not exceed 32 bits.", nameof(partSizes));
                }

                var isLastPart = i == partSizes.Length - 1;

                if (fullyCompressMaxValue || !isLastPart)
                {
                    if (stream.ReadBits(1) == 0)
                    {
                        break;
                    }

                    if (isLastPart)
                    {
                        reachedMaxValue = true;
                    }
                }
            }

            if (valueBitCount == 0)
            {
                return 0;
            }

            if (fullyCompressMaxValue && reachedMaxValue)
            {
                return (uint)(1 << valueBitCount) - 1;
            }

            return stream.ReadBits(valueBitCount);
        }
    }
}
