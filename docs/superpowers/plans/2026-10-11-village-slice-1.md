# The Village, Slice 1 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Real resources, placeable/upgradeable buildings and a Hearth that grows 1 → 3, on the battle grid, run on the phone behind a server-ready gateway.

**Architecture:** A `Base/` domain in the shared Simulation library (`BaseState` aggregate, integer lazy economy, commands with ids, time passed in). The client talks to it only through `IBaseGateway` (`LocalBaseGateway` + `IBaseStore` + `IClock` today; a `RemoteBaseGateway` later). A pure `VillagePresenter` turns snapshots into card/frame models; `BoardView` and `HudView` render; `GameController` routes taps into commands.

**Tech Stack:** netstandard2.1 C# 9 (Simulation), xUnit + FluentAssertions; Unity 6.3 (UI Toolkit, `JsonUtility`), NUnit EditMode/PlayMode; Blender icon pipeline.

**Spec:** `docs/superpowers/specs/2026-10-11-village-slice-1-design.md`

## Global Constraints

- Domain has zero dependencies and never reads a clock: every time-dependent method takes `DateTimeOffset nowUtc`. Integer maths only (amounts in thousandths in producer buffers, rates per hour, elapsed time from `UtcTicks`).
- The client changes the village **only** through `IBaseGateway.SendAsync(BaseCommand, CancellationToken)`; async all the way with `CancellationToken` (CS-DATA-03); DTOs mapped at the persistence boundary (CS-DDD-03).
- Every command carries a `Guid CommandId`; a repeated id is applied once (`CommandStatus.Duplicate`).
- Combat balance and the determinism probe hash `1ddcdf5947cfcaba` are unchanged (`BattleSetup.ExtraSluiceCharges` defaults to 0).
- Numbers per the spec table (`BuildingTable` version `village-2026.10.1`).
- Tests: xUnit + FluentAssertions, `Method_State_Expected`, AAA comments. 60 fps on the Pixel 10. Commits as adzius.lech@gmail.com, no `Co-Authored-By`.

## Review Focus

1. Device clock moved backwards (or a save from "the future") — accrual must never go negative or crash; elapsed time clamps at zero — test in Task 2 (`Snapshot_ClockBehindAsOf_NothingIsLost`).
2. Collecting with storage almost full — only the space is moved, the remainder stays in the building — test in Task 2.
3. A construction that finished while the app was closed must count from its end time, not from "now" — test in Task 2 (`Snapshot_UpgradeFinishedWhileAway_ProducesFromItsEndTime`).
4. A corrupt or hand-edited save file (unknown building type, missing Hearth) starts a fresh village instead of crashing on launch — test in Task 4.
5. Placing a building on the tile the Prophet stands on at Night start / on the spawn or Hearth — rejected; the leader start is recomputed by `BattleLayout` — test in Task 3 (`Apply_PlaceOnSpawnOrHearth_Rejected`).

---

## File Structure

| File | Responsibility |
|---|---|
| `src/CultOfComfort.Simulation/Base/BaseTypes.cs` | `BuildingType`, `ResourceType`, `ResourceBag` |
| `src/CultOfComfort.Simulation/Base/BuildingTable.cs` | `BuildingLevel`, `BuildingSpec`, `BuildingTable` (balance) |
| `src/CultOfComfort.Simulation/Base/VillageMap.cs` | Grid, spawn, Hearth tile, rocks; `Demo` map; `ToBattleLayout` helper |
| `src/CultOfComfort.Simulation/Base/Building.cs` | Building entity (+ producer buffer) |
| `src/CultOfComfort.Simulation/Base/BaseCommands.cs` | Commands, `CommandStatus`, `CommandOutcome` |
| `src/CultOfComfort.Simulation/Base/BaseSnapshot.cs` | `BuildingView`, `BaseSnapshot` |
| `src/CultOfComfort.Simulation/Base/BaseData.cs` | Persistence shape at the domain boundary (`BaseData`, `BuildingData`, `InvalidBaseDataException`) |
| `src/CultOfComfort.Simulation/Base/BaseState.cs` | The aggregate |
| `src/CultOfComfort.Simulation/Combat/BattleTypes.cs`, `Battle.cs` | `ExtraSluiceCharges` |
| `tests/CultOfComfort.Simulation.Tests/Base/*.cs` | Domain tests |
| `client/.../Scripts/Village/IBaseGateway.cs` | `IBaseGateway`, `CommandResult`, `IClock`, `SystemClock`, `IBaseStore` |
| `client/.../Scripts/Village/LocalBaseGateway.cs` | Runs `BaseState` on the device |
| `client/.../Scripts/Village/FileBaseStore.cs` | JSON save via DTOs |
| `client/.../Scripts/Village/VillageComposition.cs` | Composition root for the gateway (test override) |
| `client/.../Scripts/Village/VillagePresenter.cs` | Snapshot → build cards, building card, frame |
| `client/.../Scripts/Views/BoardView.cs` | Buildings, Hearth by level, scaffold, collect icons |
| `client/.../Scripts/Hud/HudView.cs`, `Resources/UI/Hud.uss` | Build drawer, building card |
| `client/.../Scripts/GameController.cs` | Day flow through the gateway; Night layout and salvage |
| `art/blender/icons/b_*.py` | Building icons |

Paths: `client/...` = `client/CultOfComfort.Unity/Assets`. Unity tests as in previous plans (scratch `ut.sh EditMode|PlayMode [filter]`). After any Simulation change: `dotnet build src/CultOfComfort.Simulation` (copies the DLL to Unity) before Unity tests.

---

### Task 1: Resource bag, building table, village map

**Files:**
- Create: `src/CultOfComfort.Simulation/Base/BaseTypes.cs`, `BuildingTable.cs`, `VillageMap.cs`
- Test: `tests/CultOfComfort.Simulation.Tests/Base/BuildingTableTests.cs`

**Interfaces:**
- Produces: `ResourceBag` (record struct `Timber, Peat, Scrap, Devotion`; `+`, `-`, `Covers`, `Half`, `Get`, `Missing(cost)`), `BuildingTable.Default` with `Spec(type)`, `HearthStorageCaps`, `StartingStock`, `BufferHours`, `Salvage(stars)`; `BuildingSpec { Type, Name, UnlockHearth, Levels, Produces, Tower, IsTower, MaxLevel, MaxCount(hearth) }`; `BuildingLevel { Cost, BuildSeconds, PerHour, StorageBonus, SluiceCharges }`; `VillageMap(Width, Height, Spawn, Hearth, Rocks)` with `VillageMap.Demo`, `Contains(tile)`, `ToBattleLayout(towers, obstacles)`.

- [ ] **Step 1: Failing tests**

```csharp
// tests/CultOfComfort.Simulation.Tests/Base/BuildingTableTests.cs
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class BuildingTableTests
{
    [Fact]
    public void Default_Hearth_FollowsTheGddCostsAndCaps()
    {
        // Act
        var hearth = BuildingTable.Default.Spec(BuildingType.Hearth);

        // Assert
        hearth.MaxLevel.Should().Be(3);
        hearth.Levels[1].Cost.Should().Be(new ResourceBag(600, 0, 50, 0));
        hearth.Levels[1].BuildSeconds.Should().Be(600);
        hearth.Levels[2].Cost.Should().Be(new ResourceBag(2000, 200, 300, 100));
        BuildingTable.Default.HearthStorageCaps.Should().Equal(1000, 2500, 6000);
        BuildingTable.Default.StartingStock.Should().Be(new ResourceBag(500, 60, 380, 0));
    }

    [Fact]
    public void Default_Towers_AreUnlimitedAndInstant_FlingerAtHearthTwo()
    {
        // Act
        var chute = BuildingTable.Default.Spec(BuildingType.SlurryChute);
        var flinger = BuildingTable.Default.Spec(BuildingType.SaplingFlinger);

        // Assert
        chute.IsTower.Should().BeTrue();
        chute.Tower.Should().Be(TowerType.SlurryChute);
        chute.UnlockHearth.Should().Be(1);
        chute.MaxCount(1).Should().Be(int.MaxValue);
        chute.Levels[0].BuildSeconds.Should().Be(0);
        flinger.UnlockHearth.Should().Be(2);
        flinger.Levels[0].Cost.Should().Be(new ResourceBag(0, 0, 70, 0));
    }

    [Fact]
    public void ResourceBag_Missing_ListsOnlyWhatIsShort()
    {
        // Arrange
        var stock = new ResourceBag(40, 100, 5, 0);

        // Act
        var missing = stock.Missing(new ResourceBag(50, 20, 10, 0));

        // Assert
        missing.Should().Be(new ResourceBag(10, 0, 5, 0));
        stock.Covers(new ResourceBag(50, 20, 10, 0)).Should().BeFalse();
        stock.Covers(new ResourceBag(40, 100, 5, 0)).Should().BeTrue();
    }

    [Fact]
    public void Salvage_StarsOutOfRange_AreClamped()
    {
        BuildingTable.Default.Salvage(-1).Should().Be(40);
        BuildingTable.Default.Salvage(3).Should().Be(160);
        BuildingTable.Default.Salvage(9).Should().Be(160);
    }

    [Fact]
    public void VillageMap_Demo_MatchesTheBattleBoard()
    {
        // Act
        var map = VillageMap.Demo;

        // Assert
        (map.Width, map.Height).Should().Be((18, 11));
        map.Spawn.Should().Be(new GridPosition(0, 1));
        map.Hearth.Should().Be(new GridPosition(17, 9));
        map.Rocks.Should().HaveCount(20);
        map.ToBattleLayout([], []).ResolveEnemyPath().Should().NotBeEmpty();
    }
}
```

- [ ] **Step 2: Run** `dotnet test --filter BuildingTableTests` → build errors (namespace `Base` missing).

- [ ] **Step 3: Implement**

```csharp
// src/CultOfComfort.Simulation/Base/BaseTypes.cs
using System;

namespace CultOfComfort.Simulation.Base;

public enum BuildingType
{
    Hearth,
    Woodcutter,
    PeatCut,
    StorageHut,
    CultistHut,
    Cistern,
    SlurryChute,
    SaplingFlinger,
}

public enum ResourceType
{
    Timber,
    Peat,
    Scrap,
    Devotion,
}

/// <summary>An amount of each of the four core resources (costs, stock, caps). Value object.</summary>
public readonly record struct ResourceBag(int Timber, int Peat, int Scrap, int Devotion)
{
    public bool IsZero => this == default;

    public int Get(ResourceType type) => type switch
    {
        ResourceType.Timber => Timber,
        ResourceType.Peat => Peat,
        ResourceType.Scrap => Scrap,
        _ => Devotion,
    };

    public static ResourceBag Of(ResourceType type, int amount) => type switch
    {
        ResourceType.Timber => new ResourceBag(amount, 0, 0, 0),
        ResourceType.Peat => new ResourceBag(0, amount, 0, 0),
        ResourceType.Scrap => new ResourceBag(0, 0, amount, 0),
        _ => new ResourceBag(0, 0, 0, amount),
    };

    public static ResourceBag operator +(ResourceBag a, ResourceBag b) =>
        new(a.Timber + b.Timber, a.Peat + b.Peat, a.Scrap + b.Scrap, a.Devotion + b.Devotion);

    public static ResourceBag operator -(ResourceBag a, ResourceBag b) =>
        new(a.Timber - b.Timber, a.Peat - b.Peat, a.Scrap - b.Scrap, a.Devotion - b.Devotion);

    public bool Covers(ResourceBag cost) =>
        Timber >= cost.Timber && Peat >= cost.Peat && Scrap >= cost.Scrap && Devotion >= cost.Devotion;

    /// <summary>How much more of each resource <paramref name="cost"/> needs.</summary>
    public ResourceBag Missing(ResourceBag cost) => new(
        Math.Max(0, cost.Timber - Timber), Math.Max(0, cost.Peat - Peat), Math.Max(0, cost.Scrap - Scrap), Math.Max(0, cost.Devotion - Devotion));

    public ResourceBag Half() => new(Timber / 2, Peat / 2, Scrap / 2, Devotion / 2);

    public override string ToString()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (Timber != 0) parts.Add($"{Timber} Timber");
        if (Peat != 0) parts.Add($"{Peat} Peat");
        if (Scrap != 0) parts.Add($"{Scrap} Scrap");
        if (Devotion != 0) parts.Add($"{Devotion} Devotion");
        return parts.Count == 0 ? "free" : string.Join(" · ", parts);
    }
}
```

