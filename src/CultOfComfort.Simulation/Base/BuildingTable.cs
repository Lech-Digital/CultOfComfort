using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;

namespace CultOfComfort.Simulation.Base;

/// <param name="Cost">Paid when this level is started (level 1 = placing it).</param>
/// <param name="BuildSeconds">Construction time for this level; zero means instant.</param>
/// <param name="PerHour">Production per hour (producers only).</param>
/// <param name="StorageBonus">Added to every resource's storage cap (Storage Hut).</param>
/// <param name="SluiceCharges">Extra Open Sluice charges per Night (Cistern).</param>
/// <param name="Health">How much smashing it takes in a Night before it is wrecked.</param>
/// <param name="Beds">Villagers it houses.</param>
/// <param name="MinHearth">The Hearth level this level needs (0 = whatever the building needs).</param>
public sealed record BuildingLevel(ResourceBag Cost, int BuildSeconds, int PerHour = 0, int StorageBonus = 0, int SluiceCharges = 0, int Health = 0, int Beds = 0, int MinHearth = 0);

/// <param name="MaxCountByHearth">How many may stand at Hearth level 1, 2, 3.</param>
public sealed record BuildingSpec(
    BuildingType Type,
    string Name,
    int UnlockHearth,
    IReadOnlyList<int> MaxCountByHearth,
    IReadOnlyList<BuildingLevel> Levels,
    ResourceType? Produces = null,
    TowerType? Tower = null,
    bool UsesBuilder = true,
    int Width = 1,
    int Depth = 1,
    int Workers = 0)
{
    public bool IsTower => Tower != null;

    public int MaxLevel => Levels.Count;

    public int MaxCount(int hearthLevel) => MaxCountByHearth[Math.Min(Math.Max(hearthLevel, 1), MaxCountByHearth.Count) - 1];
}

/// <summary>The village balance (spec: Content). Versioned separately from combat balance.</summary>
public sealed class BuildingTable
{
    private const int Unlimited = int.MaxValue;

    public static BuildingTable Default { get; } = new();

    public string Version { get; init; } = "village-2026.10.3";

    /// <summary>Health of a building whose first level is still being built.</summary>
    public int UnderConstructionHealth { get; init; } = 30;

    /// <summary>Producers hold at most this many hours of output before someone collects.</summary>
    public int BufferHours { get; init; } = 8;

    public IReadOnlyList<int> HearthStorageCaps { get; init; } = new[] { 1000, 2500, 6000 };

    public ResourceBag StartingStock { get; init; } = new(500, 60, 380, 0);

    public TensionTable Tension { get; init; } = TensionTable.Default;

    /// <summary>Timber from cutting a wild tree.</summary>
    public int WildTreeTimber { get; init; } = 15;

    /// <summary>A cut wild tree grows back on its tile after this long (unless something was built there).</summary>
    public int WildTreeRegrowHours { get; init; } = 8;

    /// <summary>Clearing a loose boulder costs this and gives <see cref="BoulderScrap"/> back (stone: in = out).</summary>
    public ResourceBag BoulderCost { get; init; } = default;

    public int BoulderScrap { get; init; } = 10;

    /// <summary>Beds in the Hearth itself (a new village starts with them filled).</summary>
    public int HearthBeds { get; init; } = 2;

    /// <summary>A villager moves in this often while there is a free bed.</summary>
    public int VillagerArrivalMinutes { get; init; } = 10;

    /// <summary>Peat each villager burns per hour (GDD: heating upkeep). None left: everyone downs tools.</summary>
    public int PeatPerVillagerPerHour { get; init; } = 1;

    public int SalvageBase { get; init; } = 40;

    public int SalvagePerStar { get; init; } = 40;

