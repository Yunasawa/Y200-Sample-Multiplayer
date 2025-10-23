// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Common.Tests
{
    using NUnit.Framework;

    public class Vector3dTests
    {
        [Test]
        public void Vector3d_Equality()
        {
            Assert.That(new Vector3d(1, 2, 3) == new Vector3d(1, 2, 3), Is.True);
            Assert.That(new Vector3d(1, 2, 3) == new Vector3d(1.00000000001, 2, 3), Is.True);
            Assert.That(new Vector3d(1, 2, 3) == new Vector3d(1.0000000001, 2, 3), Is.False);

            Assert.That(new Vector3d(1, 2, 3) != new Vector3d(1, 2, 3), Is.False);
            Assert.That(new Vector3d(1, 2, 3) != new Vector3d(1.00000000001, 2, 3), Is.False);
            Assert.That(new Vector3d(1, 2, 3) != new Vector3d(1.0000000001, 2, 3), Is.True);
        }
    }
}
