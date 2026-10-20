using System;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>The Prophet's errands: which one is current, whether it is done, and paying it out.</summary>
public sealed partial class BaseState
{
    private int _errandIndex;

    /// <summary>The current event errand (collect, Night) has happened since it came up.</summary>
    private bool _errandEventDone;

    private Errand? CurrentErrand => _errandIndex < _table.Errands.Errands.Count ? _table.Errands.Errands[_errandIndex] : null;

    private ErrandView? ErrandNow()
    {
        var errand = CurrentErrand;
        if (errand == null)
        {
            return null;
        }

        var progress = Math.Min(Progress(errand), errand.Target);
        return new ErrandView(_errandIndex, _table.Errands.Errands.Count, errand.Id, errand.Text, progress, errand.Target, progress >= errand.Target, errand.Reward,
            errand.Goal, errand.Type);
    }

    private int Progress(Errand errand) => errand.Goal switch
    {
        ErrandGoal.Build => _buildings.Count(b => b.Type == errand.Type && b.Level >= 1),
        ErrandGoal.UpgradeTo => _buildings.Where(b => b.Type == errand.Type).Select(b => b.Level).DefaultIfEmpty(0).Max(),
        ErrandGoal.HearthLevel => HearthLevel,
        ErrandGoal.BuildOnRoad => _buildings.Count(b => b.Type == errand.Type && b.Level >= 1 && b.Tiles.Any(_road.Contains)),
        ErrandGoal.SendMissionary => _missionaries.Count > 0 || _influenceMilli.Any(i => i > 0) ? 1 : 0,
        ErrandGoal.Patron => _world?.Patronages ?? _influenceMilli.Count(i => i >= _table.Domain.PatronInfluence * 1000L), // on a shared world the server's ledger holds the influence
        ErrandGoal.Study => _researched.Count,
        _ => _errandEventDone ? errand.Target : 0, // events: the Night or the collect happened
    };

    private CommandOutcome ClaimCurrentErrand()
    {
        var view = ErrandNow();
        if (view == null)
        {
            return CommandOutcome.Reject("The Prophet has no more errands. For now.");
        }

        if (!view.Done)
        {
            return CommandOutcome.Reject("Not done yet. The Prophet is patient, but not that patient.");
        }

        // Like salvage: a reward never overfills storage.
        var caps = Caps;
        var paid = new ResourceBag(
            Math.Min(view.Reward.Timber, Math.Max(0, caps.Timber - _stock.Timber)),
            Math.Min(view.Reward.Peat, Math.Max(0, caps.Peat - _stock.Peat)),
            Math.Min(view.Reward.Scrap, Math.Max(0, caps.Scrap - _stock.Scrap)),
            Math.Min(view.Reward.Devotion, Math.Max(0, caps.Devotion - _stock.Devotion)));
        _stock += paid;
        _errandIndex++;
        _errandEventDone = false;
        _towels += DailyBook.ErrandTowels; // a speed-up for every errand (spec 2026-10-20 long builds)
        return CommandOutcome.Accept(paid);
    }

    private void NoteCollected(ResourceBag gained)
    {
        var errand = CurrentErrand;
        if (errand?.Goal == ErrandGoal.Collect)
        {
            var produced = errand.Type == null ? null : _table.Spec(errand.Type.Value).Produces;
            _errandEventDone |= produced == null ? !gained.IsZero : gained.Get(produced.Value) > 0;
        }
    }

    private void NoteNight(int stars, DateTimeOffset at)
    {
        NoteDaily(DailyGoal.SurviveNight, 1, at);
        NoteDaily(DailyGoal.WinNight, stars >= 2 ? 1 : 0, at);
        var errand = CurrentErrand;
        _errandEventDone |= errand?.Goal == ErrandGoal.SurviveNight || (errand?.Goal == ErrandGoal.WinRaid && stars >= errand.Target);
    }
}