```csharp
// src/CultOfComfort.Simulation/Base/BuildingTable.cs
using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;

namespace CultOfComfort.Simulation.Base;

/// <param name="Cost">Paid when this level is started (level 1 = placing it).</param>
/// <param name="BuildSeconds">Construction time for this level; zero means instant.</param>
/// <param name="PerHour">Production per hour (producers only).</param>
/// <param name="StorageBonus">Added to every resource's storage cap (Storage Hut).</param>
/// <param name="SluiceCharges">Extra Open Sluice charges per Night (Cistern).</param>
public sealed record BuildingLevel(ResourceBag Cost, int BuildSeconds, int PerHour = 0, int StorageBonus = 0, int SluiceCharges = 0);

/// <param name="MaxCountByHearth">How many may stand at Hearth level 1, 2, 3.</param>
public sealed record BuildingSpec(
    BuildingType Type,
    string Name,
    int UnlockHearth,
    IReadOnlyList<int> MaxCountByHearth,
    IReadOnlyList<BuildingLevel> Levels,
    ResourceType? Produces = null,
    TowerType? Tower = null)
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

    public string Version { get; init; } = "village-2026.10.1";

    /// <summary>Producers hold at most this many hours of output before someone collects.</summary>
    public int BufferHours { get; init; } = 8;

    public IReadOnlyList<int> HearthStorageCaps { get; init; } = new[] { 1000, 2500, 6000 };

    public ResourceBag StartingStock { get; init; } = new(500, 60, 380, 0);

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
            new BuildingLevel(new ResourceBag(50, 0, 0, 0), 30, PerHour: 100),
            new BuildingLevel(new ResourceBag(150, 0, 10, 0), 5 * 60, PerHour: 170),
            new BuildingLevel(new ResourceBag(400, 0, 40, 0), 20 * 60, PerHour: 260),
        }, Produces: ResourceType.Timber),
        [BuildingType.PeatCut] = new(BuildingType.PeatCut, "Peat Cut", 1, new[] { 1, 2, 3 }, new[]
        {
            new BuildingLevel(new ResourceBag(80, 0, 10, 0), 60, PerHour: 25),
            new BuildingLevel(new ResourceBag(200, 0, 30, 0), 8 * 60, PerHour: 45),
            new BuildingLevel(new ResourceBag(500, 0, 80, 0), 30 * 60, PerHour: 70),
        }, Produces: ResourceType.Peat),
        [BuildingType.StorageHut] = new(BuildingType.StorageHut, "Storage Hut", 1, new[] { 1, 2, 3 }, new[]
        {
            new BuildingLevel(new ResourceBag(100, 0, 0, 0), 60, StorageBonus: 500),
            new BuildingLevel(new ResourceBag(300, 0, 0, 0), 10 * 60, StorageBonus: 1000),
            new BuildingLevel(new ResourceBag(800, 0, 50, 0), 40 * 60, StorageBonus: 2000),
        }),
        [BuildingType.CultistHut] = new(BuildingType.CultistHut, "Cultist Hut", 1, new[] { 1, 2, 3 }, new[]
        {
            new BuildingLevel(new ResourceBag(120, 0, 0, 0), 2 * 60, PerHour: 4),
            new BuildingLevel(new ResourceBag(350, 50, 0, 0), 12 * 60, PerHour: 8),
            new BuildingLevel(new ResourceBag(900, 150, 0, 0), 45 * 60, PerHour: 14),
        }, Produces: ResourceType.Devotion),
        [BuildingType.Cistern] = new(BuildingType.Cistern, "Cistern", 2, new[] { 0, 1, 2 }, new[]
        {
            new BuildingLevel(new ResourceBag(150, 0, 30, 0), 2 * 60, SluiceCharges: 1),
            new BuildingLevel(new ResourceBag(400, 0, 100, 0), 15 * 60, SluiceCharges: 2),
        }),
        [BuildingType.SlurryChute] = new(BuildingType.SlurryChute, "Slurry Chute", 1, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(0, 0, 50, 0), 0) }, Tower: TowerType.SlurryChute),
        [BuildingType.SaplingFlinger] = new(BuildingType.SaplingFlinger, "Sapling Flinger", 2, new[] { Unlimited, Unlimited, Unlimited },
            new[] { new BuildingLevel(new ResourceBag(0, 0, 70, 0), 0) }, Tower: TowerType.SaplingFlinger),
    };

    public BuildingSpec Spec(BuildingType type) => Specs[type];

    public int Salvage(int stars) => SalvageBase + SalvagePerStar * Math.Min(Math.Max(stars, 0), 3);
}
```

```csharp
// src/CultOfComfort.Simulation/Base/VillageMap.cs
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>The ground the village and the Night share: grid, spawn, Hearth tile and rocks.</summary>
public sealed record VillageMap(int Width, int Height, GridPosition Spawn, GridPosition Hearth, IReadOnlyList<GridPosition> Rocks)
{
    /// <summary>The muddy valley the prototype has always used.</summary>
    public static VillageMap Demo { get; } = new(18, 11, new GridPosition(0, 1), new GridPosition(17, 9), DemoRocks());

    public bool Contains(GridPosition tile) => tile.X >= 0 && tile.Y >= 0 && tile.X < Width && tile.Y < Height;

    public BattleLayout ToBattleLayout(IEnumerable<PlacedTower> towers, IEnumerable<GridPosition> obstacles) =>
        new(Width, Height, Spawn, Hearth, towers, Rocks.Concat(obstacles));

    private static IReadOnlyList<GridPosition> DemoRocks()
    {
        var rocks = new List<GridPosition>();
        for (var y = 0; y <= 7; y++)
        {
            rocks.Add(new GridPosition(5, y));
        }

        for (var y = 3; y <= 10; y++)
        {
            rocks.Add(new GridPosition(11, y));
        }

        rocks.AddRange(new[]
        {
            new GridPosition(1, 6), new GridPosition(2, 9), new GridPosition(8, 4),
            new GridPosition(14, 1), new GridPosition(16, 4), new GridPosition(8, 0),
        });
        return rocks;
    }
}
```

