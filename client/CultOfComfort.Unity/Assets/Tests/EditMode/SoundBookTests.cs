using System.Linq;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class SoundBookTests
    {
        [Test]
        public void All_EverySound_HasAClip()
        {
            // Assert
            foreach (var name in SoundBook.All.Concat(new[] { SoundBook.DayMusic, SoundBook.NightMusic }))
            {
                Assert.IsNotNull(Resources.Load<AudioClip>("Audio/" + name), $"Run python3 art/audio/make_sounds.py ({name}).");
            }
        }

        [Test]
        public void For_NightEvents_MakeTheirSounds()
        {
            // Act & Assert
            Assert.AreEqual("sluice", SoundBook.For(new InterventionUsed(1, PlayerActionType.OpenSluiceGate, 3)));
            Assert.AreEqual("laser", SoundBook.For(new RelicUsed(1, PlayerActionType.LaserPointer, default, 4, 1)));
            Assert.AreEqual("defeat", SoundBook.For(new BattleEnded(1, BattleOutcome.Lost, 0)));
            Assert.AreEqual("victory", SoundBook.For(new BattleEnded(1, BattleOutcome.Won, 3)));
        }

        [Test]
        public void For_QuietEvents_Silence()
        {
            // Act & Assert
            Assert.IsNull(SoundBook.For(new LeaderMoveOrdered(1, new GridPosition(2, 2))), "walking is silent");
            Assert.IsNull(SoundBook.For(new TowerFired(1, 1, 1, default, false)), "towers fire too often to clonk every time");
        }

        [Test]
        public void For_EveryMappedSound_IsInTheKit()
        {
            // Arrange
            var events = new BattleEvent[]
            {
                new WaveStarted(1, 1), new HearthDamaged(1, 1, default, 5, 90), new EnemyDefeated(1, 1, default), new LeaderHit(1, 1, default, 3, 100),
                new LeaderSwooned(1), new LeaderAttacked(1, 1, default), new HotStonesThrown(1, default, 1), new InputRejected(1, PlayerActionType.HotStones, "no"),
                new InterventionUsed(1, PlayerActionType.PhantomVoice, 1), new RelicUsed(1, PlayerActionType.DynamoTorch, default, -1, 0),
                new RelicUsed(1, PlayerActionType.SunFire, default, -1, 2),
            };

            // Assert
            foreach (var battleEvent in events)
            {
                CollectionAssert.Contains(SoundBook.All, SoundBook.For(battleEvent), battleEvent.GetType().Name);
            }
        }
    }
}
