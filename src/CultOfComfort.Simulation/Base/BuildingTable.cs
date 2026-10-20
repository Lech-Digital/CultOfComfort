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
/// <param name="Comfort">What it adds to the village's Comfort (negative: Squalor).</param>
/// <param name="PeatPerHour">Peat it burns while it stands (the Bathhouse's stove).</param>
/// <param name="Slots">Worker slots at this level (0: the spec's <see cref="BuildingSpec.Workers"/>); a full crew makes <paramref name="PerHour"/>.</param>
public sealed record BuildingLevel(ResourceBag Cost, int BuildSeconds, int PerHour = 0, int StorageBonus = 0, int SluiceCharges = 0, int Health = 0, int Beds = 0,
    int Comfort = 0, int PeatPerHour = 0, int Slots = 0);

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

    /// <summary>Worker slots at <paramref name="level"/> (0 while unbuilt, or for a building nobody works in).</summary>
    public int SlotsAt(int level) => Workers == 0 || level < 1 ? 0 : Levels[Math.Min(level, Levels.Count) - 1].Slots is var s && s > 0 ? s : Workers;

    /// <summary>
    /// The share of the full rate <paramref name="crew"/> workers make out of <paramref name="slots"/>, in thousandths:
    /// each worker adds less than the one before (GDD 01: diminishing returns per slot).
    /// </summary>
    public static int CrewPerMille(int crew, int slots)
    {
        if (slots <= 0)
        {
            return 1000;
        }

        int Shares(int n)
        {
            var sum = 0;
            for (var i = 0; i < n; i++)
            {
                sum += i < CrewShares.Length ? CrewShares[i] : CrewShares[^1];
            }

            return sum;
        }

        return Shares(Math.Min(Math.Max(crew, 0), slots)) * 1000 / Shares(slots);
    }

    /// <summary>What each worker adds, in order (spec 2026-10-14 cultist work).</summary>
    private static readonly int[] CrewShares = { 100, 50, 30, 20 };
}

/// <summary>The village balance (spec: Content). Versioned separately from combat balance.</summary>
public sealed class BuildingTable
{
    private const int Unlimited = int.MaxValue;

    public static BuildingTable Default { get; } = new();

    public string Version { get; init; } = "village-2026.10.20";

    /// <summary>Health of a building whose first level is still being built.</summary>
    public int UnderConstructionHealth { get; init; } = 30;

    /// <summary>Producers hold at most this many hours of output before someone collects.</summary>
    public int BufferHours { get; init; } = 8;

    public IReadOnlyList<int> HearthStorageCaps { get; init; } = new[] { 1000, 2500, 6000, 12000 };

    public ResourceBag StartingStock { get; init; } = new(500, 60, 380, 0);

    public TensionTable Tension { get; init; } = TensionTable.Default;

    /// <summary>The Prophet's errands: the guided chain from the first Woodcutter to Hearth 3.</summary>
    public ErrandBook Errands { get; init; } = ErrandBook.Default;

    /// <summary>The Marchlands: missionaries, offerings, tithes (spec 2026-10-14 world map).</summary>
    public DomainTable Domain { get; init; } = DomainTable.Default;

    /// <summary>Studies at the Hearth (spec 2026-10-14, research).</summary>
    public ResearchBook Research { get; init; } = ResearchBook.Default;

    /// <summary>The Trolley Market's markup: a swap costs this many percent of the goods' worth (Timber 1, Peat 2, Scrap 2, Devotion 4).</summary>
    public int MarketMarkupPercent { get; init; } = 250;

    /// <summary>What a market swap costs in <paramref name="give"/> for <paramref name="amount"/> of <paramref name="get"/> (rounded up).</summary>
    public int TradePrice(ResourceType give, ResourceType get, int amount)
    {
        static int Worth(ResourceType r) => r switch { ResourceType.Timber => 1, ResourceType.Devotion => 4, _ => 2 };
        var value = (long)amount * Worth(get) * MarketMarkupPercent;
        var per = 100L * Worth(give);
        return (int)((value + per - 1) / per);
    }

    /// <summary>Timber from cutting a wild tree.</summary>
    public int WildTreeTimber { get; init; } = 15;

    /// <summary>A cut wild tree grows back on its tile after this long (unless something was built there).</summary>
    public int WildTreeRegrowHours { get; init; } = 8;

    /// <summary>
    /// What the raiders' road can be paved with, in order (spec 2026-10-20 roads and facings): a look and a little
    /// Comfort, never a change to the Night. Level 0 is the trodden mud it starts as.
    /// </summary>
    public IReadOnlyList<RoadPaving> RaidersRoadPavings { get; init; } = new[]
    {
        new RoadPaving("Gravel", 2, new ResourceBag(300, 0, 150, 0), 3),
        new RoadPaving("Cobbles", 3, new ResourceBag(900, 0, 500, 100), 8),
    };