(The client's `DemoMap` then delegates to `VillageMap.Demo` in Task 7; the determinism probe keeps using the same tiles.)

- [ ] **Step 4: Run** `dotnet test --filter BuildingTableTests` → 5/5 pass. **Step 5: Commit** `"Village domain: resource bag, building table, village map"`.

---

### Task 2: Buildings, lazy production and construction (aggregate core)

**Files:**
- Create: `Base/Building.cs`, `Base/BaseCommands.cs`, `Base/BaseSnapshot.cs`, `Base/BaseState.cs`
- Test: `tests/.../Base/BaseStateEconomyTests.cs`

**Interfaces:**
- Produces: `BaseState.CreateNew(BuildingTable, VillageMap, DateTimeOffset)`, `Apply(BaseCommand, DateTimeOffset) : CommandOutcome`, `Snapshot(DateTimeOffset) : BaseSnapshot`, `Version`, `HearthLevel`; commands `PlaceBuilding(Guid, BuildingType, GridPosition)`, `UpgradeBuilding(Guid, int)`, `UpgradeHearth(Guid)`, `DemolishBuilding(Guid, int)`, `Collect(Guid, int?)`, `RecordNightResult(Guid, int, int)`, `FinishConstructionNow(Guid)`; `CommandOutcome(CommandStatus Status, long Version, string? Reason, ResourceBag Collected)`; `BuildingView(int Id, BuildingType Type, int Level, GridPosition Position, int Ready, int Capacity, int PerHour, DateTimeOffset? UpgradeEndsAtUtc)`; `BaseSnapshot(long Version, DateTimeOffset AsOfUtc, int HearthLevel, ResourceBag Stock, ResourceBag Caps, IReadOnlyList<BuildingView> Buildings, int SluiceBonus, int LastNightRecorded)` with `ConstructionEndsAtUtc`, `Building(id)`.

- [ ] **Step 1: Failing tests**

```csharp
// tests/CultOfComfort.Simulation.Tests/Base/BaseStateEconomyTests.cs
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class BaseStateEconomyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly GridPosition Field = new(3, 9);

    private static BaseState NewBase() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

    private static CommandOutcome Send(BaseState state, BaseCommand command, DateTimeOffset at) => state.Apply(command, at);

    /// <summary>Places a Woodcutter at T0 and lets its 30 s construction finish.</summary>
    private static (BaseState State, int Id) BaseWithWoodcutter()
    {
        var state = NewBase();
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, Field), T0).Status.Should().Be(CommandStatus.Accepted);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
        return (state, id);
    }

    [Fact]
    public void CreateNew_FreshVillage_HasTheHearthAndStartingStock()
    {
        // Act
        var snapshot = NewBase().Snapshot(T0);

        // Assert
        snapshot.HearthLevel.Should().Be(1);
        snapshot.Stock.Should().Be(new ResourceBag(500, 60, 380, 0));
        snapshot.Caps.Should().Be(new ResourceBag(1000, 1000, 1000, 1000));
        snapshot.Buildings.Should().ContainSingle(b => b.Type == BuildingType.Hearth && b.Position == VillageMap.Demo.Hearth);
        snapshot.Version.Should().Be(0);
    }

    [Fact]
    public void Place_Woodcutter_ChargesTimberAndStartsConstruction()
    {
        // Act
        var (state, id) = BaseWithWoodcutter();
        var snapshot = state.Snapshot(T0.AddSeconds(10));

        // Assert
        snapshot.Stock.Timber.Should().Be(450);
        snapshot.Building(id).Level.Should().Be(0);
        snapshot.Building(id).UpgradeEndsAtUtc.Should().Be(T0.AddSeconds(30));
        snapshot.ConstructionEndsAtUtc.Should().Be(T0.AddSeconds(30));
        snapshot.Version.Should().Be(1);
    }

    [Fact]
    public void Snapshot_AfterConstructionAndOneHour_HasProducedFromTheEndTime()
    {
        // Arrange — built at T0+30s, producing 100 Timber/h from then.
        var (state, id) = BaseWithWoodcutter();

        // Act
        var snapshot = state.Snapshot(T0.AddSeconds(30).AddHours(1));

        // Assert
        snapshot.Building(id).Level.Should().Be(1);
        snapshot.Building(id).Ready.Should().Be(100);
        snapshot.Building(id).Capacity.Should().Be(800, "8 hours of output");
        snapshot.ConstructionEndsAtUtc.Should().BeNull();
    }

    [Fact]
    public void Snapshot_LongAbsence_BufferStopsAtCapacity()
    {
        // Arrange
        var (state, id) = BaseWithWoodcutter();

        // Act
        var snapshot = state.Snapshot(T0.AddDays(3));

        // Assert
        snapshot.Building(id).Ready.Should().Be(800);
    }

    [Fact]
    public void Snapshot_ClockBehindAsOf_NothingIsLost()
    {
        // Arrange — an hour of production, then the device clock jumps back a day.
        var (state, id) = BaseWithWoodcutter();
        var later = T0.AddSeconds(30).AddHours(1);
        state.Snapshot(later);

        // Act
        var snapshot = state.Snapshot(later.AddDays(-1));

        // Assert
        snapshot.Building(id).Ready.Should().BeGreaterThanOrEqualTo(0);
        state.Snapshot(later).Building(id).Ready.Should().Be(100);
    }

    [Fact]
    public void Collect_OneBuilding_MovesItsBufferIntoStock()
    {
        // Arrange
        var (state, id) = BaseWithWoodcutter();
        var at = T0.AddSeconds(30).AddHours(2);

        // Act
        var outcome = Send(state, new Collect(Guid.NewGuid(), id), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        outcome.Collected.Should().Be(new ResourceBag(200, 0, 0, 0));
        var snapshot = state.Snapshot(at);
        snapshot.Stock.Timber.Should().Be(650);
        snapshot.Building(id).Ready.Should().Be(0);
    }

    [Fact]
    public void Collect_StorageNearlyFull_MovesOnlyTheSpaceAndKeepsTheRest()
    {
        // Arrange — 450 Timber after building; 8 h = 800 ready; cap 1000 leaves room for 550.
        var (state, id) = BaseWithWoodcutter();
        var at = T0.AddDays(1);

        // Act
        var outcome = Send(state, new Collect(Guid.NewGuid(), id), at);

        // Assert
        outcome.Collected.Timber.Should().Be(550);
        var snapshot = state.Snapshot(at);
        snapshot.Stock.Timber.Should().Be(1000);
        snapshot.Building(id).Ready.Should().Be(250);
    }

    [Fact]
    public void Collect_NothingReady_IsRejected()
    {
        // Arrange
        var (state, id) = BaseWithWoodcutter();

        // Act
        var outcome = Send(state, new Collect(Guid.NewGuid(), id), T0.AddSeconds(31));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        state.Version.Should().Be(1);
    }

    [Fact]
    public void Snapshot_UpgradeFinishedWhileAway_ProducesFromItsEndTime()
    {
        // Arrange — the Hearth must be level 2 before a building can reach level 2; start with 900 Timber to afford it.
        var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(900, 60, 380, 0) }, VillageMap.Demo, T0);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, Field), T0).Status.Should().Be(CommandStatus.Accepted);
        var id = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
        var built = T0.AddSeconds(30);
        Send(state, new UpgradeHearth(Guid.NewGuid()), built).Status.Should().Be(CommandStatus.Accepted);
        var hearthDone = built.AddMinutes(10);
        Send(state, new Collect(Guid.NewGuid(), id), hearthDone); // empty the buffer at a known time
        Send(state, new UpgradeBuilding(Guid.NewGuid(), id), hearthDone).Status.Should().Be(CommandStatus.Accepted);
        var upgradeDone = hearthDone.AddMinutes(5);

        // Act — the app was closed for an hour after the upgrade finished.
        var snapshot = state.Snapshot(upgradeDone.AddHours(1));

        // Assert — 0.666 left after collecting 16, + 5 min at 100/h while upgrading (8.333) + 1 h at 170/h = 178.999.
        snapshot.Building(id).Level.Should().Be(2);
        snapshot.Building(id).Ready.Should().Be(8 + 170);
    }

    [Fact]
    public void Place_WhileTheBuilderIsBusy_IsRejected()
    {
        // Arrange
        var (state, _) = BaseWithWoodcutter();

        // Act
        var outcome = Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(4, 9)), T0.AddSeconds(5));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("builder");
    }

    [Fact]
    public void Place_TowerWhileTheBuilderIsBusy_IsInstantAndAllowed()
    {
        // Arrange
        var (state, _) = BaseWithWoodcutter();

        // Act
        var outcome = Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0.AddSeconds(5));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var chute = state.Snapshot(T0.AddSeconds(5)).Buildings.Single(b => b.Type == BuildingType.SlurryChute);
        chute.Level.Should().Be(1);
        state.Snapshot(T0.AddSeconds(5)).Stock.Scrap.Should().Be(330);
    }

    [Fact]
    public void Place_NotEnoughResources_IsRejectedWithWhatIsMissing()
    {
        // Arrange — starting Timber 500; a Storage Hut costs 100, so place five... use a cheap path: spend down first.
        var state = NewBase();
        var at = T0;
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, new GridPosition(3, 9)), at);
        at = at.AddMinutes(1);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.CultistHut, new GridPosition(4, 9)), at);
        at = at.AddMinutes(2);
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.PeatCut, new GridPosition(6, 9)), at);
        at = at.AddMinutes(1); // Timber left: 500 − 100 − 120 − 80 = 200; Scrap 370
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(7, 9)), at);
        at = at.AddSeconds(30); // 150 Timber left
        Send(state, new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(8, 9)), at);
        at = at.AddSeconds(30); // 100 left

        // Act — upgrading the Hearth needs 600 Timber and 50 Scrap.
        var outcome = Send(state, new UpgradeHearth(Guid.NewGuid()), at);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("500 Timber");
    }

    [Fact]
    public void FinishConstructionNow_DuringAnUpgrade_CompletesIt()
    {
        // Arrange
        var (state, id) = BaseWithWoodcutter();

        // Act
        var outcome = Send(state, new FinishConstructionNow(Guid.NewGuid()), T0.AddSeconds(2));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0.AddSeconds(2)).Building(id).Level.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run** `dotnet test --filter BaseStateEconomyTests` → build errors.

- [ ] **Step 3: Implement**

```csharp
// src/CultOfComfort.Simulation/Base/Building.cs
using System;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>A placed building. Level 0 = still being built. Producers carry a lazily evaluated output buffer.</summary>
public sealed class Building
{
    internal Building(int id, BuildingType type, GridPosition position, int level, DateTimeOffset asOfUtc)
    {
        Id = id;
        Type = type;
        Position = position;
        Level = level;
        AsOfUtc = asOfUtc;
    }

    public int Id { get; }
    public BuildingType Type { get; }
    public GridPosition Position { get; }
    public int Level { get; internal set; }
    public DateTimeOffset? UpgradeEndsAtUtc { get; internal set; }
    public bool IsUpgrading => UpgradeEndsAtUtc != null;

    /// <summary>Output waiting to be collected, in thousandths of a unit, as of <see cref="AsOfUtc"/>.</summary>
    internal long BufferMilli { get; set; }

    internal DateTimeOffset AsOfUtc { get; set; }
}
```

```csharp
// src/CultOfComfort.Simulation/Base/BaseCommands.cs
using System;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>An intent sent to whoever holds authority over the village (the device today, the server later).</summary>
public abstract record BaseCommand(Guid CommandId);

public sealed record PlaceBuilding(Guid CommandId, BuildingType Type, GridPosition Position) : BaseCommand(CommandId);

public sealed record UpgradeBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

public sealed record UpgradeHearth(Guid CommandId) : BaseCommand(CommandId);

public sealed record DemolishBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

/// <param name="BuildingId">Null collects from every producer.</param>
public sealed record Collect(Guid CommandId, int? BuildingId) : BaseCommand(CommandId);

public sealed record RecordNightResult(Guid CommandId, int Night, int Stars) : BaseCommand(CommandId);

/// <summary>Development only: completes the running construction. A server refuses (or charges for) this.</summary>
public sealed record FinishConstructionNow(Guid CommandId) : BaseCommand(CommandId);

public enum CommandStatus
{
    Accepted,
    Duplicate,
    Rejected,
}

/// <param name="Collected">Resources gained by a Collect or night salvage.</param>
public sealed record CommandOutcome(CommandStatus Status, long Version, string? Reason = null, ResourceBag Collected = default)
{
    internal static CommandOutcome Accept(ResourceBag collected = default) => new(CommandStatus.Accepted, 0, null, collected);

    internal static CommandOutcome Reject(string reason) => new(CommandStatus.Rejected, 0, reason);
}
```

```csharp
// src/CultOfComfort.Simulation/Base/BaseSnapshot.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <param name="Ready">Whole units waiting to be collected.</param>
/// <param name="Capacity">Most it holds before production pauses.</param>
public sealed record BuildingView(int Id, BuildingType Type, int Level, GridPosition Position, int Ready, int Capacity, int PerHour, DateTimeOffset? UpgradeEndsAtUtc)
{
    public bool IsUpgrading => UpgradeEndsAtUtc != null;
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
    int LastNightRecorded)
{
    public DateTimeOffset? ConstructionEndsAtUtc => Buildings.FirstOrDefault(b => b.IsUpgrading)?.UpgradeEndsAtUtc;

    public BuildingView Building(int id) => Buildings.First(b => b.Id == id);

    public BuildingView? BuildingAt(GridPosition tile) => Buildings.FirstOrDefault(b => b.Position == tile);
}
```

```csharp
// src/CultOfComfort.Simulation/Base/BaseState.cs
using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>
/// The village aggregate (ADR 0003): resources are worked out on read from rates and timestamps, construction timers
/// resolve lazily, and the only way in is <see cref="Apply"/> with a command. Time is always passed in.
/// </summary>
public sealed class BaseState
{
    private const int RememberedCommands = 64;
    private const int HearthId = 0;

    private readonly BuildingTable _table;
    private readonly VillageMap _map;
    private readonly List<Building> _buildings = new();
    private readonly Queue<Guid> _recentOrder = new();
    private readonly HashSet<Guid> _recent = new();
    private ResourceBag _stock;
    private int _nextId = 1;

    private BaseState(BuildingTable table, VillageMap map)
    {
        _table = table;
        _map = map;
    }

    public long Version { get; private set; }

    public int LastNightRecorded { get; private set; }

    public int HearthLevel => Hearth.Level;

    private Building Hearth => _buildings[0];

    private Building? Constructing => _buildings.FirstOrDefault(b => b.IsUpgrading);

    public static BaseState CreateNew(BuildingTable table, VillageMap map, DateTimeOffset nowUtc)
    {
        var state = new BaseState(table, map) { _stock = table.StartingStock };
        state._buildings.Add(new Building(HearthId, BuildingType.Hearth, map.Hearth, 1, nowUtc));
        return state;
    }

    public CommandOutcome Apply(BaseCommand command, DateTimeOffset nowUtc)
    {
        if (_recent.Contains(command.CommandId))
        {
            return new CommandOutcome(CommandStatus.Duplicate, Version);
        }

        Advance(nowUtc);
        var outcome = command switch
        {
            PlaceBuilding place => Place(place, nowUtc),
            UpgradeBuilding upgrade => Upgrade(Find(upgrade.BuildingId), nowUtc),
            UpgradeHearth _ => Upgrade(Hearth, nowUtc),
            DemolishBuilding demolish => Demolish(demolish.BuildingId),
            Collect collect => DoCollect(collect.BuildingId, nowUtc),
            RecordNightResult night => RecordNight(night),
            FinishConstructionNow _ => FinishNow(nowUtc),
            _ => CommandOutcome.Reject("Unknown order."),
        };

        if (outcome.Status == CommandStatus.Accepted)
        {
            Version++;
            Remember(command.CommandId);
        }

        return outcome with { Version = Version };
    }

    public BaseSnapshot Snapshot(DateTimeOffset nowUtc)
    {
        Advance(nowUtc);
        var views = _buildings.Select(b => new BuildingView(
            b.Id, b.Type, b.Level, b.Position, (int)(Buffer(b, nowUtc) / 1000), (int)(CapacityMilli(b) / 1000), Rate(b), b.UpgradeEndsAtUtc)).ToList();
        return new BaseSnapshot(Version, nowUtc, HearthLevel, _stock, Caps, views, SluiceBonus, LastNightRecorded);
    }

    public ResourceBag Caps
    {
        get
        {
            var cap = _table.HearthStorageCaps[HearthLevel - 1];
            foreach (var b in _buildings)
            {
                if (b.Level > 0)
                {
                    cap += _table.Spec(b.Type).Levels[b.Level - 1].StorageBonus;
                }
            }

            return new ResourceBag(cap, cap, cap, cap);
        }
    }

    public int SluiceBonus => _buildings.Where(b => b.Level > 0).Sum(b => _table.Spec(b.Type).Levels[b.Level - 1].SluiceCharges);

    /// <summary>The locked layout for a Night: towers fight, every other building is in the way.</summary>
    public BattleLayout ToBattleLayout() => _map.ToBattleLayout(
        _buildings.Where(b => _table.Spec(b.Type).IsTower).Select(b => new PlacedTower(_table.Spec(b.Type).Tower!.Value, b.Position)),
        _buildings.Where(b => b.Type != BuildingType.Hearth && !_table.Spec(b.Type).IsTower).Select(b => b.Position));

    // ---------------- commands ----------------

    private CommandOutcome Place(PlaceBuilding command, DateTimeOffset now)
    {
        var spec = _table.Spec(command.Type);
        if (command.Type == BuildingType.Hearth)
        {
            return CommandOutcome.Reject("There is only one Hearth.");
        }

        if (HearthLevel < spec.UnlockHearth)
        {
            return CommandOutcome.Reject($"{spec.Name} unlocks at Hearth {spec.UnlockHearth}.");
        }

        if (_buildings.Count(b => b.Type == command.Type) >= spec.MaxCount(HearthLevel))
        {
            return CommandOutcome.Reject($"No room for another {spec.Name} until the Hearth grows.");
        }

        if (!spec.IsTower && Constructing != null)
        {
            return CommandOutcome.Reject("The builder is busy. One thing at a time; he's a cultist, not a crane.");
        }

        var reason = PlacementProblem(command.Position);
        if (reason != null)
        {
            return CommandOutcome.Reject(reason);
        }

        var cost = spec.Levels[0].Cost;
        if (!_stock.Covers(cost))
        {
            return CommandOutcome.Reject($"Need {_stock.Missing(cost)} more.");
        }

        _stock -= cost;
        var building = new Building(_nextId++, command.Type, command.Position, spec.IsTower ? 1 : 0, now);
        if (!spec.IsTower)
        {
            building.UpgradeEndsAtUtc = now.AddSeconds(spec.Levels[0].BuildSeconds);
        }

        _buildings.Add(building);
        Advance(now); // zero-second builds complete at once
        return CommandOutcome.Accept();
    }

    private CommandOutcome Upgrade(Building? building, DateTimeOffset now)
    {
        if (building == null)
        {
            return CommandOutcome.Reject("That building is gone.");
        }

        var spec = _table.Spec(building.Type);
        if (spec.IsTower || building.Level >= spec.MaxLevel)
        {
            return CommandOutcome.Reject($"{spec.Name} is as good as it gets.");
        }

        if (building.IsUpgrading || Constructing != null)
        {
            return CommandOutcome.Reject("The builder is busy. One thing at a time; he's a cultist, not a crane.");
        }

        if (building.Type != BuildingType.Hearth && building.Level + 1 > HearthLevel)
        {
            return CommandOutcome.Reject($"Grow the Hearth to level {building.Level + 1} first.");
        }

        var cost = spec.Levels[building.Level].Cost;
        if (!_stock.Covers(cost))
        {
            return CommandOutcome.Reject($"Need {_stock.Missing(cost)} more.");
        }

        _stock -= cost;
        Materialize(building, now);
        building.UpgradeEndsAtUtc = now.AddSeconds(spec.Levels[building.Level].BuildSeconds);
        return CommandOutcome.Accept();
    }

    private CommandOutcome Demolish(int id)
    {
        var building = Find(id);
        if (building == null || building.Type == BuildingType.Hearth)
        {
            return CommandOutcome.Reject(building == null ? "That building is gone." : "Demolish the Hearth? The cult would riot.");
        }

        if (building.IsUpgrading)
        {
            return CommandOutcome.Reject("Let the builder finish first.");
        }

        var spec = _table.Spec(building.Type);
        var cost = spec.Levels[building.Level - 1].Cost;
        _stock += spec.IsTower ? cost : cost.Half();
        _buildings.Remove(building);
        return CommandOutcome.Accept();
    }

    private CommandOutcome DoCollect(int? id, DateTimeOffset now)
    {
        var targets = id == null ? _buildings.Where(b => _table.Spec(b.Type).Produces != null).ToList() : new List<Building?> { Find(id.Value) }.Where(b => b != null).Select(b => b!).ToList();
        if (targets.Count == 0)
        {
            return CommandOutcome.Reject("That building is gone.");
        }

        var gained = default(ResourceBag);
        var caps = Caps;
        foreach (var building in targets)
        {
            var resource = _table.Spec(building.Type).Produces;
            if (resource == null)
            {
                continue;
            }

            Materialize(building, now);
            var space = Math.Max(0, caps.Get(resource.Value) - (_stock + gained).Get(resource.Value));
            var take = (int)Math.Min(space, building.BufferMilli / 1000);
            building.BufferMilli -= take * 1000L;
            gained += ResourceBag.Of(resource.Value, take);
        }

        if (gained.IsZero)
        {
            return CommandOutcome.Reject(targets.Any(b => Buffer(b, now) >= 1000) ? "Storage is full. Build or upgrade a Storage Hut." : "Nothing to collect yet.");
        }

        _stock += gained;
        return CommandOutcome.Accept(gained);
    }

    private CommandOutcome RecordNight(RecordNightResult night)
    {
        if (night.Night <= LastNightRecorded)
        {
            return CommandOutcome.Reject("That Night is already in the books.");
        }

        var salvage = Math.Min(_table.Salvage(night.Stars), Math.Max(0, Caps.Scrap - _stock.Scrap));
        _stock += new ResourceBag(0, 0, salvage, 0);
        LastNightRecorded = night.Night;
        return CommandOutcome.Accept(new ResourceBag(0, 0, salvage, 0));
    }

    private CommandOutcome FinishNow(DateTimeOffset now)
    {
        var building = Constructing;
        if (building == null)
        {
            return CommandOutcome.Reject("Nothing is being built.");
        }

        building.UpgradeEndsAtUtc = now;
        Advance(now);
        return CommandOutcome.Accept();
    }

    // ---------------- rules ----------------

    private string? PlacementProblem(GridPosition tile)
    {
        if (!_map.Contains(tile) || tile == _map.Spawn || tile == _map.Hearth || _map.Rocks.Contains(tile) || _buildings.Any(b => b.Position == tile))
        {
            return "Can't build there.";
        }

        try
        {
            var layout = ToBattleLayout();
            _map.ToBattleLayout(layout.Towers, layout.Obstacles.Except(_map.Rocks).Append(tile)).Validate();
        }
        catch (InvalidLayoutException)
        {
            return "That would seal the road. The Bishop's lawyers would have a field day.";
        }

        return null;
    }

    /// <summary>Completes a construction whose timer ran out, at its own end time (so production counts from then).</summary>
    private void Advance(DateTimeOffset now)
    {
        var building = Constructing;
        if (building == null || building.UpgradeEndsAtUtc > now)
        {
            return;
        }

        var endedAt = building.UpgradeEndsAtUtc!.Value;
        Materialize(building, endedAt);
        building.Level++;
        building.UpgradeEndsAtUtc = null;
        building.AsOfUtc = endedAt;
    }

    private int Rate(Building b)
    {
        var spec = _table.Spec(b.Type);
        return spec.Produces == null || b.Level == 0 ? 0 : spec.Levels[b.Level - 1].PerHour;
    }

    private long CapacityMilli(Building b) => Rate(b) * (long)_table.BufferHours * 1000;

    private long Buffer(Building b, DateTimeOffset at)
    {
        var seconds = Math.Max(0, (at.UtcTicks - b.AsOfUtc.UtcTicks) / TimeSpan.TicksPerSecond);
        return Math.Min(CapacityMilli(b), b.BufferMilli + Rate(b) * 1000L * seconds / 3600);
    }

    private void Materialize(Building b, DateTimeOffset at)
    {
        if (at <= b.AsOfUtc)
        {
            return; // a clock that went backwards never takes anything away
        }

        b.BufferMilli = Buffer(b, at);
        b.AsOfUtc = at;
    }

    private Building? Find(int id) => _buildings.FirstOrDefault(b => b.Id == id);

    private void Remember(Guid id)
    {
        _recent.Add(id);
        _recentOrder.Enqueue(id);
        if (_recentOrder.Count > RememberedCommands)
        {
            _recent.Remove(_recentOrder.Dequeue());
        }
    }
}
```

Note: `Materialize` keeps whole-second precision; partial seconds are dropped consistently because `AsOfUtc` moves to `at` — acceptable (≤ 1 s of output per event).

- [ ] **Step 4: Run** `dotnet test --filter BaseStateEconomyTests` → all pass; fix arithmetic only by re-deriving expected values from the spec (ledger any ruling). **Step 5: Commit** `"Village domain: BaseState with lazy production, construction timers, collect"`.

---

### Task 3: Placement, limits, demolish, idempotency, Night link, persistence shape

**Files:**
- Create: `Base/BaseData.cs`; Modify: `Base/BaseState.cs` (`Restore`, `ToData`), `Combat/BattleTypes.cs`, `Combat/Battle.cs`
- Test: `tests/.../Base/BaseStateRulesTests.cs`, `tests/.../Combat/BattleTests.cs` (one test)

**Interfaces:**
- Produces: `BaseData(long Version, ResourceBag Stock, int NextId, int LastNightRecorded, IReadOnlyList<BuildingData> Buildings, IReadOnlyList<Guid> RecentCommands)`, `BuildingData(int Id, BuildingType Type, int Level, int X, int Y, long? UpgradeEndsAtUtcTicks, long BufferMilli, long AsOfUtcTicks)`, `InvalidBaseDataException`, `BaseState.Restore(BuildingTable, VillageMap, BaseData)`, `BaseState.ToData()`; `BattleSetup.ExtraSluiceCharges` (int, default 0).

- [ ] **Step 1: Failing tests**

```csharp
// tests/CultOfComfort.Simulation.Tests/Base/BaseStateRulesTests.cs
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class BaseStateRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static BaseState NewBase() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

    [Theory]
    [InlineData(0, 1)]   // spawn
    [InlineData(17, 9)]  // Hearth
    [InlineData(5, 3)]   // rock
    [InlineData(-1, 0)]  // outside
    [InlineData(18, 0)]  // outside
    public void Apply_PlaceOnSpawnOrHearth_Rejected(int x, int y)
    {
        // Act
        var outcome = NewBase().Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(x, y)), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Be("Can't build there.");
    }

    [Fact]
    public void Apply_PlaceThatSealsTheRoad_Rejected()
    {
        // Arrange — the rock wall at x = 5 leaves y = 8..10 open; fill (5,8) and (5,9), then try (5,10).
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(5, 8)), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(5, 9)), T0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(5, 10)), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("seal the road");
    }

    [Fact]
    public void Apply_SameCommandIdTwice_AppliedOnce()
    {
        // Arrange
        var state = NewBase();
        var command = new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6));

        // Act
        var first = state.Apply(command, T0);
        var second = state.Apply(command, T0);

        // Assert
        first.Status.Should().Be(CommandStatus.Accepted);
        second.Status.Should().Be(CommandStatus.Duplicate);
        second.Version.Should().Be(1);
        state.Snapshot(T0).Stock.Scrap.Should().Be(330, "charged once");
    }

    [Fact]
    public void Apply_RejectedCommand_DoesNotBumpTheVersion()
    {
        // Arrange
        var state = NewBase();

        // Act
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SaplingFlinger, new GridPosition(4, 6)), T0);

        // Assert
        state.Version.Should().Be(0);
    }

    [Fact]
    public void Apply_FlingerAtHearthOne_RejectedAsLocked()
    {
        // Act
        var outcome = NewBase().Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SaplingFlinger, new GridPosition(4, 6)), T0);

        // Assert
        outcome.Reason.Should().Be("Sapling Flinger unlocks at Hearth 2.");
    }

    [Fact]
    public void Apply_ThirdWoodcutterAtHearthOne_RejectedByTheLimit()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(4, 9)), T0.AddSeconds(30));

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(6, 9)), T0.AddSeconds(60));

        // Assert
        outcome.Reason.Should().Contain("until the Hearth grows");
    }

    [Fact]
    public void Apply_HearthUpgradeFinished_RaisesCapsAndUnlocks()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Reason.Should().Contain("Need"); // 500 Timber is not enough
        var rich = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData() with { Stock = new ResourceBag(900, 60, 380, 0) });

        // Act
        rich.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        var snapshot = rich.Snapshot(T0.AddMinutes(10));

        // Assert
        snapshot.HearthLevel.Should().Be(2);
        snapshot.Caps.Timber.Should().Be(2500);
        rich.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SaplingFlinger, new GridPosition(4, 6)), T0.AddMinutes(10)).Status.Should().Be(CommandStatus.Accepted);
    }

    [Fact]
    public void Apply_DemolishBuildingAndTower_RefundsHalfAndFull()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.StorageHut, new GridPosition(3, 9)), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0);
        var at = T0.AddMinutes(1);
        var snapshot = state.Snapshot(at);
        var hut = snapshot.Buildings.Single(b => b.Type == BuildingType.StorageHut).Id;
        var chute = snapshot.Buildings.Single(b => b.Type == BuildingType.SlurryChute).Id;

        // Act
        state.Apply(new DemolishBuilding(Guid.NewGuid(), hut), at).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new DemolishBuilding(Guid.NewGuid(), chute), at).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(at).Stock.Should().Be(new ResourceBag(500 - 100 + 50, 60, 380, 0));
    }

    [Fact]
    public void Apply_DemolishTheHearth_Rejected()
    {
        NewBase().Apply(new DemolishBuilding(Guid.NewGuid(), 0), T0).Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Apply_RecordNightResult_AddsSalvageOnceAndIsCappedByStorage()
    {
        // Arrange
        var state = NewBase();

        // Act
        var first = state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 3), T0);
        var again = state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 3), T0);

        // Assert
        first.Collected.Scrap.Should().Be(160);
        again.Status.Should().Be(CommandStatus.Rejected);
        state.Snapshot(T0).Stock.Scrap.Should().Be(540);
        state.LastNightRecorded.Should().Be(1);
    }

    [Fact]
    public void ToBattleLayout_TowersAndBuildings_TowersFightAndBuildingsBlock()
    {
        // Arrange
        var state = NewBase();
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);

        // Act
        var layout = state.ToBattleLayout();

        // Assert
        layout.Towers.Should().ContainSingle(t => t.Position == new GridPosition(4, 6));
        layout.Obstacles.Should().Contain(new GridPosition(3, 9));
        layout.Obstacles.Should().NotContain(VillageMap.Demo.Hearth);
        layout.ResolveEnemyPath().Should().NotContain(new GridPosition(3, 9));
    }

    [Fact]
    public void Restore_RoundTrip_KeepsEverything()
    {
        // Arrange
        var state = NewBase();
        var place = new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9));
        state.Apply(place, T0);
        var at = T0.AddHours(2);

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData());

        // Assert
        restored.Snapshot(at).Should().BeEquivalentTo(state.Snapshot(at));
        restored.Apply(place, at).Status.Should().Be(CommandStatus.Duplicate, "remembered command ids survive a save");
    }

    [Fact]
    public void Restore_DataWithoutAHearth_Throws()
    {
        // Arrange
        var data = NewBase().ToData();
        var broken = data with { Buildings = data.Buildings.Where(b => b.Type != BuildingType.Hearth).ToList() };

        // Act
        var act = () => BaseState.Restore(BuildingTable.Default, VillageMap.Demo, broken);

        // Assert
        act.Should().Throw<InvalidBaseDataException>();
    }
}
```

Add to `BattleTests`:

```csharp
    [Fact]
    public void Constructor_ExtraSluiceCharges_AddsToTheBalanceCharges()
    {
        // Arrange
        var setup = Setup(TestLayouts.Corridor()) with { ExtraSluiceCharges = 2 };

        // Act
        var battle = new Battle(setup);

        // Assert
        battle.SluiceChargesLeft.Should().Be(BalanceTable.Default.SluiceGate.Charges + 2);
    }
