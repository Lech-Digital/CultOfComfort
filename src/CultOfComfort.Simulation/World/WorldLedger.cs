using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.World;

/// <summary>A cult on the shared map: a player's village or a bot (no village behind it).</summary>
public sealed record WorldCult(string Id, string Name, int Q, int R, bool IsBot, int Colour);

/// <summary>A missionary on the shared map: the sermon ends at <see cref="PreachEndsAtUtcTicks"/>, then the influence lands.</summary>
public sealed record WorldMissionary(int Id, string CultId, int NodeId, int Hours, int BonusPercent, long PreachEndsAtUtcTicks);

/// <summary>What one pass of <see cref="WorldLedger.Advance"/> did to a cult: Tension spikes and whether its shares moved.</summary>
public sealed record CultChange(string CultId, int TensionSpike, bool SharesChanged);

/// <summary>
/// The shared world's influence (spec 2026-10-14 shared world): every place holds 100 influence split between cults and
/// the Authority's loyalty (the rest). New influence takes from loyalty first, then displaces the other cults in
/// proportion; Patron is the top holder at 40+; 2% fades a day; offerings accrue lazily per cult at its current shares.
/// Pure and deterministic: the server, the bots and the test harness all run this.
/// </summary>
public sealed class WorldLedger
{
    public const long Full = 100_000; // influence in thousandths of 100

    private readonly WorldMap _map;
    private readonly DomainTable _table;
    private readonly Dictionary<string, WorldCult> _cults = new(StringComparer.Ordinal);
    private readonly Dictionary<int, SortedDictionary<string, long>> _holdings = new();
    private readonly Dictionary<string, SortedSet<int>> _placesOf = new(StringComparer.Ordinal); // where each cult has (or had) influence: an index, may hold stale places
    private readonly List<WorldMissionary> _missionaries = new();
    private readonly Dictionary<string, long[]> _purses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _purseAsOf = new(StringComparer.Ordinal);
    private int _nextMissionaryId = 1;

    public WorldLedger(WorldMap map, DomainTable table, DateTimeOffset createdAt)
    {
        _map = map;
        _table = table;
        NextDecayAt = createdAt.AddDays(1);
    }

    public WorldMap Map => _map;

    public DateTimeOffset NextDecayAt { get; private set; }

    public IReadOnlyCollection<WorldCult> Cults => _cults.Values;

    public IReadOnlyList<WorldMissionary> Missionaries => _missionaries;

    public WorldCult? Cult(string id) => _cults.TryGetValue(id, out var cult) ? cult : null;

    /// <summary>Cults with a real village behind them (the world's capacity counts these).</summary>
    public int Humans => _cults.Values.Count(c => !c.IsBot);

    /// <summary>Takes a cult off the map: its influence goes back to the Authority, its sermons stop (a bot retiring).</summary>
    public void Remove(string cultId)
    {
        if (!_cults.Remove(cultId))
        {
            return;
        }

        foreach (var holders in _holdings.Values)
        {
            holders.Remove(cultId);
        }

        _missionaries.RemoveAll(m => m.CultId == cultId);
        _placesOf.Remove(cultId);
        _purses.Remove(cultId);
        _purseAsOf.Remove(cultId);
    }

    // ---------------- joining ----------------

    /// <summary>
    /// Places a new cult (random but fair) and adds it; null when the world is full. A cult that is already in stays put.
    /// An empty <paramref name="name"/> names it after the nearest place ("Cult of Muddlecombe").
    /// </summary>
    public WorldCult? Join(string id, string name, bool isBot, ulong seed, DateTimeOffset at)
    {
        if (_cults.TryGetValue(id, out var existing))
        {
            return existing;
        }

        var spot = _map.PlaceVillage(_cults.Values.Select(c => (c.Q, c.R)).ToList(), seed);
        if (spot == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            var nearest = _map.Near(spot.Value.Q, spot.Value.R, 8).OrderBy(n => n.DistanceTo(spot.Value.Q, spot.Value.R)).ThenBy(n => n.Id).FirstOrDefault();
            name = nearest == null ? "Cult of Nowhere" : $"Cult of {nearest.Name}";
        }

        var cult = new WorldCult(id, name, spot.Value.Q, spot.Value.R, isBot, _cults.Count % 12);
        _cults[id] = cult;
        _purses[id] = new long[4];
        _purseAsOf[id] = at;
        return cult;
    }

    // ---------------- missionaries ----------------

