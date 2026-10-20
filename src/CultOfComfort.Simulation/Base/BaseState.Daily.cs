using System;
using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>
/// The Prophet's daily list (spec 2026-10-15 daily errands; owner: "after we finish the onboarding initial tasks, we
/// should use those for daily missions"): from the end of the starter errands, three errands a day, a Thermos for
/// all three, a streak for a week of Thermoses, and Towels to spend. Days start at 04:00 on the player's clock.
/// </summary>
public sealed partial class BaseState
{
    private int _dailyDay = int.MinValue;
    private int _dayOffset;
    private readonly List<string> _dailyIds = new();
    private readonly int[] _dailyProgress = new int[DailyBook.Slots];
    private readonly bool[] _dailyClaimed = new bool[DailyBook.Slots];
    private bool _thermos;
    private int _streak;
    private int _lastThermosDay = int.MinValue;
    private int _towels;
    private readonly List<string> _cosmetics = new();

    /// <summary>The starter errands are done: the daily list takes over the errand note.</summary>
    private bool DailyOpen => _errandIndex >= _table.Errands.Errands.Count;

    /// <summary>A new day (on the player's clock) brings a new list; a day without a Thermos breaks the streak.</summary>
    private void EnsureDay(DateTimeOffset at)
    {
        if (!DailyOpen)
        {
            return;
        }

        var day = DailyBook.DayOf(at, _dayOffset);
        if (day <= _dailyDay)
        {
            return;
        }

        if (_lastThermosDay < day - 1)
        {
            _streak = 0;
        }

        _dailyDay = day;
        _dailyIds.Clear();
        _dailyIds.AddRange(DailyBook.Pick(day, PossibleDaily));
        Array.Clear(_dailyProgress, 0, _dailyProgress.Length);
        Array.Clear(_dailyClaimed, 0, _dailyClaimed.Length);
        _thermos = false;
    }

    private bool PossibleDaily(DailyErrand errand) =>
        errand.Goal != DailyGoal.Study || Research.Techs.Any(t => !_researched.Contains(t.Id));

    /// <summary>Something today's list may count happened at <paramref name="at"/>.</summary>
    private void NoteDaily(DailyGoal goal, int amount, DateTimeOffset at)
    {
        if (!DailyOpen || amount <= 0)
        {
            return;
        }

        EnsureDay(at);
        for (var slot = 0; slot < _dailyIds.Count; slot++)
        {
            var errand = DailyBook.Get(_dailyIds[slot]);
            if (errand != null && errand.Goal == goal && !_dailyClaimed[slot])
            {
                _dailyProgress[slot] = Math.Min(DailyBook.Target(errand, HearthLevel), _dailyProgress[slot] + amount);
            }
        }
    }

    /// <summary>What an accepted command counts towards today's list.</summary>
    private void NoteDailyCommand(BaseCommand command, CommandOutcome outcome, DateTimeOffset now, int roadsBefore)
    {
        switch (command)
        {
            case SendMissionary send:
                NoteDaily(DailyGoal.PreachHours, send.Hours, now);
                break;
            case SendWorldMissionary send:
                NoteDaily(DailyGoal.PreachHours, send.Hours, now);
                break;
            case MeetDemand or PayTithe:
                NoteDaily(DailyGoal.Calm, 1, now);
                break;
            case UpgradeBuilding or UpgradeHearth:
                NoteDaily(DailyGoal.Upgrade, 1, now);
                break;
            case RepairBuilding or RepairAll:
                NoteDaily(DailyGoal.Repair, 1, now);
                break;
            case PaintTiles:
                NoteDaily(DailyGoal.PaintRoads, _buildings.Count(b => BuildingTable.IsRoad(b.Type)) - roadsBefore, now);
                break;
            case PlaceBuilding place when !BuildingTable.IsRoad(place.Type):
                NoteDaily(DailyGoal.Build, 1, now);
                break;
            case Trade:
                NoteDaily(DailyGoal.Trade, 1, now);
                break;
            case Base.CollectOfferings or ReceiveOfferings:
                NoteDaily(DailyGoal.Offerings, DomainTable.TitheValue(outcome.Collected), now);
                break;
        }
    }

