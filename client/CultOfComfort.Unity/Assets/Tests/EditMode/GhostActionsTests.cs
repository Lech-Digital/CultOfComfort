using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class GhostActionsTests
    {
        private static readonly Vector2 Size = new Vector2(180f, 60f);

        [Test]
        public void GhostActionsAt_NothingInTheWay_UnderTheGhost()
        {
            // Act
            var at = HudView.GhostActionsAt(new Rect(150f, 200f, 80f, 60f), Size, 480f, 700f, null);

            // Assert
            Assert.AreEqual(270f, at.y, 1e-3f, "10 under the ghost");
            Assert.AreEqual(100f, at.x, 1e-3f, "centred on it");
        }

        [Test]
        public void GhostActionsAt_HearthUnder_GoesOverTheGhost()
        {
            // Arrange — the Hearth just under the ghost (the Peat Cut, the Cultist Hut in the playtests).
            var ghost = new Rect(150f, 200f, 80f, 60f);
            var hearth = new Rect(120f, 270f, 140f, 120f);

            // Act
            var at = HudView.GhostActionsAt(ghost, Size, 480f, 700f, hearth);

            // Assert
            Assert.IsFalse(new Rect(at, Size).Overlaps(hearth), "the Hearth stays visible");
            Assert.IsFalse(new Rect(at, Size).Overlaps(ghost), "and the ghost too");
            Assert.AreEqual(130f, at.y, 1e-3f, "over the ghost");
        }
    }
}
