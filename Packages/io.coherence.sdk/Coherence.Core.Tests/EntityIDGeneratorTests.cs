// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core.Tests
{
    using Moq;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    using Coherence.Entities;
    using Coherence.Log;
    using Coherence.Tests;

    public class EntityIDGeneratorTests : CoherenceTest
    {
        [TestCase(1, (Entities.Index)100)]
        [TestCase(5, (Entities.Index)100)]
        [TestCase(10, (Entities.Index)100)]
        [TestCase(99, (Entities.Index)100)]
        [TestCase((int)Entity.MaxRelativeIndices, Entity.MaxRelativeID)]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public void CanGenerateIDs(int numIDs, Entities.Index maxID)
        {
            var logger = Log.GetLogger<EntityIDGeneratorTests>();
            var entityIDGenerator = new EntityIDGenerator((Entities.Index)1, maxID, Entity.Relative, logger);
            var entities = new Dictionary<Entities.Index, Entity>();

            for (ushort i = 0; i < numIDs; i++)
            {
                var err = entityIDGenerator.GetEntity(out var entity);
                Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                Assert.False(entities.ContainsKey(entity.Index)); //Make sure they're unique
                entities.Add(entity.Index, entity);
            }
        }

        [Test]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public void CanRecycleAllIDs()
        {
            var logger = Log.GetLogger<EntityIDGeneratorTests>();
            var entityIDGenerator = new EntityIDGenerator((Entities.Index)1, Entity.MaxRelativeID, Entity.Relative, logger);
            var numIDs = (int)Entity.MaxRelativeID;

            var entities = new Queue<Entity>();
            var indices = new HashSet<Entities.Index>();

            EntityIDGenerator.Error err;

            for (ushort i = 0; i < numIDs; i++)
            {
                err = entityIDGenerator.GetEntity(out var entity);
                Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                entities.Enqueue(entity);
                Assert.False(indices.Contains(entity.Index));
                Assert.That(entity.Index, Is.EqualTo((Entities.Index)(i + 1)));
                _ = indices.Add(entity.Index);
            }

            // Can't get any more.
            err = entityIDGenerator.GetEntity(out var _);
            Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.OutOfIDs));

            for (ushort i = 0; i < numIDs; i++)
            {
                var entity = entities.Dequeue();
                _ = indices.Remove(entity.Index);
                entityIDGenerator.ReleaseEntity(entity);
            }

            for (ushort i = 0; i < numIDs; i++)
            {
                err = entityIDGenerator.GetEntity(out var entity);
                Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                entities.Enqueue(entity);
                Assert.False(indices.Contains(entity.Index));
                Assert.That(entity.Index, Is.EqualTo((Entities.Index)(i + 1)));
                _ = indices.Add(entity.Index);
            }

            // Can't get any more.
            err = entityIDGenerator.GetEntity(out var _);
            Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.OutOfIDs));
        }

        [TestCase(1, 10)]
        [TestCase(10, 10)]
        [TestCase(100, 10)]
        [TestCase((int)Entity.MaxRelativeIndices, 1)]
        [TestCase((int)Entity.MaxRelativeIndices, 3)]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public void CanRecycleIDsLinearly(int numIDs, int iterations)
        {
            var logger = Log.GetLogger<EntityIDGeneratorTests>();
            var endID = (Entities.Index)numIDs;
            var entityIDGenerator = new EntityIDGenerator((Entities.Index)1, endID, Entity.Relative, logger);
            var lastIndex = numIDs == 1 ? (Entities.Index)1 : Entity.MaxRelativeID;

            for (int i = 0; i < iterations; i++)
            {
                for (int j = 0; j < numIDs; j++)
                {
                    var err = entityIDGenerator.GetEntity(out var entity);
                    Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                    if (numIDs > 1)
                    {
                        Assert.That(entity.Index, Is.Not.EqualTo(lastIndex));
                    }
                    else
                    {
                        Assert.That(entity.Index, Is.EqualTo(lastIndex));
                    }
                    entityIDGenerator.ReleaseEntity(entity);
                    lastIndex = entity.Index;
                }
            }
        }

        [TestCase(1, 10)]
        [TestCase(10, 10)]
        [TestCase(100, 10)]
        [TestCase((int)Entity.MaxRelativeIndices, 10)]
        [TestCase((int)Entity.MaxRelativeIndices, 100)]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public void CanRecycleIDsRandomly(int numIDs, int multiplier)
        {
            var random = new System.Random(1234);
            var logger = Log.GetLogger<EntityIDGeneratorTests>();
            var endID = (Entities.Index)numIDs;
            var entityIDGenerator = new EntityIDGenerator((Entities.Index)1, endID, Entity.Relative, logger);
            var iterations = (int)Entity.MaxRelativeIndex * multiplier;

            var entities = new Queue<Entity>();
            var indices = new HashSet<Entities.Index>();

            for (int i = 0; i < iterations; i++)
            {
                var rand = random.Next() % 2;
                if (entities.Count < numIDs
                    && (rand == 0 || entities.Count == 0))
                {
                    var err = entityIDGenerator.GetEntity(out var entity);
                    Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                    Assert.False(indices.Contains(entity.Index));
                    _ = indices.Add(entity.Index);
                    entities.Enqueue(entity);
                }
                else if (entities.Count > 0)
                {
                    var entity = entities.Dequeue();
                    _ = indices.Remove(entity.Index);
                    entityIDGenerator.ReleaseEntity(entity);
                }
            }
        }

        [TestCase(1, 2)]
        [TestCase(10, 11)]
        [TestCase(100, 101)]
        [TestCase((int)Entity.MaxRelativeIndices, (int)Entity.MaxRelativeIndices + 1)]
        public void TooManyIDs(int numIDs, int numToGenerate)
        {
            var logger = Log.GetLogger<EntityIDGeneratorTests>();
            var endID = (Entities.Index)numIDs;
            var entityIDGenerator = new EntityIDGenerator((Entities.Index)1, endID, Entity.Relative, logger);
            var entities = new HashSet<Entities.Index>();

            for (int i = 0; i < numToGenerate; i++)
            {
                var err = entityIDGenerator.GetEntity(out var entity);
                if (i < numToGenerate - 1)
                {
                    Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                }
                else
                {
                    Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.OutOfIDs));
                }
                Assert.False(entities.Contains(entity.Index)); //Make sure they're unique
                _ = entities.Add(entity.Index);
            }
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase((int)Entity.MaxRelativeIndices)]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public void VersionIncrementsCorrectly(int numIDs)
        {
            var logger = Log.GetLogger<EntityIDGeneratorTests>();
            var endID = (Entities.Index)numIDs;
            var entityIDGenerator = new EntityIDGenerator((Entities.Index)1, endID, Entity.Relative, logger);
            var entities = new Queue<Entity>();

            for (int v = 0; v < Entity.MaxVersions + 1; v++)
            {
                for (int i = 0; i < numIDs; i++)
                {
                    var err = entityIDGenerator.GetEntity(out var entity);
                    Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                    Assert.That(entity.Version, Is.EqualTo(v % Entity.MaxVersions));
                    entities.Enqueue(entity);
                }

                for (int i = 0; i < numIDs; i++)
                {
                    var entity = entities.Dequeue();
                    entityIDGenerator.ReleaseEntity(entity);
                }
            }
        }

        private void TestEdgeValues(EntityIDGenerator entityIDGenerator, Entities.Index endID)
        {
            _ = Assert.Throws<Exception>(() =>
            {
                entityIDGenerator.ReleaseEntity(new Entity(0, 0, Entity.Relative));
            });

            _ = Assert.Throws<Exception>(() =>
            {
                entityIDGenerator.ReleaseEntity(new Entity(endID + 1, 0, Entity.Relative));
            });
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase((int)Entity.MaxRelativeIndices)]
#if COHERENCE_SKIP_LONG_UNIT_TESTS
        [Ignore("Long running test")]