    /// <summary>When a missionary sent at <paramref name="departedAt"/> finishes preaching (the walk out, then the hours).</summary>
    public DateTimeOffset PreachEnds(WorldCult cult, WorldNode node, int hours, DateTimeOffset departedAt) =>
        departedAt.AddMinutes(node.DistanceTo(cult.Q, cult.R) * _table.MinutesPerHex).AddHours(hours);

    /// <summary>Why a cult can't preach there (null: it can): the place must exist and be within reach.</summary>
    public string? DispatchProblem(string cultId, int nodeId, int hours, int reach)
    {
        var cult = Cult(cultId);
        var node = _map.Node(nodeId);
        if (cult == null || node == null)
        {
            return "No such place.";
        }

        if (node.DistanceTo(cult.Q, cult.R) > reach)
        {
            return "Too far for now. A bigger Hearth, a longer walk.";
        }

        return _table.EfficiencyPercent(hours) == null ? "Preach for 1, 2, 4 or 8 hours." : null;
    }

    public WorldMissionary Dispatch(string cultId, int nodeId, int hours, int bonusPercent, DateTimeOffset departedAt)
    {
        var cult = _cults[cultId];
        var node = _map.Node(nodeId)!;
        var missionary = new WorldMissionary(_nextMissionaryId++, cultId, nodeId, hours, bonusPercent, PreachEnds(cult, node, hours, departedAt).UtcTicks);
        _missionaries.Add(missionary);
        return missionary;
    }

    /// <summary>Influence (thousandths) a sermon brings, before any displacing: the Marchlands formula on a world place.</summary>
    public long Gain(WorldNode node, int hours, int bonusPercent) =>
        _table.GainMilli(new DomainNode(node.Id, node.Name, node.Type, node.Q, node.R, node.Squalor), hours) * (100 + bonusPercent) / 100;

    // ---------------- time ----------------

    /// <summary>
    /// Runs everything due up to <paramref name="now"/> in time order: sermons ending (influence lands, displacing) and
    /// daily decay. Purses are settled before any share moves. Returns what changed per cult (for Tension).
    /// </summary>
    public IReadOnlyList<CultChange> Advance(DateTimeOffset now)
    {
        var spikes = new Dictionary<string, int>(StringComparer.Ordinal);
        var moved = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var sermon = _missionaries.Where(m => m.PreachEndsAtUtcTicks <= now.UtcTicks).OrderBy(m => m.PreachEndsAtUtcTicks).ThenBy(m => m.Id).FirstOrDefault();
            var decayDue = NextDecayAt <= now;
            if (sermon == null && !decayDue)
            {
                break;
            }

            if (sermon != null && (!decayDue || sermon.PreachEndsAtUtcTicks <= NextDecayAt.UtcTicks))
            {
                var at = new DateTimeOffset(sermon.PreachEndsAtUtcTicks, TimeSpan.Zero);
                _missionaries.Remove(sermon);
                var node = _map.Node(sermon.NodeId)!;
                foreach (var holder in Holders(node.Id).Keys.Append(sermon.CultId).Distinct().ToList())
                {
                    Settle(holder, at);
                    moved.Add(holder);
                }

                var wasPatron = PatronOf(node.Id) == sermon.CultId;
                Grant(node.Id, sermon.CultId, Gain(node, sermon.Hours, sermon.BonusPercent));
                var spike = 0;
                if (!wasPatron && PatronOf(node.Id) == sermon.CultId)
                {
                    spike += _table.PatronTensionSpike;
                }

                if (node.Type is NodeType.Keep or NodeType.Cathedral)
                {
                    spike += _table.KeepTensionSpike;
                }

                if (spike > 0)
                {
                    spikes[sermon.CultId] = spikes.TryGetValue(sermon.CultId, out var s) ? s + spike : spike;
                }

                continue;
            }

            foreach (var cult in _cults.Keys)
            {
                Settle(cult, NextDecayAt);
            }

            foreach (var node in _holdings.Values)
            {
                foreach (var cult in node.Keys.ToList())
                {
                    node[cult] = node[cult] * (100 - _table.DecayPercentPerDay) / 100;
                    moved.Add(cult);
                    if (node[cult] <= 0)
                    {
                        node.Remove(cult);
                    }
                }
            }

            NextDecayAt = NextDecayAt.AddDays(1);
        }

