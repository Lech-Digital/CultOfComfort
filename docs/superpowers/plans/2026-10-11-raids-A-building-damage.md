# Raids Slice — Plan A: Building Damage, Repair, Wattle Fence

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raiders smash buildings and towers they pass; wrecked towers stop firing; wrecked buildings stop working until repaired; a cheap Wattle Fence joins the drawer.

**Architecture:** `BattleLayout` gains optional structures; `Battle` gives raiders a one-swing-per-structure smash state and skips wrecked towers. The village domain maps buildings to structures (health by level), applies wrecks from a Night, and adds `RepairBuilding` and the fence. The client shows health bars/floaters in the Night and soggy buildings + a Repair button in the Day.

**Tech Stack:** netstandard2.1 simulation (xUnit + FluentAssertions); Unity 6.3 client (NUnit).

**Spec:** `docs/superpowers/specs/2026-10-11-raids-and-damage-design.md` (section A)

## Global Constraints

- Layouts without structures behave exactly as before: demo hash `1ddcdf5947cfcaba` unchanged (probe + sandbox).
- Fixed-point only in the sim; new battle state (structure health, raider swing state, hit count) is hashed.
- Combat balance version `2026.10.4`; village table version `village-2026.10.2`.
- `IBaseGateway` contract unchanged except new commands; DTO save format stays 1 (new `wrecked` field defaults to false).
- xUnit + FluentAssertions, AAA; commits as adzius.lech@gmail.com, no `Co-Authored-By`.

## Review Focus

1. A raider standing next to two structures hits them one after the other, never both in the same swing, and never one twice — test in Task 1.
2. A raider chasing the Prophet or frightened does not stop to smash — test in Task 1.
3. A tower wrecked mid-Night fires no more shots after the wreck tick — test in Task 1.
4. Repair when the building isn't wrecked, or when short of resources — rejected, nothing charged — test in Task 2.
5. A wrecked Storage Hut no longer raises caps (stock above the cap is kept, collecting stops) — test in Task 2.

---

### Task 1: Structures and smashing in the battle

**Files:** Modify `src/CultOfComfort.Simulation/Grid/BattleLayout.cs`, `Combat/BattleTypes.cs`, `Combat/Enemy.cs`, `Combat/Battle.cs`, `Balance/BalanceTable.cs`; Create `Combat/Structure.cs`; Test `tests/.../Combat/StructureDamageTests.cs`.

**Interfaces (produces):**
- `public readonly record struct PlacedStructure(int Id, GridPosition Position, int MaxHealth, int Health);`
- `BattleLayout(..., GridPosition? leaderStart = null, IEnumerable<PlacedStructure>? structures = null)`; `BattleLayout.Structures : IReadOnlyList<PlacedStructure>`.
- `public sealed class Structure { int Id; GridPosition Position; int MaxHealth; int Health; bool IsWrecked; }`; `Battle.Structures : IReadOnlyList<Structure>`.
- Events `StructureHit(int Tick, int StructureId, int EnemyId, int Damage, int HealthRemaining)`, `StructureWrecked(int Tick, int StructureId)`.
- `EnemyStats.StructureDamage` (int, last param, default 0); `BalanceTable.StructureSwingTicks` (int, 16); defaults Levy 15, ManAtArms 40, Zealot 10; `Version = "2026.10.4"`.

- [ ] **Step 1: Failing tests**

