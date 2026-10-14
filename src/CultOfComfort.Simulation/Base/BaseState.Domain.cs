using System;
using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>Where a missionary is.</summary>
public enum MissionaryPhase
{
    Travelling,
    Preaching,
    Returning,
}

public sealed record MissionaryView(int Id, int NodeId, int Hours, MissionaryPhase Phase, DateTimeOffset PhaseEndsAtUtc);

/// <param name="InfluencePercent">Your influence there, 0–100 (the Authority's loyalty is the rest).</param>
/// <param name="Reachable">Close enough for a missionary at this Hearth level.</param>
public sealed record DomainNodeView(DomainNode Node, int InfluencePercent, bool Patron, bool Reachable, ResourceBag OfferingsPerHour, int TravelMinutes);

/// <summary>The Authority's map as the player sees it now.</summary>
public sealed record DomainView(
    bool Unlocked,
    IReadOnlyList<DomainNodeView> Nodes,
    IReadOnlyList<MissionaryView> Missionaries,
    int MissionarySlots,
    ResourceBag Offerings,
    ResourceBag OfferingsCap,
    ResourceBag OfferingsPerHour,
    int TensionPerHourMilli,
    DateTimeOffset? TitheHalfUntilUtc);

/// <summary>The Domain (spec 2026-10-14 world map): missionaries, influence, offerings, tithes.</summary>
public sealed partial class BaseState
{
    private readonly long[] _influenceMilli = new long[DomainMap.Marchlands.Nodes.Max(n => n.Id) + 1];
    private readonly List<MissionaryData> _missionaries = new();
    private readonly long[] _offeringsMilli = new long[4];
    private DateTimeOffset _domainAsOf;
    private DateTimeOffset? _nextDecayAt;
    private DateTimeOffset? _lastTitheAt;
    private int _nextMissionaryId = 1;

    private DomainTable Domain => _table.Domain;

    private static DomainMap Marchlands => DomainMap.Marchlands;

    /// <summary>Villagers away preaching (or on the road): they still eat, but they don't work.</summary>
    private int MissionariesAway => _missionaries.Count;

    // ---------------- commands ----------------

    private CommandOutcome SendOut(SendMissionary send, DateTimeOffset now)
    {
        var node = Marchlands.Node(send.NodeId);
        if (HearthLevel < Domain.UnlockHearth)
        {
            return CommandOutcome.Reject($"The Marchlands open at Hearth {Domain.UnlockHearth}.");
        }

        if (node == null || node.Distance > Domain.Reach(HearthLevel))
        {
            return CommandOutcome.Reject("Too far for now. A bigger Hearth, a longer walk.");
        }

        if (Domain.EfficiencyPercent(send.Hours) == null)
        {
            return CommandOutcome.Reject("Preach for 1, 2, 4 or 8 hours.");
        }

        if (_missionaries.Count >= Domain.Missionaries(HearthLevel))
        {
            return CommandOutcome.Reject("Every missionary is already out.");
        }

        if (_population - MissionariesAway < 1)
        {
            return CommandOutcome.Reject("Nobody to send: every villager is out already.");
        }

        var cost = Times(Domain.ProvisionsPerHour, send.Hours);
        if (!_stock.Covers(cost))
        {
            return CommandOutcome.Reject($"Provisions: {cost}.");
        }

        MaterializeDomain(now);
        _stock -= cost;
        _missionaries.Add(new MissionaryData(_nextMissionaryId++, node.Id, send.Hours, now.UtcTicks, Preached: false));
        return CommandOutcome.Accept();
    }

