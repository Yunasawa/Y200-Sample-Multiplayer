// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Channels
{
    using System;
    using System.Collections.Generic;
    using Coherence.Brook;
    using Coherence.Entities;
    using Coherence.Log;
    using Coherence.ProtocolDef;
    using Coherence.Serializer;
    using Coherence.SimulationFrame;
    using Coherence.Stats;

    /// <summary>
    /// OutNetworkChannel is responsible for buffering outgoing changes, and also serializing them
    /// while taking care of priority (in case of unordered channels) or ordering (in case of ordered channel).
    /// </summary>
    internal class OutNetworkChannel : IOutNetworkChannel
    {
        private readonly ChannelSerializationResult lastSerializationResult = new();

        public event Action<Entity> OnEntityAcked;

        private readonly ISchemaSpecificComponentSerialize serializer;
        private readonly IComponentInfo definition;

        private SendChangeBuffer changeBuffer;
        private ChangeBufferSentCache sentCache;

        // Cache for RemoveComponents() so we don't need to re-allocate the list every time
        private readonly List<uint> removesToApply = new(32);

        // Cache for Serialize() so we don't need to re-allocate the lists every time
        private readonly List<EntityChange> existenceChangeBuffer = new(32);
        private readonly List<EntityChange> updateChangeBuffer = new(32);

        // Cache for Serialize() so we don't need to re-allocate the queue every time
        private readonly Queue<SerializedEntityMessage> emptyQueue = new();

        private readonly Stats stats;
        private readonly Logger logger;

        public OutNetworkChannel(
            ISchemaSpecificComponentSerialize serializer,
            IComponentInfo definition,
            Stats stats,
            Logger logger)
        {
            this.serializer = serializer;
            this.definition = definition;
            this.stats = stats;
            this.logger = logger.With<OutNetworkChannel>();

            changeBuffer = new SendChangeBuffer(definition, this.logger);
            sentCache = new ChangeBufferSentCache(this.logger);
        }

        public void CreateEntity(Entity id, ICoherenceComponentData[] data)
        {
            var componentUpdates = ComponentUpdates.New(data);
            changeBuffer.CreateEntity(new EntityCreateChange
            {
                ID = id,
                Data = componentUpdates,
            });
        }

        public void UpdateComponents(Entity id, ICoherenceComponentData[] data)
        {
            var componentUpdates = ComponentUpdates.New(data);
            if (componentUpdates.Count <= 0)
            {
                return;
            }

            changeBuffer.UpdateEntity(new EntityUpdateChange
            {
                ID = id,
                Data = componentUpdates,
                Priority = 1,
            });
        }

        public void RemoveComponents(Entity id, uint[] componentTypes, Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity)
        {
            removesToApply.Clear();

            foreach (var componentID in componentTypes)
            {
                // If we haven't ackd the creation of a component and there isn't
                // an ack pending, we can delete the change now, otherwise we
                // have to post the remove expecting that it will be ackd.
                // If the packet is dropped then the remove will override the
                // returning create when merged.
                var containsKey = ackedComponentsPerEntity.TryGetValue(id, out var comps);
                var ackedComponent = containsKey && comps.Contains(componentID);
                var compInFlight = sentCache.HasComponentChangesForEntity(id, componentID);

                if (!ackedComponent && !compInFlight)
                {
                    changeBuffer.ClearComponentChangesForEntity(id, componentID);
                }
                else
                {
                    removesToApply.Add(componentID);
                }
            }

            if (removesToApply.Count > 0)
            {
                changeBuffer.RemoveComponent(new EntityRemoveChange
                {
                    ID = id,
                    Remove = removesToApply.ToArray(),
                    Priority = 1
                });
            }
        }

        public void DestroyEntity(Entity id, IReadOnlyCollection<Entity> ackedEntities) => changeBuffer.DestroyEntity(id, ackedEntities);

        public void PushCommand(IEntityCommand command, bool useDebugStreams)
        {
            var serializedMessage = Serializer.Serialize.SerializeCommand(command, serializer, useDebugStreams, logger);
            if (serializedMessage != null)
            {
                changeBuffer.commandBuffer.Enqueue(serializedMessage);
            }
        }

        public void PushInput(IEntityInput message, bool useDebugStreams)
        {
            var serializedMessage = Serializer.Serialize.SerializeInput(MessageTarget.StateAuthorityOnly, message, message.Entity, serializer, useDebugStreams, logger);
            if (serializedMessage != null)
            {
                changeBuffer.inputBuffer.Enqueue(serializedMessage);
            }
        }

        public bool HasChangesForEntity(Entity entity)
        {
            return changeBuffer.HasChangesForEntity(entity) || sentCache.HasChangesForEntity(entity);
        }

        public void ClearAllChangesForEntity(Entity entity)
        {
            changeBuffer.ClearAllChangesForEntity(entity);
            sentCache.ClearAllChangesForEntity(entity);
        }

        public bool HasChanges(IReadOnlyCollection<Entity> ackedEntities) => changeBuffer.HasChanges(ackedEntities);

        public bool Serialize(SerializerContext<IOutBitStream> serializerCtx, AbsoluteSimulationFrame referenceSimulationFrame,
            bool holdOnToCommands, IReadOnlyCollection<Entity> ackedEntities)
        {
            existenceChangeBuffer.Clear();
            updateChangeBuffer.Clear();
            lastSerializationResult.Clear();

            changeBuffer.ApplyOrderedComponentsFromSent(sentCache, definition);
            changeBuffer.GetPrioritizedChanges(existenceChangeBuffer, updateChangeBuffer, ackedEntities);


            using (_ = Serializer.Serialize.NewEndOfMessagesReservationScope(serializerCtx))
            {
                Serializer.Serialize.WriteEntityUpdates(lastSerializationResult.ExistenceChangesSent, existenceChangeBuffer,
                    referenceSimulationFrame, serializer, serializerCtx);

                if (!holdOnToCommands)
                {
                    Serializer.Serialize.WriteMessages(lastSerializationResult.CommandsSent, MessageType.Command,
                        changeBuffer.commandBuffer, serializerCtx);
                    Serializer.Serialize.WriteMessages(lastSerializationResult.InputsSent, MessageType.Input,
                        changeBuffer.inputBuffer, serializerCtx);
                }

                Serializer.Serialize.WriteEntityUpdates(
                    lastSerializationResult.UpdateChangesSent, updateChangeBuffer,
                    referenceSimulationFrame, serializer, serializerCtx);

                if (lastSerializationResult.IsEmpty())
                {
                    return false;
                }

                Serializer.Serialize.WriteEndOfMessages(serializerCtx);
            }

            return true;
        }

        public Dictionary<Entity, OutgoingEntityUpdate> MarkAsSent(uint sequenceId)
        {
            if (lastSerializationResult.IsEmpty())
            {
                sentCache.Enqueue(new ChangeBuffer(null, emptyQueue, emptyQueue, logger), sequenceId);

                return null;
            }

            var updatesSent = new Dictionary<Entity, OutgoingEntityUpdate>();

            changeBuffer.AppendSentUpdates(ref updatesSent, lastSerializationResult.ReadOnly.ExistenceChangesSent);
            changeBuffer.AppendSentUpdates(ref updatesSent, lastSerializationResult.ReadOnly.UpdateChangesSent);

            changeBuffer.ReprioritizeChanges(SendChangeBuffer.HELDBACK_PRIORITY);

            var sentBuffer = new ChangeBuffer(
                updatesSent,
                new Queue<SerializedEntityMessage>(lastSerializationResult.CommandsSent),
                new Queue<SerializedEntityMessage>(lastSerializationResult.InputsSent),
                logger);
            sentCache.Enqueue(sentBuffer, sequenceId);

            if (stats != null)
            {
                stats.TrackOutgoingMessages(MessageType.EcsWorldUpdate, updatesSent?.Count ?? 0);
                stats.TrackOutgoingMessages(MessageType.Command, lastSerializationResult.CommandsSent.Count);
                stats.TrackOutgoingMessages(MessageType.Input, lastSerializationResult.InputsSent.Count);
            }

            // Bump priorities of changes in SentCache so if they are dropped they have the biggest priority
            // in ChangeBuffer so they are resent as fast as possible
            sentCache.BumpPriorities();

            return updatesSent;
        }

        public void OnDeliveryInfo(uint sequenceId, bool wasDelivered, ref HashSet<Entity> ackedEntities, ref Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity)
        {
            if (!sentCache.TryDequeue(sequenceId, out var ackChanges, out var inFlight))
            {
                logger.Error(Error.CoreChannelOutOrderedNetworkChannelAckNoSent);
                return;
            }

            if (!wasDelivered)
            {
                changeBuffer.ResetWithLostChanges(ackChanges, inFlight, ackedEntities);
                return;
            }

            changeBuffer.Acknowledge(ackChanges, inFlight);

            foreach (var (entity, entityUpdate) in ackChanges.Buffer)
            {
                if (entityUpdate.IsCreate)
                {
                    logger.Trace("Ack sent create",
                        ("entity", entity));

                    ackedEntities.Add(entity);

                    if (!ackedComponentsPerEntity.ContainsKey(entity))
                    {
                        ackedComponentsPerEntity.Add(entity, new HashSet<uint>());
                    }

                    UpdateAckedComponents(entity, entityUpdate, ref ackedComponentsPerEntity);
                }
                else if (entityUpdate.IsDestroy)
                {
                    logger.Trace("Ack sent destroy", ("entity", entity));

                    ackedEntities.Remove(entity);
                    ackedComponentsPerEntity.Remove(entity);
                }
                else
                {
                    logger.Trace("Ack sent change", ("entity", entity));

                    if (!ackedComponentsPerEntity.ContainsKey(entity))
                    {
                        // This is probably a server created entity like a client connection.
                        ackedComponentsPerEntity.Add(entity, new HashSet<uint>());
                    }

                    UpdateAckedComponents(entity, entityUpdate, ref ackedComponentsPerEntity);
                }

                OnEntityAcked?.Invoke(entity);

                entityUpdate.Return();
            }
        }

        public void Reset()
        {
            changeBuffer = new SendChangeBuffer(this.definition, logger);
            sentCache = new ChangeBufferSentCache(logger);
        }

        public void ClearLastSerializationResult()
        {
            lastSerializationResult.Clear();
        }

        private void UpdateAckedComponents(Entity id, OutgoingEntityUpdate update, ref Dictionary<Entity, HashSet<uint>> ackedComponentsPerEntity)
        {
            var ackedComponents = ackedComponentsPerEntity[id];

            // Union (non-allocating)
            foreach (var kv in update.Components.Updates.Store)
            {
                ackedComponents.Add(kv.Key);
            }

            // Except (non-allocating)
            foreach (var dest in update.Components.Destroys)
            {
                ackedComponents.Remove(dest);
            }
        }
    }

    internal class ChannelSerializationResult
    {
        public readonly List<Entity> ExistenceChangesSent = new(32);
        public readonly List<Entity> InternalEntitiesSent = new(32);
        public readonly List<Entity> AuthorityChangesSent = new(32);
        public readonly List<Entity> UpdateChangesSent = new(32);
        public readonly List<SerializedEntityMessage> CommandsSent = new(32);
        public readonly List<SerializedEntityMessage> InputsSent = new(32);
        public readonly List<Entity> Null = new(0);

        public readonly ReadOnlyAccess ReadOnly;

        public ChannelSerializationResult()
        {
            ReadOnly = new ReadOnlyAccess(this);
        }

        public bool IsEmpty() =>
            ExistenceChangesSent.Count == 0 &&
            InternalEntitiesSent.Count == 0 &&
            AuthorityChangesSent.Count == 0 &&
            UpdateChangesSent.Count == 0 &&
            CommandsSent.Count == 0 &&
            InputsSent.Count == 0;

        public void Clear()
        {
            ExistenceChangesSent.Clear();
            InternalEntitiesSent.Clear();
            AuthorityChangesSent.Clear();
            UpdateChangesSent.Clear();
            CommandsSent.Clear();
            InputsSent.Clear();
            Null.Clear();
        }

        // Not strictly necessary but makes it easier to understand which functions do not modify the result
        // or keep it around for longer than the function call.
        public class ReadOnlyAccess
        {
            private readonly ChannelSerializationResult channelSerializationResult;

            public ReadOnlyAccess(ChannelSerializationResult channelSerializationResult) =>
                this.channelSerializationResult = channelSerializationResult;

            public IReadOnlyList<Entity> ExistenceChangesSent => channelSerializationResult.ExistenceChangesSent;
            public IReadOnlyList<Entity> InternalEntitiesSent => channelSerializationResult.InternalEntitiesSent;
            public IReadOnlyList<Entity> AuthorityChangesSent => channelSerializationResult.AuthorityChangesSent;
            public IReadOnlyList<Entity> UpdateChangesSent => channelSerializationResult.UpdateChangesSent;
            public IReadOnlyList<SerializedEntityMessage> CommandsSent => channelSerializationResult.CommandsSent;
            public IReadOnlyList<SerializedEntityMessage> InputsSent => channelSerializationResult.InputsSent;
        }
    }
}
