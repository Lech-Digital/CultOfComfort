using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Waves;

namespace CultOfComfort.Simulation.Tests.Waves;

public class RaidRosterTests
{
    private static readonly BalanceTable Balance = BalanceTable.Default;

    [Fact]
    public void HearthOne_CuriousLocals_AnimalsAndPeasantsOnly()
    {
        // Act
        var spawns = WaveGenerator.Generate(40, 4, 11, Balance, RaidRoster.ForHearth(1));

        // Assert
        spawns.Select(s => s.Type).Distinct().Should().BeSubsetOf(new[] { EnemyType.Goose, EnemyType.Goat, EnemyType.Boar, EnemyType.Peasant });
        spawns.Select(s => s.Type).Distinct().Count().Should().BeGreaterThan(2, "a mix, not a herd");
    }

    [Fact]
    public void HearthTwo_WulfricsLevy_NoLordsNoPriests()
    {
        // Act
        var types = WaveGenerator.Generate(54, 4, 11, Balance, RaidRoster.ForHearth(2)).Select(s => s.Type).ToHashSet();

        // Assert
        types.Should().Contain(EnemyType.Levy);
        types.Should().NotContain(new[] { EnemyType.Lord, EnemyType.Priest, EnemyType.Peasant });
    }

    [Fact]
    public void HearthThree_TheLordComesOnceInTheLastWave()
    {
        // Act
        var spawns = WaveGenerator.Generate(72, 4, 11, Balance, RaidRoster.ForHearth(3));

        // Assert
        spawns.Where(s => s.Type == EnemyType.Lord).Should().ContainSingle().Which.Wave.Should().Be(4);
        spawns.Should().Contain(s => s.Type == EnemyType.Priest);
    }

    [Fact]
    public void Roster_SameSeed_SameRaid()
    {
        // Act & Assert
        WaveGenerator.Generate(54, 4, 7, Balance, RaidRoster.ForHearth(2))
            .Should().Equal(WaveGenerator.Generate(54, 4, 7, Balance, RaidRoster.ForHearth(2)));
    }

    [Fact]
    public void Priests_Blessing_RaidersNearbyTakeLessDamage()
    {
        // Arrange — a row of priests walking past a line of flingers; once with the blessing, once without.
        var road = Enumerable.Range(0, 14).Select(x => new GridPosition(x, 0)).ToList();
        var layout = new BattleLayout(14, 4, new GridPosition(0, 0), new GridPosition(13, 0),
            new[] { 3, 5, 7, 9 }.Select(x => new PlacedTower(TowerType.SaplingFlinger, new GridPosition(x, 1))).ToList(), Array.Empty<GridPosition>(), new GridPosition(0, 3), null, road);
        var priestsOnly = new RaidRoster(new[] { (EnemyType.Priest, 1) }, null);
        // The raiders' total health at the same moment, with and without the blessing.
        long HealthAfter(CultOfComfort.Simulation.Numerics.Fix reduction)
        {
            var battle = new Battle(new BattleSetup(layout, 3, 20, 1, [], new BalanceTable { HearthMaxHealth = 100_000, BlessingReduction = reduction })
            {
                Roster = priestsOnly,
            });
            while (!battle.IsFinished && battle.Tick < 600)
            {
                battle.Step();
            }

            return battle.Enemies.Sum(e => (long)e.Health.Raw);
        }

        // Act
        var blessed = HealthAfter(CultOfComfort.Simulation.Numerics.Fix.Ratio(1, 4));
        var plain = HealthAfter(CultOfComfort.Simulation.Numerics.Fix.Zero);

        // Assert
        blessed.Should().BeGreaterThan(plain, "priests walking together shrug off a quarter of every blow");
    }

    [Fact]
    public void NightPlan_RosterFollowsTheHearth()
    {
        // Arrange
        var t0 = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(2000, 900, 900, 0) }, VillageMap.Demo, t0);

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), t0).Plan!;

        // Assert
        plan.Roster.Should().Be(1);
        plan.ToBattleSetup(VillageMap.Demo, []).Roster.Should().Be(RaidRoster.ForHearth(1));
    }
}
