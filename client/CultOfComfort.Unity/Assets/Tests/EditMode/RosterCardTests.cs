using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class RosterCardTests
    {
        [Test]
        public void Rows_Hearth1_TheCuriousLocalsMostCommonFirst()
        {
            // Act
            var rows = RosterCard.Rows(1, 0, new HashSet<string>());

            // Assert
            CollectionAssert.AreEqual(new[] { EnemyType.Peasant, EnemyType.Goose, EnemyType.Goat, EnemyType.Boar }, rows.Select(r => r.Type).ToArray());
            Assert.IsTrue(rows.All(r => r.IsNew && r.Line.Length > 10));
        }

        [Test]
        public void Rows_Hearth3_TheLordLeadsLast()
        {
            // Act
            var rows = RosterCard.Rows(3, 0, new HashSet<string>());

            // Assert
            Assert.AreEqual(EnemyType.Lord, rows.Last().Type);
            Assert.IsTrue(rows.Last().Leads);
            Assert.AreEqual(1, rows.Count(r => r.Leads));
        }

        [Test]
        public void HasNew_AllSeen_IsFalse()
        {
            // Arrange
            var seen = new HashSet<string> { "Peasant", "Goose", "Goat", "Boar" };

            // Act & Assert
            Assert.IsFalse(RosterCard.HasNew(1, 0, seen));
            Assert.IsTrue(RosterCard.HasNew(2, 0, seen), "the levy is new at Hearth 2");
        }

        [Test]
        public void Rows_NoRoster_IsEmpty()
        {
            // Act & Assert
            Assert.AreEqual(0, RosterCard.Rows(0, 0, new HashSet<string>()).Count);
        }
    }
}