    private CommandOutcome CollectOfferings(DateTimeOffset now)
    {
        MaterializeDomain(now);
        var purse = new ResourceBag((int)(_offeringsMilli[0] / 1000), (int)(_offeringsMilli[1] / 1000), (int)(_offeringsMilli[2] / 1000), (int)(_offeringsMilli[3] / 1000));
        var caps = Caps;
        var taken = new ResourceBag(
            Math.Min(purse.Timber, Math.Max(0, caps.Timber - _stock.Timber)), Math.Min(purse.Peat, Math.Max(0, caps.Peat - _stock.Peat)),
            Math.Min(purse.Scrap, Math.Max(0, caps.Scrap - _stock.Scrap)), Math.Min(purse.Devotion, Math.Max(0, caps.Devotion - _stock.Devotion)));
        if (taken.IsZero)
        {
            return CommandOutcome.Reject(purse.IsZero ? "No offerings yet. The faithful are thinking about it." : "The stores are full.");
        }

        _stock += taken;
        for (var i = 0; i < 4; i++)
        {
            _offeringsMilli[i] -= taken.Get((ResourceType)i) * 1000L; // what didn't fit stays in the purse
        }

        return CommandOutcome.Accept(taken);
    }

    private CommandOutcome Tithe(PayTithe tithe, DateTimeOffset now)
    {
        var amount = tithe.Amount;
        if (amount.Timber < 0 || amount.Peat < 0 || amount.Scrap < 0 || amount.Devotion < 0 || DomainTable.TitheValue(amount) < Domain.TitheValuePerTension)
        {
            return CommandOutcome.Reject("Wulfric doesn't get out of bed for less.");
        }

        if (!_stock.Covers(amount))
        {
            return CommandOutcome.Reject("You don't have that much to give.");
        }

        MaterializeTension(now);
        var halved = _lastTitheAt != null && now < _lastTitheAt.Value.AddHours(Domain.TitheRepeatHours);
        var reduction = DomainTable.TitheValue(amount) * 1000L / Domain.TitheValuePerTension / (halved ? 2 : 1);
        _stock -= amount;
        _tensionMilli = Math.Max(0, _tensionMilli - reduction);
        _lastTitheAt = now;
        if (_raid != null && _night == null && _tensionMilli < _table.Tension.Uneasy * 1000L)
        {
            _raid = null; // Wulfric takes the money and calls the raid off
        }

        Advance(now);
        return CommandOutcome.Accept();
    }

    private static ResourceBag Times(ResourceBag bag, int k) => new(bag.Timber * k, bag.Peat * k, bag.Scrap * k, bag.Devotion * k);

    // ---------------- events (in Advance, in time order) ----------------

    private DateTimeOffset MissionaryPreachEnds(MissionaryData m)
    {
        var node = Marchlands.Node(m.NodeId)!;
        return new DateTimeOffset(m.DepartedAtUtcTicks, TimeSpan.Zero).AddMinutes(node.Distance * Domain.MinutesPerHex).AddHours(m.Hours);
    }

    private DateTimeOffset MissionaryHome(MissionaryData m) => MissionaryPreachEnds(m).AddMinutes(Marchlands.Node(m.NodeId)!.Distance * Domain.MinutesPerHex);

    /// <summary>The next thing to happen in the Domain: a sermon ends, a missionary gets home, influence fades a little.</summary>
    private DateTimeOffset? NextDomainEvent()
    {
        DateTimeOffset? next = _nextDecayAt;
        foreach (var m in _missionaries)
        {
            var at = m.Preached ? MissionaryHome(m) : MissionaryPreachEnds(m);
            next = next == null || at < next ? at : next;
        }

        return next;
    }

    /// <summary>Runs every Domain event due at <paramref name="at"/> (Tension and offerings are brought up to it first).</summary>
    private void RunDomainEvents(DateTimeOffset at)
    {
        MaterializeTension(at);
        MaterializeDomain(at);
        for (var i = 0; i < _missionaries.Count; i++)
        {
            var m = _missionaries[i];
            if (!m.Preached && MissionaryPreachEnds(m) <= at)
            {
                Preach(m);
                _missionaries[i] = m with { Preached = true };
            }
        }

        _missionaries.RemoveAll(m => m.Preached && MissionaryHome(m) <= at);
        if (_nextDecayAt <= at)
        {
            for (var id = 0; id < _influenceMilli.Length; id++)
            {
                _influenceMilli[id] = _influenceMilli[id] * (100 - Domain.DecayPercentPerDay) / 100;
            }

            _nextDecayAt = _influenceMilli.Any(i => i > 0) ? at.AddDays(1) : null;
        }
    }