    /// <summary>Clearing a loose boulder costs this and gives <see cref="BoulderScrap"/> back (stone: in = out).</summary>
    public ResourceBag BoulderCost { get; init; } = default;

    public int BoulderScrap { get; init; } = 10;

    /// <summary>How many gates may stand on the raiders' road at Hearth level 1, 2, 3.</summary>
    public IReadOnlyList<int> RoadGatesByHearth { get; init; } = new[] { 1, 2, 3 };

    /// <summary>How many towers (of every kind together) may stand at Hearth 1, 2, 3, 4 (spec 2026-10-20 defence).</summary>
    public IReadOnlyList<int> TowerCapByHearth { get; init; } = new[] { 4, 7, 10, 14 };

    public int TowerCap(int hearthLevel) => TowerCapByHearth[Math.Min(Math.Max(hearthLevel, 1), TowerCapByHearth.Count) - 1];

    /// <summary>How far (tiles, from its walls) a Guardhouse's wardens go to man a tower.</summary>
    public int GuardhouseReach { get; init; } = 8;

    public int RoadGates(int hearthLevel) => RoadGatesByHearth[Math.Min(Math.Max(hearthLevel, 1), RoadGatesByHearth.Count) - 1];

    /// <summary>Beds in the Hearth itself (a new village starts with them filled). 2026.10.5: 4, one per first job.</summary>
    public int HearthBeds { get; init; } = 4;

    /// <summary>A producer's first level starts with this many minutes of output in store, so the first collect is worth a tap.</summary>
    public int StarterStockMinutes { get; init; } = 15;

    /// <summary>A villager moves in this often while there is a free bed.</summary>
    public int VillagerArrivalMinutes { get; init; } = 10;

    /// <summary>Peat each villager burns per hour (GDD: heating upkeep). None left: everyone downs tools.</summary>
    public int PeatPerVillagerPerHour { get; init; } = 1;

    /// <summary>The Hearth's own Comfort at level 1, 2, 3 (GDD Economy: Comfort = Σ building comfort − Squalor).</summary>
    public IReadOnlyList<int> HearthComfort { get; init; } = new[] { 10, 20, 35 };

    /// <summary>Extra Squalor a Midden Sorter gives off per home (Cottage, Cultist Hut, Bathhouse) within <see cref="SqualorRange"/> tiles.</summary>
    public int SqualorNearHome { get; init; } = 5;

    public int SqualorRange { get; init; } = 3;

    /// <summary>Devotion scales with Comfort: × (1 + Comfort / this), between ½ and ×3.</summary>
    public int ComfortPerDoubleDevotion { get; init; } = 200;

    public int SalvageBase { get; init; } = 40;

    public int SalvagePerStar { get; init; } = 40;