```csharp
// tests/CultOfComfort.Simulation.Tests/Combat/StructureDamageTests.cs
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Combat;

public class StructureDamageTests
{
    /// <summary>Corridor 10x3, road along y = 1; one Levy (budget 1) walks right from tick 40 at 1 tile/s.</summary>
    private static Battle StartBattle(int budget, GridPosition? leader, params PlacedStructure[] structures) =>
        StartBattle(budget, leader, Array.Empty<PlacedTower>(), structures);

    private static Battle StartBattle(int budget, GridPosition? leader, PlacedTower[] towers, params PlacedStructure[] structures)
    {
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), towers,
            structures.Where(s => towers.All(t => t.Position != s.Position)).Select(s => s.Position), leader ?? new GridPosition(9, 0), structures);
        var balance = new BalanceTable { HearthMaxHealth = 100_000 };
        return new Battle(new BattleSetup(layout, 42, budget, 1, [], balance));
    }

    private static List<BattleEvent> RunToEnd(Battle battle)
    {
        var events = new List<BattleEvent>();
        while (!battle.IsFinished)
        {
            battle.Step();
            events.AddRange(battle.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void Defaults_Balance_HasStructureDamagePerRaider()
    {
        BalanceTable.Default.Version.Should().Be("2026.10.4");
        BalanceTable.Default.StructureSwingTicks.Should().Be(16);
        BalanceTable.Default.Enemies[EnemyType.Levy].StructureDamage.Should().Be(15);
        BalanceTable.Default.Enemies[EnemyType.ManAtArms].StructureDamage.Should().Be(40);
        BalanceTable.Default.Enemies[EnemyType.Zealot].StructureDamage.Should().Be(10);
    }

    [Fact]
    public void Step_LevyPassesAHut_HitsItOnceAndWalksOn()
    {
        // Arrange — a hut beside the road at (4,0).
        var battle = StartBattle(1, null, new PlacedStructure(7, new GridPosition(4, 0), 60, 60));

        // Act
        var events = RunToEnd(battle);

        // Assert
        events.OfType<StructureHit>().Should().ContainSingle().Which.Should().Match<StructureHit>(h => h.StructureId == 7 && h.Damage == 15 && h.HealthRemaining == 45);
        battle.Structures.Single().Health.Should().Be(45);
        events.OfType<HearthDamaged>().Should().NotBeEmpty("it still reaches the Hearth after its swing");
    }

    [Fact]
    public void Step_LevyBetweenTwoHuts_HitsEachOnceOneAfterTheOther()
    {
        // Arrange — huts on both sides of the same road tile.
        var battle = StartBattle(1, null,
            new PlacedStructure(1, new GridPosition(4, 0), 60, 60),
            new PlacedStructure(2, new GridPosition(4, 2), 60, 60));

        // Act
        var hits = RunToEnd(battle).OfType<StructureHit>().ToList();

        // Assert
        hits.Select(h => h.StructureId).Should().BeEquivalentTo(new[] { 1, 2 });
        (hits[1].Tick - hits[0].Tick).Should().BeGreaterThanOrEqualTo(BalanceTable.Default.StructureSwingTicks);
    }

    [Fact]
    public void Step_StructureAtZero_IsWreckedOnce()
    {
        // Arrange — a fence with 10 health.
        var battle = StartBattle(1, null, new PlacedStructure(3, new GridPosition(4, 0), 50, 10));

        // Act
        var events = RunToEnd(battle);

        // Assert
        events.OfType<StructureWrecked>().Should().ContainSingle().Which.StructureId.Should().Be(3);
        battle.Structures.Single().IsWrecked.Should().BeTrue();
        battle.Structures.Single().Health.Should().Be(0);
    }

    [Fact]
    public void Step_WreckedTower_StopsFiring()
    {
        // Arrange — a Slurry Chute at (4,0) with 1 health: the first Levy wrecks it.
        var chute = new PlacedTower(TowerType.SlurryChute, new GridPosition(4, 0));
        var battle = StartBattle(3, null, new[] { chute }, new PlacedStructure(1, new GridPosition(4, 0), 80, 1));

        // Act
        var events = RunToEnd(battle);

        // Assert
        var wreck = events.OfType<StructureWrecked>().Single();
        events.OfType<TowerFired>().Should().OnlyContain(f => f.Tick <= wreck.Tick);
    }

    [Fact]
    public void Step_AlreadyWreckedTowerAtNightStart_NeverFires()
    {
        // Arrange
        var chute = new PlacedTower(TowerType.SlurryChute, new GridPosition(4, 0));
        var battle = StartBattle(3, null, new[] { chute }, new PlacedStructure(1, new GridPosition(4, 0), 80, 0));

        // Act
        var events = RunToEnd(battle);

        // Assert
        events.OfType<TowerFired>().Should().BeEmpty();
        events.OfType<StructureHit>().Should().BeEmpty("nobody kicks a ruin");
    }

    [Fact]
    public void Step_LevyChasingTheProphet_DoesNotStopToSmash()
    {
        // Arrange — the Prophet beside the road at (4,2) lures the Levy; a hut at (3,0) is within a tile of its road path.
        var battle = StartBattle(1, new GridPosition(4, 2), new PlacedStructure(1, new GridPosition(3, 0), 60, 60));
        var smashedWhileHunting = false;

        // Act
        while (!battle.IsFinished && battle.Tick < 600)
        {
            battle.Step();
            smashedWhileHunting |= battle.LastTickEvents.OfType<StructureHit>().Any() && battle.Enemies.Any(e => e.IsFightingLeader || e.IsOffRoad);
        }

        // Assert
        smashedWhileHunting.Should().BeFalse();
    }

    [Fact]
    public void Simulate_LayoutWithoutStructures_UnchangedFromBefore()
    {
        // Arrange — the same corridor run twice, with and without an empty structure list.
        var plain = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], []);
        var empty = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], [], null, Array.Empty<PlacedStructure>());

        // Act
        var a = Battle.Simulate(new BattleSetup(plain, 7, 12, 3, []));
        var b = Battle.Simulate(new BattleSetup(empty, 7, 12, 3, []));

        // Assert
        b.StateHash.Should().Be(a.StateHash);
    }
}
```

