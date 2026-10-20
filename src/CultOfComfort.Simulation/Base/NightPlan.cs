using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>
/// What a Night will be, fixed when it begins: seed, size and the village layout as it stood. The client fights exactly
/// this; the village (or the server) replays exactly this. Nothing built or finished meanwhile can change it.
/// </summary>
public sealed record NightPlan(
    int Night,
    ulong Seed,
    int Budget,
    int Waves,
    int ExtraSluiceCharges,
    bool Scheduled,
    IReadOnlyList<PlacedTower> Towers,
    IReadOnlyList<GridPosition> Obstacles,
    IReadOnlyList<PlacedStructure> Structures,
    int Roster = 0,
    bool AutoRelics = false,
    IReadOnlyList<GridPosition>? Defenders = null,
    int TowerDamagePercent = 0,
    int ProphetHealthPercent = 0,
    int Road = 0)
{
    /// <summary>
    /// The balance this Night is fought with: research makes towers hit harder and the Prophet sturdier. Nothing
    /// researched (and every Night planned before research could): the default balance, so old replays stay as they were.
    /// </summary>
    public BalanceTable? Balance()
    {
        if (TowerDamagePercent == 0 && ProphetHealthPercent == 0)
        {
            return null;
        }

        var d = BalanceTable.Default;
        return new BalanceTable
        {
            Towers = d.Towers.ToDictionary(t => t.Key, t => t.Value with { Damage = t.Value.Damage * (100 + TowerDamagePercent) / 100 }),
            Leader = d.Leader with { MaxHealth = d.Leader.MaxHealth * (100 + ProphetHealthPercent) / 100 },
        };
    }

    public BattleSetup ToBattleSetup(VillageMap map, IReadOnlyList<PlayerInput> inputs) =>
        new(new BattleLayout(map.Width, map.Height, map.SpawnOf(Road), map.Hearth, Towers, Obstacles, null, Structures, map.RoadAt(Road)), Seed, Budget, Waves, inputs, Balance()) // the fixed road: a gate blocks it, nothing reroutes it
        {
            ExtraSluiceCharges = ExtraSluiceCharges,
            Roster = CultOfComfort.Simulation.Waves.RaidRoster.ForRoad(Roster, Road), // each road brings its own (spec 2026-10-20 across the river)
            Relics = RelicBook.ForHearth(Roster), // Roster is the Hearth level the Night was planned at
            AutoRelics = AutoRelics,
            Defenders = Defenders ?? System.Array.Empty<GridPosition>(),

        };
}

/// <summary>How a Night went, as the village worked it out (shown once; <see cref="WhileAway"/> when it played itself).</summary>
public sealed record NightReport(
    int Night,
    bool Scheduled,
    bool WhileAway,
    int Stars,
    int HearthHealth,
    int HearthMaxHealth,
    IReadOnlyList<int> Wrecked,
    int Salvage,
    string StateHashHex);
