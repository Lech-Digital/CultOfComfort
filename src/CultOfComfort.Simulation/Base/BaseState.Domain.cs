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

/// <param name="BonusPercent">How much more faith this sermon wins: research plus the preacher's level and quirk (the server reads it for the shared world).</param>
public sealed record MissionaryView(int Id, int NodeId, int Hours, MissionaryPhase Phase, DateTimeOffset PhaseEndsAtUtc, int PreacherId = 0, int BonusPercent = 0);

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
    DateTimeOffset? TitheHalfUntilUtc,
    int MissionaryBonusPercent = 0,
    int TitheBonusPercent = 0,
    WorldLink? World = null,
    IReadOnlyList<PreacherView>? Preachers = null,
    IReadOnlyList<RoadReport>? Reports = null,
    DemandView? Demand = null,
    IReadOnlyList<TaleView>? Tales = null,
    DateTimeOffset? NextDemandAtUtc = null);

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
    private readonly Dictionary<int, int> _preacherXp = new();
    private readonly List<RoadReport> _reports = new();
    private DemandData? _demand;
    private DateTimeOffset? _nextDemandAt;
    private int _nextDemandId = 1;
    private readonly List<TaleData> _tales = new();
    private const int ReportsKept = 6;

    private DomainTable Domain => _table.Domain;

    private static DomainMap Marchlands => DomainMap.Marchlands;

    /// <summary>Villagers away preaching (or on the road): they still eat, but they don't work.</summary>
    private int MissionariesAway => _missionaries.Count;

    // ---------------- commands ----------------

    private CommandOutcome SendOut(SendMissionary send, DateTimeOffset now)
    {
        var node = Marchlands.Node(send.NodeId);
        if (_world != null)
        {
            return CommandOutcome.Reject("Preach on the world map now.");
        }

        if (HearthLevel < Domain.UnlockHearth)
        {
            return CommandOutcome.Reject($"The Marchlands open at Hearth {Domain.UnlockHearth}.");
        }

        if (node == null || node.Distance > Domain.Reach(HearthLevel))
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
        _missionaries.Add(new MissionaryData(_nextMissionaryId++, node.Id, send.Hours, now.UtcTicks, Preached: false, PreacherId: preacher));
        Festival(FestivalDeed.PreachHours, send.Hours, now);
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
        var reduction = DomainTable.TitheValue(amount) * 1000L * (100 + Bonus(TechEffect.TithePercent)) / 100 / Domain.TitheValuePerTension / (halved ? 2 : 1);
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

    /// <summary>
    /// What stops a preacher going out for <paramref name="hours"/> (Marchlands or world alike), or null; and which
    /// preacher goes (<paramref name="requested"/>, or the first free one for 0).
    /// </summary>
    private (string? Problem, int PreacherId) Dispatch(int hours, int requested)
    {
        var problem = DispatchProblem(hours);
        if (problem != null)
        {
            return (problem, 0);
        }

        var slots = Domain.Missionaries(HearthLevel);
        var busy = PreacherOf().Values.ToHashSet();
        var preacher = requested != 0 ? requested : Enumerable.Range(1, slots).FirstOrDefault(id => !busy.Contains(id));
        if (preacher < 1 || preacher > slots)
        {
            return ("No such preacher in the Mission House.", 0);
        }

        if (busy.Contains(preacher))
        {
            return ($"{Preachers.Name(preacher)} is already out.", 0);
        }

        var cost = Preachers.Provisions(preacher, Times(Domain.ProvisionsPerHour, hours));
        return _stock.Covers(cost) ? (null, preacher) : ($"Provisions: {cost}.", 0);
    }

    /// <summary>What stops any missionary going out (slots, villagers); provisions are the preacher's (<see cref="Dispatch"/>).</summary>
    private string? DispatchProblem(int hours)
    {
        if (HearthLevel < Domain.UnlockHearth)
        {
            return $"Missionaries go out from Hearth {Domain.UnlockHearth}.";
        }

        if (Domain.EfficiencyPercent(hours) == null)
        {
            return "Preach for 1, 2, 4 or 8 hours.";
        }

        if (_missionaries.Count >= Domain.Missionaries(HearthLevel))
        {
            return "Every missionary is already out.";
        }

        if (_population - MissionariesAway < 1)
        {
            return "Nobody to send: every villager is out already.";
        }

        return null;
    }

    private int PreacherXp(int id) => _preacherXp.TryGetValue(id, out var xp) ? xp : 0;

    /// <summary>
    /// Which preacher each missionary out is (missionary id → preacher id). One sent before the Mission House has no
    /// preacher of their own: they count as the lowest seat nobody else holds, so the house never shows a free preacher
    /// whose seat is out on the road.
    /// </summary>
    private Dictionary<int, int> PreacherOf()
    {
        var map = new Dictionary<int, int>();
        var taken = _missionaries.Where(m => m.PreacherId > 0).Select(m => m.PreacherId).ToHashSet();
        var next = 1;
        foreach (var m in _missionaries.OrderBy(m => m.Id))
        {
            if (m.PreacherId > 0)
            {
                map[m.Id] = m.PreacherId;
                continue;
            }

            while (taken.Contains(next))
            {
                next++;
            }

            map[m.Id] = next;
            taken.Add(next);
        }

        return map;
    }

    /// <summary>Research plus the preacher's level and quirk, in percent.</summary>
    private int SermonBonusPercent(MissionaryData m) =>
        Bonus(TechEffect.MissionaryPercent) + (m.PreacherId > 0 ? Preachers.BonusPercent(m.PreacherId, PreacherXp(m.PreacherId)) : 0);

    /// <summary>A preacher gets home: experience for the sermon, and a road tale for you to end (spec 2026-10-16).</summary>
    private void ComeHome(MissionaryData m, DateTimeOffset at)
    {
        var preacher = m.PreacherId > 0 ? m.PreacherId : PreacherOf()[m.Id]; // one from before the Mission House: their seat's preacher
        _preacherXp[preacher] = PreacherXp(preacher) + Preachers.XpFor(preacher, m.Hours);
        if (_tales.Count >= Tales.Kept)
        {
            var oldest = _tales[0];
            EndTale(oldest, Tales.FreeChoice(Tales.Options(oldest.Tale, oldest.PreacherId, oldest.Hours))); // nobody listened: it ends the free way
        }

        _tales.Add(new TaleData(m.Id, preacher, m.NodeId, m.Distance != null, m.Hours, Tales.Pick(m.Id, preacher), at.UtcTicks));
    }

    private CommandOutcome Choose(ChooseTale choose, DateTimeOffset now)
    {
        var tale = _tales.FirstOrDefault(t => t.Id == choose.TaleId);
        if (tale == null)
        {
            return CommandOutcome.Reject("That story's been told.");
        }

        var options = Tales.Options(tale.Tale, tale.PreacherId, tale.Hours);
        if (choose.Choice < 0 || choose.Choice >= options.Count)
        {
            return CommandOutcome.Reject("The story doesn't end that way.");
        }

        if (!_stock.Covers(options[choose.Choice].Cost))
        {
            return CommandOutcome.Reject("You don't have that much to give.");
        }

        MaterializeTension(now);
        var gift = EndTale(tale, choose.Choice);
        NoteDaily(DailyGoal.RoadFind, gift.IsZero ? 0 : 1, now);
        Advance(now);
        return CommandOutcome.Accept(gift);
    }

    /// <summary>Ends a tale the chosen way: pays its cost, grants what it gives (if the stores have room), writes the report.</summary>
    private ResourceBag EndTale(TaleData tale, int choice)
    {
        var option = Tales.Options(tale.Tale, tale.PreacherId, tale.Hours)[choice];
        var won = Tales.Lucky(tale.Id, option.ChancePercent);
        var gift = won ? option.Gift : default;
        _stock -= option.Cost;
        _preacherXp[tale.PreacherId] = PreacherXp(tale.PreacherId) + (won ? option.Xp : 1);
        _tensionMilli = Math.Min(_table.Tension.Max * 1000L, _tensionMilli + option.Tension * 1000L);
        var caps = Caps;
        var taken = new ResourceBag(
            Math.Min(gift.Timber, Math.Max(0, caps.Timber - _stock.Timber)), Math.Min(gift.Peat, Math.Max(0, caps.Peat - _stock.Peat)),
            Math.Min(gift.Scrap, Math.Max(0, caps.Scrap - _stock.Scrap)), Math.Min(gift.Devotion, Math.Max(0, caps.Devotion - _stock.Devotion)));
        _stock += taken;
        _tales.Remove(tale);
        _reports.Add(new RoadReport(tale.PreacherId, tale.NodeId, tale.InWorld, tale.Hours, RoadFind.Nothing, taken, tale.HomeAtUtcTicks, tale.Tale,
            won ? choice : -1 - choice));
        if (_reports.Count > ReportsKept)
        {
            _reports.RemoveAt(0);
        }

        return taken;
    }

    // ---------------- the lord's demands (spec 2026-10-15) ----------------

    private CommandOutcome Meet(MeetDemand meet, DateTimeOffset now)
    {
        if (_demand == null || _demand.Id != meet.DemandId)
        {
            return CommandOutcome.Reject("He isn't asking for that any more.");
        }

        if (!_stock.Covers(_demand.Amount))
        {
            return CommandOutcome.Reject("You don't have that much to give.");
        }

        MaterializeTension(now);
        var drop = Demands.TemperDrop(_demand.Amount, Domain) * 1000L * (100 + Bonus(TechEffect.TithePercent)) / 100;
        _stock -= _demand.Amount;
        _tensionMilli = Math.Max(0, _tensionMilli - drop);
        if (_raid != null && _night == null && _tensionMilli < _table.Tension.Uneasy * 1000L)
        {
            _raid = null; // he got what he asked for and calls the raid off
        }

        _demand = null;
        _nextDemandAt = now + Demands.Gap(_nextDemandId);
        Advance(now);
        return CommandOutcome.Accept();
    }

    private CommandOutcome Plead(PleadWithLord plea, DateTimeOffset now)
    {
        if (_demand == null || _demand.Id != plea.DemandId)
        {
            return CommandOutcome.Reject("He isn't asking for that any more.");
        }

        MaterializeTension(now);
        var max = _table.Tension.Max * 1000L;
        switch (plea.Plea)
        {
            case Plea.Stall when _demand.Stalled:
                return CommandOutcome.Reject("He won't wait twice.");
            case Plea.Stall:
                _demand = _demand with { Stalled = true, DueAtUtcTicks = _demand.DueAtUtcTicks + TimeSpan.FromHours(Demands.StallHours).Ticks };
                _tensionMilli = Math.Min(max, _tensionMilli + Demands.StallPenalty * 1000L);
                break;
            case Plea.Flatter when _demand.Haggle != DemandHaggle.None || _demand.Flattered:
                return CommandOutcome.Reject(_demand.Flattered ? "He's been flattered enough. Any more and he'll suspect something." : "Too late for compliments.");
            case Plea.Flatter:
                var cost = Demands.FlatterCost(HearthLevel);
                if (!_stock.Covers(cost))
                {
                    return CommandOutcome.Reject("You don't have that much to give.");
                }

                _stock -= cost;
                _demand = _demand with { Flattered = true };
                break;
            case Plea.Haggle when _demand.Haggle != DemandHaggle.None:
                return CommandOutcome.Reject("You've haggled once. He remembers.");
            case Plea.Haggle when Demands.HaggleWorks(_demand.Id, _demand.Flattered):
                _demand = _demand with { Haggle = DemandHaggle.Won, Amount = Demands.Haggled(_demand.Amount) };
                break;
            case Plea.Haggle:
                _demand = _demand with { Haggle = DemandHaggle.Refused };
                _tensionMilli = Math.Min(max, _tensionMilli + Demands.RefusePenalty * 1000L);
                break;
            default:
                return CommandOutcome.Reject("He doesn't follow.");
        }

        Advance(now);
        return CommandOutcome.Accept();
    }

    /// <summary>The demand clock starts when the Hearth reaches the Domain's level (a construction's end: the same everywhere).</summary>
    private void StartDemands(DateTimeOffset at)
    {
        if (_demand == null && _nextDemandAt == null)
        {
            _nextDemandAt = at.AddMinutes(Demands.FirstGapMinutes);
        }
    }

    /// <summary>The next moment the lord's demands change: a demand arrives, or one runs out.</summary>
    private DateTimeOffset? NextDemandEvent() =>
        _demand != null ? new DateTimeOffset(_demand.DueAtUtcTicks, TimeSpan.Zero) : HearthLevel >= Domain.UnlockHearth ? _nextDemandAt : null;

    private void RunDemandEvents(DateTimeOffset at)
    {
        if (_demand != null && _demand.DueAtUtcTicks <= at.UtcTicks)
        {
            _tensionMilli = Math.Min(_table.Tension.Max * 1000L, _tensionMilli + Demands.MissPenalty * 1000L); // ignored: he's furious
            _demand = null;
            _nextDemandAt = at + Demands.Gap(_nextDemandId);
        }
        else if (_demand == null && _nextDemandAt <= at && HearthLevel >= Domain.UnlockHearth)
        {
            var id = _nextDemandId++;
            _demand = new DemandData(id, Demands.Scaled(Demands.Amount(id, HearthLevel), 100 - Math.Min(90, Bonus(TechEffect.DemandPercent))), at.AddHours(Demands.WindowHours).UtcTicks);
            _nextDemandAt = null;
        }
    }

    private static ResourceBag Times(ResourceBag bag, int k) => new(bag.Timber * k, bag.Peat * k, bag.Scrap * k, bag.Devotion * k);

    // ---------------- events (in Advance, in time order) ----------------

    /// <summary>Hexes from home: carried by a world missionary, looked up in the Marchlands otherwise.</summary>
    private static int MissionaryDistance(MissionaryData m) => m.Distance ?? Marchlands.Node(m.NodeId)!.Distance;

    private DateTimeOffset MissionaryPreachEnds(MissionaryData m) =>
        new DateTimeOffset(m.DepartedAtUtcTicks, TimeSpan.Zero).AddMinutes(MissionaryDistance(m) * Domain.MinutesPerHex).AddHours(m.Hours);

    private DateTimeOffset MissionaryHome(MissionaryData m) => MissionaryPreachEnds(m).AddMinutes(MissionaryDistance(m) * Domain.MinutesPerHex);

    /// <summary>The next thing to happen in the Domain: a sermon ends, a missionary gets home, influence fades a little.</summary>
    private DateTimeOffset? NextDomainEvent()
    {
        DateTimeOffset? next = _nextDecayAt;
        var demand = NextDemandEvent();
        next = next == null || demand < next ? demand ?? next : next;
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

        foreach (var m in _missionaries.Where(m => m.Preached && MissionaryHome(m) <= at).ToList())
        {
            ComeHome(m, MissionaryHome(m));
        }

        _missionaries.RemoveAll(m => m.Preached && MissionaryHome(m) <= at);
        RunDemandEvents(at);
        if (_nextDecayAt <= at)
        {
            for (var id = 0; id < _influenceMilli.Length; id++)
            {
                _influenceMilli[id] = _influenceMilli[id] * (100 - Math.Max(0, Domain.DecayPercentPerDay - Bonus(TechEffect.SlowerDecay))) / 100;
            }

            _nextDecayAt = _influenceMilli.Any(i => i > 0) ? at.AddDays(1) : null;
        }
    }

    private void Preach(MissionaryData m)
    {
        if (m.Distance != null || _world != null)
        {
            return; // on a shared world the server's ledger grants the influence (and sends any Tension spike)
        }

        var node = Marchlands.Node(m.NodeId)!;
        var before = _influenceMilli[node.Id];
        _influenceMilli[node.Id] = Math.Min(100_000, before + Domain.GainMilli(node, m.Hours) * (100 + SermonBonusPercent(m)) / 100);
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
        if (_world != null)
        {
            return _world.TensionPerHourMilli;
        }

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
        var preacherOf = PreacherOf();
        var missionaries = _missionaries.Select(m =>
        {
            var preachEnds = MissionaryPreachEnds(m);
            var arrives = preachEnds.AddHours(-m.Hours);
            var phase = m.Preached ? MissionaryPhase.Returning : now < arrives ? MissionaryPhase.Travelling : MissionaryPhase.Preaching;
            return new MissionaryView(m.Id, m.NodeId, m.Hours, phase, phase == MissionaryPhase.Returning ? MissionaryHome(m) : phase == MissionaryPhase.Travelling ? arrives : preachEnds,
                preacherOf[m.Id], SermonBonusPercent(m));
        }).ToList();
        var preachers = Enumerable.Range(1, Domain.Missionaries(HearthLevel)).Select(id =>
        {
            var xp = PreacherXp(id);
            return new PreacherView(id, Preachers.Name(id), Preachers.Quirk(id), Preachers.Level(xp), xp, Preachers.XpForNext(xp), Preachers.BonusPercent(id, xp),
                preacherOf.Where(p => p.Value == id).Select(p => (int?)p.Key).FirstOrDefault());
        }).ToList();
        var demand = _demand == null ? null : new DemandView(_demand.Id, _demand.Amount, new DateTimeOffset(_demand.DueAtUtcTicks, TimeSpan.Zero),
            Demands.TemperDrop(_demand.Amount, Domain) * (100 + Bonus(TechEffect.TithePercent)) / 100, Demands.MissPenalty, Demands.Reason(_demand.Id),
            _demand.Stalled, _demand.Haggle, _demand.Flattered, Demands.Haggled(_demand.Amount), Demands.FlatterCost(HearthLevel),
            _demand.Flattered ? 100 : Demands.HaggleChancePercent);
        var tales = _tales.Select(t => new TaleView(t.Id, t.PreacherId, t.NodeId, t.InWorld, t.Hours, t.Tale, Tales.Options(t.Tale, t.PreacherId, t.Hours))).ToList();
        var purse = OfferingsMilliAt(now);
        var perHour = OfferingsPerHour();
        var halfUntil = _lastTitheAt?.AddHours(Domain.TitheRepeatHours);
        return new DomainView(HearthLevel >= Domain.UnlockHearth, nodes, missionaries, Domain.Missionaries(HearthLevel),
            new ResourceBag((int)(purse[0] / 1000), (int)(purse[1] / 1000), (int)(purse[2] / 1000), (int)(purse[3] / 1000)),
            Times(perHour, Domain.OfferingsBufferHours), perHour, (int)(TensionRateMilli()), halfUntil > now ? halfUntil : null,
            Bonus(TechEffect.MissionaryPercent), Bonus(TechEffect.TithePercent), _world, preachers, _reports.ToList(), demand, tales,
            _demand == null && HearthLevel >= Domain.UnlockHearth ? _nextDemandAt : null);
    }

    private DomainData DomainToData() => new(
        Marchlands.Nodes.Where(n => _influenceMilli[n.Id] > 0).Select(n => (n.Id, _influenceMilli[n.Id])).ToList(),
        _missionaries.ToList(),
        _offeringsMilli.ToList(),
        _domainAsOf.UtcTicks,
        _nextDecayAt?.UtcTicks,
        _lastTitheAt?.UtcTicks,
        _nextMissionaryId,
        _world,
        _preacherXp.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)).ToList(),
        _reports.ToList(),
        _demand,
        _nextDemandAt?.UtcTicks,
        _nextDemandId,
        _tales.ToList());

    private void RestoreDomain(DomainData? data, DateTimeOffset fallback)
    {
        _domainAsOf = fallback;
        if (data?.NextDemandUtcTicks == null && data?.Demand == null && HearthLevel >= Domain.UnlockHearth)
        {
            StartDemands(fallback); // a village from before the lord's demands: the first comes half an hour after its latest moment
        }

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
            var placeKnown = m.Distance != null ? m.Distance > 0 && data.World != null : Marchlands.Node(m.NodeId) != null;
            if (!placeKnown || _table.Domain.EfficiencyPercent(m.Hours) == null || m.Id >= data.NextMissionaryId)
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
        if (data.World != null && data.World.TensionPerHourMilli < 0)
        {
            throw new InvalidBaseDataException("impossible world Tension rate");
        }

        _world = data.World;
        foreach (var (preacher, xp) in data.PreacherXp ?? Array.Empty<(int, int)>())
        {
            if (preacher < 1 || xp < 0)
            {
                throw new InvalidBaseDataException($"impossible preacher {preacher}");
            }

            _preacherXp[preacher] = xp;
        }

        _reports.AddRange((data.Reports ?? Array.Empty<RoadReport>()).TakeLast(ReportsKept));
        var asked = data.Demand?.Amount ?? default;
        if (data.Demand != null && (data.Demand.Id < 1 || data.Demand.Id >= data.NextDemandId || asked.Timber < 0 || asked.Peat < 0 || asked.Scrap < 0 || asked.Devotion < 0))
        {
            throw new InvalidBaseDataException("impossible demand");
        }

        _demand = data.Demand ?? _demand;
        if (data.NextDemandUtcTicks != null)
        {
            _nextDemandAt = new DateTimeOffset(data.NextDemandUtcTicks.Value, TimeSpan.Zero);
        }

        _nextDemandId = Math.Max(_nextDemandId, data.NextDemandId);
        foreach (var t in data.Tales ?? Array.Empty<TaleData>())
        {
            if (t.PreacherId < 1 || _table.Domain.EfficiencyPercent(t.Hours) == null || !Enum.IsDefined(typeof(RoadTale), t.Tale))
            {
                throw new InvalidBaseDataException($"impossible road tale {t.Id}");
            }

            _tales.Add(t);
        }
    }
}
