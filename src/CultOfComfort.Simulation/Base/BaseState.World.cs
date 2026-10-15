using System;

namespace CultOfComfort.Simulation.Base;

/// <summary>
/// The village on a shared world (spec 2026-10-14 shared world). The village still pays for its missionaries and
/// misses them while they walk; the world (on the server) owns the influence, the offerings purse and the Tension
/// rate, and hands them in through server-only commands.
/// </summary>
public sealed partial class BaseState
{
    private WorldLink? _world;

    /// <summary>True once the village stands on a shared world (the Marchlands no longer apply).</summary>
    public bool InWorld => _world != null;

    private CommandOutcome SendOutInWorld(SendWorldMissionary send, DateTimeOffset now)
    {
        if (_world == null)
        {
            return CommandOutcome.Reject("Your village isn't on a shared world.");
        }

        if (send.Distance < 1 || send.Distance > Domain.Reach(HearthLevel))
        {
            return CommandOutcome.Reject("Too far for now. A bigger Hearth, a longer walk.");
        }

        var (problem, preacher) = Dispatch(send.Hours, send.PreacherId);
        if (problem != null)
        {
            return CommandOutcome.Reject(problem);
        }

        MaterializeDomain(now);
        _stock -= Preachers.Provisions(preacher, Times(Domain.ProvisionsPerHour, send.Hours));
        _missionaries.Add(new MissionaryData(_nextMissionaryId++, send.NodeId, send.Hours, now.UtcTicks, Preached: false, send.Distance, preacher));
        return CommandOutcome.Accept();
    }

    private CommandOutcome Join(JoinWorld join, DateTimeOffset now)
    {
        if (_world != null)
        {
            return CommandOutcome.Reject("Already on a world.");
        }

        MaterializeTension(now);
        MaterializeDomain(now);
        Array.Clear(_influenceMilli, 0, _influenceMilli.Length); // the Marchlands are left behind; the purse comes along
        _nextDecayAt = null;
        _world = new WorldLink(join.Q, join.R, 0);
        Advance(now);
        return CommandOutcome.Accept();
    }

    private CommandOutcome ApplyWorldUpdate(WorldUpdate update, DateTimeOffset now)
    {
        if (_world == null || update.TensionPerHourMilli < 0 || update.TensionSpike < 0)
        {
            return CommandOutcome.Reject("Not on a world.");
        }

        MaterializeTension(now); // Tension up to now rose at the old rate
        _world = _world with { TensionPerHourMilli = update.TensionPerHourMilli };
        _tensionMilli = Math.Min(_table.Tension.Max * 1000L, _tensionMilli + update.TensionSpike * 1000L);
        Advance(now);
        return CommandOutcome.Accept();
    }

    private CommandOutcome Receive(ResourceBag offerings, DateTimeOffset now)
    {
        if (_world == null || offerings.Timber < 0 || offerings.Peat < 0 || offerings.Scrap < 0 || offerings.Devotion < 0)
        {
            return CommandOutcome.Reject("Not on a world.");
        }

        var caps = Caps;
        var taken = new ResourceBag(
            Math.Min(offerings.Timber, Math.Max(0, caps.Timber - _stock.Timber)), Math.Min(offerings.Peat, Math.Max(0, caps.Peat - _stock.Peat)),
            Math.Min(offerings.Scrap, Math.Max(0, caps.Scrap - _stock.Scrap)), Math.Min(offerings.Devotion, Math.Max(0, caps.Devotion - _stock.Devotion)));
        if (taken.IsZero)
        {
            return CommandOutcome.Reject(offerings.IsZero ? "No offerings yet. The faithful are thinking about it." : "The stores are full.");
        }

        _stock += taken;
        SettlePeople(now);
        return CommandOutcome.Accept(taken);
    }
}
