using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Combat;

namespace CultOfComfort.Simulation.Base;

/// <summary>The Pilgrims' Way as shown: the next stage, whether it's a boss, the highest cleared, and the alms box.</summary>
public sealed record TrialView(int NextStage, bool Boss, int Cleared, ResourceBag AlmsWaiting, ResourceBag AlmsPerHour, bool Walking);

/// <summary>
/// The Pilgrims' Way (spec 2026-10-20 the Pilgrims' Way; from Kingshot's Conquest): an endless ladder of challenge
/// Nights, fought any time against the village as it stands (shield or no shield), wrecking nothing. Each stage cleared
/// pays a reward; every fifth is led by a boss. The alms box fills while away, by the highest stage cleared.
/// </summary>
public static class TrialBook
{
    public const int AlmsCapHours = 12;

    public static bool IsBoss(int stage) => stage % 5 == 0;

    public static int Budget(int stage) => (14 + 5 * stage) * (IsBoss(stage) ? 3 : 2) / 2;

    public static int Waves(int stage) => 3 + Math.Min(stage / 4, 3);

    public static ulong Seed(int stage) => 7_000UL + (ulong)stage;

    /// <summary>What a cleared stage pays (and a Towel every fifth).</summary>
    public static (ResourceBag Bag, int Towels) Reward(int stage) =>
        (new ResourceBag(100 + 25 * stage, 0, 40 + 12 * stage, IsBoss(stage) ? 20 * stage : 0), IsBoss(stage) ? 2 : 0);

    public static ResourceBag AlmsPerHour(int cleared) => new(15 * cleared, 0, 5 * cleared, 2 * cleared);
}

public sealed partial class BaseState
{
    private int _trialsCleared;
    private NightPlan? _trial;
    private DateTimeOffset? _almsAsOf;

    private TrialView TrialViewAt(DateTimeOffset now)
    {
        var next = _trialsCleared + 1;
        return new TrialView(next, TrialBook.IsBoss(next), _trialsCleared, AlmsAt(now), TrialBook.AlmsPerHour(_trialsCleared), _trial != null);
    }

    private ResourceBag AlmsAt(DateTimeOffset now)
    {
        if (_almsAsOf == null || _trialsCleared == 0)
        {
            return default;
        }

        var hours = Math.Min(TrialBook.AlmsCapHours, Math.Max(0, (now - _almsAsOf.Value).TotalHours));
        var rate = TrialBook.AlmsPerHour(_trialsCleared);
        return new ResourceBag((int)(rate.Timber * hours), (int)(rate.Peat * hours), (int)(rate.Scrap * hours), (int)(rate.Devotion * hours));
    }

    private CommandOutcome BeginTrial(DateTimeOffset now)
    {
        if (_night != null)
        {
            return CommandOutcome.Reject("A Night is on. The pilgrims can wait.");
        }

        var stage = _trialsCleared + 1;
        var open = _map.OpenRoads(HearthLevel);
        var road = open[stage % open.Count];
        var layout = ToBattleLayout(road);
        var (towers, wardens) = Garrison(layout);
        _trial = new NightPlan(stage, TrialBook.Seed(stage), TrialBook.Budget(stage), TrialBook.Waves(stage), SluiceBonus, false, towers, layout.Obstacles,
            layout.Structures.Select(s => s with { Health = s.MaxHealth }).ToList(), // a trial is fought fresh: nothing stays wrecked
            HearthLevel, AutoRelics: true, Defenders: DefenderPosts(layout).Concat(wardens).ToList(),
            TowerDamagePercent: Bonus(TechEffect.TowerDamagePercent), ProphetHealthPercent: Bonus(TechEffect.ProphetHealthPercent) + ProphetHealthBonus,
            Road: road, ProphetDamagePercent: ProphetDamageBonus, ProphetSpeedPercent: TakenPerks.Sum(p => p.SpeedPercent),
            VoicePercent: TakenPerks.Sum(p => p.VoicePercent), StonesPercent: TakenPerks.Sum(p => p.StonesPercent));
        return CommandOutcome.Accept() with { Plan = _trial };
    }

    private CommandOutcome ResolveTrial(ResolveTrial command, DateTimeOffset now)
    {
        if (_trial == null || command.Stage != _trial.Night)
        {
            return CommandOutcome.Reject("That isn't the stage being walked.");
        }

        var plan = _trial;
        _trial = null;
        var result = new Battle(plan.ToBattleSetup(_map, command.Inputs)).RunToCompletion(); // replayed, never trusted
        ResourceBag paid = default;
        if (result.Stars >= 1)
        {
            MaterializeEconomy(now);
            if (_trialsCleared == 0)
            {
                _almsAsOf = now; // the alms box starts filling with the first stage
            }
            else
            {
                paid += Refund(AlmsAt(now)); // the box is emptied into the stores before its rate goes up
                _almsAsOf = now;
            }

            _trialsCleared = plan.Night;
            var (bag, towels) = TrialBook.Reward(plan.Night);
            paid += Refund(bag);
            _towels += towels;
        }

        var report = new NightReport(plan.Night, false, false, result.Stars, result.HearthHealth, result.HearthMaxHealth, Array.Empty<int>(), 0, result.StateHashHex);
        return CommandOutcome.Accept(paid) with { Report = report };
    }

    private CommandOutcome ClaimAlms(DateTimeOffset now)
    {
        var alms = AlmsAt(now);
        if (alms.IsZero)
        {
            return CommandOutcome.Reject("The alms box is empty. Walk the Pilgrims' Way to fill it.");
        }

        MaterializeEconomy(now);
        var paid = Refund(alms);
        _almsAsOf = now;
        return CommandOutcome.Accept(paid);
    }

    private void RestoreTrials(BaseData data)
    {
        _trialsCleared = Math.Max(0, data.TrialsCleared);
        _trial = data.TrialInProgress;
        _almsAsOf = data.AlmsAsOfUtcTicks > 0 ? new DateTimeOffset(data.AlmsAsOfUtcTicks, TimeSpan.Zero) : null;
    }
}
