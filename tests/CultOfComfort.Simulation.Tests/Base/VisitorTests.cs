using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-20 visitors: someone at the Hearth every few hours with something to ask or trade.</summary>
public class VisitorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    private static BaseState Village(int hearth)
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        }

        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        return state;
    }

    /// <summary>The first moment in the day a visitor of <paramref name="kind"/> stands at the Hearth.</summary>
    private static DateTimeOffset When(BaseState state, VisitorKind? kind = null)
    {
        for (var at = T0; at < T0.AddDays(10); at = at.AddMinutes(30))
        {
            if (state.Snapshot(at).Visitor is { } v && (kind == null || v.Offer.Kind == kind))
            {
                return at;
            }
        }

        throw new InvalidOperationException("nobody came");
    }

    [Fact]
    public void Snapshot_HearthTwo_SomeoneCallsMostOfTheDay()
    {
        // Arrange
        var state = Village(2);

        // Act — every half hour over a day.
        var seen = Enumerable.Range(0, 48).Count(k => state.Snapshot(T0.AddMinutes(30 * k)).Visitor != null);

        // Assert — a visitor stays three hours of every four (two for a sheep).
        seen.Should().BeInRange(20, 40);
    }

    [Fact]
    public void Snapshot_HearthOne_Nobody()
    {
        // Arrange
        var state = Village(1);

        // Act & Assert
        Enumerable.Range(0, 48).Should().OnlyContain(k => state.Snapshot(T0.AddMinutes(30 * k)).Visitor == null);
    }

    [Fact]
    public void AnswerVisitor_Accept_PaysWhatTheyAskAndGetsTheGift()
    {
        // Arrange
        var state = Village(3);
        var at = When(state, VisitorKind.Pedlar);
        var visitor = state.Snapshot(at).Visitor!;
        var before = state.Snapshot(at);

        // Act
        var outcome = state.Apply(new AnswerVisitor(Guid.NewGuid(), true), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var after = state.Snapshot(at);
        after.Visitor.Should().BeNull("they've had what they came for");
        (after.Towels - before.Towels).Should().Be(visitor.Offer.Towels);
        (after.Stock.Timber - before.Stock.Timber).Should().BeLessThanOrEqualTo(visitor.Offer.Gives.Timber - visitor.Offer.Asks.Timber);
    }

    [Fact]
    public void AnswerVisitor_SendOn_GoneAndStaysGoneAfterAReload()
    {
        // Arrange
        var state = Village(2);
        var at = When(state);

        // Act
        state.Apply(new AnswerVisitor(Guid.NewGuid(), false), at).Status.Should().Be(CommandStatus.Accepted);
        var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData());

        // Assert
        restored.Snapshot(at).Visitor.Should().BeNull();
        restored.Apply(new AnswerVisitor(Guid.NewGuid(), true), at).Status.Should().Be(CommandStatus.Rejected, "nobody's there");
    }

    [Fact]
    public void Snapshot_TwoDevicesLookingAtDifferentTimes_SeeTheSameVisitor()
    {
        // Arrange — the server looked an hour before the phone did.
        var phone = Village(2);
        var server = Village(2);
        var at = When(phone);
        server.Snapshot(at.AddHours(-1));

        // Act & Assert
        server.Snapshot(at).Visitor.Should().BeEquivalentTo(phone.Snapshot(at).Visitor);
    }
}