    // ---------------- commands ----------------

    private CommandOutcome ClaimDailyErrand(ClaimDaily claim, DateTimeOffset now)
    {
        EnsureDay(now);
        if (!DailyOpen || claim.Slot < 0 || claim.Slot >= _dailyIds.Count)
        {
            return CommandOutcome.Reject("No such errand today.");
        }

        var errand = DailyBook.Get(_dailyIds[claim.Slot])!;
        if (_dailyClaimed[claim.Slot])
        {
            return CommandOutcome.Reject("Already paid. The Prophet keeps a ledger.");
        }

        if (_dailyProgress[claim.Slot] < DailyBook.Target(errand, HearthLevel))
        {
            return CommandOutcome.Reject("Not done yet.");
        }

        _dailyClaimed[claim.Slot] = true;
        return CommandOutcome.Accept(Pay(DailyBook.Reward(errand, HearthLevel)));
    }

    private CommandOutcome OpenThermos(DateTimeOffset now)
    {
        EnsureDay(now);
        if (!DailyOpen || _dailyIds.Count == 0 || _dailyClaimed.Take(_dailyIds.Count).Any(c => !c))
        {
            return CommandOutcome.Reject("The Thermos is for a whole day's list.");
        }

        if (_thermos)
        {
            return CommandOutcome.Reject("Already drunk. There's a ring in the bottom.");
        }

        _thermos = true;
        _streak = _lastThermosDay == _dailyDay - 1 ? _streak + 1 : 1;
        _lastThermosDay = _dailyDay;
        _towels += DailyBook.ThermosTowels;
        if (_streak % DailyBook.StreakLength == 0)
        {
            _towels += DailyBook.StreakTowels; // a full week: a pile of towels and something to show for it
            var next = DailyBook.Cosmetics.FirstOrDefault(c => !_cosmetics.Contains(c));
            if (next != null)
            {
                _cosmetics.Add(next);
            }
        }

        return CommandOutcome.Accept(Pay(DailyBook.ThermosReward));
    }

    private CommandOutcome Spend(SpendTowels spend, DateTimeOffset now)
    {
        EnsureDay(now);
        switch (spend.Use)
        {
            case TowelUse.Hurry:
            {
                // Slot names the building as its id + 1 (its card's Hurry; the Hearth is id 0); 0: whatever finishes first.
                var building = spend.Slot > 0 ? _buildings.FirstOrDefault(b => b.Id == spend.Slot - 1 && b.IsUpgrading)
                    : _buildings.Where(b => b.IsUpgrading).OrderBy(b => b.UpgradeEndsAtUtc).FirstOrDefault();
                if (building == null)
                {
                    return CommandOutcome.Reject("Nothing is being built.");
                }

                if (_towels < DailyBook.HurryCost)
                {
                    return CommandOutcome.Reject("Not enough Towels.");
                }

                _towels -= DailyBook.HurryCost;
                var hurried = building.UpgradeEndsAtUtc!.Value.AddMinutes(-DailyBook.HurryMinutes);
                building.UpgradeEndsAtUtc = hurried < now ? now : hurried;
                Advance(now); // done already? it finishes now
                return CommandOutcome.Accept();
            }

            case TowelUse.Reroll:
            {
                if (!DailyOpen || spend.Slot < 0 || spend.Slot >= _dailyIds.Count || _dailyClaimed[spend.Slot])
                {
                    return CommandOutcome.Reject("That errand can't be swapped.");
                }

                if (_towels < DailyBook.RerollCost)
                {
                    return CommandOutcome.Reject("Not enough Towels.");
                }

                var other = DailyBook.Pick(_dailyDay + 7919 * (spend.Slot + 1) + _towels, PossibleDaily, _dailyIds).FirstOrDefault();
                if (other == null)
                {
                    return CommandOutcome.Reject("There's nothing else to ask for today.");
                }

                _towels -= DailyBook.RerollCost;
                _dailyIds[spend.Slot] = other;
                _dailyProgress[spend.Slot] = 0;
                return CommandOutcome.Accept();
            }

            default:
                return CommandOutcome.Reject("Towels don't do that.");
        }
    }

