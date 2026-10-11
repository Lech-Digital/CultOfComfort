using System.Collections.Generic;
using CultOfComfort.Simulation.Combat;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class TipBookTests
    {
        private sealed class MemoryStore : ITipStore
        {
            private readonly HashSet<string> _seen = new HashSet<string>();
            public bool HasSeen(string id) => _seen.Contains(id);
            public void MarkSeen(string id) => _seen.Add(id);
            public void Reset(IEnumerable<string> ids) => _seen.Clear();
        }

        [Test]
        public void Take_FirstSluice_ReturnsTheSluiceTip()
        {
            // Arrange
            var tips = new TipBook(new MemoryStore());

            // Act
            var tip = tips.Take(PlayerActionType.OpenSluiceGate);

            // Assert
            StringAssert.Contains("washed three tiles back", tip);
        }

        [Test]
        public void Take_SecondSluice_ReturnsNull()
        {
            // Arrange
            var tips = new TipBook(new MemoryStore());
            tips.Take(PlayerActionType.OpenSluiceGate);

            // Act
            var tip = tips.Take(PlayerActionType.OpenSluiceGate);

            // Assert
            Assert.IsNull(tip);
        }

        [Test]
        public void Take_MoveLeader_HasNoTip()
        {
            Assert.IsNull(new TipBook(new MemoryStore()).Take(PlayerActionType.MoveLeader));
        }

        [Test]
        public void Reset_AfterAllSeen_ShowsTipsAgain()
        {
            // Arrange
            var tips = new TipBook(new MemoryStore());
            tips.Take(PlayerActionType.PhantomVoice);

            // Act
            tips.Reset();

            // Assert
            StringAssert.Contains("megaphone", tips.Take(PlayerActionType.PhantomVoice));
        }
    }
}