- [ ] **Step 2: Run** `dotnet test --filter StructureDamageTests` → build errors.

- [ ] **Step 3: Implement**

`BattleLayout`: add `public readonly record struct PlacedStructure(int Id, GridPosition Position, int MaxHealth, int Health);` next to `PlacedTower`; constructor parameter `IEnumerable<PlacedStructure>? structures = null` after `leaderStart`; `Structures = structures?.ToList() ?? new List<PlacedStructure>();` (structures sit on tiles that are already towers or obstacles; they add no blocking of their own).

`Combat/Structure.cs`:

```csharp
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Combat;

/// <summary>A village building or tower that raiders can smash during the Night.</summary>
public sealed class Structure
{
    internal Structure(int id, GridPosition position, int maxHealth, int health)
    {
        Id = id;
        Position = position;
        MaxHealth = maxHealth;
        Health = health;
    }

    public int Id { get; }
    public GridPosition Position { get; }
    public int MaxHealth { get; }
    public int Health { get; private set; }
    public bool IsWrecked => Health <= 0;

    /// <returns>True when this hit wrecked it.</returns>
    internal bool TakeHit(int damage)
    {
        if (IsWrecked)
        {
            return false;
        }

        Health = System.Math.Max(0, Health - damage);
        return IsWrecked;
    }
}
```

`BattleTypes.cs`: add the two events.

`BalanceTable.cs`: `EnemyStats` gains `int StructureDamage = 0` (last); `public int StructureSwingTicks { get; init; } = 16;`; Levy `StructureDamage: 15`, ManAtArms `40`, Zealot `10`; `Version = "2026.10.4"`.

`Enemy.cs`: 

```csharp
    /// <summary>Ticks left in a swing at a structure (0 = not smashing).</summary>
    public int SmashTicksRemaining { get; internal set; }

    /// <summary>The structure being smashed, or −1.</summary>
    public int SmashTargetId { get; internal set; } = -1;

    internal HashSet<int> StructuresHit { get; } = new();
```

`Battle.cs`:
- Field `private readonly List<Structure> _structures = new();` + `public IReadOnlyList<Structure> Structures => _structures;`; in the constructor after the towers: `foreach (var s in setup.Layout.Structures) _structures.Add(new Structure(s.Id, s.Position, s.MaxHealth, s.Health));`.
- `FireTowers`: at the top of the loop `if (IsWreckedAt(tower.Position)) continue;` with `private bool IsWreckedAt(GridPosition tile) => _structures.Any(s => s.IsWrecked && s.Position == tile);` (a few structures; fine).
- In `MoveEnemies`, after the leader-slot branch and before the "walk back to the road" branch:

```csharp
            if (!enemy.IsOffRoad && TrySmash(enemy, stats))
            {
                continue;
            }
```

and

```csharp
    /// <summary>
    /// A raider on the road stops for one swing at each intact structure within a tile of it (one at a time, each at
    /// most once), then marches on. Returns true while it is busy swinging.
    /// </summary>
    private bool TrySmash(Enemy enemy, EnemyStats stats)
    {
        if (stats.StructureDamage <= 0 || _structures.Count == 0)
        {
            return false;
        }

        if (enemy.SmashTargetId < 0)
        {
            var position = PositionOf(enemy);
            foreach (var structure in _structures)
            {
                if (!structure.IsWrecked && !enemy.StructuresHit.Contains(structure.Id)
                    && position.DistanceSquaredTo(structure.Position.ToVector()) <= Fix.One)
                {
                    enemy.SmashTargetId = structure.Id;
                    enemy.SmashTicksRemaining = _balance.StructureSwingTicks;
                    break;
                }
            }

            if (enemy.SmashTargetId < 0)
            {
                return false;
            }
        }

        if (--enemy.SmashTicksRemaining > 0)
        {
            return true;
        }

        var target = _structures.First(s => s.Id == enemy.SmashTargetId);
        enemy.StructuresHit.Add(target.Id);
        enemy.SmashTargetId = -1;
        if (!target.IsWrecked)
        {
            var wrecked = target.TakeHit(stats.StructureDamage);
            _tickEvents.Add(new StructureHit(Tick, target.Id, enemy.Id, stats.StructureDamage, target.Health));
            if (wrecked)
            {
                _tickEvents.Add(new StructureWrecked(Tick, target.Id));
            }
        }

        return true;
    }
```

