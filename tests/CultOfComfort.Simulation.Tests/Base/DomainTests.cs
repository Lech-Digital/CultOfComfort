using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

public class DomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DomainNode Muddlecombe = DomainMap.Marchlands.Node(1)!; // hamlet, 1 hex
    private static readonly DomainNode Keep = DomainMap.Marchlands.Node(16)!;       // 5 hexes

    private static BuildingTable Rich() => new()
    {
        StartingStock = new ResourceBag(8000, 2000, 2000, 1000),
        HearthStorageCaps = new[] { 9000, 9000, 9000 },
        HearthBeds = 12,
    };

    private static (BaseState State, DateTimeOffset Now) Village(int hearth)
    {
        var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
        var now = T0;
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new UpgradeHearth(Guid.NewGuid()), now).Status.Should().Be(CommandStatus.Accepted);
            now = now.AddHours(2);
        }

        state.Apply(new DebugFillStores(Guid.NewGuid()), now);
        return (state, now);
    }

    private static CommandOutcome Send(BaseState state, DomainNode node, int hours, DateTimeOffset at) =>
        state.Apply(new SendMissionary(Guid.NewGuid(), node.Id, hours), at);

    private static DomainNodeView View(BaseState state, DomainNode node, DateTimeOffset at) =>
        state.Snapshot(at).Domain!.Nodes.Single(n => n.Node.Id == node.Id);

    [Fact]
    public void Marchlands_TheMap_HasTheSpecsMix()
    {
        // Act
        var nodes = DomainMap.Marchlands.Nodes;

        // Assert
        nodes.Should().HaveCount(16);
        nodes.Count(n => n.Type == NodeType.Hamlet).Should().Be(6);
        nodes.Count(n => n.Type == NodeType.Village).Should().Be(5);
        nodes.Count(n => n.Type == NodeType.MarketTown).Should().Be(2);
        nodes.Count(n => n.Type == NodeType.Abbey).Should().Be(2);
        nodes.Should().ContainSingle(n => n.Type == NodeType.Keep).Which.Distance.Should().Be(5);
        nodes.Select(n => (n.Q, n.R)).Should().OnlyHaveUniqueItems().And.NotContain((0, 0), "the village is at the centre");
    }

    [Fact]
    public void Send_HearthOne_TheMarchlandsAreClosed()
    {
        // Arrange
        var (state, now) = Village(1);

        // Act
        var outcome = Send(state, Muddlecombe, 1, now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        state.Snapshot(now).Domain!.Unlocked.Should().BeFalse();
    }

    [Fact]
    public void Send_HearthTwo_ChargesProvisionsAndTakesAVillagerAway()
    {
        // Arrange
        var (state, now) = Village(2);
        var before = state.Snapshot(now).Stock;

        // Act
        var outcome = Send(state, Muddlecombe, 4, now);
        var snapshot = state.Snapshot(now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        (before - snapshot.Stock).Should().Be(new ResourceBag(100, 0, 0, 40), "25 Timber + 10 Devotion per preaching hour");
        snapshot.Domain!.Missionaries.Should().ContainSingle().Which.Phase.Should().Be(MissionaryPhase.Travelling);
    }

    [Fact]
    public void Send_SecondMissionaryAtHearthTwo_OnlyOneSlot()
    {
        // Arrange
        var (state, now) = Village(2);
        Send(state, Muddlecombe, 1, now);

        // Act
        var outcome = Send(state, DomainMap.Marchlands.Node(2)!, 1, now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Send_BeyondReach_Rejected()
    {
        // Arrange — the Keep is 5 hexes out; Hearth 2 reaches 3.
        var (state, now) = Village(2);

        // Act
        var outcome = Send(state, Keep, 1, now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Preach_SessionEnds_InfluenceLandsAndTheMissionaryWalksHome()
    {
        // Arrange — 1 hex: 20 min out, 8 h preaching, 20 min back.
        var (state, now) = Village(2);
        Send(state, Muddlecombe, 8, now);
        var expected = (int)(DomainTable.Default.GainMilli(Muddlecombe, 8) / 1000);

        // Act
        var during = View(state, Muddlecombe, now.AddHours(4));
        var after = state.Snapshot(now.AddHours(8).AddMinutes(25));
        var home = state.Snapshot(now.AddHours(8).AddMinutes(41));

        // Assert
        during.InfluencePercent.Should().Be(0, "influence lands when the sermon ends");
        after.Domain!.Nodes.Single(n => n.Node.Id == Muddlecombe.Id).InfluencePercent.Should().Be(expected);
        after.Domain.Missionaries.Single().Phase.Should().Be(MissionaryPhase.Returning);
        home.Domain!.Missionaries.Should().BeEmpty();
        expected.Should().BeInRange(40, 55, "one long sermon at a muddy hamlet makes you Patron");
    }

    [Fact]
    public void Preach_BecomingPatron_TensionSpikes()
    {
        // Arrange
        var (quiet, now) = Village(2);
        var (preached, _) = Village(2);
        Send(preached, Muddlecombe, 8, now);
        var after = now.AddHours(8).AddMinutes(25);

        // Act
        var difference = preached.Snapshot(after).Tension - quiet.Snapshot(after).Tension;

        // Assert
        difference.Should().BeGreaterThanOrEqualTo(DomainTable.Default.PatronTensionSpike);
        View(preached, Muddlecombe, after).Patron.Should().BeTrue();
    }

    [Fact]
    public void Offerings_AfterPreaching_FillThePurseUpToEightHours()
    {
        // Arrange
        var (state, now) = Village(2);
        Send(state, Muddlecombe, 8, now);
        var landed = now.AddHours(8).AddMinutes(20);

        // Act
        var perHour = state.Snapshot(landed).Domain!.OfferingsPerHour;
        var dayLater = state.Snapshot(landed.AddDays(1)).Domain!;

        // Assert
        perHour.Timber.Should().BeGreaterThan(0, "a hamlet sends Timber");
        dayLater.Offerings.Timber.Should().BeLessThanOrEqualTo(perHour.Timber * 8 + 1, "the purse holds eight hours (of the rate it filled at)");
        dayLater.Offerings.Timber.Should().BeGreaterThan(perHour.Timber * 7);
    }

    [Fact]
    public void CollectOfferings_PurseFull_MovesItIntoTheStores()
    {
        // Arrange — spend some Timber first so it fits.
        var (state, now) = Village(2);
        Send(state, Muddlecombe, 8, now);
        var later = now.AddHours(20);
        state.Apply(new PayTithe(Guid.NewGuid(), new ResourceBag(3000, 0, 0, 0)), later);
        var before = state.Snapshot(later);

        // Act
        var outcome = state.Apply(new CollectOfferings(Guid.NewGuid()), later);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        outcome.Collected.Timber.Should().Be(before.Domain!.Offerings.Timber);
        state.Snapshot(later).Domain!.Offerings.Timber.Should().Be(0);
    }

    [Fact]
    public void Decay_ADayLater_InfluenceFadesTwoPercent()
    {
        // Arrange
        var (state, now) = Village(2);
        Send(state, Muddlecombe, 8, now);
        var landed = now.AddHours(9);
        var start = View(state, Muddlecombe, landed).InfluencePercent;

        // Act
        var weekLater = View(state, Muddlecombe, landed.AddDays(7)).InfluencePercent;

        // Assert
        weekLater.Should().BeLessThan(start);
        weekLater.Should().BeGreaterThanOrEqualTo((int)(start * 0.85), "2% a day, not a cliff");
    }

    [Fact]
    public void Tension_WithInfluence_RisesFasterThanWithout()
    {
        // Arrange
        var (state, now) = Village(2);
        var passive = state.Snapshot(now).Domain!.TensionPerHourMilli;
        Send(state, Muddlecombe, 8, now);

        // Act
        var withInfluence = state.Snapshot(now.AddHours(9)).Domain!.TensionPerHourMilli;

        // Assert
        passive.Should().Be(6000, "Hearth 2's passive rise");
        withInfluence.Should().BeGreaterThan(passive);
    }

    [Fact]
    public void Tithe_TensionDropsAndASecondWithinADayHalves()
    {
        // Arrange
        var (state, now) = Village(2);
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 30), now);
        var start = state.Snapshot(now).Tension;

        // Act — 400 Timber = 20 Tension; the second one only 10.
        state.Apply(new PayTithe(Guid.NewGuid(), new ResourceBag(400, 0, 0, 0)), now);
        var first = state.Snapshot(now).Tension;
        state.Apply(new PayTithe(Guid.NewGuid(), new ResourceBag(400, 0, 0, 0)), now);
        var second = state.Snapshot(now).Tension;

        // Assert
        first.Should().Be(start - 20);
        second.Should().Be(start - 30);
        state.Snapshot(now).Domain!.TitheHalfUntilUtc.Should().Be(now.AddHours(24));
    }

    [Fact]
    public void Tithe_BigEnoughDuringAWarning_CallsTheRaidOff()
    {
        // Arrange — a raid is announced.
        var (state, now) = Village(2);
        state.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), now);
        state.Snapshot(now).RaidWarningEndsAtUtc.Should().NotBeNull();

        // Act — 80 Tension's worth (400 Devotion) brings it under Uneasy.
        state.Apply(new PayTithe(Guid.NewGuid(), new ResourceBag(0, 0, 0, 400)), now);

        // Assert
        state.Snapshot(now).RaidWarningEndsAtUtc.Should().BeNull("Wulfric takes the money");
    }

    [Fact]
    public void Tithe_TooSmall_Rejected()
    {
        // Arrange
        var (state, now) = Village(2);

        // Act
        var outcome = state.Apply(new PayTithe(Guid.NewGuid(), new ResourceBag(5, 0, 0, 0)), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void ToData_MissionaryOnTheRoad_RestoresAndFinishesTheSameWay()
    {
        // Arrange
        var (state, now) = Village(2);
        Send(state, Muddlecombe, 4, now);
        var mid = now.AddHours(2);
        state.Snapshot(mid);

        // Act
        var restored = BaseState.Restore(Rich(), VillageMap.Demo, state.ToData());
        var later = now.AddHours(30);

        // Assert
        var a = state.Snapshot(later);
        var b = restored.Snapshot(later);
        b.Domain!.Nodes.Select(n => n.InfluencePercent).Should().Equal(a.Domain!.Nodes.Select(n => n.InfluencePercent));
        b.Domain.Offerings.Should().Be(a.Domain.Offerings);
        b.Tension.Should().Be(a.Tension);
    }

    [Fact]
    public void Snapshot_LookingOftenOrRarely_SameDomainLater()
    {
        // Arrange — lazy evaluation must not depend on how often anyone looks.
        var (often, now) = Village(3);
        var (rarely, _) = Village(3);
        foreach (var s in new[] { often, rarely })
        {
            Send(s, Muddlecombe, 8, now);
            Send(s, DomainMap.Marchlands.Node(7)!, 2, now);
        }

        // Act
        for (var h = 1; h < 72; h += 3)
        {
            often.Snapshot(now.AddHours(h));
        }

        var end = now.AddHours(72);

        // Assert — Tension is left out: missed demands bring a raid in this time, and a raid while away restarts Tension
        // from when you next look, by design (DemandTests covers the demand clock on its own).
        often.Snapshot(end).Domain!.Offerings.Should().Be(rarely.Snapshot(end).Domain!.Offerings);
        often.Snapshot(end).Domain!.Nodes.Select(n => n.InfluencePercent).Should().Equal(rarely.Snapshot(end).Domain!.Nodes.Select(n => n.InfluencePercent));
    }
}

public class MarchlandsErrandTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Errands_MissionaryStudyAndPatron_ReadTheVillage()
    {
        // Arrange — a village at the missionary errand (the earlier ones claimed by setting the index through the save).
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        var now = T0.AddHours(1);
        state.Apply(new DebugFillStores(Guid.NewGuid()), now);
        var data = state.ToData() with { ErrandId = "missionary" };
        state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data);

        // Act & Assert — sending one finishes the missionary errand.
        state.Snapshot(now).Errand!.Done.Should().BeFalse();
        state.Apply(new SendMissionary(Guid.NewGuid(), 1, 8), now);
        state.Snapshot(now).Errand!.Done.Should().BeTrue();
        state.Apply(new ClaimErrand(Guid.NewGuid()), now).Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(now).Errand!.Id.Should().Be("midden-sorter", "Scrap comes next (spec 2026-10-16 first day)");

        // The study errand: a finished study.
        state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData() with { ErrandId = "study" });
        state.Snapshot(now).Errand!.Goal.Should().Be(ErrandGoal.Study);
        state.Apply(new StartResearch(Guid.NewGuid(), "sermon-notes"), now);
        state.Snapshot(now.AddHours(2)).Errand!.Done.Should().BeTrue();
    }
}
