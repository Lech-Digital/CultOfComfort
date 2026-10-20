using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <param name="Ready">Whole units waiting to be collected.</param>
/// <param name="Capacity">Most it holds before production pauses.</param>
public sealed record BuildingView(int Id, BuildingType Type, int Level, GridPosition Position, int Ready, int Capacity, int PerHour, DateTimeOffset? UpgradeEndsAtUtc, bool Wrecked = false,
    bool Rotated = false, IReadOnlyList<GridPosition>? Footprint = null, bool Connected = true, int Crew = 0, int CrewNeeded = 0, int CrewWanted = 0,
    bool Flipped = false, bool Patrol = false)
{
    public bool IsUpgrading => UpgradeEndsAtUtc != null;

    /// <summary>Every tile it covers (its anchor, <see cref="Position"/>, is the lowest x and y).</summary>
    public IReadOnlyList<GridPosition> Tiles => Footprint ?? new[] { Position };
}

/// <summary>Read model of the village at <see cref="AsOfUtc"/>; what every view renders from.</summary>
public sealed record BaseSnapshot(
    long Version,
    DateTimeOffset AsOfUtc,
    int HearthLevel,
    ResourceBag Stock,
    ResourceBag Caps,
    IReadOnlyList<BuildingView> Buildings,
    int SluiceBonus,
    int LastNightRecorded,
    int Tension = 0,
    TensionState TensionState = TensionState.Calm,
    DateTimeOffset? RaidWarningEndsAtUtc = null,
    DateTimeOffset? ShieldEndsAtUtc = null,
    NightPlan? NightInProgress = null,
    NightReport? UnseenReport = null,
    IReadOnlyList<GridPosition>? ClearedTerrain = null,
    int Population = 0,
    int Beds = 0,
    bool Cold = false,
    ErrandView? Errand = null,
    int Comfort = 0,
    DomainView? Domain = null,
    IReadOnlyList<TechView>? Research = null,
    IReadOnlyList<(string Label, int Amount)>? ComfortParts = null,
    int IdleVillagers = 0,
    DailyView? Daily = null,
    int RaidersRoad = 0,
    int NextRoad = 0,
    IReadOnlyList<StowedTower>? Stowed = null,
    int FreeBuilders = 1,
    int Towels = 0,
    VisitorView? Visitor = null,
    ProphetView? Prophet = null,
    IReadOnlyList<NotableView>? Notables = null,
    Season Season = Season.Spring,
    DateTimeOffset? SeasonEndsAtUtc = null,
    IReadOnlyList<(int Night, int Road, string Who)>? Forecast = null,
    TrialView? Trial = null,
    FestivalView? Festival = null)
{
    public DateTimeOffset? ConstructionEndsAtUtc => Buildings.FirstOrDefault(b => b.IsUpgrading)?.UpgradeEndsAtUtc;

    public BuildingView Building(int id) => Buildings.First(b => b.Id == id);

    public BuildingView? BuildingAt(GridPosition tile) => Buildings.FirstOrDefault(b => b.Tiles.Contains(tile));
}