    public IReadOnlyDictionary<BuildingType, BuildingSpec> Specs { get; init; } = new Dictionary<BuildingType, BuildingSpec>
    {
        [BuildingType.Hearth] = new(BuildingType.Hearth, "Hearth", 1, new[] { 1, 1, 1 }, new[]
        {
            new BuildingLevel(default, 0),
            new BuildingLevel(new ResourceBag(450, 0, 50, 0), 10 * 60), // 2026.10.4: the tutorial step (GDD ~20 min), was 600
            new BuildingLevel(new ResourceBag(1200, 150, 200, 100), 45 * 60), // 2026.10.16 first day: ~5 h, not ~30 (was 2000/200/300/100, 60 min)
            new BuildingLevel(new ResourceBag(3000, 600, 600, 300), 3 * 60 * 60), // 2026.10.16: Hearth 4, the Comfort Era
        }),
        [BuildingType.Woodcutter] = new(BuildingType.Woodcutter, "Woodcutter's Lean-to", 1, new[] { 2, 3, 4, 5 }, new[]
        {
            new BuildingLevel(new ResourceBag(50, 0, 0, 0), 30, PerHour: 100, Health: 60),
            new BuildingLevel(new ResourceBag(150, 0, 10, 0), 5 * 60, PerHour: 170, Health: 90, Slots: 2),
            new BuildingLevel(new ResourceBag(400, 0, 40, 0), 20 * 60, PerHour: 260, Health: 120, Slots: 3),
        }, Produces: ResourceType.Timber, Width: 2, Depth: 1, Workers: 1),
        [BuildingType.PeatCut] = new(BuildingType.PeatCut, "Peat Cut", 1, new[] { 1, 2, 3, 4 }, new[]
        {
            new BuildingLevel(new ResourceBag(80, 0, 10, 0), 60, PerHour: 25, Health: 60),
            new BuildingLevel(new ResourceBag(200, 0, 30, 0), 8 * 60, PerHour: 45, Health: 90, Slots: 3),
            new BuildingLevel(new ResourceBag(500, 0, 80, 0), 30 * 60, PerHour: 70, Health: 120, Slots: 4),
        }, Produces: ResourceType.Peat, Width: 2, Depth: 4, Workers: 2),
        [BuildingType.StorageHut] = new(BuildingType.StorageHut, "Storage Hut", 1, new[] { 1, 2, 3, 4 }, new[]
        {
            new BuildingLevel(new ResourceBag(100, 0, 0, 0), 60, StorageBonus: 500, Health: 60),
            new BuildingLevel(new ResourceBag(300, 0, 0, 0), 10 * 60, StorageBonus: 1000, Health: 90),
            new BuildingLevel(new ResourceBag(800, 0, 50, 0), 40 * 60, StorageBonus: 2000, Health: 120),
        }, Width: 2, Depth: 2),
        [BuildingType.CultistHut] = new(BuildingType.CultistHut, "Cultist Hut", 1, new[] { 1, 2, 3, 4 }, new[]
        {
            // 2026.10.4: Devotion was the wall before Hearth 3 (~12 h); now ~3 h with two huts, on the GDD clock.
            new BuildingLevel(new ResourceBag(120, 0, 0, 0), 2 * 60, PerHour: 10, Health: 60, Comfort: 3),
            new BuildingLevel(new ResourceBag(350, 50, 0, 0), 12 * 60, PerHour: 18, Health: 90, Comfort: 5, Slots: 2),
            new BuildingLevel(new ResourceBag(900, 150, 0, 0), 45 * 60, PerHour: 28, Health: 120, Comfort: 8, Slots: 3),
        }, Produces: ResourceType.Devotion, Width: 3, Depth: 2, Workers: 1),
        [BuildingType.Cistern] = new(BuildingType.Cistern, "Cistern", 2, new[] { 0, 1, 2, 3 }, new[]
        {
            new BuildingLevel(new ResourceBag(150, 0, 30, 0), 2 * 60, SluiceCharges: 1, Health: 80),
            new BuildingLevel(new ResourceBag(400, 0, 100, 0), 15 * 60, SluiceCharges: 2, Health: 120),
        }, Width: 2, Depth: 2),
        // Towers (spec 2026-10-20 defence): three levels, each a tile more reach and harder hits, so a tower can stand back from the road.
        [BuildingType.SlurryChute] = new(BuildingType.SlurryChute, "Slurry Chute", 1, new[] { Unlimited, Unlimited, Unlimited }, new[]
        {
            new BuildingLevel(new ResourceBag(0, 0, 50, 0), 0, Health: 80),
            new BuildingLevel(new ResourceBag(60, 0, 120, 0), 0, Health: 120),
            new BuildingLevel(new ResourceBag(150, 0, 250, 0), 0, Health: 160),
        }, Tower: TowerType.SlurryChute),
        [BuildingType.SaplingFlinger] = new(BuildingType.SaplingFlinger, "Sapling Flinger", 2, new[] { Unlimited, Unlimited, Unlimited }, new[]
        {
            new BuildingLevel(new ResourceBag(0, 0, 70, 0), 0, Health: 80),
            new BuildingLevel(new ResourceBag(80, 0, 150, 0), 0, Health: 120),
            new BuildingLevel(new ResourceBag(200, 0, 300, 0), 0, Health: 160),
        }, Tower: TowerType.SaplingFlinger),
        [BuildingType.WattleFence] = new(BuildingType.WattleFence, "Wattle Fence", 1, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(15, 0, 0, 0), 0, Health: 50) }),
        [BuildingType.WoodenWall] = new(BuildingType.WoodenWall, "Wooden Wall", 1, new[] { Unlimited, Unlimited, Unlimited }, new[]
        {
            new BuildingLevel(new ResourceBag(30, 0, 0, 0), 0, Health: 250),
            new BuildingLevel(new ResourceBag(10, 0, 20, 0), 0, Health: 600), // the Stone Wall (level 2 needs Hearth 2)
        }),
        [BuildingType.Gate] = new(BuildingType.Gate, "Gate", 1, new[] { Unlimited, Unlimited, Unlimited }, new[]
        {
            new BuildingLevel(new ResourceBag(40, 0, 10, 0), 0, Health: 200),   // Wooden Gate
            new BuildingLevel(new ResourceBag(120, 0, 60, 0), 0, Health: 450),  // Iron-bound Gate (Hearth 2)
            new BuildingLevel(new ResourceBag(300, 0, 200, 0), 0, Health: 900), // Gatehouse (Hearth 3)
        }),
        [BuildingType.Cottage] = new(BuildingType.Cottage, "Cottage", 1, new[] { 3, 5, 8, 10 }, new[]
        {
            new BuildingLevel(new ResourceBag(60, 0, 0, 0), 60, Health: 60, Beds: 4, Comfort: 4),
            new BuildingLevel(new ResourceBag(150, 0, 20, 0), 8 * 60, Health: 90, Beds: 6, Comfort: 6),
        }, Width: 2, Depth: 2),
        // 2026.10.16 first day: Scrap was the wall before Hearth 3 (12/h, one sorter); now 30/50 an hour, two then three.
        [BuildingType.MiddenSorter] = new(BuildingType.MiddenSorter, "Midden Sorter", 2, new[] { 0, 2, 3, 3 }, new[]
        {
            new BuildingLevel(new ResourceBag(150, 0, 0, 0), 3 * 60, PerHour: 30, Health: 70, Comfort: -10),
            new BuildingLevel(new ResourceBag(400, 0, 40, 0), 15 * 60, PerHour: 50, Health: 100, Comfort: -15, Slots: 2),
        }, Produces: ResourceType.Scrap, Width: 2, Depth: 2, Workers: 1),
        [BuildingType.Bathhouse] = new(BuildingType.Bathhouse, "Bathhouse", 3, new[] { 0, 0, 1, 2 }, new[]
        {
            new BuildingLevel(new ResourceBag(1500, 200, 200, 50), 40 * 60, PerHour: 15, Health: 150, Comfort: 40, PeatPerHour: 10),
            new BuildingLevel(new ResourceBag(3000, 400, 400, 150), 2 * 60 * 60, PerHour: 25, Health: 220, Comfort: 70, PeatPerHour: 15, Slots: 3),
        }, Produces: ResourceType.Devotion, Width: 3, Depth: 3, Workers: 2),
        // 2026.10.16 Hearth 4 (spec 2026-10-16 hearth four): models that were made but never used.
        [BuildingType.Beehive] = new(BuildingType.Beehive, "Beehive", 2, new[] { 0, 3, 3, 5 }, new[]
        {
            new BuildingLevel(new ResourceBag(60, 0, 0, 0), 60, PerHour: 4, Health: 30, Comfort: 2),
        }, Produces: ResourceType.Devotion),
        [BuildingType.Sauna] = new(BuildingType.Sauna, "Sauna", 4, new[] { 0, 0, 0, 1 }, new[]
        {
            new BuildingLevel(new ResourceBag(1200, 200, 100, 0), 60 * 60, PerHour: 12, Health: 150, Comfort: 25, PeatPerHour: 6),
        }, Produces: ResourceType.Devotion, Width: 2, Depth: 2, Workers: 1),
        [BuildingType.Blacksmith] = new(BuildingType.Blacksmith, "Blacksmith", 4, new[] { 0, 0, 0, 2 }, new[]
        {
            new BuildingLevel(new ResourceBag(1500, 0, 300, 0), 60 * 60, PerHour: 70, Health: 150, Comfort: -5),
        }, Produces: ResourceType.Scrap, Width: 2, Depth: 2, Workers: 2),
        [BuildingType.PressureSluice] = new(BuildingType.PressureSluice, "Pressure Sluice", 4, new[] { Unlimited, Unlimited, Unlimited, Unlimited }, new[]
        {
            new BuildingLevel(new ResourceBag(150, 0, 150, 0), 0, Health: 120),
            new BuildingLevel(new ResourceBag(300, 0, 300, 0), 0, Health: 160),
            new BuildingLevel(new ResourceBag(500, 0, 500, 0), 0, Health: 200),
        }, Tower: TowerType.PressureSluice),
        [BuildingType.Guardhouse] = new(BuildingType.Guardhouse, "Guardhouse", 2, new[] { 0, 1, 2, 3 }, new[]
        {
            new BuildingLevel(new ResourceBag(300, 0, 100, 0), 10 * 60, Health: 200, Slots: 2),
            new BuildingLevel(new ResourceBag(700, 0, 250, 0), 30 * 60, Health: 300, Slots: 3),
            new BuildingLevel(new ResourceBag(1500, 0, 500, 50), 60 * 60, Health: 400, Slots: 4),
        }, Width: 2, Depth: 2, Workers: 2),
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

    /// <summary>Things laid tile by tile with a drag: roads, fences and walls.</summary>
    public static bool IsPaintable(BuildingType type) => IsRoad(type) || type is BuildingType.WattleFence or BuildingType.WoodenWall;

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
    /// <summary>Works without a road: bees fetch nothing and carry nothing (spec 2026-10-16 hearth four).</summary>
    public static bool NeedsRoad(BuildingType type) => type != BuildingType.Beehive;

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

/// <summary>A surface for the raiders' road: from Hearth <paramref name="Hearth"/>, for <paramref name="Cost"/>; <paramref name="Comfort"/> in all while it lies there.</summary>
public sealed record RoadPaving(string Name, int Hearth, ResourceBag Cost, int Comfort);
