using System.Collections.Generic;
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
    IReadOnlyList<GridPosition>? Defenders = null)
{
    public BattleSetup ToBattleSetup(VillageMap map, IReadOnlyList<PlayerInput> inputs) =>
        new(new BattleLayout(map.Width, map.Height, map.Spawn, map.Hearth, Towers, Obstacles, null, Structures, map.Road), Seed, Budget, Waves, inputs) // the fixed road: a gate blocks it, nothing reroutes it
        {
            ExtraSluiceCharges = ExtraSluiceCharges,
            Roster = CultOfComfort.Simulation.Waves.RaidRoster.ForHearth(Roster),
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