- `HashState`: per enemy add `enemy.SmashTicksRemaining`, `enemy.SmashTargetId`, `enemy.StructuresHit.Count`; after the towers loop `foreach (var s in _structures) _hasher.Add(s.Health);`.

- [ ] **Step 4: Run** → StructureDamageTests pass; `dotnet test` all pass; `dotnet run --project tools/CultOfComfort.Sandbox | grep "State hash"` → `1ddcdf5947cfcaba`. **Step 5: Commit** `"Raiders smash buildings and towers they pass; wrecked towers stop firing (balance 2026.10.4)"`.

---

### Task 2: Damage in the village (health, wrecked, repair, fence, Night link)

**Files:** Modify `src/.../Base/BaseTypes.cs` (`BuildingType.WattleFence`), `BuildingTable.cs`, `Building.cs`, `BaseCommands.cs`, `BaseSnapshot.cs`, `BaseData.cs`, `BaseState.cs`, `VillageMap.cs`; Test `tests/.../Base/BaseStateDamageTests.cs`.

**Interfaces (produces):**
- `BuildingLevel.Health` (int, default 0 → 60); `BuildingTable.UnderConstructionHealth = 30`; `BuildingTable.Version = "village-2026.10.2"`; `BuildingTable.RepairCost(BuildingType, int level) : ResourceBag`.
- `Building.Wrecked`, `BuildingView.Wrecked` (new last ctor param `bool Wrecked = false`), `BuildingData.Wrecked` (`bool Wrecked = false`, last).
- Commands `RepairBuilding(Guid CommandId, int BuildingId)`; `RecordNightResult(Guid, int Night, int Stars, IReadOnlyList<int>? WreckedBuildingIds = null)`.
- `VillageMap.ToBattleLayout(snapshot, table)` and `BaseState.ToBattleLayout()` pass structures: every building except the Hearth, id = building id, `MaxHealth` = level health (30 under construction), `Health` = 0 when wrecked else max.

- [ ] **Step 1: Failing tests** (`BaseStateDamageTests`): 
  - `Default_Fence_IsCheapInstantUnlimited` (15 Timber, BuildSeconds 0, MaxCount int.MaxValue, Health 50, not a tower).
  - `Place_Fence_IsInstantAndBlocksTheRoad` (placed level 1 at once; `ToBattleLayout().Obstacles` contains it).
  - `ToBattleLayout_Buildings_AreStructuresWithLevelHealth` (finished Woodcutter → structure 60/60; tower → 80; Hearth not a structure; building under construction → 30).
  - `RecordNightResult_WreckedIds_MarkThoseBuildingsWrecked` (+ snapshot `Wrecked` true; structure health 0 in the next layout).
  - `Wrecked_Woodcutter_ProducesNothing` (an hour later `Ready` unchanged).
  - `Wrecked_StorageHut_LosesItsCapBonusButKeepsStock`.
  - `Repair_WreckedWoodcutter_Costs10PercentAndWorksAgain` (L1 cost 50 T → 5 T; `Wrecked` false; production resumes from the repair time).
  - `Repair_NotWrecked_Rejected` and `Repair_NotEnough_RejectedAndNothingCharged`.
  - `Restore_SaveWithWreckedBuilding_RoundTrips`.

Write them with the existing style (`T0`, `BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0)`, `FinishConstructionNow` to complete builds).

- [ ] **Step 2: Run** → build errors.

- [ ] **Step 3: Implement**
- Health in the table: huts `Health: 60/90/120`, Cistern `80/120`, towers `80`, Hearth 0 (never a structure); `WattleFence` spec: `new(BuildingType.WattleFence, "Wattle Fence", 1, new[] { int.MaxValue, int.MaxValue, int.MaxValue }, new[] { new BuildingLevel(new ResourceBag(15, 0, 0, 0), 0, Health: 50) })` — not a tower, so it is an obstacle; instant builds don't use the builder: in `Place`, treat `BuildSeconds == 0` like towers (no builder needed, level 1 at once).
- `RepairCost(type, level)` = each resource of `Levels[level-1].Cost` × 10 % rounded up; if all zero, 1 Timber.
- `Building.Wrecked` (internal set); `Rate`, `Caps`, `SluiceBonus` ignore wrecked buildings.
- `RepairBuilding`: building exists, `Wrecked`, can afford → deduct, `Materialize(b, now)` (keeps AsOf monotonic; nothing accrued while wrecked because rate was 0), `Wrecked = false`.
- `RecordNight`: after salvage, for each id in `WreckedBuildingIds` that exists and is not the Hearth: `Materialize(b, now)` then `Wrecked = true`.
- Structures in both layout functions (see Interfaces).
- `Restore`/`ToData`/`Validate`: carry `Wrecked`.