        return moved.Union(spikes.Keys).OrderBy(c => c, StringComparer.Ordinal)
            .Select(c => new CultChange(c, spikes.TryGetValue(c, out var s) ? s : 0, moved.Contains(c))).ToList();
    }

    // ---------------- influence ----------------

    public IReadOnlyDictionary<string, long> Holders(int nodeId) =>
        _holdings.TryGetValue(nodeId, out var holders) ? holders : (IReadOnlyDictionary<string, long>)new Dictionary<string, long>();

    public long Influence(int nodeId, string cultId) => _holdings.TryGetValue(nodeId, out var h) && h.TryGetValue(cultId, out var v) ? v : 0;

    /// <summary>The Authority's share: whatever the cults don't hold.</summary>
    public long Loyalty(int nodeId) => Full - Holders(nodeId).Values.Sum();

    /// <summary>How many places <paramref name="cultId"/> is Patron of.</summary>
    public int Patronages(string cultId) => _placesOf.TryGetValue(cultId, out var places) ? places.Count(n => PatronOf(n) == cultId) : 0;

    /// <summary>The top holder at 40+ (ties: the cult whose id sorts first), or null.</summary>
    public string? PatronOf(int nodeId)
    {
        string? best = null;
        long top = _table.PatronInfluence * 1000L - 1;
        foreach (var (cult, influence) in Holders(nodeId))
        {
            if (influence > top)
            {
                best = cult;
                top = influence;
            }
        }

        return best;
    }

    /// <summary>
    /// New influence for <paramref name="cultId"/>: taken from the Authority's loyalty first, the rest displacing the other
    /// cults in proportion to what they hold (competitive, never all-or-nothing). Returns what the cult really gained.
    /// </summary>
    public long Grant(int nodeId, string cultId, long amount)
    {
        if (!_holdings.TryGetValue(nodeId, out var holders))
        {
            holders = new SortedDictionary<string, long>(StringComparer.Ordinal);
            _holdings[nodeId] = holders;
        }

        var fromLoyalty = Math.Min(amount, Full - holders.Values.Sum());
        var rest = amount - fromLoyalty;
        var others = holders.Where(h => h.Key != cultId).ToList();
        var othersTotal = others.Sum(h => h.Value);
        var displaced = Math.Min(rest, othersTotal);
        long taken = 0;
        if (displaced > 0)
        {
            foreach (var (other, held) in others)
            {
                var loss = held * displaced / othersTotal; // floor: the remainder is never conjured
                holders[other] = held - loss;
                taken += loss;
                if (holders[other] <= 0)
                {
                    holders.Remove(other);
                }
            }
        }

        var gained = fromLoyalty + taken;
        Track(cultId, nodeId);
        holders[cultId] = (holders.TryGetValue(cultId, out var mine) ? mine : 0) + gained;
        if (holders[cultId] <= 0)
        {
            holders.Remove(cultId);
        }

        return gained;
    }

    /// <summary>The places where the cult holds influence now (in id order).</summary>
    public IEnumerable<int> PlacesOf(string cultId) =>
        _placesOf.TryGetValue(cultId, out var places) ? places.Where(n => Influence(n, cultId) > 0) : Enumerable.Empty<int>();

    private void Track(string cultId, int nodeId)
    {
        if (!_placesOf.TryGetValue(cultId, out var places))
        {
            places = new SortedSet<int>();
            _placesOf[cultId] = places;
        }

        places.Add(nodeId);
    }

    // ---------------- offerings and Tension ----------------

    public ResourceBag OfferingsPerHour(string cultId)
    {
        long t = 0, p = 0, s = 0, d = 0;
        foreach (var nodeId in PlacesOf(cultId))
        {
            var influence = Influence(nodeId, cultId);
            if (influence <= 0)
            {
                continue;
            }

            var node = _map.Node(nodeId)!;
            var rate = _table.Kinds[node.Type].OfferingsPerHour;
            var bonus = PatronOf(nodeId) == cultId ? 100 + _table.PatronBonusPercent : 100;
            t += rate.Timber * influence * bonus;
            p += rate.Peat * influence * bonus;
            s += rate.Scrap * influence * bonus;
            d += rate.Devotion * influence * bonus;
        }

        const long Scale = Full * 100;
        return new ResourceBag((int)(t / Scale), (int)(p / Scale), (int)(s / Scale), (int)(d / Scale));
    }

    /// <summary>The Tension a cult's influence adds per hour (thousandths), weighted by how much each place's Authority minds.</summary>
    public int TensionPerHourMilli(string cultId)
    {
        long weighted = 0;
        foreach (var nodeId in PlacesOf(cultId))
        {
            weighted += Influence(nodeId, cultId) * _table.Kinds[_map.Node(nodeId)!.Type].WeightPercent;
        }

        return (int)(weighted * _table.TensionPer100WeightedInfluenceMilli / (Full * 100));
    }

    /// <summary>The cult's offerings purse at <paramref name="at"/> (whole resources), without changing anything.</summary>
    public ResourceBag Purse(string cultId, DateTimeOffset at)
    {
        var milli = PurseMilliAt(cultId, at);
        return new ResourceBag((int)(milli[0] / 1000), (int)(milli[1] / 1000), (int)(milli[2] / 1000), (int)(milli[3] / 1000));
    }

    /// <summary>Takes what the village could store out of the purse (the rest stays).</summary>
    public void Take(string cultId, ResourceBag taken, DateTimeOffset at)
    {
        Settle(cultId, at);
        var purse = _purses[cultId];
        for (var i = 0; i < 4; i++)
        {
            purse[i] = Math.Max(0, purse[i] - taken.Get((ResourceType)i) * 1000L);
        }
    }

    private long[] PurseMilliAt(string cultId, DateTimeOffset at)
    {
        if (!_purses.TryGetValue(cultId, out var stored))
        {
            return new long[4];
        }

        var purse = (long[])stored.Clone();
        var asOf = _purseAsOf[cultId];
        if (at <= asOf)
        {
            return purse;
        }

        var seconds = (at.UtcTicks - asOf.UtcTicks) / TimeSpan.TicksPerSecond;
        var rate = OfferingsPerHour(cultId);
        for (var i = 0; i < 4; i++)
        {
            var perHour = rate.Get((ResourceType)i);
            var cap = perHour * 1000L * _table.OfferingsBufferHours;
            purse[i] = Math.Max(purse[i], Math.Min(cap, purse[i] + perHour * 1000L * seconds / 3600));
        }

        return purse;
    }

    private void Settle(string cultId, DateTimeOffset at)
    {
        if (!_purses.ContainsKey(cultId) || at <= _purseAsOf[cultId])
        {
            return;
        }

        _purses[cultId] = PurseMilliAt(cultId, at);
        _purseAsOf[cultId] = at;
    }

    // ---------------- storage ----------------

    public WorldLedgerData ToData() => new(
        _cults.Values.OrderBy(c => c.Id, StringComparer.Ordinal).ToList(),
        _holdings.OrderBy(h => h.Key).SelectMany(h => h.Value.Select(v => new HoldingData(h.Key, v.Key, v.Value))).ToList(),
        _missionaries.ToList(),
        _purses.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new PurseData(p.Key, p.Value.ToList(), _purseAsOf[p.Key].UtcTicks)).ToList(),
        NextDecayAt.UtcTicks,
        _nextMissionaryId);

    public static WorldLedger Restore(WorldMap map, DomainTable table, WorldLedgerData data)
    {
        var ledger = new WorldLedger(map, table, new DateTimeOffset(data.NextDecayUtcTicks, TimeSpan.Zero).AddDays(-1));
        foreach (var cult in data.Cults)
        {
            ledger._cults[cult.Id] = cult;
        }

        foreach (var h in data.Holdings)
        {
            if (map.Node(h.NodeId) == null || h.Milli <= 0 || h.Milli > Full)
            {
                throw new ArgumentException($"impossible holding at node {h.NodeId}");
            }

            ledger.Grant(h.NodeId, h.CultId, 0);
            ledger._holdings[h.NodeId][h.CultId] = h.Milli;
            ledger.Track(h.CultId, h.NodeId);
        }

        ledger._missionaries.AddRange(data.Missionaries);
        foreach (var p in data.Purses)
        {
            ledger._purses[p.CultId] = p.Milli.ToArray();
            ledger._purseAsOf[p.CultId] = new DateTimeOffset(p.AsOfUtcTicks, TimeSpan.Zero);
        }

        ledger._nextMissionaryId = Math.Max(1, data.NextMissionaryId);
        return ledger;
    }
}

public sealed record HoldingData(int NodeId, string CultId, long Milli);

public sealed record PurseData(string CultId, IReadOnlyList<long> Milli, long AsOfUtcTicks);

public sealed record WorldLedgerData(
    IReadOnlyList<WorldCult> Cults,
    IReadOnlyList<HoldingData> Holdings,
    IReadOnlyList<WorldMissionary> Missionaries,
    IReadOnlyList<PurseData> Purses,
    long NextDecayUtcTicks,
    int NextMissionaryId);
