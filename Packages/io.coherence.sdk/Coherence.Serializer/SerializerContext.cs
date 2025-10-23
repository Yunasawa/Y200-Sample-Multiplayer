// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using System;
    using Coherence.Brook;
    using Entities;
    using Log;

    public class SerializerContext<TStream> where TStream : IOutBitStream
    {
        public TStream BitStream { get; private set; }
        public bool UseDebugStreams { get; private set; }
        public Logger Logger { get; private set; }
        public uint ProtocolVersion { get; private set; }

        public string Section { get; private set; }
        public Entity EntityId { get; private set; }
        public uint ComponentId { get; private set; }

        /// <summary>
        /// Number of bits reserved for future use. This is used to ensure
        /// that we have enough bits remaining at the end of the packet
        /// to serialize EndOfMessages, or EndOfChannels and similar.
        /// </summary>
        public uint ReservedBits { get; private set; } = 0;

        /// <summary>
        /// Number of remaining free bits in the stream excluding the reserved bits.
        /// </summary>
        public uint RemainingUnreservedBitCount => BitStream.RemainingBitCount > ReservedBits ? BitStream.RemainingBitCount - ReservedBits : 0;

        /// <summary>
        /// This is a number of free bits remaining in a packet after the header was serialized.
        /// </summary>
        public uint BitsRemainingInEmptyPacket { get; private set; } = 0;

        /// <summary>
        /// Number of bits that can be used for serialization of an entity in an empty packet.
        /// This is calculated by calling <see cref="SetBitsRemainingInEmptyPacket"/> after serializing all headers
        /// and subtracting the <see cref="ReservedBits"/> from it.
        /// </summary>
        public uint FreeBitsInEmptyPacket => BitsRemainingInEmptyPacket - ReservedBits;

        public SerializerContext(TStream stream, bool useDebugStreams, Logger logger, uint protocolVersion = ProtocolDef.Version.CurrentVersion)
        {
            BitStream = stream;
            UseDebugStreams = useDebugStreams;
            Logger = logger;
            ProtocolVersion = protocolVersion;
            Section = "";
            EntityId = Entity.InvalidRelative;
        }

        public void StartSection(string section)
        {
            Section = section;
            EntityId = Entity.InvalidRelative;
            ComponentId = UInt32.MaxValue;
        }

        public void EndSection()
        {
            Section = "";
            EntityId = Entity.InvalidRelative;
            ComponentId = UInt32.MaxValue;
        }

        public void SetEntity(Entity id)
        {
            EntityId = id;
            ComponentId = UInt32.MaxValue;
        }

        public void SetComponent(uint id)
        {
            ComponentId = id;
        }

        public void ReserveBits(uint count)
        {
            ReservedBits += count;
        }

        public void FreeReservedBits(uint count)
        {
            if (count > ReservedBits)
            {
                throw new ArgumentOutOfRangeException(nameof(count), $"Cannot free more bits than reserved. Reserved: {ReservedBits}, TriedToFree: {count}");
            }

            ReservedBits -= count;
        }

        public ReservationScope NewReservationScope(uint count) => new(this, count);

        public bool IsStreamFull()
        {
            return BitStream.IsFull || BitStream.RemainingBitCount < ReservedBits;
        }

        /// <summary>
        /// See <see cref="BitsRemainingInEmptyPacket"/> and <see cref="FreeBitsInEmptyPacket"/>
        /// </summary>
        public void SetBitsRemainingInEmptyPacket()
        {
            if (this.BitsRemainingInEmptyPacket == 0)
            {
                this.BitsRemainingInEmptyPacket = BitStream.RemainingBitCount;
            }
        }

        public struct ReservationScope : IDisposable
        {
            private SerializerContext<TStream> ctx;
            private uint reservedBitsCount;

            internal ReservationScope(SerializerContext<TStream> ctx, uint reservedBitsCount)
            {
                this.ctx = ctx;
                this.reservedBitsCount = reservedBitsCount;

                ctx.ReserveBits(reservedBitsCount);
            }

            public void Dispose()
            {
                ctx.FreeReservedBits(reservedBitsCount);
            }
        }
    }
}
