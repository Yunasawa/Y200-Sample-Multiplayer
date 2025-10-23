// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using System.Collections.Generic;
    using Coherence.Entities;
    using Coherence.Log;

    internal class SentCache<T> where T : class
    {
        protected LinkedList<T> sentChanges = new();
        protected Queue<uint> sentSequenceIds = new();

        private Logger logger;

        public SentCache(Logger logger)
        {
            this.logger = logger;
        }

        public bool TryDequeue(uint expectedSequenceId, out T acked, out LinkedList<T> inFlight)
        {
            if (sentChanges.Count == 0)
            {
                acked = null;
                inFlight = sentChanges;
                return false;
            }

            acked = sentChanges.Last.Value;
            sentChanges.RemoveLast();

            var sequenceId = sentSequenceIds.Dequeue();

            if (sequenceId != expectedSequenceId)
            {
                logger.Error(Error.CoreChannelOutOrderedNetworkChannelSequenceID,
                    ("expected", expectedSequenceId),
                    ("queued", sequenceId));
            }

            inFlight = sentChanges;

            return true;
        }

        public void Enqueue(T sent, uint sequenceId)
        {
            _ = sentChanges.AddFirst(sent);
            sentSequenceIds.Enqueue(sequenceId);
        }
    }

    internal class ChangeBufferSentCache : SentCache<ChangeBuffer>
    {
        public ChangeBufferSentCache(Logger logger) : base(logger) { }

        public void ClearAllChangesForEntity(Entity id)
        {
            foreach (var changes in sentChanges)
            {
                changes.ClearAllChangesForEntity(id);
            }
        }

        public bool HasChangesForEntity(Entity id)
        {
            foreach (var changes in sentChanges)
            {
                if (changes != null && changes.HasChangesForEntity(id))
                {
                    return true;
                }
            }

            return false;
        }

        public void ClearComponentChangesForEntity(Entity id, uint componentID)
        {
            foreach (var changes in sentChanges)
            {
                if (changes != null)
                {
                    changes.ClearComponentChangesForEntity(id, componentID);
                }
            }
        }

        public bool HasComponentChangesForEntity(Entity id, uint componentID)
        {
            foreach (var changes in sentChanges)
            {
                if (changes != null && changes.HasChangesForEntity(id))
                {
                    return true;
                }
            }

            return false;
        }

        public void BumpPriorities()
        {
            foreach (var changes in sentChanges)
            {
                if (changes != null)
                {
                    changes.ReprioritizeChanges(SendChangeBuffer.HELDBACK_PRIORITY);
                }
            }
        }

        public void GetOrderedComponents(Entity entity, IComponentInfo componentInfo, out DeltaComponents? components)
        {
            var comps = new DeltaComponents();

            for (var sentBuffer = sentChanges.Last; sentBuffer != null; sentBuffer = sentBuffer.Previous)
            {
                sentBuffer.Value.MergeIfOrderedComponents(entity, ref comps, componentInfo);
            }

            components = comps.IsInitialized ? comps : null;
        }
    }
}