- [ ] **Step 4: Run** → pass; full `dotnet test`. **Step 5: Commit** `"Village: buildings have health, wrecks from the Night, repair, Wattle Fence"`.

---

### Task 3: Client — Night damage, soggy buildings, repair, fence card

**Files:** `client/.../Scripts/Village/VillagePresenter.cs` (+ `FileBaseStore.cs` DTO `wrecked`), `Views/BattleView.cs`, `Views/BoardView.cs`, `Hud/HudView.cs`, `GameController.cs`, `Resources/UI/Hud.uss`; icon `art/blender/icons/b_wattle_fence.py` (+ lists in `test_icons.py` and `UiAssets.cs`); Tests `Tests/EditMode/VillagePresenterTests.cs`, `FileBaseStoreTests.cs`, `Tests/PlayMode/VillageTests.cs`.

**Interfaces:** `BuildingCardModel.RepairLabel`, `CanRepair`, `IsWrecked`; `HudView.RepairClicked` + button `repair`; `BattleView.StructureDamaged : event Action<int /*id*/, float /*fraction*/, bool /*wrecked*/>`; `BoardView.ShowStructureHealth(int id, float fraction)`, `BoardView.SetBuildings` tints wrecked buildings; `VillagePresenter.IconFor(WattleFence) = "b_wattle_fence"`.

- [ ] **Step 1: Failing tests**
  - EditMode `Card_WreckedWoodcutter_OffersRepair` ("Repair · 5 Timber", `CanRepair` true, `UpgradeLabel` "Wrecked — repair first", `CanUpgrade` false).
  - EditMode `Mapper_WreckedFlag_RoundTrips`.
  - EditMode `BuildCards_Fence_IsAvailableAtHearthOne` (instant, "15 Timber", icon `b_wattle_fence`).
  - PlayMode `Card_RepairAWreckedBuilding_FixesIt` (seed a save with a wrecked finished Woodcutter; tap it → card; click `repair` → snapshot not wrecked, Timber −5).
- [ ] **Step 2: Run** → compile errors.
- [ ] **Step 3: Implement**
  - Presenter: wrecked → `RepairLabel = $"Repair · {RepairCost}"`, `CanRepair = stock covers`, output "Wrecked — produces nothing", `UpgradeLabel = "Wrecked — repair first"`, `CanUpgrade = false`.
  - HudView card: `repair` button shown only when `IsWrecked`, `SetEnabled(CanRepair)`; event `RepairClicked`.
  - GameController: `RepairClicked → SendFromCard(b => new RepairBuilding(Guid.NewGuid(), b.Id))`; a tap on a wrecked building always opens its card (never collects); `BattleView.StructureDamaged` → `_board.ShowStructureHealth`; at Dawn, `RecordNightResult(..., _battle.Structures.Where(s => s.IsWrecked).Select(s => s.Id).ToList())`.
  - BattleView: on `StructureHit` → `Floater(world + up*1.4, "SMASH!")`, raise `StructureDamaged(id, health/max, false)`, a small shake via existing effects; on `StructureWrecked` → poof effect at the tile (reuse `fx_poof` spawn helper) and `StructureDamaged(id, 0, true)`.
  - BoardView: per building a health bar (like raider bars) shown when `0 < fraction < 1` during the Night; `SetBuildings` tints wrecked buildings `#6f7f88` (via `Shapes.Tint` on all renderers, property block) and clears the tint when repaired.
  - DTO: `public bool wrecked;` mapped both ways.
  - Fence icon: `icons/b_wattle_fence.py` re-exporting `models.fence`; render with `./art/build.sh --icons b_wattle_fence`; BoardView `ModelName(WattleFence) = "fence"`.
- [ ] **Step 4: Run** EditMode + PlayMode all; Blender tests. **Step 5: Commit** `"Client: smash floaters and health bars, soggy wrecked buildings, Repair, Wattle Fence card"`.

---

### Task 4: Device check (A)

- [ ] Build, install on the Pixel 10: probe MATCH `1ddcdf5947cfcaba`; place huts and fences near the road, play a Night, see SMASH!/health bars/a wreck; Dawn shows wrecked buildings soggy; repair one; 60 fps. Screenshots to `art/previews/raids/`. Commit + push the branch.