```

- [ ] **Step 2: Run** `dotnet test --filter "BaseStateRulesTests|Constructor_ExtraSluiceCharges"` → build errors.

- [ ] **Step 3: Implement**

```csharp
// src/CultOfComfort.Simulation/Base/BaseData.cs
using System;
using System.Collections.Generic;

namespace CultOfComfort.Simulation.Base;

/// <summary>The village as stored (device save today, database row later). Mapped to and from <see cref="BaseState"/>.</summary>
public sealed record BaseData(long Version, ResourceBag Stock, int NextId, int LastNightRecorded, IReadOnlyList<BuildingData> Buildings, IReadOnlyList<Guid> RecentCommands);

public sealed record BuildingData(int Id, BuildingType Type, int Level, int X, int Y, long? UpgradeEndsAtUtcTicks, long BufferMilli, long AsOfUtcTicks);

public sealed class InvalidBaseDataException : Exception
{
    public InvalidBaseDataException(string message) : base(message)
    {
    }
}
```

Add to `BaseState`:

```csharp
    public static BaseState Restore(BuildingTable table, VillageMap map, BaseData data)
    {
        var hearths = data.Buildings.Count(b => b.Type == BuildingType.Hearth);
        if (hearths != 1)
        {
            throw new InvalidBaseDataException($"A village needs exactly one Hearth (found {hearths}).");
        }

        var state = new BaseState(table, map)
        {
            Version = data.Version,
            _stock = data.Stock,
            _nextId = data.NextId,
            LastNightRecorded = data.LastNightRecorded,
        };
        foreach (var b in data.Buildings.OrderBy(b => b.Type == BuildingType.Hearth ? -1 : b.Id))
        {
            if (!table.Specs.TryGetValue(b.Type, out var spec) || b.Level < 0 || b.Level > spec.MaxLevel)
            {
                throw new InvalidBaseDataException($"Building {b.Id} has an impossible type or level.");
            }

            state._buildings.Add(new Building(b.Id, b.Type, new GridPosition(b.X, b.Y), b.Level, new DateTimeOffset(b.AsOfUtcTicks, TimeSpan.Zero))
            {
                BufferMilli = b.BufferMilli,
                UpgradeEndsAtUtc = b.UpgradeEndsAtUtcTicks == null ? null : new DateTimeOffset(b.UpgradeEndsAtUtcTicks.Value, TimeSpan.Zero),
            });
        }

        foreach (var id in data.RecentCommands)
        {
            state.Remember(id);
        }

        return state;
    }

    public BaseData ToData() => new(
        Version, _stock, _nextId, LastNightRecorded,
        _buildings.Select(b => new BuildingData(b.Id, b.Type, b.Level, b.Position.X, b.Position.Y, b.UpgradeEndsAtUtc?.UtcTicks, b.BufferMilli, b.AsOfUtc.UtcTicks)).ToList(),
        _recentOrder.ToList());