    private void Preach(MissionaryData m)
    {
        var node = Marchlands.Node(m.NodeId)!;
        var before = _influenceMilli[node.Id];
        _influenceMilli[node.Id] = Math.Min(100_000, before + Domain.GainMilli(node, m.Hours));
        var patronLine = Domain.PatronInfluence * 1000L;
        var spike = 0;
        if (before < patronLine && _influenceMilli[node.Id] >= patronLine)
        {
            spike += Domain.PatronTensionSpike; // the Authority notices a new Patron
        }

        if (node.Type == NodeType.Keep)
        {
            spike += Domain.KeepTensionSpike; // preaching on Wulfric's own doorstep
        }

        _tensionMilli = Math.Min(_table.Tension.Max * 1000L, _tensionMilli + spike * 1000L);
        _nextDecayAt ??= new DateTimeOffset(m.DepartedAtUtcTicks, TimeSpan.Zero).AddDays(1);
    }

    // ---------------- offerings (lazy and linear between events) ----------------

    private ResourceBag OfferingsPerHour()
    {
        long t = 0, p = 0, s = 0, d = 0;
        foreach (var node in Marchlands.Nodes)
        {
            var influence = _influenceMilli[node.Id];
            if (influence <= 0)
            {
                continue;
            }

            var rate = Domain.Kinds[node.Type].OfferingsPerHour;
            var bonus = influence >= Domain.PatronInfluence * 1000L ? 100 + Domain.PatronBonusPercent : 100;
            t += rate.Timber * influence * bonus;
            p += rate.Peat * influence * bonus;
            s += rate.Scrap * influence * bonus;
            d += rate.Devotion * influence * bonus;
        }

        const long Scale = 100_000L * 100; // influence in thousandths of 100, bonus in percent
        return new ResourceBag((int)(t / Scale), (int)(p / Scale), (int)(s / Scale), (int)(d / Scale));
    }

    private long[] OfferingsMilliAt(DateTimeOffset at)
    {
        var purse = (long[])_offeringsMilli.Clone();
        if (at <= _domainAsOf)
        {
            return purse;
        }

        var seconds = (at.UtcTicks - _domainAsOf.UtcTicks) / TimeSpan.TicksPerSecond;
        var rate = OfferingsPerHour();
        for (var i = 0; i < 4; i++)
        {
            var perHour = rate.Get((ResourceType)i);
            var cap = perHour * 1000L * Domain.OfferingsBufferHours;
            purse[i] = Math.Max(purse[i], Math.Min(cap, purse[i] + perHour * 1000L * seconds / 3600));
        }

        return purse;
    }

    private void MaterializeDomain(DateTimeOffset at)
    {
        if (at <= _domainAsOf)
        {
            return;
        }

        var purse = OfferingsMilliAt(at);
        Array.Copy(purse, _offeringsMilli, 4);
        _domainAsOf = at;
    }

    /// <summary>Tension the Domain adds per hour (thousandths): your influence, weighted by how much the Authority minds each place.</summary>
    private long DomainTensionPerHourMilli()
    {
        long weighted = 0;
        foreach (var node in Marchlands.Nodes)
        {
            weighted += _influenceMilli[node.Id] * Domain.Kinds[node.Type].WeightPercent;
        }

        // 100 influence (100 000 thousandths) at weight 100% adds TensionPer100WeightedInfluenceMilli per hour.
        return weighted * Domain.TensionPer100WeightedInfluenceMilli / (100_000L * 100);
    }

    // ---------------- view and storage ----------------