    private CommandOutcome SetClock(SetDayClock clock)
    {
        if (Math.Abs(clock.OffsetMinutes) > 14 * 60)
        {
            return CommandOutcome.Reject("No clock on Earth says that.");
        }

        if (clock.OffsetMinutes == _dayOffset)
        {
            return CommandOutcome.Reject("The clock already says so.");
        }

        _dayOffset = clock.OffsetMinutes; // the day itself never goes back: EnsureDay only moves forward
        return CommandOutcome.Accept();
    }

    /// <summary>Pays a reward into the stores, never past what they hold (like salvage and the errands).</summary>
    private ResourceBag Pay(ResourceBag reward)
    {
        var caps = Caps;
        var paid = new ResourceBag(
            Math.Min(reward.Timber, Math.Max(0, caps.Timber - _stock.Timber)), Math.Min(reward.Peat, Math.Max(0, caps.Peat - _stock.Peat)),
            Math.Min(reward.Scrap, Math.Max(0, caps.Scrap - _stock.Scrap)), Math.Min(reward.Devotion, Math.Max(0, caps.Devotion - _stock.Devotion)));
        _stock += paid;
        return paid;
    }

    // ---------------- view and storage ----------------

    private DailyView? DailyViewAt(DateTimeOffset now)
    {
        if (!DailyOpen || _dailyDay == int.MinValue)
        {
            return null;
        }

        var errands = _dailyIds.Select((id, slot) =>
        {
            var errand = DailyBook.Get(id)!;
            return new DailyErrandView(slot, id, errand.Goal, DailyBook.Text(errand, HearthLevel), _dailyProgress[slot], DailyBook.Target(errand, HearthLevel),
                _dailyClaimed[slot], DailyBook.Reward(errand, HearthLevel));
        }).ToList();
        return new DailyView(_dailyDay, errands, errands.Count > 0 && errands.All(e => e.Claimed) && !_thermos, _thermos, _streak, _towels, _cosmetics.ToList(),
            DailyBook.DayStart(_dailyDay + 1, _dayOffset));
    }

    private DailyData DailyToData() => new(_dailyDay, _dayOffset, _dailyIds.ToList(), _dailyProgress.Take(_dailyIds.Count).ToList(), _dailyClaimed.Take(_dailyIds.Count).ToList(), _thermos, _streak,
        _lastThermosDay, _towels, _cosmetics.ToList());

    private void RestoreDaily(DailyData? data)
    {
        if (data == null)
        {
            return;
        }

        if (data.Towels < 0 || data.Streak < 0 || data.Ids.Count > DailyBook.Slots || data.Progress.Count != data.Ids.Count || data.Claimed.Count != data.Ids.Count
            || data.Ids.Any(id => DailyBook.Get(id) == null) || Math.Abs(data.OffsetMinutes) > 14 * 60)
        {
            throw new InvalidBaseDataException("impossible daily list");
        }

        _dailyDay = data.Day;
        _dayOffset = data.OffsetMinutes;
        _dailyIds.AddRange(data.Ids);
        for (var i = 0; i < data.Ids.Count; i++)
        {
            _dailyProgress[i] = data.Progress[i];
            _dailyClaimed[i] = data.Claimed[i];
        }

        _thermos = data.Thermos;
        _streak = data.Streak;
        _lastThermosDay = data.LastThermosDay;
        _towels = data.Towels;
        _cosmetics.AddRange(data.Cosmetics.Where(c => DailyBook.Cosmetics.Contains(c)));
    }
}