    public IReadOnlyDictionary<BuildingType, BuildingSpec> Specs { get; init; } = new Dictionary<BuildingType, BuildingSpec>
    {
        [BuildingType.Hearth] = new(BuildingType.Hearth, "Hearth", 1, new[] { 1, 1, 1 }, new[]
        {
            new BuildingLevel(default, 0),
            new BuildingLevel(new ResourceBag(600, 0, 50, 0), 10 * 60),
            new BuildingLevel(new ResourceBag(2000, 200, 300, 100), 60 * 60),
        }),
        [BuildingType.Woodcutter] = new(BuildingType.Woodcutter, "Woodcutter's Lean-to", 1, new[] { 2, 3, 4 }, new[]
        {
            new BuildingLevel(new ResourceBag(50, 0, 0, 0), 30, PerHour: 100, Health: 60),
            new BuildingLevel(new ResourceBag(150, 0, 10, 0), 5 * 60, PerHour: 170, Health: 90),
            new BuildingLevel(new ResourceBag(400, 0, 40, 0), 20 * 60, PerHour: 260, Health: 120),
        }, Produces: ResourceType.Timber, Width: 2, Depth: 1, Workers: 1),
        [BuildingType.PeatCut] = new(BuildingType.PeatCut, "Peat Cut", 1, new[] { 1, 2, 3 }, new[]
        {
            new BuildingLevel(new ResourceBag(80, 0, 10, 0), 60, PerHour: 25, Health: 60),
            new BuildingLevel(new ResourceBag(200, 0, 30, 0), 8 * 60, PerHour: 45, Health: 90),
            new BuildingLevel(new ResourceBag(500, 0, 80, 0), 30 * 60, PerHour: 70, Health: 120),
        }, Produces: ResourceType.Peat, Width: 2, Depth: 4, Workers: 2),
        [BuildingType.StorageHut] = new(BuildingType.StorageHut, "Storage Hut", 1, new[] { 1, 2, 3 }, new[]
        {
            new BuildingLevel(new ResourceBag(100, 0, 0, 0), 60, StorageBonus: 500, Health: 60),
            new BuildingLevel(new ResourceBag(300, 0, 0, 0), 10 * 60, StorageBonus: 1000, Health: 90),
            new BuildingLevel(new ResourceBag(800, 0, 50, 0), 40 * 60, StorageBonus: 2000, Health: 120),
        }, Width: 2, Depth: 2),
        [BuildingType.CultistHut] = new(BuildingType.CultistHut, "Cultist Hut", 1, new[] { 1, 2, 3 }, new[]
        {
            new BuildingLevel(new ResourceBag(120, 0, 0, 0), 2 * 60, PerHour: 4, Health: 60),
            new BuildingLevel(new ResourceBag(350, 50, 0, 0), 12 * 60, PerHour: 8, Health: 90),
            new BuildingLevel(new ResourceBag(900, 150, 0, 0), 45 * 60, PerHour: 14, Health: 120),
        }, Produces: ResourceType.Devotion, Width: 3, Depth: 2, Workers: 1),
        [BuildingType.Cistern] = new(BuildingType.Cistern, "Cistern", 2, new[] { 0, 1, 2 }, new[]
        {
            new BuildingLevel(new ResourceBag(150, 0, 30, 0), 2 * 60, SluiceCharges: 1, Health: 80),
            new BuildingLevel(new ResourceBag(400, 0, 100, 0), 15 * 60, SluiceCharges: 2, Health: 120),
        }, Width: 2, Depth: 2),
        [BuildingType.SlurryChute] = new(BuildingType.SlurryChute, "Slurry Chute", 1, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(0, 0, 50, 0), 0, Health: 80) }, Tower: TowerType.SlurryChute),
        [BuildingType.SaplingFlinger] = new(BuildingType.SaplingFlinger, "Sapling Flinger", 2, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(0, 0, 70, 0), 0, Health: 80) }, Tower: TowerType.SaplingFlinger),
        [BuildingType.WattleFence] = new(BuildingType.WattleFence, "Wattle Fence", 1, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(15, 0, 0, 0), 0, Health: 50) }),
        [BuildingType.WoodenWall] = new(BuildingType.WoodenWall, "Wooden Wall", 1, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(30, 0, 0, 0), 0, Health: 250) }),
        [BuildingType.Gate] = new(BuildingType.Gate, "Gate", 1, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(40, 0, 10, 0), 0, Health: 200) }),
        [BuildingType.Cottage] = new(BuildingType.Cottage, "Cottage", 1, new[] { 3, 5, 8 }, new[]
        {
            new BuildingLevel(new ResourceBag(60, 0, 0, 0), 60, Health: 60, Beds: 4),
            new BuildingLevel(new ResourceBag(150, 0, 20, 0), 8 * 60, Health: 90, Beds: 6),
        }, Width: 2, Depth: 2),
        [BuildingType.DirtTrack] = new(BuildingType.DirtTrack, "Dirt Track", 1, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(2, 0, 0, 0), 0) }, UsesBuilder: false),
        [BuildingType.PlankWalk] = new(BuildingType.PlankWalk, "Plank Walk", 2, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(6, 0, 0, 0), 0) }, UsesBuilder: false),
        [BuildingType.CobbledRoad] = new(BuildingType.CobbledRoad, "Cobbled Road", 3, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(0, 0, 4, 0), 0) }, UsesBuilder: false),
        [BuildingType.Stone] = new(BuildingType.Stone, "Stone", 1, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(0, 0, 10, 0), 0) }),
        [BuildingType.Tree] = new(BuildingType.Tree, "Tree", 1, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(5, 0, 0, 0), 4 * 60 * 60) }, UsesBuilder: false), // a sapling grows by itself
    };

    public BuildingSpec Spec(BuildingType type) => Specs[type];

    /// <summary>Repairing a wrecked building costs 10% of its current level's cost, rounded up (never free).</summary>
    public ResourceBag RepairCost(BuildingType type, int level)
    {
        var cost = Spec(type).Levels[Math.Max(1, level) - 1].Cost;
        static int Tenth(int amount) => (amount + 9) / 10;
        var repair = new ResourceBag(Tenth(cost.Timber), Tenth(cost.Peat), Tenth(cost.Scrap), Tenth(cost.Devotion));
        return repair.IsZero ? new ResourceBag(1, 0, 0, 0) : repair;
    }

    /// <summary>Village roads: painted tile by tile, they connect buildings to the Hearth.</summary>
    public static bool IsRoad(BuildingType type) => type is BuildingType.DirtTrack or BuildingType.PlankWalk or BuildingType.CobbledRoad;

    /// <summary>How good a road is (0 = not a road): a better road paints over a worse one.</summary>
    public static int RoadRank(BuildingType type) => type switch
    {
        BuildingType.DirtTrack => 1,
        BuildingType.PlankWalk => 2,
        BuildingType.CobbledRoad => 3,
        _ => 0,
    };

    /// <summary>Extra output (percent) for a producer beside this road.</summary>
    public static int RoadBonusPercent(BuildingType type) => type switch
    {
        BuildingType.PlankWalk => 10,
        BuildingType.CobbledRoad => 20,
        _ => 0,
    };

    /// <summary>Fences, walls and gates: raiders who can't get round them hack through.</summary>
    public static bool IsBarrier(BuildingType type) => type is BuildingType.WattleFence or BuildingType.WoodenWall or BuildingType.Gate;

    /// <summary>A building's health in a Night: its level's, or a flimsy frame while the first level is being built.</summary>
    public int StructureHealth(BuildingType type, int level)
    {
        var spec = Spec(type);
        if (spec.Levels.All(l => l.Health == 0))
        {
            return 0; // stones and trees: nothing for raiders to smash
        }

        return level == 0 ? UnderConstructionHealth : spec.Levels[level - 1].Health;
    }

    public int Salvage(int stars) => SalvageBase + SalvagePerStar * Math.Min(Math.Max(stars, 0), 3);
}