    private DomainView DomainViewAt(DateTimeOffset now)
    {
        var reach = Domain.Reach(HearthLevel);
        var nodes = Marchlands.Nodes.Select(n =>
        {
            var influence = _influenceMilli[n.Id];
            return new DomainNodeView(n, (int)(influence / 1000), influence >= Domain.PatronInfluence * 1000L, HearthLevel >= Domain.UnlockHearth && n.Distance <= reach,
                Domain.Kinds[n.Type].OfferingsPerHour, n.Distance * Domain.MinutesPerHex);
        }).ToList();
        var missionaries = _missionaries.Select(m =>
        {
            var preachEnds = MissionaryPreachEnds(m);
            var arrives = preachEnds.AddHours(-m.Hours);
            var phase = m.Preached ? MissionaryPhase.Returning : now < arrives ? MissionaryPhase.Travelling : MissionaryPhase.Preaching;
            return new MissionaryView(m.Id, m.NodeId, m.Hours, phase, phase == MissionaryPhase.Returning ? MissionaryHome(m) : phase == MissionaryPhase.Travelling ? arrives : preachEnds);
        }).ToList();
        var purse = OfferingsMilliAt(now);
        var perHour = OfferingsPerHour();
        var halfUntil = _lastTitheAt?.AddHours(Domain.TitheRepeatHours);
        return new DomainView(HearthLevel >= Domain.UnlockHearth, nodes, missionaries, Domain.Missionaries(HearthLevel),
            new ResourceBag((int)(purse[0] / 1000), (int)(purse[1] / 1000), (int)(purse[2] / 1000), (int)(purse[3] / 1000)),
            Times(perHour, Domain.OfferingsBufferHours), perHour, (int)(TensionRateMilli()), halfUntil > now ? halfUntil : null);
    }

    private DomainData DomainToData() => new(
        Marchlands.Nodes.Where(n => _influenceMilli[n.Id] > 0).Select(n => (n.Id, _influenceMilli[n.Id])).ToList(),
        _missionaries.ToList(),
        _offeringsMilli.ToList(),
        _domainAsOf.UtcTicks,
        _nextDecayAt?.UtcTicks,
        _lastTitheAt?.UtcTicks,
        _nextMissionaryId);

    private void RestoreDomain(DomainData? data, DateTimeOffset fallback)
    {
        _domainAsOf = fallback;
        if (data == null)
        {
            return; // a village from before the Marchlands: nobody has preached anywhere yet
        }

        foreach (var (nodeId, influence) in data.Influence)
        {
            if (Marchlands.Node(nodeId) == null || influence < 0 || influence > 100_000)
            {
                throw new InvalidBaseDataException($"impossible influence at node {nodeId}");
            }

            _influenceMilli[nodeId] = influence;
        }

        foreach (var m in data.Missionaries)
        {
            if (Marchlands.Node(m.NodeId) == null || _table.Domain.EfficiencyPercent(m.Hours) == null || m.Id >= data.NextMissionaryId)
            {
                throw new InvalidBaseDataException($"impossible missionary {m.Id}");
            }

            _missionaries.Add(m);
        }

        if (data.OfferingsMilli.Count != 4 || data.OfferingsMilli.Any(v => v < 0))
        {
            throw new InvalidBaseDataException("impossible offerings");
        }

        for (var i = 0; i < 4; i++)
        {
            _offeringsMilli[i] = data.OfferingsMilli[i];
        }

        _domainAsOf = data.AsOfUtcTicks > 0 ? new DateTimeOffset(data.AsOfUtcTicks, TimeSpan.Zero) : fallback;
        _nextDecayAt = data.NextDecayUtcTicks == null ? null : new DateTimeOffset(data.NextDecayUtcTicks.Value, TimeSpan.Zero);
        _lastTitheAt = data.LastTitheUtcTicks == null ? null : new DateTimeOffset(data.LastTitheUtcTicks.Value, TimeSpan.Zero);
        _nextMissionaryId = Math.Max(1, data.NextMissionaryId);
    }
}