```

(`Enum.IsDefined` is covered by the `Specs.TryGetValue` check: an unknown numeric type is not a key.)

`BattleTypes.cs` — `BattleSetup`: append `int ExtraSluiceCharges = 0` after `BalanceTable? Balance = null`. `Battle.cs` constructor: `SluiceChargesLeft = _balance.SluiceGate.Charges + setup.ExtraSluiceCharges;` (check the field name used for the setup there).

- [ ] **Step 4: Run** `dotnet test` → all pass (69 + new); `dotnet run --project tools/CultOfComfort.Sandbox | grep "State hash"` → still `1ddcdf5947cfcaba`. **Step 5: Commit** `"Village domain: placement rules, limits, demolish, idempotency, night salvage, save shape; ExtraSluiceCharges"`.

---

### Task 4: Client gateway, clock and save file

**Files:**
- Create: `client/.../Scripts/Village/IBaseGateway.cs`, `LocalBaseGateway.cs`, `FileBaseStore.cs`, `VillageComposition.cs`
- Test: `client/.../Tests/EditMode/LocalBaseGatewayTests.cs`, `FileBaseStoreTests.cs`

**Interfaces:**
- Produces:
  - `public interface IBaseGateway { Task<BaseSnapshot> LoadAsync(CancellationToken ct); Task<CommandResult> SendAsync(BaseCommand command, CancellationToken ct); BattleLayout CurrentLayout { get; } int SluiceBonus { get; } }` — `CurrentLayout` reflects the last snapshot (a remote gateway builds it from the snapshot with `VillageMap.ToBattleLayout`).
  - `public sealed record CommandResult(CommandOutcome Outcome, BaseSnapshot Snapshot);`
  - `public interface IClock { DateTimeOffset UtcNow { get; } }`, `public sealed class SystemClock : IClock`.
  - `public interface IBaseStore { Task<BaseData> LoadAsync(CancellationToken ct); Task SaveAsync(BaseData data, CancellationToken ct); }` (`LoadAsync` returns null when there is no save).
  - `LocalBaseGateway(IBaseStore store, IClock clock, BuildingTable table, VillageMap map, Action<string> warn)`.
  - `FileBaseStore(string path)`; `VillageSaveMapper.ToJson(BaseData) : string`, `FromJson(string) : BaseData` (throws `InvalidBaseDataException` on bad JSON).
  - `VillageComposition.CreateGateway() : IBaseGateway` (file store at `Application.persistentDataPath/village.json`, `SystemClock`); `VillageComposition.Override : Func<IBaseGateway>` (tests).

- [ ] **Step 1: Failing tests**

```csharp
// client/.../Tests/EditMode/LocalBaseGatewayTests.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class LocalBaseGatewayTests
    {
        internal sealed class FakeClock : IClock
        {
            public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        }

        internal sealed class MemoryStore : IBaseStore
        {
            public BaseData Saved;
            public int Saves;
            public bool ThrowOnLoad;
            public Task<BaseData> LoadAsync(CancellationToken ct) => ThrowOnLoad ? throw new InvalidBaseDataException("corrupt") : Task.FromResult(Saved);
            public Task SaveAsync(BaseData data, CancellationToken ct) { Saved = data; Saves++; return Task.CompletedTask; }
        }

        private static LocalBaseGateway Gateway(MemoryStore store, FakeClock clock, Action<string> warn = null) =>
            new LocalBaseGateway(store, clock, BuildingTable.Default, VillageMap.Demo, warn ?? (_ => { }));

        [Test]
        public async Task LoadAsync_NoSave_StartsAFreshVillage()
        {
            // Act
            var snapshot = await Gateway(new MemoryStore(), new FakeClock()).LoadAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(1, snapshot.HearthLevel);
            Assert.AreEqual(500, snapshot.Stock.Timber);
        }

        [Test]
        public async Task SendAsync_Accepted_SavesAndReturnsTheNewSnapshot()
        {
            // Arrange
            var store = new MemoryStore();
            var gateway = Gateway(store, new FakeClock());

            // Act
            var result = await gateway.SendAsync(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(4, 6)), CancellationToken.None);

            // Assert
            Assert.AreEqual(CommandStatus.Accepted, result.Outcome.Status);
            Assert.AreEqual(330, result.Snapshot.Stock.Scrap);
            Assert.AreEqual(1, store.Saves);
            Assert.AreEqual(1, gateway.CurrentLayout.Towers.Count);
        }

        [Test]
        public async Task SendAsync_Rejected_DoesNotSave()
        {
            // Arrange
            var store = new MemoryStore();

            // Act
            await Gateway(store, new FakeClock()).SendAsync(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(0, 1)), CancellationToken.None);

            // Assert
            Assert.AreEqual(0, store.Saves);
        }

        [Test]
        public async Task LoadAsync_SavedVillage_ContinuesFromTheSave()
        {
            // Arrange
            var store = new MemoryStore();
            var clock = new FakeClock();
            await Gateway(store, clock).SendAsync(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), CancellationToken.None);
            clock.UtcNow = clock.UtcNow.AddHours(1).AddSeconds(30);

            // Act — a new gateway (next app launch) reads the save.
            var snapshot = await Gateway(store, clock).LoadAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(100, snapshot.BuildingAt(new GridPosition(3, 9)).Ready);
        }

        [Test]
        public async Task LoadAsync_CorruptSave_StartsFreshAndWarns()
        {
            // Arrange
            var warnings = 0;

            // Act
            var snapshot = await Gateway(new MemoryStore { ThrowOnLoad = true }, new FakeClock(), _ => warnings++).LoadAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(1, snapshot.HearthLevel);
            Assert.AreEqual(1, warnings);
        }
    }
}
```

```csharp
// client/.../Tests/EditMode/FileBaseStoreTests.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class FileBaseStoreTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void Mapper_RoundTrip_KeepsTheVillage()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
            var data = state.ToData();

            // Act
            var back = VillageSaveMapper.FromJson(VillageSaveMapper.ToJson(data));

            // Assert
            var at = T0.AddHours(3);
            var original = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data).Snapshot(at);
            var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, back).Snapshot(at);
            Assert.AreEqual(original.Stock, restored.Stock);
            Assert.AreEqual(original.Buildings.Count, restored.Buildings.Count);
            Assert.AreEqual(original.Version, restored.Version);
            Assert.AreEqual(original.BuildingAt(new GridPosition(3, 9)).Ready, restored.BuildingAt(new GridPosition(3, 9)).Ready);
        }

        [Test]
        public void Mapper_UnknownBuildingType_Throws()
        {
            // Arrange
            var json = VillageSaveMapper.ToJson(BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData()).Replace("\"Hearth\"", "\"Spaceship\"");

            // Act & Assert
            Assert.Throws<InvalidBaseDataException>(() => VillageSaveMapper.FromJson(json));
        }

        [Test]
        public async Task FileStore_SaveThenLoad_ReadsWhatWasWritten()
        {
            // Arrange
            var path = Path.Combine(Path.GetTempPath(), $"village-{Guid.NewGuid()}.json");
            var store = new FileBaseStore(path);
            var data = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData();

            // Act
            await store.SaveAsync(data, CancellationToken.None);
            var back = await store.LoadAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(data.Stock, back.Stock);
            Assert.IsNull(await new FileBaseStore(path + ".missing").LoadAsync(CancellationToken.None));
        }
    }
}
```

- [ ] **Step 2: Run** EditMode `-testFilter "LocalBaseGatewayTests|FileBaseStoreTests"` → compile errors.

- [ ] **Step 3: Implement**

```csharp
// client/.../Scripts/Village/IBaseGateway.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The one door to the village (inbound port). <see cref="LocalBaseGateway"/> runs the rules on the device; a
    /// remote gateway will post the same commands to /api/base/sync. Nothing above this interface changes then.
    /// </summary>
    public interface IBaseGateway
    {
        Task<BaseSnapshot> LoadAsync(CancellationToken ct);

        Task<CommandResult> SendAsync(BaseCommand command, CancellationToken ct);

        /// <summary>The Night layout for the village as last loaded or changed.</summary>
        BattleLayout CurrentLayout { get; }

        /// <summary>Extra Open Sluice charges from Cisterns.</summary>
        int SluiceBonus { get; }
    }

    public sealed class CommandResult
    {
        public CommandResult(CommandOutcome outcome, BaseSnapshot snapshot)
        {
            Outcome = outcome;
            Snapshot = snapshot;
        }

        public CommandOutcome Outcome { get; }
        public BaseSnapshot Snapshot { get; }
    }

    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    /// <summary>Outbound port for the device save. <see cref="LoadAsync"/> returns null when there is none.</summary>
    public interface IBaseStore
    {
        Task<BaseData> LoadAsync(CancellationToken ct);

        Task SaveAsync(BaseData data, CancellationToken ct);
    }
}
```

(`CommandResult` is a class, not a `record`: the Unity client assembly is C# 9 but has no `IsExternalInit` polyfill — keep client types as classes.)

```csharp
// client/.../Scripts/Village/LocalBaseGateway.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Client
{
    /// <summary>Holds authority on the device: runs <see cref="BaseState"/> and saves after every accepted command.</summary>
    public sealed class LocalBaseGateway : IBaseGateway
    {
        private readonly IBaseStore _store;
        private readonly IClock _clock;
        private readonly BuildingTable _table;
        private readonly VillageMap _map;
        private readonly Action<string> _warn;
        private BaseState _state;

        public LocalBaseGateway(IBaseStore store, IClock clock, BuildingTable table, VillageMap map, Action<string> warn)
        {
            _store = store;
            _clock = clock;
            _table = table;
            _map = map;
            _warn = warn;
        }

        public BattleLayout CurrentLayout => _state?.ToBattleLayout() ?? _map.ToBattleLayout(Array.Empty<PlacedTower>(), Array.Empty<GridPosition>());

        public int SluiceBonus => _state?.SluiceBonus ?? 0;

        public async Task<BaseSnapshot> LoadAsync(CancellationToken ct)
        {
            await EnsureLoadedAsync(ct);
            return _state.Snapshot(_clock.UtcNow);
        }

        public async Task<CommandResult> SendAsync(BaseCommand command, CancellationToken ct)
        {
            await EnsureLoadedAsync(ct);
            var now = _clock.UtcNow;
            var outcome = _state.Apply(command, now);
            if (outcome.Status == CommandStatus.Accepted)
            {
                await _store.SaveAsync(_state.ToData(), ct);
            }

            return new CommandResult(outcome, _state.Snapshot(now));
        }

        private async Task EnsureLoadedAsync(CancellationToken ct)
        {
            if (_state != null)
            {
                return;
            }

            try
            {
                var data = await _store.LoadAsync(ct);
                _state = data == null ? BaseState.CreateNew(_table, _map, _clock.UtcNow) : BaseState.Restore(_table, _map, data);
            }
            catch (InvalidBaseDataException e)
            {
                _warn($"Village save unreadable ({e.Message}); starting a fresh village.");
                _state = BaseState.CreateNew(_table, _map, _clock.UtcNow);
            }
        }
    }
}
```

```csharp
// client/.../Scripts/Village/FileBaseStore.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>The village save on the device: JSON via plain DTOs (the save format never leaks into the domain).</summary>
    public sealed class FileBaseStore : IBaseStore
    {
        private readonly string _path;

        public FileBaseStore(string path) => _path = path;

        public async Task<BaseData> LoadAsync(CancellationToken ct)
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            using (var reader = new StreamReader(_path))
            {
                var json = await reader.ReadToEndAsync();
                ct.ThrowIfCancellationRequested();
                return VillageSaveMapper.FromJson(json);
            }
        }

        public async Task SaveAsync(BaseData data, CancellationToken ct)
        {
            var temp = _path + ".tmp";
            using (var writer = new StreamWriter(temp, false))
            {
                await writer.WriteAsync(VillageSaveMapper.ToJson(data));
            }

            ct.ThrowIfCancellationRequested();
            if (File.Exists(_path))
            {
                File.Delete(_path); // File.Replace is unreliable on Mono/Android; the .tmp is complete before this
            }

            File.Move(temp, _path);
        }
    }

    /// <summary>Anti-corruption mapping between <see cref="BaseData"/> and the JSON save.</summary>
    public static class VillageSaveMapper
    {
        public static string ToJson(BaseData data) => JsonUtility.ToJson(new VillageSaveDto
        {
            version = data.Version,
            timber = data.Stock.Timber,
            peat = data.Stock.Peat,
            scrap = data.Stock.Scrap,
            devotion = data.Stock.Devotion,
            nextId = data.NextId,
            lastNight = data.LastNightRecorded,
            buildings = data.Buildings.Select(b => new BuildingDto
            {
                id = b.Id, type = b.Type.ToString(), level = b.Level, x = b.X, y = b.Y,
                upgradeEndsAtTicks = b.UpgradeEndsAtUtcTicks ?? -1, bufferMilli = b.BufferMilli, asOfTicks = b.AsOfUtcTicks,
            }).ToList(),
            recentCommands = data.RecentCommands.Select(g => g.ToString()).ToList(),
        });

        public static BaseData FromJson(string json)
        {
            VillageSaveDto dto;
            try
            {
                dto = JsonUtility.FromJson<VillageSaveDto>(json);
            }
            catch (ArgumentException e)
            {
                throw new InvalidBaseDataException("not JSON: " + e.Message);
            }

            if (dto == null || dto.buildings == null)
            {
                throw new InvalidBaseDataException("empty save");
            }

            var buildings = new List<BuildingData>();
            foreach (var b in dto.buildings)
            {
                if (!Enum.TryParse<BuildingType>(b.type, out var type))
                {
                    throw new InvalidBaseDataException($"unknown building type '{b.type}'");
                }

                buildings.Add(new BuildingData(b.id, type, b.level, b.x, b.y, b.upgradeEndsAtTicks < 0 ? (long?)null : b.upgradeEndsAtTicks, b.bufferMilli, b.asOfTicks));
            }

            var recent = (dto.recentCommands ?? new List<string>()).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
            return new BaseData(dto.version, new ResourceBag(dto.timber, dto.peat, dto.scrap, dto.devotion), dto.nextId, dto.lastNight, buildings, recent);
        }

        [Serializable]
        private sealed class VillageSaveDto
        {
            public int format = 1;
            public long version;
            public int timber;
            public int peat;
            public int scrap;
            public int devotion;
            public int nextId;
            public int lastNight;
            public List<BuildingDto> buildings;
            public List<string> recentCommands;
        }

        [Serializable]
        private sealed class BuildingDto
        {
            public int id;
            public string type;
            public int level;
            public int x;
            public int y;
            public long upgradeEndsAtTicks = -1;
            public long bufferMilli;
            public long asOfTicks;
        }
    }
}
```

```csharp
// client/.../Scripts/Village/VillageComposition.cs
using System;
using System.IO;
using CultOfComfort.Simulation.Base;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>Composition root for the village: picks the gateway. Tests set <see cref="Override"/>.</summary>
    public static class VillageComposition
    {
        public static Func<IBaseGateway> Override { get; set; }

        public static IBaseGateway CreateGateway() =>
            Override?.Invoke() ?? new LocalBaseGateway(
                new FileBaseStore(Path.Combine(Application.persistentDataPath, "village.json")),
                new SystemClock(), BuildingTable.Default, VillageMap.Demo, message => Debug.LogWarning("[CoC] " + message));
    }
}
```

- [ ] **Step 4: Run** EditMode `-testFilter "LocalBaseGatewayTests|FileBaseStoreTests"` → 8/8. **Step 5: Commit** `"Village client ports: IBaseGateway, local gateway, JSON save, composition root"`.

---

### Task 5: Building icons

**Files:** Create `art/blender/icons/b_hearth.py`, `b_woodcutter.py`, `b_peat_cut.py`, `b_storage_hut.py`, `b_cultist_hut.py`, `b_cistern.py`, `b_slurry_chute.py`, `b_sapling_flinger.py`; modify `art/blender/tests/test_icons.py` (`EXPECTED` += the eight names), `client/.../Scripts/Hud/UiAssets.cs` (`Icons` += the eight).

- [ ] **Step 1:** Add the names to both lists; `./art/build.sh --test` → `test_icon_modules_match_the_hud_list` FAIL.
- [ ] **Step 2:** One-line modules re-exporting each model (`b_hearth` uses `hearth_l2`):

```python
# icons/b_woodcutter.py
"""Build card: Woodcutter's Lean-to."""
from models.woodcutter import TRIANGLE_BUDGET, build  # noqa: F401
```

(same pattern: `b_peat_cut` ← `models.peat_cut`, `b_storage_hut` ← `models.storage_hut`, `b_cultist_hut` ← `models.cultist_hut`, `b_cistern` ← `models.cistern`, `b_slurry_chute` ← `models.slurry_chute`, `b_sapling_flinger` ← `models.sapling_flinger`, `b_hearth` ← `models.hearth_l2`.)

- [ ] **Step 3:** `./art/build.sh --test` → OK; `./art/build.sh --icons b_hearth b_woodcutter b_peat_cut b_storage_hut b_cultist_hut b_cistern b_slurry_chute b_sapling_flinger`; view the PNGs; EditMode `UiAssetsTests` → pass.
- [ ] **Step 4: Commit** `"Icons: building cards rendered from the village models"`.

---

### Task 6: Village presenter (snapshot → cards and frame)

**Files:** Create `client/.../Scripts/Village/VillagePresenter.cs`; Test `client/.../Tests/EditMode/VillagePresenterTests.cs`.

**Interfaces:**
- Produces: `BuildCardModel { Type, Name, Icon, Cost, Time, Locked, LockText, Affordable }`, `BuildingCardModel { BuildingId, Type, Title, Output, Ready, UpgradeLabel, CanUpgrade, CanDemolish, IsUpgrading, Remaining, IsHearth }`, `VillagePresenter.BuildCards(BaseSnapshot, BuildingTable) : IReadOnlyList<BuildCardModel>` (every type except Hearth, in table order), `VillagePresenter.Card(BaseSnapshot, BuildingTable, int buildingId) : BuildingCardModel`, `VillagePresenter.Frame(BaseSnapshot) : FrameState`, `VillagePresenter.Duration(long seconds) : string` ("30s", "5m", "1h 05m"), `VillagePresenter.IconFor(BuildingType) : string` ("b_woodcutter" …).

- [ ] **Step 1: Failing tests**

```csharp
// client/.../Tests/EditMode/VillagePresenterTests.cs
using System;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class VillagePresenterTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

        private static BaseSnapshot Fresh() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).Snapshot(T0);

        [Test]
        public void BuildCards_FreshVillage_FlingerAndCisternLockedAtHearthTwo()
        {
            // Act
            var cards = VillagePresenter.BuildCards(Fresh(), BuildingTable.Default);

            // Assert
            Assert.IsFalse(cards.Any(c => c.Type == BuildingType.Hearth));
            var flinger = cards.Single(c => c.Type == BuildingType.SaplingFlinger);
            Assert.IsTrue(flinger.Locked);
            Assert.AreEqual("Hearth 2", flinger.LockText);
            var woodcutter = cards.Single(c => c.Type == BuildingType.Woodcutter);
            Assert.IsFalse(woodcutter.Locked);
            Assert.AreEqual("50 Timber", woodcutter.Cost);
            Assert.AreEqual("30s", woodcutter.Time);
            Assert.AreEqual("b_woodcutter", woodcutter.Icon);
        }

        [Test]
        public void Card_HearthAtLevelOne_OffersTheLevelTwoUpgrade()
        {
            // Act
            var card = VillagePresenter.Card(Fresh(), BuildingTable.Default, 0);

            // Assert
            Assert.IsTrue(card.IsHearth);
            Assert.AreEqual("Hearth · level 1", card.Title);
            Assert.AreEqual("Upgrade · 600 Timber · 50 Scrap · 10m", card.UpgradeLabel);
            Assert.IsFalse(card.CanDemolish);
        }

        [Test]
        public void Frame_RealResources_AreNotDemo()
        {
            // Act
            var frame = VillagePresenter.Frame(Fresh());

            // Assert
            Assert.AreEqual(500, frame.Timber.Value);
            Assert.AreEqual(1000, frame.Timber.Cap);
            Assert.IsFalse(frame.Timber.IsDemo || frame.Peat.IsDemo || frame.Scrap.IsDemo || frame.Devotion.IsDemo);
            Assert.IsTrue(frame.Pennies.IsDemo && frame.Comfort.IsDemo && frame.TensionIsDemo);
        }

        [Test]
        public void Duration_Values_ReadLikeAClock()
        {
            Assert.AreEqual("30s", VillagePresenter.Duration(30));
            Assert.AreEqual("5m", VillagePresenter.Duration(300));
            Assert.AreEqual("1h 05m", VillagePresenter.Duration(3900));
            Assert.AreEqual("0s", VillagePresenter.Duration(-4));
        }
    }
}
```

- [ ] **Step 2: Run** EditMode `-testFilter VillagePresenterTests` → compile errors.

- [ ] **Step 3: Implement** (`VillagePresenter.cs`)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client
{
    public sealed class BuildCardModel
    {
        public BuildingType Type; public string Name; public string Icon; public string Cost; public string Time;
        public bool Locked; public string LockText; public bool Affordable;
    }

    public sealed class BuildingCardModel
    {
        public int BuildingId; public BuildingType Type; public string Title; public string Output; public string Ready;
        public string UpgradeLabel; public bool CanUpgrade; public bool CanDemolish; public bool IsUpgrading; public string Remaining; public bool IsHearth;
    }

    /// <summary>Turns a village snapshot into what the HUD shows. Pure: no Unity, easy to test.</summary>
    public static class VillagePresenter
    {
        public static string IconFor(BuildingType type) => type switch
        {
            BuildingType.Hearth => "b_hearth",
            BuildingType.Woodcutter => "b_woodcutter",
            BuildingType.PeatCut => "b_peat_cut",
            BuildingType.StorageHut => "b_storage_hut",
            BuildingType.CultistHut => "b_cultist_hut",
            BuildingType.Cistern => "b_cistern",
            BuildingType.SlurryChute => "b_slurry_chute",
            _ => "b_sapling_flinger",
        };

        public static IReadOnlyList<BuildCardModel> BuildCards(BaseSnapshot snapshot, BuildingTable table) =>
            table.Specs.Values.Where(s => s.Type != BuildingType.Hearth).Select(spec =>
            {
                var cost = spec.Levels[0].Cost;
                var count = snapshot.Buildings.Count(b => b.Type == spec.Type);
                var locked = snapshot.HearthLevel < spec.UnlockHearth || count >= spec.MaxCount(snapshot.HearthLevel);
                return new BuildCardModel
                {
                    Type = spec.Type, Name = spec.Name, Icon = IconFor(spec.Type), Cost = cost.ToString(),
                    Time = spec.Levels[0].BuildSeconds == 0 ? "instant" : Duration(spec.Levels[0].BuildSeconds),
                    Locked = locked,
                    LockText = snapshot.HearthLevel < spec.UnlockHearth ? $"Hearth {spec.UnlockHearth}" : locked ? "Max built" : string.Empty,
                    Affordable = snapshot.Stock.Covers(cost),
                };
            }).ToList();

        public static BuildingCardModel Card(BaseSnapshot snapshot, BuildingTable table, int buildingId)
        {
            var b = snapshot.Building(buildingId);
            var spec = table.Spec(b.Type);
            var maxed = b.Level >= spec.MaxLevel || spec.IsTower;
            var next = maxed ? null : spec.Levels[b.Level];
            var upgradeLabel = maxed ? "Max level" : $"Upgrade · {next.Cost} · {Duration(next.BuildSeconds)}";
            return new BuildingCardModel
            {
                BuildingId = b.Id, Type = b.Type, IsHearth = b.Type == BuildingType.Hearth,
                Title = b.Level == 0 ? $"{spec.Name} · being built" : $"{spec.Name} · level {b.Level}",
                Output = spec.Produces != null ? $"+{b.PerHour} {spec.Produces}/h" : Effect(spec, b.Level),
                Ready = spec.Produces != null ? $"{b.Ready}/{b.Capacity} ready" : string.Empty,
                UpgradeLabel = upgradeLabel,
                CanUpgrade = !maxed && !b.IsUpgrading && snapshot.ConstructionEndsAtUtc == null && snapshot.Stock.Covers(next.Cost)
                             && (b.Type == BuildingType.Hearth || b.Level + 1 <= snapshot.HearthLevel),
                CanDemolish = b.Type != BuildingType.Hearth && !b.IsUpgrading,
                IsUpgrading = b.IsUpgrading,
                Remaining = b.IsUpgrading ? Duration((long)(b.UpgradeEndsAtUtc.Value - snapshot.AsOfUtc).TotalSeconds) + " left" : string.Empty,
            };
        }

        public static FrameState Frame(BaseSnapshot s) => new FrameState(
            new Reading(s.Stock.Timber, s.Caps.Timber, false),
            new Reading(s.Stock.Scrap, s.Caps.Scrap, false),
            new Reading(s.Stock.Peat, s.Caps.Peat, false),
            new Reading(s.Stock.Devotion, s.Caps.Devotion, false),
            new Reading(50, 0, true),
            new Reading(42, 0, true),
            s.HearthLevel, 0.62f, "Wulfric's coming in ~3h", true);

        public static string Duration(long seconds)
        {
            seconds = Math.Max(0, seconds);
            if (seconds < 60) return $"{seconds}s";
            if (seconds < 3600) return $"{seconds / 60}m";
            return $"{seconds / 3600}h {seconds % 3600 / 60:00}m";
        }

        private static string Effect(BuildingSpec spec, int level)
        {
            if (level == 0) return string.Empty;
            var l = spec.Levels[level - 1];
            if (l.StorageBonus > 0) return $"+{l.StorageBonus} storage for everything";
            if (l.SluiceCharges > 0) return $"+{l.SluiceCharges} Open Sluice per Night";
            return spec.IsTower ? "Defends the road" : spec.Type == BuildingType.Hearth ? "The heart of the cult" : string.Empty;
        }
    }
}
```

