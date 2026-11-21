using System;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    /// <summary>When the game updates (2026-11-21, ported from Coo's policy and its tests).</summary>
    public class UpdatePolicyTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

        private static AppVersionPolicy Server(int latest, int minimum) => new AppVersionPolicy { Latest = latest, Minimum = minimum };

        [Test]
        public void Decide_NormalRelease_DownloadsInTheBackgroundOncePerSession()
        {
            // Act
            var first = UpdatePolicy.Decide(3, Server(4, 2), true, 0, true, true, flexibleOfferedThisSession: false);
            var again = UpdatePolicy.Decide(3, Server(4, 2), true, 0, true, true, flexibleOfferedThisSession: true);

            // Assert
            Assert.AreEqual(UpdateAction.Flexible, first);
            Assert.AreEqual(UpdateAction.None, again);
        }

        [TestCase(2, 3, 0)]
        [TestCase(3, 2, 4)]
        [TestCase(3, 2, 5)]
        public void Decide_BelowTheServersMinimumOrHighPriority_IsImmediate(int versionCode, int minimum, int priority)
        {
            // Act & Assert — client and server ship together: an old client shouldn't play on.
            Assert.AreEqual(UpdateAction.Immediate, UpdatePolicy.Decide(versionCode, Server(5, minimum), true, priority, true, true, false));
        }

        [Test]
        public void Decide_RequiredButPlayHasNothingToOffer_SendsToTheStore()
        {
            // Act & Assert — a copy not from Play, or a release Play hasn't rolled out to it yet.
            Assert.AreEqual(UpdateAction.OpenStore, UpdatePolicy.Decide(2, Server(4, 3), false, 0, false, false, false));
        }

        [Test]
        public void Decide_ServerUnreachableAndNoUpdate_DoesNothing()
        {
            // Act & Assert
            Assert.AreEqual(UpdateAction.None, UpdatePolicy.Decide(3, null, false, 0, false, false, false));
        }

        [Test]
        public void ShouldSuggest_ServerKnowsANewerVersion_OnceADay()
        {
            // Act & Assert — Play's own update can lag a release by hours (Coo, 2026-10-27).
            Assert.IsTrue(UpdatePolicy.ShouldSuggest(3, Server(4, 2), null, Now));
            Assert.IsTrue(UpdatePolicy.ShouldSuggest(3, Server(4, 2), Now.AddDays(-1), Now));
            Assert.IsFalse(UpdatePolicy.ShouldSuggest(3, Server(4, 2), Now.AddHours(-3), Now), "already today");
            Assert.IsFalse(UpdatePolicy.ShouldSuggest(4, Server(4, 2), null, Now), "on the latest");
            Assert.IsFalse(UpdatePolicy.ShouldSuggest(3, null, null, Now), "the server can't be asked");
        }
    }
}
