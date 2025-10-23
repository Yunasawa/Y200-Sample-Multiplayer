// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Serializer
{
    using Entities;
    using System.Collections.Generic;
    using Brook;
    using ProtocolDef;
    using System;
    using Brook.Octet;
    using Log;
    using SimulationFrame;
    using Coherence.Common;
    using Connection;
    using Version = ProtocolDef.Version;

    public static class Serialize
    {
        private static readonly uint NUM_BITS_FOR_END_OF_ENTITIES = 3;
        private static readonly uint DEBUG_NUM_BITS_FOR_END_OF_ENTITIES = NUM_BITS_FOR_END_OF_ENTITIES + (uint)Brook.DebugStreamTypes.DebugBitsSize(3); //three writes
        public const uint NUM_BITS_FOR_MESSAGE_TYPE = 8;
        public const int NUM_BITS_FOR_DESTROY_REASON = 3;
        public const int NUM_BITS_FOR_SIMFRAME_DELTA_FLAG = 1;
        public const int MAX_SERIALIZED_MESSAGE_BYTES = 1024;
        public const int NUM_BITS_FOR_AUTHORITY = 1;
        public const int NUM_BITS_FOR_ORPHAN = 1;
        public const int NUM_BITS_FOR_OPERATION = 2;
        public const int NUM_BITS_FOR_LOD = 4;
        public const int NUM_BITS_FOR_COMPONENT_COUNT = 5;
        public const int NUM_BITS_FOR_COMPONENT_STATE = 2;
        public const int NUM_BITS_FOR_MESSAGE_TARGET = 2;
        public const int NUM_BITS_FOR_CHANNEL_ID = 4;

        private static uint ChannelIDBits(SerializerContext<IOutBitStream> ctx)
        {
            var numBitsForChannelID = ctx.UseDebugStreams
                ? NUM_BITS_FOR_CHANNEL_ID + (uint)DebugStreamTypes.DebugBitsSize(1)
                : NUM_BITS_FOR_CHANNEL_ID;
            return ctx.ProtocolVersion >= Version.VersionIncludesChannelID ? numBitsForChannelID : 0;
        }

        private static uint MessageTypeBits(SerializerContext<IOutBitStream> ctx)
        {
            return ctx.UseDebugStreams
                ? NUM_BITS_FOR_MESSAGE_TYPE + (uint)DebugStreamTypes.DebugBitsSize(1)
                : NUM_BITS_FOR_MESSAGE_TYPE;
        }

        public static void WriteEntityUpdates(
            List<Entity> writtenEntitiesBuffer,
            IReadOnlyList<EntityChange> changes,
            AbsoluteSimulationFrame referenceSimulationFrame,
            ISchemaSpecificComponentSerialize componentSerializer,
            SerializerContext<IOutBitStream> ctx)
        {
            if (changes.Count == 0)
            {
                return;
            }

            var rewindPoint = ctx.BitStream.Position;

            ctx.StartSection(nameof(MessageType.EcsWorldUpdate));
            WriteMessageType(MessageType.EcsWorldUpdate, ctx.BitStream);

            ctx.SetBitsRemainingInEmptyPacket();

            using (_ = NewEndOfEntitiesReservationScope(ctx))
            {
                // make sure we have at least enough to write an empty entity update.
                if (ctx.IsStreamFull())
                {
                    ctx.BitStream.Seek(rewindPoint);
                    return;
                }

                Entities.Index lastIndex = 0;

                for (var i = 0; i < changes.Count; i++)
                {
                    var change = changes[i];
                    ctx.SetEntity(change.ID);

                    rewindPoint = ctx.BitStream.Position;
                    uint bitsTaken = 0;

                    if (change.Update.IsDestroy)
                    {
                        SerializeDestroyed(change, ctx, ref lastIndex);
                    }
                    else
                    {
                        SerializeUpdated(change, referenceSimulationFrame, componentSerializer, ctx, ref lastIndex,
                            out bitsTaken);
                    }

                    if (ctx.IsStreamFull())
                    {
                        ctx.BitStream.Seek(rewindPoint);

                        var maxSizeBits = ctx.FreeBitsInEmptyPacket;
                        if (bitsTaken > maxSizeBits)
                        {
                            ctx.Logger.Warning(Warning.SerializeTooBig,
                                ("entity", change.ID),
                                ("entitySizeBits", bitsTaken),
                                ("maxSizeBits", maxSizeBits));
                        }

                        break;
                    }

                    writtenEntitiesBuffer.Add(change.ID);
                }

                WriteEndOfEntities(ctx);
            }

            ctx.EndSection();
        }

        private static bool SerializeSimulationFrame(AbsoluteSimulationFrame referenceSimulationFrame,
            IOutBitStream bitStream,
            Logger logger,
            AbsoluteSimulationFrame simulationFrame)
        {
            var isValid = true;

            AbsoluteSimulationFrame frameDelta = 0;

            if (simulationFrame != 0)
            {
                frameDelta = simulationFrame - referenceSimulationFrame;
                if (frameDelta > byte.MaxValue)
                {
                    frameDelta = byte.MaxValue;
                }
                else if (frameDelta < -byte.MaxValue)
                {
                    frameDelta = -byte.MaxValue;
                    isValid = false;
                }
            }

            if (frameDelta == 0)
            {
                bitStream.WriteBits(0, NUM_BITS_FOR_SIMFRAME_DELTA_FLAG);
            }
            else
            {
                bitStream.WriteBits(1, NUM_BITS_FOR_SIMFRAME_DELTA_FLAG);
                SerializeTools.WriteShortVarIntSigned(bitStream, (short)frameDelta);
            }

            return isValid;
        }

        // This one does more writes so it earlies out if it can't write the whole thing.
        public static void SerializeUpdated(EntityChange change,
            AbsoluteSimulationFrame referenceSimulationFrame,
            ISchemaSpecificComponentSerialize componentSerializer,
            SerializerContext<IOutBitStream> ctx,
            ref Entities.Index lastIndex,
            out uint bitsTaken)
        {
            if (change.Update.Operation == EntityOperation.Unknown)
            {
                ctx.Logger.Error(Error.SerializeInvalidEntityOperation,
                    ("entity", change.ID),
                    ("operation", change.Update.Operation));
                bitsTaken = 0;
                return;
            }


            var initialRemainingBitCount = ctx.BitStream.RemainingBitCount;
            var initialBufferPosition = ctx.BitStream.Position;

            lastIndex = WriteEntityIndex(change.ID, ctx.BitStream, lastIndex);

            WriteEntityMeta(change.Meta, ctx.BitStream);

            var entityRefSimulationFrame = GetMinSimFrame(change, ctx.Logger) ?? referenceSimulationFrame;
            var isRefSimFrameValid = SerializeSimulationFrame(referenceSimulationFrame, ctx.BitStream, ctx.Logger, entityRefSimulationFrame);

            WriteComponentCount(change.Update.Components.Count, ctx.BitStream);

            foreach (var kvp in change.Update.Components.Updates.Store)
            {
                const ComponentState state = ComponentState.Update;
                uint baseType = kvp.Key;
                uint lodedType = kvp.Value.ComponentSerializeType;
                ctx.SetComponent(lodedType);

                WriteComponentState(state, ctx.BitStream);
                WriteComponentId(lodedType, ctx.BitStream);

                var protocolStream = OutProtocolBitStream.Shared.Reset(ctx.BitStream, ctx.Logger);
                uint leftoverMask = componentSerializer.WriteComponentUpdate(kvp.Value.Data, lodedType, isRefSimFrameValid, entityRefSimulationFrame, protocolStream, ctx.Logger);
                if (leftoverMask != 0)
                {
                    // Changed to debug since we know this is possible but we should only see it when we're tracking down
                    // issues.
                    ctx.Logger.Debug("After serializing a component, it's mask wasn't fully consumed.",
                        ("entity", change.ID),
                        ("componentType", baseType),
                        ("lodedType", lodedType),
                        ("originalMask", kvp.Value.Data.FieldsMask),
                        ("leftoverMask", leftoverMask));
                }
            }

            foreach (var componentType in change.Update.Components.Destroys)
            {
                const ComponentState state = ComponentState.Destruct;
                ctx.SetComponent(componentType);

                WriteComponentState(state, ctx.BitStream);
                WriteComponentId(componentType, ctx.BitStream);
            }

            if (ctx.BitStream.IsFull)
            {
                bitsTaken = initialRemainingBitCount + ctx.BitStream.OverflowBitCount;
            }
            else
            {
                bitsTaken = ctx.BitStream.Position - initialBufferPosition;
            }

            if (change.Update.IsUpdate)
            {
                ctx.Logger.Trace(
                    "SerializeUpdated",
                    ("entity", change.ID),
                    ("operation", change.Update.Operation),
                    ("comps", change.Update.Components));
            }
            else
            {
                ctx.Logger.Debug(
                    "SerializeUpdated",
                    ("entity", change.ID),
                    ("operation", change.Update.Operation),
                    ("comps", change.Update.Components));
            }
        }

        private static void SerializeDestroyed(EntityChange change,
            SerializerContext<IOutBitStream> ctx,
            ref Entities.Index lastIndex)
        {
            if (change.Update.Operation == EntityOperation.Unknown)
            {
                ctx.Logger.Error(Error.SerializeInvalidEntityOperation,
                    ("entity", change.ID),
                    ("operation", change.Update.Operation));
                return;
            }

            lastIndex = WriteEntityIndex(change.ID, ctx.BitStream, lastIndex);

            WriteEntityMeta(change.Meta, ctx.BitStream);

            WriteEntityDestroyReason(change.Meta.DestroyReason, ctx.BitStream);

            ctx.Logger.Debug("SerializeDestroyed", ("entity", change.ID));
        }

        public static void WriteMessageType(MessageType messageType, IOutBitStream outBitStream)
        {
            byte messageOctet = (byte)messageType;
            outBitStream.WriteUint8(messageOctet);
        }

        private static void WriteMessageIDDelta(ushort id, ushort lastId, IOutBitStream stream)
        {
            var delta = id - lastId;
            SerializeTools.WriteShortVarInt(stream, (ushort)Math.Abs(delta));
            stream.WriteBits(delta < 0 ? 0u : 1u, 1);
        }

        private static Entities.Index WriteEntityIndex(Entity entityId, IOutBitStream stream, Entities.Index lastIndex)
        {
            var delta = entityId.Index - lastIndex;

            WriteEntityIndexDelta((int)delta, stream);
            return entityId.Index;
        }

        private static void WriteEntityIndexDelta(int delta, IOutBitStream stream)
        {
            SerializeTools.WriteShortVarInt(stream, (ushort)Math.Abs(delta));
            stream.WriteBits(delta < 0 ? 0u : 1u, 1);
        }

        private static void WriteEntityMeta(SerializedMeta entityMeta, IOutBitStream stream)
        {
            const bool shouldWriteMeta = true;    // We currently always write meta
            stream.WriteBits(shouldWriteMeta ? 1u : 0u, 1);
            if (shouldWriteMeta)
            {
                WriteEntityVersion(entityMeta.Version, stream);
                WriteEntityAuthority(entityMeta.HasStateAuthority, stream);
                WriteEntityAuthority(entityMeta.HasInputAuthority, stream);
                WriteEntityOrphan(entityMeta.IsOrphan, stream);
                WriteEntityLOD(entityMeta.LOD, stream);
                WriteEntityOperation(entityMeta.Operation, stream);
            }
        }

        private static void WriteEntityDestroyReason(DestroyReason reason, IOutBitStream stream)
        {
            stream.WriteBits((uint)reason, NUM_BITS_FOR_DESTROY_REASON);
        }

        private static void WriteEntityAuthority(bool hasAuthority, IOutBitStream stream)
        {
            stream.WriteBits(hasAuthority ? 1u : 0u, NUM_BITS_FOR_AUTHORITY);
        }

        private static void WriteEntityOrphan(bool isOrphan, IOutBitStream stream)
        {
            stream.WriteBits(isOrphan ? 1u : 0u, NUM_BITS_FOR_ORPHAN);
        }

        private static void WriteEntityLOD(uint lod, IOutBitStream stream)
        {
            stream.WriteBits(lod, NUM_BITS_FOR_LOD);
        }

        private static void WriteEntityOperation(EntityOperation operation, IOutBitStream stream)
        {
            stream.WriteBits((uint)operation, NUM_BITS_FOR_OPERATION);
        }

        private static void WriteEntityVersion(uint version, IOutBitStream stream)
        {
            uint serializeVersion = version % Entity.MaxVersions;
            stream.WriteBits(serializeVersion, Entity.NumVersionBits);
        }

        private static SerializerContext<IOutBitStream>.ReservationScope NewEndOfEntitiesReservationScope(SerializerContext<IOutBitStream> ctx)
        {
            var endOfEntitiesSize = NUM_BITS_FOR_END_OF_ENTITIES;
            if (ctx.UseDebugStreams)
            {
                endOfEntitiesSize = DEBUG_NUM_BITS_FOR_END_OF_ENTITIES;
            }

            return ctx.NewReservationScope(endOfEntitiesSize);
        }

        private static void WriteEndOfEntities(SerializerContext<IOutBitStream> ctx)
        {
            SerializeTools.WriteShortVarInt(ctx.BitStream, (ushort)Entity.EndOfEntities);
        }

        private static void WriteComponentCount(int count, IOutBitStream stream)
        {
            stream.WriteBits((uint)count, NUM_BITS_FOR_COMPONENT_COUNT);
        }

        private static void WriteComponentState(ComponentState state, IOutBitStream stream)
        {
            stream.WriteBits((uint)state, NUM_BITS_FOR_COMPONENT_STATE);
        }

        private static void WriteComponentId(uint componentSerializeId, IOutBitStream stream)
        {
            stream.WriteUint16((ushort)componentSerializeId);
        }

        private static void WriteMessageEntityId(Entity entityID, IOutBitStream outBitStream)
        {
            entityID.AssertRelative();

            SerializeTools.SerializeEntity(entityID, outBitStream);
        }

        private static void WriteMessageTarget(MessageTarget target, IOutBitStream outBitStream)
        {
            outBitStream.WriteBits((uint)target, NUM_BITS_FOR_MESSAGE_TARGET);
        }

        public static void WriteMessages(
            List<SerializedEntityMessage> serializedMessagesBuffer,
            MessageType messageType,
            Queue<SerializedEntityMessage> messages,
            SerializerContext<IOutBitStream> ctx)
        {
            ctx.StartSection(messageType.AsString());
            MessageQueueSerializer.SerializeQueue(serializedMessagesBuffer, messageType, ctx, messages, bitBudget: ctx.RemainingUnreservedBitCount);
            ctx.EndSection();
        }

        public static List<MessageID> WriteOrderedCommands(List<(MessageID, SerializedEntityMessage)> messages, SerializerContext<IOutBitStream> ctx)
        {
            ctx.StartSection("OrderedCommands");

            List<MessageID> res = new(32);
            ushort lastMessageID = 0;

            using (_ = NewEndOfMessagesReservationScope(ctx))
            {
                foreach (var (id, message) in messages)
                {
                    var rewindPos = ctx.BitStream.Position;

                    WriteMessageType(MessageType.Command, ctx.BitStream);

                    WriteMessageIDDelta(id.Value, lastMessageID, ctx.BitStream);
                    lastMessageID = id.Value;

                    ctx.SetEntity(message.TargetEntity);
                    ctx.BitStream.WriteBytesUnaligned(message.Octets, (int)message.BitCount);

                    // Ensure there are enough bits left to write both EndOfMessages and EndOfChannels
                    if (!ctx.IsStreamFull())
                    {
                        res.Add(id);
                    }
                    else
                    {
                        ctx.BitStream.Seek(rewindPos);
                        break;
                    }
                }

                if (res.Count > 0)
                {
                    WriteEndOfMessages(ctx);
                }
            }

            ctx.EndSection();

            return res;
        }

        public static SerializedEntityMessage SerializeCommand(
            IEntityCommand command,
            ISchemaSpecificComponentSerialize serializer,
            bool useDebugStream,
            Logger logger,
            bool includeSenderClientId =  false)
        {
            var target = command.Target;
            var id = command.Entity;
            var hasMeta = command.UsesMeta;

            var buffer = PrepareMessageSerialization(target, command,
                id, useDebugStream,
                logger, out var bitStream,
                out var fieldStream);

            SerializeCommandMeta(command, includeSenderClientId, fieldStream, hasMeta);

            serializer.WriteCommand(command, command.GetComponentType(), fieldStream, logger);

            if (bitStream.IsFull)
            {
                throw new Exception($"Command is too large and will not be sent to {target}.");
            }

            bitStream.Flush();

            return new SerializedEntityMessage(id, buffer, bitStream.Position);
        }

        private static void SerializeCommandMeta(IEntityCommand command, bool includeSenderClientId,
            OutProtocolBitStream fieldStream, bool hasMeta)
        {
            fieldStream.WriteBool(hasMeta);

            if (hasMeta)
            {
                fieldStream.WriteBool(includeSenderClientId);
                if (includeSenderClientId)
                {
                    fieldStream.WriteBits((uint)command.SenderClientID, ClientID.SIZE_IN_BITS);
                }

                fieldStream.WriteLong(command.Frame);
            }
        }

        public static SerializedEntityMessage SerializeInput(
            MessageTarget target,
            IEntityMessage message,
            Entity id,
            ISchemaSpecificComponentSerialize serializer,
            bool useDebugStream,
            Logger logger)
        {
            var buffer = PrepareMessageSerialization(target, message,
                id, useDebugStream,
                logger, out var bitStream,
                out var fieldStream);

            var input = (IEntityInput)message;
            fieldStream.WriteLong(input.Frame);

            serializer.WriteInput(input, message.GetComponentType(), fieldStream, logger);

            if (bitStream.IsFull)
            {
                throw new Exception($"Input is too large and will not be sent to {target}.");
            }

            bitStream.Flush();

            return new SerializedEntityMessage(id, buffer, bitStream.Position);
        }

        private static byte[] PrepareMessageSerialization(MessageTarget target,
            IEntityMessage message, Entity id, bool useDebugStream, Logger logger,
            out IOutBitStream bitStream, out OutProtocolBitStream fieldStream)
        {
            var buffer = new byte[MAX_SERIALIZED_MESSAGE_BYTES];
            var octetStream = new OutOctetStream(buffer);
            bitStream = new OutBitStream(octetStream);
            if (useDebugStream)
            {
                bitStream = new DebugOutBitStream(bitStream);
            }

            WriteMessageEntityId(id, bitStream);
            WriteMessageTarget(target, bitStream);
            WriteComponentId(message.GetComponentType(), bitStream);

            fieldStream = OutProtocolBitStream.Shared.Reset(bitStream, logger);
            return buffer;
        }

        public static void WriteFloatingOrigin(Vector3d floatingOrigin, SerializerContext<IOutBitStream> ctx)
        {
            ctx.Logger.Trace("WriteFloatingOrigin", ("origin", floatingOrigin.ToString()));

            ctx.StartSection("FloatOrigin");
            var protocolBitStream = OutProtocolBitStream.Shared.Reset(ctx.BitStream, ctx.Logger);

            protocolBitStream.WriteVector3d(floatingOrigin);
            ctx.EndSection();
        }

        private static AbsoluteSimulationFrame? GetMinSimFrame(EntityChange change, Logger logger)
        {
            AbsoluteSimulationFrame? min = null;

            foreach (var component in change.Update.Components.Updates.Store)
            {
                var result = component.Value.Data.GetMinSimulationFrame();

                if (result == 0)
                {
                    logger.Error(Error.SerializeSimulationFrameZero, ("component", component.Value.Data));
                }

                if (result == null)
                {
                    continue;
                }

                if (min == null || result < min)
                {
                    min = result;
                }
            }

            return min;
        }

        public static void WriteChannelID(ChannelID channelID, SerializerContext<IOutBitStream> ctx)
        {
            if (ctx.ProtocolVersion >= Version.VersionIncludesChannelID)
            {
                if (!channelID.IsValid())
                {
                    throw new Exception($"Invalid ChannelID {channelID}, only channels {ChannelID.MinValue}-{ChannelID.MaxValue} are supported");
                }

                ctx.BitStream.WriteBits((byte)channelID, NUM_BITS_FOR_CHANNEL_ID);
                if (ctx.BitStream.IsFull && ctx.BitStream.OverflowBitCount > 0)
                {
                    throw new Exception("Failed to write ChannelID, not enough space left");
                }
            }
        }

        public static SerializerContext<IOutBitStream>.ReservationScope NewEndOfMessagesReservationScope(SerializerContext<IOutBitStream> ctx)
        {
            return ctx.NewReservationScope(MessageTypeBits(ctx));
        }

        public static void WriteEndOfMessages(SerializerContext<IOutBitStream> ctx)
        {
            // if (ctx.ProtocolVersion >= Version.VersionIncludesEndOfMessagesMarker)
            WriteMessageType(MessageType.EndOfMessages, ctx.BitStream);
            if (ctx.BitStream.IsFull && ctx.BitStream.OverflowBitCount > 0)
            {
                throw new Exception("Failed to write EndOfMessages, not enough space left for marker");
            }
        }

        public static SerializerContext<IOutBitStream>.ReservationScope NewEndOfChannelsReservationScope(SerializerContext<IOutBitStream> ctx)
        {
            return ctx.NewReservationScope(ChannelIDBits(ctx));
        }

        public static void WriteEndOfChannels(SerializerContext<IOutBitStream> ctx)
        {
            if (ctx.ProtocolVersion >= Version.VersionIncludesChannelID)
            {
                ctx.BitStream.WriteBits((byte)ChannelID.EndOfChannels, NUM_BITS_FOR_CHANNEL_ID);
                if (ctx.BitStream.IsFull && ctx.BitStream.OverflowBitCount > 0)
                {
                    throw new Exception("Failed to write EndOfChannels, not enough space left");
                }
            }
        }
    }
}