(`DemoEconomy` stays only for the demo parts' text constants; the frame now comes from the presenter. If `FrameState`'s ctor order differs, follow `Hud/FrameState.cs`.)

- [ ] **Step 4: Run** → 4/4. **Step 5: Commit** `"Village presenter: build cards, building card, real frame"`.

---

### Task 7: The village on the board and in the HUD, wired through the gateway

**Files:** Modify `Views/BoardView.cs`, `Hud/HudView.cs`, `Resources/UI/Hud.uss`, `GameController.cs`, `DemoMap.cs`; Test `Tests/PlayMode/VillageTests.cs` (new), adjust `HudFrameTests.cs`/`GameSmokeTests.cs` for the new HudView constructor and the gateway override.

**Interfaces:**
- `HudView(VisualElement root)` (towers dictionary removed); events `BuildCardSelected(BuildingType?)`, `UpgradeClicked`, `DemolishClicked`, `FinishNowClicked`; methods `SetBuildCards(IReadOnlyList<BuildCardModel>, BuildingType? selected)`, `ShowBuildingCard(BuildingCardModel)` (null hides). Element names: `build-drawer`, `card-<BuildingType>`, `building-card`, `building-title`, `upgrade`, `demolish`, `finish-now`.
- `BoardView.SetBuildings(IReadOnlyList<BuildingView> buildings, int hearthLevel)` replaces `SetTowers`; Hearth model `hearth_l{level}`.
- `GameController`: field `IBaseGateway _village` from `VillageComposition.CreateGateway()`; `BaseSnapshot _snapshot` refreshed on load, after each send and every 0.5 s; private `async void SendVillage(BaseCommand)`; private `void HandleDayTap(GridPosition tile)` (testable by reflection).