#endif
        public void InvalidRecycle(int numIDs)
        {
            var logger = Log.GetLogger<EntityIDGeneratorTests>();
            var endID = (Entities.Index)numIDs;
            var entityIDGenerator = new EntityIDGenerator((Entities.Index)1, endID, Entity.Relative, logger);

            TestEdgeValues(entityIDGenerator, endID);

            var entities = new Queue<Entity>();

            for (int i = 0; i < numIDs; i++)
            {
                var err = entityIDGenerator.GetEntity(out var entity);
                Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                entities.Enqueue(entity);
            }

            TestEdgeValues(entityIDGenerator, endID);

            for (int i = 0; i < numIDs; i++)
            {
                var entity = entities.Dequeue();
                entityIDGenerator.ReleaseEntity(entity);
            }

            for (int i = 0; i < (int)Entity.MaxRelativeID; i++)
            {
                _ = Assert.Throws<Exception>(() =>
                {
                    entityIDGenerator.ReleaseEntity(new Entity((Entities.Index)i, 0, Entity.Relative));
                });
            }

            _ = Assert.Throws<Exception>(() =>
            {
                var err = entityIDGenerator.GetEntity(out var entity);
                Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                entityIDGenerator.ReleaseEntity(new Entity(entity.Index, entity.Version, Entity.Absolute));
            });
        }

        [Test]
        public void IDGenerationAfterReset()
        {
            var logger = Log.GetLogger<EntityIDGeneratorTests>();
            var entityIDGenerator = new EntityIDGenerator((Entities.Index)1, Entity.MaxRelativeID, Entity.Relative, logger);
            var entities = new List<Entity>();
            var numIDs = 10;

            for (ushort i = 0; i < numIDs; i++)
            {
                var err = entityIDGenerator.GetEntity(out var entity);
                Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                entities.Add(entity);
            }

            entityIDGenerator.Reset();

            for (ushort i = 0; i < numIDs; i++)
            {
                var err = entityIDGenerator.GetEntity(out var entity);
                Assert.That(err, Is.EqualTo(EntityIDGenerator.Error.None));
                Assert.That(entity, Is.EqualTo(entities[i]));
            }
        }

    }
}
