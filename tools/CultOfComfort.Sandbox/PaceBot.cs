using CultOfComfort.Simulation.Balance;
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
    private readonly bool _activeFighter; // --fight active: the Night is played by NightBot (spec 2026-11-17 fun first, C7)
    private readonly BaseState _state;
    private DateTimeOffset _now = T0;

    private PaceBot(string policy, bool verbose, bool activeFighter)
    {
        _policy = policy;
        _verbose = verbose;
        _activeFighter = activeFighter;
        _state = BaseState.CreateNew(_table, _map, T0);
    }

    public static void Run(string[] args)
    {
        var policy = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "errands";
        var hoursAt = Array.IndexOf(args, "--hours");
        var hours = hoursAt >= 0 ? double.Parse(args[hoursAt + 1]) : 8;
        var fightAt = Array.IndexOf(args, "--fight");
        var active = fightAt >= 0 && args[fightAt + 1] == "active";
        new PaceBot(policy, args.Contains("--verbose"), active).Play(TimeSpan.FromHours(hours));
    }

    private double Minutes => (_now - T0).TotalMinutes;

    private void Play(TimeSpan length)
    {
        Console.WriteLine($"PACE — policy '{_policy}', {length.TotalHours:0.#} h, {_table.Version}, {_table.Errands.Version}, Nights fought {(_activeFighter ? "actively" : "idly")}");
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
            ErrandGoal.Build when free || !_table.Spec(goal.Type!.Value).UsesBuilder || _table.Spec(goal.Type.Value).Levels[0].BuildSeconds == 0 =>
                Place(goal.Type!.Value, besideRoad: _table.Spec(goal.Type.Value).IsTower),
            ErrandGoal.UpgradeTo when free => Upgrade(goal.Type!.Value),
            ErrandGoal.HearthLevel when free => Send(new UpgradeHearth(Guid.NewGuid()), "UPGRADE HEARTH"),
            ErrandGoal.BuildOnRoad => PlaceOnRoad(goal.Type!.Value),
            ErrandGoal.SurviveNight or ErrandGoal.WinRaid => FightIfAllowed(arm: goal.Goal == ErrandGoal.WinRaid),
            ErrandGoal.SendMissionary => Send(new SendMissionary(Guid.NewGuid(), 1, 1), "SEND MISSIONARY"),
            ErrandGoal.Patron => Send(new SendMissionary(Guid.NewGuid(), 1, 8), "SEND MISSIONARY 8h"),
            ErrandGoal.Study => StudyCheapest(),
            ErrandGoal.MicroRaid => MeetTheLocals(),
            _ => false,
        };
    }

    /// <summary>An errand player saves up when the note says "Raise the Hearth" and the Hearth isn't affordable yet.</summary>
    private bool SavingForErrand()
    {
        var s = Snap();
        if (_policy != "errands" || s.Errand == null || s.Errand.Done)
        {
            return false;
        }

        // An errand player saves for what the note asks (2026-10-16: not only the Hearth): the next build or upgrade.
        var goal = _table.Errands.Errands[s.Errand.Index];
        ResourceBag? cost = goal.Goal switch
        {
            ErrandGoal.HearthLevel when s.HearthLevel < _table.Spec(BuildingType.Hearth).MaxLevel => _table.Spec(BuildingType.Hearth).Levels[s.HearthLevel].Cost,
            ErrandGoal.Build or ErrandGoal.BuildOnRoad when goal.Type != null =>
                _table.Spec(goal.Type.Value).Levels[0].Cost + new ResourceBag(_table.Spec(goal.Type.Value).IsTower ? 0 : 30, 0, 0, 0), // as Place: Timber for its road
            ErrandGoal.UpgradeTo when goal.Type != null && goal.Target - 1 < _table.Spec(goal.Type.Value).Levels.Count => _table.Spec(goal.Type.Value).Levels[goal.Target - 1].Cost,
            ErrandGoal.Study => (s.Research ?? Array.Empty<TechView>()).Where(t => t.State == TechState.Available)
                .Select(t => (ResourceBag?)t.Tech.Cost).OrderBy(c => c!.Value.Timber + c.Value.Peat + c.Value.Scrap + c.Value.Devotion).FirstOrDefault(),
            _ => null,
        };
        return cost != null && !Affords(s.Stock, cost.Value);
    }

    private bool Economy() =>
        Place(BuildingType.Woodcutter) || Place(BuildingType.Cottage) || Place(BuildingType.PeatCut) ||
        Send(new UpgradeHearth(Guid.NewGuid()), "UPGRADE HEARTH") ||
        Place(BuildingType.CultistHut) || Place(BuildingType.StorageHut) || Place(BuildingType.MiddenSorter) || Place(BuildingType.Cistern) ||
        Upgrade(BuildingType.Woodcutter) || Upgrade(BuildingType.CultistHut) || Upgrade(BuildingType.PeatCut) || Upgrade(BuildingType.StorageHut) || Upgrade(BuildingType.Cottage);

    /// <summary>Starts the cheapest study it can afford (an errand player reads whatever the note lets them).</summary>
    private bool StudyCheapest()
    {
        var tech = (Snap().Research ?? Array.Empty<TechView>())
            .Where(t => t.State == TechState.Available && t.Reason == null)
            .OrderBy(t => t.Tech.Cost.Timber + t.Tech.Cost.Peat + t.Tech.Cost.Scrap + t.Tech.Cost.Devotion)
            .FirstOrDefault();
        return tech != null && Send(new StartResearch(Guid.NewGuid(), tech.Tech.Id), $"STUDY {tech.Tech.Name}");
    }

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

        var plan = begun.Plan!;
        var idle = new Battle(plan.ToBattleSetup(_map, Array.Empty<PlayerInput>()));
        var (fought, inputs, battle) = _activeFighter
            ? NightBot.PlayActive(plan.ToBattleSetup(_map, Array.Empty<PlayerInput>()))
            : (RunLogged(idle), (IReadOnlyList<PlayerInput>)Array.Empty<PlayerInput>(), idle);
        var left = battle.Enemies.Where(e => e.IsActive).ToList();
        if (left.Count > 0)
        {
            Note($"  the clock ran out at wave {battle.CurrentWave}/{battle.WaveCount}: {left.Count} still on the road ("
                + string.Join(", ", left.Select(e => $"{e.Type} #{e.Id} at {e.DistanceAlongPath.ToDouble():0.0}/{battle.PathLength.ToDouble():0} offset {e.Offset.X.ToDouble():0.0},{e.Offset.Y.ToDouble():0.0} smash {e.SmashTargetId} raid {e.RaidTargetId} fighting {e.IsFightingLeader} health {e.Health.ToDouble():0}")) + ")"
                + $"; Prophet at {battle.Leader.Position.X.ToDouble():0.0},{battle.Leader.Position.Y.ToDouble():0.0} health {battle.Leader.Health}; road starts {battle.Path[0]}, {battle.Path[1]}, {battle.Path[2]}", always: true);
            var near = battle.Path.Take(5).ToList();
            foreach (var st in battle.Structures.Where(st => (st.Tiles ?? new[] { st.Position }).Any(t => near.Any(p => Math.Abs(p.X - t.X) <= 1 && Math.Abs(p.Y - t.Y) <= 1))))
            {
                Note($"    near the road's start: structure #{st.Id} at {st.Position} health {st.Health}/{st.MaxHealth} barrier {st.IsBarrier}", always: true);
            }

            foreach (var d in battle.Defenders)
            {
                Note($"    defender at {d.Position.X.ToDouble():0.0},{d.Position.Y.ToDouble():0.0} up {d.IsUp}", always: true);
            }
        }
        if (!_activeFighter)
        {
            var old = new Battle(plan.ToBattleSetup(_map, Array.Empty<PlayerInput>()) with { Rules = 0, Objective = NightObjective.None, MaxWrecks = 0 });
            var oldResult = old.RunToCompletion();
            Note($"  the same plan by the old rules: {oldResult.Stars}★, {old.Structures.Count(st => st.IsWrecked)} wrecked, {oldResult.TicksElapsed / BalanceTable.TicksPerSecond}s, "
                + $"Hearth {oldResult.HearthHealth}, {old.Enemies.Count(e => e.IsActive)} still on the road", always: true);
        }

        var resolved = _state.Apply(new ResolveNight(Guid.NewGuid(), plan.Night, inputs), _now);
        var seconds = fought.TicksElapsed / BalanceTable.TicksPerSecond;
        Note($"NIGHT {plan.Night}: {resolved.Report?.Stars}★ salvage {resolved.Report?.Salvage}, {resolved.Report?.Wrecked.Count} wrecked, "
            + $"{seconds / 60}m{seconds % 60:00}s, Hearth {fought.HearthHealth}/{fought.HearthMaxHealth}, {plan.Waves} waves (budget {plan.Budget}), "
            + $"objective {Battle.ObjectiveFor(plan.Night)} {(fought.ObjectiveMet ? "met" : "missed")}, {fought.EarlyCalls} called early, {inputs.Count} inputs", always: true);
        Send(new RepairAll(Guid.NewGuid()), "repair all");
        return true;
    }

    /// <summary>The micro-raid of the first minutes (spec 2026-11-17 fun first, E1): fought idly or actively, like a Night.</summary>
    private bool MeetTheLocals()
    {
        var begun = _state.Apply(new BeginTrial(Guid.NewGuid()), _now);
        if (begun.Status != CommandStatus.Accepted || begun.Plan?.Night != 0)
        {
            return false;
        }

        var setup = begun.Plan.ToBattleSetup(_map, Array.Empty<PlayerInput>());
        var (result, inputs, _) = _activeFighter ? NightBot.PlayActive(setup) : (Battle.Simulate(setup), (IReadOnlyList<PlayerInput>)Array.Empty<PlayerInput>(), null!);
        _state.Apply(new ResolveTrial(Guid.NewGuid(), 0, inputs), _now);
        Note($"MICRO-RAID: {result.Stars}★, {result.TicksElapsed / BalanceTable.TicksPerSecond}s, {result.EnemiesDefeated} driven off, {inputs.Count} inputs", always: true);
        return true;
    }

    private BattleResult RunLogged(Battle battle)
    {
        var timeline = new List<string>();
        while (!battle.IsFinished)
        {
            battle.Step();
            foreach (var e in battle.LastTickEvents)
            {
                switch (e)
                {
                    case WaveStarted w:
                        timeline.Add($"wave {w.Wave} at {w.Tick / BalanceTable.TicksPerSecond}s");
                        break;
                    case WaveGap g:
                        timeline.Add($"gap at {g.Tick / BalanceTable.TicksPerSecond}s");
                        break;
                    case EnemyDefeated d:
                        break;
                }
            }
        }

        Note("  timeline: " + string.Join(" · ", timeline) + $" · {battle.TotalEnemies} raiders, last spawn wave {battle.Enemies.LastOrDefault()?.Wave}", always: true);
        return battle.Result!;
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