- [ ] **Step 1: Failing PlayMode tests**

```csharp
// client/.../Tests/PlayMode/VillageTests.cs
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CultOfComfort.Client.Tests
{
    public class VillageTests
    {
        private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero); }

        private sealed class Store : IBaseStore
        {
            public BaseData Saved;
            public System.Threading.Tasks.Task<BaseData> LoadAsync(System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(Saved);
            public System.Threading.Tasks.Task SaveAsync(BaseData data, System.Threading.CancellationToken ct) { Saved = data; return System.Threading.Tasks.Task.CompletedTask; }
        }

        private Clock _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = new Clock();
            var store = new Store();
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
        }

        [TearDown]
        public void TearDown() => VillageComposition.Override = null;

        private static IEnumerator Frames(int n) { for (var i = 0; i < n; i++) yield return null; }

        private static void Click(Button b) { using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); } }

        private static void Tap(GameController game, GridPosition tile) =>
            typeof(GameController).GetMethod("HandleDayTap", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, new object[] { tile });

        private static BaseSnapshot Snapshot(GameController game) =>
            (BaseSnapshot)typeof(GameController).GetField("_snapshot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);

        [UnityTest]
        public IEnumerator Drawer_PickWoodcutterThenTapAField_BuildsItAndChargesTimber()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Act
            Click(root.Q<Button>("card-Woodcutter"));
            yield return null;
            Tap(game, new GridPosition(3, 9));
            yield return Frames(2);

            // Assert
            var snapshot = Snapshot(game);
            Assert.IsNotNull(snapshot.BuildingAt(new GridPosition(3, 9)));
            Assert.AreEqual(450, snapshot.Stock.Timber);
            Assert.AreEqual("450/1k", root.Q("pill-timber").Q<Label>(className: "pill-value").text);
        }

        [UnityTest]
        public IEnumerator Board_TapAProducerWithOutput_CollectsIt()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Click(root.Q<Button>("card-Woodcutter"));
            yield return null;
            Tap(game, new GridPosition(3, 9));
            yield return null;
            _clock.UtcNow = _clock.UtcNow.AddHours(1).AddSeconds(30);

            // Act
            Tap(game, new GridPosition(3, 9));
            yield return Frames(2);

            // Assert
            Assert.AreEqual(550, Snapshot(game).Stock.Timber);
        }

        [UnityTest]
        public IEnumerator HearthCard_UpgradeAndFinishNow_ReachesLevelTwo()
        {
            // Arrange — enough Timber: collect isn't needed, the save is seeded rich.
            var store = new Store
            {
                Saved = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow).ToData() with { Stock = new ResourceBag(900, 60, 380, 0) },
            };
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Act
            Tap(game, VillageMap.Demo.Hearth);
            yield return Frames(2);
            Click(root.Q<Button>("upgrade"));
            yield return Frames(2);
            Click(root.Q<Button>("finish-now"));
            yield return Frames(2);

            // Assert
            Assert.AreEqual(2, Snapshot(game).HearthLevel);
            Assert.AreEqual("2", root.Q<Label>("hearth-badge").text);
        }

        [UnityTest]
        public IEnumerator Night_BuildingOnTheRoad_RaidersWalkAroundIt()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var before = VillageMap.Demo.ToBattleLayout(Array.Empty<PlacedTower>(), Array.Empty<GridPosition>()).ResolveEnemyPath();
            var onRoad = before[before.Count / 2];
            Click(root.Q<Button>("card-StorageHut"));
            yield return null;
            Tap(game, onRoad);
            yield return Frames(2);

            // Act
            game.StartNight();
            yield return null;
            var battle = (CultOfComfort.Simulation.Combat.Battle)typeof(GameController).GetField("_battle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);

            // Assert
            CollectionAssert.DoesNotContain(battle.Path.ToList(), onRoad);
        }
    }
}
```

