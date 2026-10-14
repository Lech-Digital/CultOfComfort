using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Sandbox;

/// <summary>
/// Plays the real village rules on a fake clock, one minute at a time, to see how long the Hearth levels take
/// (spec 2026-10-13 first-hour arc). Policies: <c>errands</c> (follows the Prophet's errands, the default),
/// <c>builder</c> (builds a bit of everything), <c>rush</c> (Woodcutters, then straight for the Hearth).
/// Usage: dotnet run --project tools/CultOfComfort.Sandbox -- pace [errands|builder|rush] [--hours N] [--verbose]
/// </summary>
internal sealed class PaceBot
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private readonly BuildingTable _table = BuildingTable.Default;
    private readonly VillageMap _map = VillageMap.Demo;
    private readonly List<string> _log = new();
    private readonly string _policy;
    private readonly bool _verbose;
    private readonly BaseState _state;
    private DateTimeOffset _now = T0;

    private PaceBot(string policy, bool verbose)
    {
        _policy = policy;
        _verbose = verbose;
        _state = BaseState.CreateNew(_table, _map, T0);
    }

    public static void Run(string[] args)
    {
        var policy = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "errands";
        var hoursAt = Array.IndexOf(args, "--hours");
        var hours = hoursAt >= 0 ? double.Parse(args[hoursAt + 1]) : 8;
        new PaceBot(policy, args.Contains("--verbose")).Play(TimeSpan.FromHours(hours));
    }

    private double Minutes => (_now - T0).TotalMinutes;

    private void Play(TimeSpan length)
    {
        Console.WriteLine($"PACE — policy '{_policy}', {length.TotalHours:0.#} h, {_table.Version}, {_table.Errands.Version}");
        var hearth = 1;
        var busyFirst20 = 0;
        var hearthAt = new Dictionary<int, double>();
        while (_now - T0 < length)
        {
            var s = Snap();
            if (s.HearthLevel != hearth)
            {
                hearth = s.HearthLevel;
                hearthAt[hearth] = Minutes;
                Note($"*** HEARTH {hearth}  {Fmt(s.Stock)}", always: true);
            }

            foreach (var b in s.Buildings.Where(b => !b.Connected && !BuildingTable.IsRoad(b.Type)))
            {
                Connect(b);
            }

            if (_policy == "errands")
            {
                FollowErrand();
            }

            if (!BuilderBusy(Snap()) && !SavingForErrand())
            {
                var _ = _policy == "rush" ? Rush()
                    : _policy == "errands" && Snap().HearthLevel == 1 ? Place(BuildingType.Woodcutter) // the tutorial: do what the note says
                    : Economy();
            }

            if (Minutes < 20 && BuilderBusy(Snap()))
            {
                busyFirst20++;
            }

            _now += TimeSpan.FromMinutes(1);
        }

        var end = Snap();
        Console.WriteLine();
        foreach (var line in _log)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
        Console.WriteLine($"Hearth 2 at : {(hearthAt.TryGetValue(2, out var h2) ? $"{h2:0} min" : "not reached")}   (target ~20–35 min, the tutorial)");
        Console.WriteLine($"Hearth 3 at : {(hearthAt.TryGetValue(3, out var h3) ? $"{h3 / 60:0.0} h" : "not reached")}   (target ≥ 3.5 h; owner: slower is fine)");
        Console.WriteLine($"Builder busy: {busyFirst20}/20 of the first 20 min");
        Console.WriteLine($"Errand      : {(end.Errand == null ? "all done" : $"{end.Errand.Index + 1}/{end.Errand.Count} {end.Errand.Id} {end.Errand.Progress}/{end.Errand.Target}")}");
        Console.WriteLine($"End stock   : {Fmt(end.Stock)} (caps {Fmt(end.Caps)})");
    }

    // ---------------- policies ----------------

    private void FollowErrand()
    {
        var errand = Snap().Errand;
        if (errand == null)
        {
            return;
        }

        if (errand.Done)
        {
            var paid = _state.Apply(new ClaimErrand(Guid.NewGuid()), _now);
            Note($"errand {errand.Index + 1} '{errand.Id}' claimed +{Fmt(paid.Collected)}", always: true);
            return;
        }

        var goal = _table.Errands.Errands[errand.Index];
        var free = !BuilderBusy(Snap());
        var _ = goal.Goal switch
        {
            ErrandGoal.Build when free || !_table.Spec(goal.Type!.Value).UsesBuilder || _table.Spec(goal.Type.Value).Levels[0].BuildSeconds == 0 => Place(goal.Type!.Value),
            ErrandGoal.UpgradeTo when free => Upgrade(goal.Type!.Value),
            ErrandGoal.HearthLevel when free => Send(new UpgradeHearth(Guid.NewGuid()), "UPGRADE HEARTH"),
            ErrandGoal.BuildOnRoad => PlaceOnRoad(goal.Type!.Value),
            ErrandGoal.SurviveNight or ErrandGoal.WinRaid => FightIfAllowed(arm: goal.Goal == ErrandGoal.WinRaid),
            _ => false,
        };
    }

    /// <summary>An errand player saves up when the note says "Raise the Hearth" and the Hearth isn't affordable yet.</summary>
    private bool SavingForErrand()
    {
        var s = Snap();
        if (_policy != "errands" || s.Errand == null || _table.Errands.Errands[s.Errand.Index].Goal != ErrandGoal.HearthLevel || s.HearthLevel >= _table.Spec(BuildingType.Hearth).MaxLevel)
        {
            return false;
        }

        return !Affords(s.Stock, _table.Spec(BuildingType.Hearth).Levels[s.HearthLevel].Cost);
    }

    private bool Economy() =>
        Place(BuildingType.Woodcutter) || Place(BuildingType.Cottage) || Place(BuildingType.PeatCut) ||
        Send(new UpgradeHearth(Guid.NewGuid()), "UPGRADE HEARTH") ||
        Place(BuildingType.CultistHut) || Place(BuildingType.StorageHut) || Place(BuildingType.Cistern) ||
        Upgrade(BuildingType.Woodcutter) || Upgrade(BuildingType.CultistHut) || Upgrade(BuildingType.PeatCut) || Upgrade(BuildingType.StorageHut) || Upgrade(BuildingType.Cottage);

    private bool Rush() =>
        Place(BuildingType.Woodcutter) || Send(new UpgradeHearth(Guid.NewGuid()), "UPGRADE HEARTH") || (Snap().HearthLevel >= 2 && Economy());

    /// <summary>Lights the lamps when allowed. For a raid it wants to win, it first spends spare Scrap on chutes.</summary>
    private bool FightIfAllowed(bool arm)
    {
        var s = Snap();
        if (arm)
        {
            while (Snap().Stock.Scrap >= 120 && Place(BuildingType.SlurryChute, besideRoad: true))
            {
            }
        }

        if (s.ShieldEndsAtUtc != null || s.NightInProgress != null || BuilderBusy(s))
        {
            return false;
        }

        var begun = _state.Apply(new BeginNight(Guid.NewGuid()), _now);
        if (begun.Status != CommandStatus.Accepted)
        {
            return false;
        }

        var resolved = _state.Apply(new ResolveNight(Guid.NewGuid(), begun.Plan!.Night, Array.Empty<PlayerInput>()), _now);
        Note($"NIGHT {begun.Plan.Night}: {resolved.Report?.Stars}★ salvage {resolved.Report?.Salvage}, {resolved.Report?.Wrecked.Count} wrecked", always: true);
        Send(new RepairAll(Guid.NewGuid()), "repair all");
        return true;
    }

    // ---------------- actions ----------------

    private bool Place(BuildingType type, bool besideRoad = false)
    {
        var s = Snap();
        var spec = _table.Spec(type);
        if (s.HearthLevel < spec.UnlockHearth || s.Buildings.Count(b => b.Type == type) >= spec.MaxCount(s.HearthLevel)
            || !Affords(s.Stock, spec.Levels[0].Cost + new ResourceBag(spec.IsTower ? 0 : 30, 0, 0, 0))) // keep Timber for its road
        {
            return false;
        }

        var road = _map.Road;
        var hearth = _map.Hearth;
        var candidates = AllTiles()
            .Where(p => besideRoad
                ? road.Any(r => Distance(r, p) == 1) && !road.Contains(p)
                : road.All(r => Distance(r, p) > 1))
            .OrderBy(p => besideRoad ? road.Select((r, i) => (r, i)).Where(x => Distance(x.r, p) == 1).Max(x => x.i) * -1 : Distance(p, hearth));
        foreach (var p in candidates)
        {
            if (_map.PlacementProblem(s, spec, p, false, null, _table) == null && Send(new PlaceBuilding(Guid.NewGuid(), type, p), $"place {type}"))
            {
                var placed = Snap().Buildings.Where(b => b.Type == type).OrderByDescending(b => b.Id).First();
                if (!placed.Connected && !spec.IsTower)
                {
                    Connect(placed);
                }

                return true;
            }
        }

        return false;
    }

    private bool PlaceOnRoad(BuildingType type)
    {
        var s = Snap();
        var spec = _table.Spec(type);
        foreach (var tile in _map.Road.Skip(_map.Road.Count / 3))
        {
            if (_map.PlacementProblem(s, spec, tile, false, null, _table) == null && Send(new PlaceBuilding(Guid.NewGuid(), type, tile), $"place {type} on the road"))
            {
                return true;
            }
        }

        return false;
    }

    private bool Upgrade(BuildingType type)
    {
        foreach (var b in Snap().Buildings.Where(b => b.Type == type && !b.IsUpgrading && b.Level >= 1 && b.Level < _table.Spec(type).MaxLevel).OrderBy(b => b.Level))
        {
            if (Send(new UpgradeBuilding(Guid.NewGuid(), b.Id), $"upgrade {type} → L{b.Level + 1}"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Paints the shortest Dirt Track from the building to the road network (BFS over buildable tiles).</summary>
    private void Connect(BuildingView building)
    {
        var s = Snap();
        var network = Network(s);
        var track = _table.Spec(BuildingType.DirtTrack);
        var previous = new Dictionary<GridPosition, GridPosition?>();
        var queue = new Queue<GridPosition>();
        foreach (var start in building.Tiles.SelectMany(Neighbours).Where(n => !building.Tiles.Contains(n)).Distinct())
        {
            if (network.Contains(start))
            {
                return;
            }

            if (_map.PlacementProblem(s, track, start, false, null, _table) == null)
            {
                previous[start] = null;
                queue.Enqueue(start);
            }
        }

        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            if (Neighbours(tile).Any(network.Contains))
            {
                var path = new List<GridPosition>();
                for (GridPosition? p = tile; p != null; p = previous[p.Value])
                {
                    path.Add(p.Value);
                }

                Send(new PaintTiles(Guid.NewGuid(), BuildingType.DirtTrack, path), $"road ×{path.Count} to {building.Type}");
                return;
            }

            foreach (var n in Neighbours(tile).Where(n => !previous.ContainsKey(n) && _map.PlacementProblem(s, track, n, false, null, _table) == null))
            {
                previous[n] = tile;
                queue.Enqueue(n);
            }
        }
    }

    // ---------------- helpers ----------------

    private BaseSnapshot Snap() => _state.Snapshot(_now);

    private bool Send(BaseCommand command, string what)
    {
        var accepted = _state.Apply(command, _now).Status == CommandStatus.Accepted;
        if (accepted)
        {
            Note($"{what}  {Fmt(Snap().Stock)}");
        }

        return accepted;
    }

    private void Note(string text, bool always = false)
    {
        if (always || _verbose)
        {
            _log.Add($"{Minutes,6:0} min  {text}");
        }
    }

    private bool BuilderBusy(BaseSnapshot s) => s.Buildings.Any(b => b.IsUpgrading && _table.Spec(b.Type).UsesBuilder);

    private static bool Affords(ResourceBag have, ResourceBag cost) =>
        have.Timber >= cost.Timber && have.Peat >= cost.Peat && have.Scrap >= cost.Scrap && have.Devotion >= cost.Devotion;

    private HashSet<GridPosition> Network(BaseSnapshot s)
    {
        var hearth = s.Buildings.First(b => b.Type == BuildingType.Hearth);
        // As the village's rule: the raiders' road, painted roads and gates carry the network.
        var roads = s.Buildings.Where(b => BuildingTable.IsRoad(b.Type) || b.Type == BuildingType.Gate).SelectMany(b => b.Tiles).Concat(_map.Road).ToHashSet();
        var network = new HashSet<GridPosition>(hearth.Tiles);
        var queue = new Queue<GridPosition>(hearth.Tiles);
        while (queue.Count > 0)
        {
            foreach (var n in Neighbours(queue.Dequeue()).Where(n => roads.Contains(n) && network.Add(n)))
            {
                queue.Enqueue(n);
            }
        }

        return network;
    }

    private IEnumerable<GridPosition> AllTiles() =>
        Enumerable.Range(0, _map.Width).SelectMany(x => Enumerable.Range(0, _map.Height).Select(y => new GridPosition(x, y)));

    private static IEnumerable<GridPosition> Neighbours(GridPosition p) =>
        new[] { new GridPosition(p.X + 1, p.Y), new GridPosition(p.X - 1, p.Y), new GridPosition(p.X, p.Y + 1), new GridPosition(p.X, p.Y - 1) };

    private static int Distance(GridPosition a, GridPosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static string Fmt(ResourceBag b) => $"T{b.Timber} P{b.Peat} S{b.Scrap} D{b.Devotion}";
}
