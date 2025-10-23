// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Prefs.Tests
{
    using System;
    using Coherence.Tests;
    using NUnit.Framework;

    public class ProcessIdTests : CoherenceTest
    {
        private const string ProjectId = "TestProjectId";

        [Test]
        public void Claiming_Works()
        {
            var prefs = new FakePrefs();
            var claimedId = ProcessId.FirstProcessId;
            var claimForSeconds = 10d;
            var claimUntil = DateTime.UtcNow + TimeSpan.FromSeconds(claimForSeconds);
            var prefsKey = ProcessId.GetPrefsKey(ProjectId, claimedId);

            ProcessId.ClaimUntil(prefs, prefsKey, claimUntil);
            Assert.That(ProcessId.GetClaimedUntilTime(prefs, ProjectId, claimedId), Is.EqualTo(claimUntil));
            Assert.That(ProcessId.IsClaimed(prefs, ProjectId, claimedId), Is.True);

            var unclaimedId = ProcessId.GetFirstUnclaimedId(prefs, ProjectId);
            Assert.That(unclaimedId, Is.EqualTo(claimedId + 1));
            Assert.That(ProcessId.IsClaimed(prefs, ProjectId, unclaimedId), Is.False);

            ProcessId.Release(prefs, prefsKey);
            Assert.That(ProcessId.IsClaimed(prefs, ProjectId, claimedId), Is.False);
        }
    }
}
