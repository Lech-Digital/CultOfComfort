using System;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>The Comfort Festival's points and claims (spec 2026-10-20 the festival): see <see cref="FestivalBook"/>.</summary>
public sealed partial class BaseState
{
    private long _festivalDay = -1;
    private int _festivalPoints;
    private readonly bool[] _festivalClaimed = new bool[3];

    /// <summary>A new day: the points start again (whatever wasn't claimed is gone).</summary>
    private void EnsureFestivalDay(DateTimeOffset at)
    {
        var day = FestivalBook.DayOf(at);
        if (day == _festivalDay)
        {
            return;
        }

        _festivalDay = day;
        _festivalPoints = 0;
        Array.Clear(_festivalClaimed, 0, _festivalClaimed.Length);
    }

    /// <summary>Something done that today's theme may count.</summary>
    private void Festival(FestivalDeed deed, int amount, DateTimeOffset at)
    {
        if (amount <= 0)
        {
            return;
        }

        EnsureFestivalDay(at);
        foreach (var (counted, points) in FestivalBook.On(_festivalDay).Counts)
        {
            if (counted == deed)
            {
                _festivalPoints += amount * points;
            }
        }
    }

    private FestivalView FestivalViewAt(DateTimeOffset now)
    {
        var day = FestivalBook.DayOf(now);
        var points = day == _festivalDay ? _festivalPoints : 0;
        var claimed = day == _festivalDay ? _festivalClaimed.ToList() : new[] { false, false, false }.ToList();
        return new FestivalView(FestivalBook.DayOfWeek(day), FestivalBook.On(day), points, claimed, FestivalBook.EndOf(day));
    }

    private CommandOutcome ClaimFestival(ClaimFestival claim, DateTimeOffset now)
    {
        EnsureFestivalDay(now);
        var tiers = FestivalBook.On(_festivalDay).Tiers;
        if (claim.Tier < 0 || claim.Tier >= tiers.Count)
        {
            return CommandOutcome.Reject("No such reward.");
        }

        var (points, bag, towels) = tiers[claim.Tier];
        if (_festivalClaimed[claim.Tier])
        {
            return CommandOutcome.Reject("Claimed already. Greedy.");
        }

        if (_festivalPoints < points)
        {
            return CommandOutcome.Reject($"{points - _festivalPoints} more points first.");
        }

        MaterializeEconomy(now);
        _festivalClaimed[claim.Tier] = true;
        _towels += towels;
        return CommandOutcome.Accept(Refund(bag));
    }

    private void RestoreFestival(BaseData data)
    {
        _festivalDay = data.FestivalDay;
        _festivalPoints = Math.Max(0, data.FestivalPoints);
        for (var i = 0; i < _festivalClaimed.Length && data.FestivalClaimed != null && i < data.FestivalClaimed.Count; i++)
        {
            _festivalClaimed[i] = data.FestivalClaimed[i];
        }
    }
}