`HudFrameTests` / `GameSmokeTests`: add the same `[SetUp]`/`[TearDown]` override (a fresh in-memory village per test) and change `StandaloneHud` to `new HudView(root)`. `Orientation_PortraitAndLandscape_RestackTheDuskBar` keeps asserting `dusk-bar` direction.

Run PlayMode → compile errors (`HandleDayTap`, `_snapshot`, `HudView(root)` missing).

- [ ] **Step 2: HudView — build drawer and building card**

Replace the tower picker in the Dusk bar with a horizontal `ScrollView` named `build-drawer` (`mode = ScrollViewMode.Horizontal`, class `drawer`). `SetBuildCards(cards, selected)` rebuilds its buttons only when the card list's types/lock/affordable flags change (keep a signature string), otherwise just toggles `selected`. Each card: `Button` named `card-{Type}`, classes `btn build-card note-…` (cycle yellow/pink/green/blue) + `locked`/`unaffordable`/`selected`; children: `Icon(card.Icon, "card-icon")`, `Text(card.Name, "card-name")`, `Text(card.Cost, "card-cost")`, `Text(card.Time, "card-time")`, `Text(card.LockText, "card-lock")` (+ padlock when locked). Clicking a locked card shows its lock text as a Prophet quip via `Announce`; clicking an unlocked card raises `BuildCardSelected(type)` (or `null` when it was already selected).

The building card: a root overlay `Panel("sheet")` named `building-card` (same overlay pattern as `settings-sheet`), children `building-title`, `building-output`, `building-ready`, `building-remaining`, buttons `upgrade` (text = `UpgradeLabel`, `SetEnabled(CanUpgrade)`), `finish-now` (shown only when `IsUpgrading && Debug.isDebugBuild`), `demolish` (shown when `CanDemolish`), `card-close`. `ShowBuildingCard(model)` fills and shows it, `null` hides it. Events: `UpgradeClicked`, `DemolishClicked`, `FinishNowClicked`.

USS additions:

```css
.drawer { height: 92px; flex-grow: 1; }
.portrait .drawer { margin-bottom: 8px; }
.build-card { width: 96px; height: 86px; margin-right: 8px; padding: 4px; align-items: center; justify-content: flex-start; }
.card-icon { width: 38px; height: 38px; }
.card-name { font-size: 11px; padding: 0; -unity-text-align: middle-center; white-space: normal; }
.card-cost, .card-time { font-size: 9px; padding: 0; color: #5a3d1e; }
.card-lock { display: none; position: absolute; top: 4px; right: 4px; font-size: 9px; color: #a8322a; padding: 0; }
.build-card.locked { opacity: 0.6; }
.build-card.locked > .card-lock { display: flex; }
.build-card.unaffordable .card-cost { color: #a8322a; }
.build-card.selected { scale: 1.06 1.06; border-color: #c9862e; border-width: 3px; }
.icon-b_hearth { background-image: resource("UI/Icons/b_hearth"); }
.icon-b_woodcutter { background-image: resource("UI/Icons/b_woodcutter"); }
.icon-b_peat_cut { background-image: resource("UI/Icons/b_peat_cut"); }
.icon-b_storage_hut { background-image: resource("UI/Icons/b_storage_hut"); }
.icon-b_cultist_hut { background-image: resource("UI/Icons/b_cultist_hut"); }
.icon-b_cistern { background-image: resource("UI/Icons/b_cistern"); }
.icon-b_slurry_chute { background-image: resource("UI/Icons/b_slurry_chute"); }
.icon-b_sapling_flinger { background-image: resource("UI/Icons/b_sapling_flinger"); }
```

- [ ] **Step 3: BoardView — buildings**

`SetBuildings(buildings, hearthLevel)`: keep a `Dictionary<int, (GameObject Go, BuildingType Type, int Level)>` by building id; create/destroy/replace only what changed. Models: `woodcutter`, `peat_cut`, `storage_hut`, `cultist_hut`, `cistern`, `slurry_chute`, `sapling_flinger` (fallback: the old tower primitives for towers, a brown cube for others), `FaceCamera`, scale 0.8 (towers keep their current scale). Level 0 (being built): the model at 0.55 scale. Over an upgrading building, a progress bar cube (like raider health bars) scaled by remaining fraction (needs the building's start: use `ConstructionStartedUtc` = `UpgradeEndsAtUtc − level build seconds`, from `BuildingTable`). Over a producer with `Ready ≥ 1`: a billboard sprite of its resource icon (`timber`/`peat`/`devotion`) bobbing. The Hearth: `BuildHearth()` uses `hearth_l1`; `SetBuildings` swaps it to `hearth_l{hearthLevel}` when the level changes (keep fire/steam lookups working: re-find `Fire`/`Steam` children after the swap).

- [ ] **Step 4: GameController — Day through the gateway**

- Remove `TowerCost`, `_towers`, `_scrap`, `_selectedTower`; add `_village = VillageComposition.CreateGateway()`, `_snapshot`, `_buildSelection` (`BuildingType?`), `_cardBuilding` (`int?`), `_refreshAt`.
- `Start`: `_snapshot = await _village.LoadAsync(_cts.Token)` (an `async void Start` with try/catch logging), then `RefreshVillage()` (board buildings, road, HUD cards, frame).
- `UpdateDusk`: cursor validity for placement = tile empty and not spawn/Hearth/rock (fast local check; the gateway has the final word); tap → `HandleDayTap(tile)`.
- `HandleDayTap(tile)` is `async void` and first refreshes `_snapshot = await _village.LoadAsync(_cts.Token)` (so a tap always sees what is ready now); then: if `_buildSelection != null` → `SendVillage(new PlaceBuilding(Guid.NewGuid(), type, tile))` and keep the selection for repeated towers, clear it for buildings; else if a building is there: if it has `Ready ≥ 1` → `SendVillage(new Collect(Guid.NewGuid(), id))` and float `"+{n} {Resource}"` over it; else open its card (`_cardBuilding = id`).
- `SendVillage(command)`: `var result = await _village.SendAsync(command, _cts.Token); _snapshot = result.Snapshot; if Rejected → Say(reason, prophet: true); RefreshVillage();`.
- HUD events: `BuildCardSelected` → `_buildSelection`; `UpgradeClicked` → `UpgradeHearth` or `UpgradeBuilding(_cardBuilding)`; `DemolishClicked` → `DemolishBuilding` then close the card; `FinishNowClicked` → `FinishConstructionNow`.
- Every 0.5 s in the Day: `_snapshot = await _village.LoadAsync(...)`, refresh the frame/card/board icons (not the road).
- `UpdateHud`: `_hud.UpdateFrame(VillagePresenter.Frame(_snapshot))` (null-safe before the first load).
- `StartNight`: `new BattleSetup(_village.CurrentLayout, seed, RaidBudget, RaidWaves, new List<PlayerInput>()) { ExtraSluiceCharges = _village.SluiceBonus }`; close card and selection.
- `BackToDusk`: `SendVillage(new RecordNightResult(Guid.NewGuid(), _night, _battle.Result.Stars))` before clearing; announce `"+{scrap} Scrap salvaged."` from the outcome.
- `DemoMap`: make `Width/Height/Spawn/Hearth/CreateRocks` delegate to `VillageMap.Demo` (the probe stays identical).
- A `CancellationTokenSource _cts` cancelled in `OnDestroy`.

- [ ] **Step 5: Run** PlayMode (all) → pass; EditMode (all) → pass; `dotnet test` → pass. Run `HudCapture` and look at the Day screen; fix only clipped/overlapping items.

- [ ] **Step 6: Commit** `"The village on one map: build drawer, building cards, collect and upgrade through the gateway; Night uses the village layout and pays salvage"`.

---

### Task 8: Device check and docs

- [ ] **Step 1:** Full suites (dotnet, Blender, kit, EditMode, PlayMode).
- [ ] **Step 2:** Build, install on the Pixel; probe MATCH `1ddcdf5947cfcaba`; play: build a Woodcutter and a Storage Hut, wait for the Woodcutter, collect, upgrade the Hearth with Finish now, place a Sapling Flinger, light the lamps, check a building diverts the road, finish the Night, check salvage. Kill and relaunch the app: the village is still there. 60 fps in the Day and a busy Night.
- [ ] **Step 3:** Screenshots to `art/previews/village/` (drawer, building card, Hearth L2 on the board, Night with buildings).
- [ ] **Step 4:** Docs: README (Day/village bullet, save location, "Finish now" in dev builds), GDD `02-economy.md` note "Slice 1 numbers: see `BuildingTable` (`village-2026.10.1`)", backlog (done line; next: server `RemoteBaseGateway`, raiders damaging buildings, map expansion).
- [ ] **Step 5:** Commit and push `village/slice-1`.
