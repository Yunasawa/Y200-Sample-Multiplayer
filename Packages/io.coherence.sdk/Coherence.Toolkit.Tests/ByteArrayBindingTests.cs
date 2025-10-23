namespace Coherence.Toolkit.Tests
{
    using Bindings;
    using Bindings.ValueBindings;
    using NUnit.Framework;
    using UnityEngine;
    using Coherence.Tests;

    public class ByteArrayBindingTest : CoherenceTest
    {
        public class TestBehaviour : MonoBehaviour
        {
            public byte[] bytes;
        }

        private TestBehaviour test;
        private ByteArrayBinding binding;

        public override void SetUp()
        {
            base.SetUp();

            test = new GameObject().AddComponent<TestBehaviour>();

            var memberInfo = typeof(TestBehaviour).GetMember("bytes")[0];
            var descriptor = new Descriptor(typeof(TestBehaviour), memberInfo);

            binding = (ByteArrayBinding)descriptor.InstantiateBinding(test);
        }

        [TestCase(null)]
        [TestCase(new byte[] { 1, 2, 3, 4 })]
        public void TestByteArrayBinding(byte[] value)
        {
            // Arrange

            // Act
            binding.Value = value;

            // Assert
            Assert.That(binding.Value, Is.EqualTo(value));

            // Act
            binding.Value = null;
            test.bytes = value;

            // Assert
            Assert.That(binding.Value, Is.EqualTo(value));
        }

        [TestCase(null, null, false)]
        [TestCase(null, new byte[] { 1, 2, 3, 4 }, true)]
        [TestCase(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3, 4 }, false)]
        [TestCase(new byte[] { 1, 2, 3, 4 }, null, true)]
        public void TestByteArrayBindingIsDirtyFullArray(byte[] initialValue, byte[] changedValue, bool isChanged)
        {
            // Arrange

            // Act
            binding.Value = initialValue;
            binding.IsDirty(0, out var isDirty, out var justStopped);

            // Assert
            Assert.True(isDirty);
            Assert.False(justStopped);

            // Act
            binding.Value = changedValue;
            binding.IsDirty(0, out isDirty, out justStopped);

            // Assert
            Assert.That(isDirty, Is.EqualTo(isChanged));
            Assert.False(justStopped);
        }

        [TestCase(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3, 4 }, false)]
        // just change one by index: https://github.com/coherence/unity/issues/5765
        [TestCase(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 42, 4 }, true)]
        public void TestByteArrayBindingIsDirtyByIndex(byte[] initialValue, byte[] changedValue, bool isChanged)
        {
            // Arrange
            // so that we don't modify the initial value because
            // its a reference type.
            var initialV = new byte[initialValue.Length];
            initialValue.CopyTo(initialV, 0);

            // Act
            binding.Value = initialV;
            binding.IsDirty(0, out var isDirty, out var justStopped);

            // Assert
            Assert.True(isDirty);
            Assert.False(justStopped);

            // Act
            for (int i = 0; i < changedValue.Length; i++)
            {
                binding.Value[i] = changedValue[i];
            }

            binding.IsDirty(0, out isDirty, out justStopped);

            // Assert
            Assert.That(isDirty, Is.EqualTo(isChanged));
            Assert.False(justStopped);
        }
    }
}
