# The Prophet as Bait & Skill Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raiders step off the road to fight a Prophet standing near it (deterministic sim change, balance 2026.10.3), and the client explains every skill and raider state.

**Architecture:** The simulation keeps raiders on the 1-D road (`DistanceAlongPath`) and adds a 2-D `Offset`; `Battle.PositionOf` returns road point + offset, so towers, skills and views pick it up unchanged. `MoveEnemies` decides per tick: frightened → back to road then backwards; lured → step toward the Prophet or fight; otherwise → back to road then march. The client adds billboard status icons, rising "washed back!" notes in the HUD, a travelling flood wave, and once-ever tips behind a small store.

**Tech Stack:** .NET 8 / netstandard2.1 simulation (xUnit + FluentAssertions), Unity 6.3 (UI Toolkit, URP), Blender icon pipeline.

**Spec:** `docs/superpowers/specs/2026-10-11-prophet-bait-and-skill-feedback-design.md`

## Global Constraints

- Determinism: fixed-point `Fix` only, no floats or square roots in the simulation; every new state field is hashed.
- Balance version becomes `2026.10.3`; the demo hash is re-pinned in `DeterminismProbe.ExpectedHash` and verified on the Pixel 10.
- `dotnet build src/CultOfComfort.Simulation` copies the DLL into `Assets/Plugins` — run it after every sim change before Unity tests.
- Tests: xUnit + FluentAssertions, `MethodName_State_Expected`, AAA comments (repo standard).
- Owner-accepted: straight-line off-road movement (may clip a tower corner); diagonals up to √2 faster.
- 60 fps on the Pixel 10 in a busy Night. Commits as adzius.lech@gmail.com, no `Co-Authored-By`.

## Review Focus

1. Two raiders lured at once when `MaxEngaged` is reached — the third must walk past on the road, not hover off-road — test in Task 1 (`Step_EngagedCapReached_ExtraRaiderStaysOnTheRoad`).
2. A lured raider pushed back by the sluice keeps its offset and keeps chasing/returning sensibly (no teleport back to the road) — test in Task 1.
3. A raider off-road when the Night ends or when it reaches the Hearth path end — only advances with zero offset, so it can never "leak" while off-road — covered by the movement rule; test in Task 1 (`Step_OffRoadRaider_NeverAdvancesAlongTheRoad`).
4. First-use tips must not fire for rejected interventions (cistern empty) — test in Task 4.
5. Status icons for raiders defeated mid-frame must not leak GameObjects (they are children of the raider root) — covered by construction; checked in review.

---

### Task 1: Raiders step off the road (simulation)

**Files:**
- Modify: `src/CultOfComfort.Simulation/Numerics/FixVector2.cs`, `src/CultOfComfort.Simulation/Combat/Enemy.cs`, `src/CultOfComfort.Simulation/Combat/Battle.cs`, `src/CultOfComfort.Simulation/Balance/BalanceTable.cs`
- Test: `tests/CultOfComfort.Simulation.Tests/Combat/ProphetLureTests.cs` (new); `CultLeaderTests.cs` (adjust the voice test, see Step 5)
- Modify: `client/CultOfComfort.Unity/Assets/Scripts/DeterminismProbe.cs` (new expected hash)

**Interfaces:**
- Produces: `Enemy.Offset : FixVector2`, `Enemy.IsOffRoad : bool`, `Battle.RoadPointOf(Enemy) : FixVector2`, `EnemyStats.NoticeRange : Fix` (last ctor param, default 0), `LeaderStats.LureLeash : Fix` (last ctor param, default 0). `EngageRange` now means **reach** (attack distance) for every raider; `NoticeRange > 0` means it leaves the road.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/CultOfComfort.Simulation.Tests/Combat/ProphetLureTests.cs
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Tests.Combat;

public class ProphetLureTests
{
    /// <summary>Budget 1, one wave: a single Levy spawns at (0,1) on tick 40 and walks right at 1 tile/s along y = 1.</summary>
    private const int SingleLevy = 1;

    private static readonly GridPosition BesideTheRoad = new(4, 2);

    /// <summary>He never hits back, so lured raiders survive and stay interesting.</summary>
    private static BalanceTable Balance(Func<LeaderStats, LeaderStats>? leader = null, Func<EnemyStats, EnemyStats>? levy = null)
    {
        var enemies = new Dictionary<EnemyType, EnemyStats>(BalanceTable.Default.Enemies);
        enemies[EnemyType.Levy] = (levy ?? (s => s))(enemies[EnemyType.Levy]);
        return new BalanceTable
        {
            HearthMaxHealth = 100_000,
            Leader = (leader ?? (l => l with { AttackDamage = Fix.Zero }))(BalanceTable.Default.Leader with { AttackDamage = Fix.Zero }),
            Enemies = enemies,
        };
    }

    private static Battle StartBattle(BalanceTable balance, int budget, GridPosition leaderStart, int height = 3, params PlacedTower[] towers)
    {
        var layout = new BattleLayout(10, height, new GridPosition(0, 1), new GridPosition(9, 1), towers, [], leaderStart);
        return new Battle(new BattleSetup(layout, Seed: 42, budget, WaveCount: 1, [], balance));
    }

    private static List<BattleEvent> StepUntil(Battle battle, int tick)
    {
        var events = new List<BattleEvent>();
        while (!battle.IsFinished && battle.Tick <= tick)
        {
            battle.Step();
            events.AddRange(battle.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void Defaults_Balance_MeleeRaidersNoticeTheProphetAndZealotsDoNot()
    {
        // Act
        var enemies = BalanceTable.Default.Enemies;

        // Assert
        BalanceTable.Default.Version.Should().Be("2026.10.3");
        enemies[EnemyType.Levy].NoticeRange.Should().Be(Fix.FromInt(2));
        enemies[EnemyType.ManAtArms].NoticeRange.Should().Be(Fix.Ratio(3, 2));
        enemies[EnemyType.Zealot].NoticeRange.Should().Be(Fix.Zero);
        BalanceTable.Default.Leader.LureLeash.Should().Be(Fix.Ratio(5, 2));
    }

    [Fact]
    public void Step_ProphetBesideTheRoad_LevyStepsOffTheRoadAndFights()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);

        // Act
        var events = StepUntil(battle, 300);

        // Assert
        var levy = battle.Enemies.Single();
        levy.IsOffRoad.Should().BeTrue();
        levy.IsFightingLeader.Should().BeTrue();
        battle.PositionOf(levy).Y.Should().BeGreaterThan(Fix.Ratio(13, 10), "it left the road (y = 1) towards him (y = 2)");
        events.OfType<LeaderHit>().Should().NotBeEmpty();
    }

    [Fact]
    public void Step_ProphetBeyondNoticeRange_LevyMarchesPastOnTheRoad()
    {
        // Arrange — three tiles off the road is beyond the Levy's notice range of 2 (corridor 10x5).
        var battle = StartBattle(Balance(), SingleLevy, new GridPosition(4, 4), height: 5);

        // Act
        var events = StepUntil(battle, 400);

        // Assert
        battle.Enemies.Single().IsOffRoad.Should().BeFalse();
        events.OfType<LeaderHit>().Should().BeEmpty();
    }

    [Fact]
    public void Step_ProphetWalksAwayBeyondTheLeash_LevyReturnsToTheRoadAndMarchesOn()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);
        StepUntil(battle, 300);
        var levy = battle.Enemies.Single();
        var roadDistance = levy.DistanceAlongPath;

        // Act — he walks to the far corner, more than 2½ tiles from the Levy's road point.
        battle.Intervene(PlayerActionType.MoveLeader, new GridPosition(0, 0));
        StepUntil(battle, 420);

        // Assert
        levy.IsOffRoad.Should().BeFalse();
        levy.IsFightingLeader.Should().BeFalse();
        levy.DistanceAlongPath.Should().BeGreaterThan(roadDistance, "back on the road, it marches on");
    }

    [Fact]
    public void Step_ProphetSwoons_LuredLevyWalksBackToTheRoad()
    {
        // Arrange — 5 HP: the second hit knocks him out for 100 ticks.
        var battle = StartBattle(Balance(l => l with { AttackDamage = Fix.Zero, MaxHealth = 5, SwoonTicks = 100 }), SingleLevy, BesideTheRoad);
        var swoon = StepUntil(battle, 400).OfType<LeaderSwooned>().First();

        // Act
        StepUntil(battle, swoon.Tick + 40);

        // Assert
        battle.Enemies.Single().IsOffRoad.Should().BeFalse("nobody fights a man lying in the mud");
    }

    [Fact]
    public void Step_RaiderWithoutNoticeRange_FightsFromTheRoad()
    {
        // Arrange — a Levy made to behave like a Zealot: no notice range, throws from 2½ tiles.
        var balance = Balance(levy: s => s with { NoticeRange = Fix.Zero, EngageRange = Fix.Ratio(5, 2) });
        var battle = StartBattle(balance, SingleLevy, BesideTheRoad);

        // Act
        var events = StepUntil(battle, 300);

        // Assert
        var levy = battle.Enemies.Single();
        levy.IsOffRoad.Should().BeFalse();
        levy.IsFightingLeader.Should().BeTrue();
        events.OfType<LeaderHit>().Should().NotBeEmpty();
    }

    [Fact]
    public void Step_EngagedCapReached_ExtraRaiderStaysOnTheRoad()
    {
        // Arrange — cap of one, a budget for several Levies.
        var battle = StartBattle(Balance(l => l with { AttackDamage = Fix.Zero, MaxEngaged = 1 }), budget: 4, BesideTheRoad);
        var maxOffRoadAndHunting = 0;

        // Act
        while (!battle.IsFinished && battle.Tick < 500)
        {
            battle.Step();
            var hunting = battle.Enemies.Count(e => e.IsActive && (e.IsFightingLeader || (e.IsOffRoad && e.Offset != default && IsCloserToHimThanItsRoadPoint(battle, e))));
            maxOffRoadAndHunting = Math.Max(maxOffRoadAndHunting, hunting);
        }

        // Assert
        maxOffRoadAndHunting.Should().Be(1);
        battle.Enemies.Count.Should().BeGreaterThan(1, "the test needs more than one raider");
    }

    private static bool IsCloserToHimThanItsRoadPoint(Battle battle, Enemy enemy) =>
        battle.PositionOf(enemy).DistanceSquaredTo(battle.Leader.Position) < battle.RoadPointOf(enemy).DistanceSquaredTo(battle.Leader.Position);

    [Fact]
    public void Step_TowerOutOfReachOfTheRoad_HitsALuredRaider()
    {
        // Arrange — 10x7 field, road along y = 1; a Flinger at (3,5) is 4 tiles from the road (range 3½).
        // He stands at (3,3): the Levy comes up to about (3,2.5), inside the Flinger's range.
        var flinger = new PlacedTower(TowerType.SaplingFlinger, new GridPosition(3, 5));
        var lured = StartBattle(Balance(), SingleLevy, new GridPosition(3, 3), height: 7, flinger);
        var control = StartBattle(Balance(), SingleLevy, new GridPosition(0, 6), height: 7, flinger);

        // Act
        var luredEvents = StepUntil(lured, 400);
        var controlEvents = StepUntil(control, 400);

        // Assert
        luredEvents.OfType<TowerFired>().Should().NotBeEmpty("pulled off the road, it walked into range");
        controlEvents.OfType<TowerFired>().Should().BeEmpty("on the road it never comes within range");
    }

    [Fact]
    public void Step_LuredRaiderFrightened_WalksBackToTheRoadBeforeFleeing()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);
        StepUntil(battle, 300);
        var levy = battle.Enemies.Single();
        var roadDistance = levy.DistanceAlongPath;

        // Act
        battle.Intervene(PlayerActionType.PhantomVoice);
        battle.Step();
        while (levy.IsOffRoad && battle.Tick < 400)
        {
            levy.DistanceAlongPath.Should().Be(roadDistance, "it first scrambles back onto the road");
            battle.Step();
        }

        StepUntil(battle, battle.Tick + 10);

        // Assert
        levy.IsOffRoad.Should().BeFalse();
        levy.DistanceAlongPath.Should().BeLessThan(roadDistance, "then it flees back up the road");
    }

    [Fact]
    public void Step_LuredRaiderHitBySluice_KeepsItsOffsetAndIsWashedBack()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);
        StepUntil(battle, 300);
        var levy = battle.Enemies.Single();
        var offset = levy.Offset;
        var roadDistance = levy.DistanceAlongPath;

        // Act
        battle.Intervene(PlayerActionType.OpenSluiceGate);
        battle.Step();

        // Assert
        levy.DistanceAlongPath.Should().BeLessThan(roadDistance);
        levy.Offset.Should().NotBe(default(FixVector2), "washed along, still off the road");
        (levy.Offset.Y - offset.Y).Abs().Should().BeLessThan(Fix.Ratio(1, 10));
    }

    [Fact]
    public void Step_OffRoadRaider_NeverAdvancesAlongTheRoad()
    {
        // Arrange
        var battle = StartBattle(Balance(), SingleLevy, BesideTheRoad);

        // Act & Assert
        var last = Fix.Zero;
        while (!battle.IsFinished && battle.Tick < 600)
        {
            battle.Step();
            var levy = battle.Enemies.SingleOrDefault();
            if (levy is { IsActive: true })
            {
                if (levy.IsOffRoad)
                {
                    levy.DistanceAlongPath.Should().BeLessThanOrEqualTo(last);
                }

                last = levy.DistanceAlongPath;
            }
        }
    }

    [Fact]
    public void Simulate_LuredBattleReplay_MatchesLiveStateHash()
    {
        // Arrange
        var layout = new BattleLayout(10, 3, new GridPosition(0, 1), new GridPosition(9, 1), [], [], BesideTheRoad);
        var inputs = new[] { new PlayerInput(250, PlayerActionType.MoveLeader, new GridPosition(0, 0)), new PlayerInput(300, PlayerActionType.PhantomVoice) };
        var setup = new BattleSetup(layout, Seed: 42, Budget: 6, WaveCount: 2, inputs, Balance());
        var live = new Battle(setup);
        while (!live.IsFinished)
        {
            live.Step();
        }

        // Act
        var replay = Battle.Simulate(setup);

        // Assert
        replay.Should().Be(live.Result);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --filter ProphetLureTests 2>&1 | tail -5`. Expected: build errors (`NoticeRange`, `LureLeash`, `IsOffRoad`, `Offset`, `RoadPointOf` missing).

- [ ] **Step 3: Implement**

`FixVector2.cs` — add after `DistanceSquaredTo`:

```csharp
    public static FixVector2 operator +(FixVector2 a, FixVector2 b) => new(a.X + b.X, a.Y + b.Y);

    public static FixVector2 operator -(FixVector2 a, FixVector2 b) => new(a.X - b.X, a.Y - b.Y);
```

`Enemy.cs` — after `FrightenedUntilTick`:

```csharp
    /// <summary>How far it has stepped off the road (tiles) to chase the Prophet; zero while on the road.</summary>
    public FixVector2 Offset { get; private set; }

    public bool IsOffRoad => Offset != default;
```

and before `ReachHearth`:

```csharp
    /// <summary>Moves the offset towards <paramref name="target"/>, each axis by at most <paramref name="step"/> (no square roots).</summary>
    internal void StepOffsetToward(FixVector2 target, Fix step) =>
        Offset = new FixVector2(Approach(Offset.X, target.X, step), Approach(Offset.Y, target.Y, step));

    private static Fix Approach(Fix from, Fix to, Fix step)
    {
        var delta = to - from;
        if (delta.Abs() <= step)
        {
            return to;
        }

        return delta > Fix.Zero ? from + step : from - step;
    }
```

`BalanceTable.cs`:
- `EnemyStats`: append `Fix NoticeRange = default` as the last parameter; update the doc comments: `EngageRange` → "Attacks the Prophet from this close (tiles). Zero means never."; add `/// <param name="NoticeRange">Leaves the road to chase the Prophet when he is this close to its road point (tiles). Zero means it fights from the road.</param>`.
- `LeaderStats`: append `Fix LureLeash = default` with `/// <param name="LureLeash">A chasing raider gives up and walks back to the road when he is farther than this from its road point.</param>`.
- `Version = "2026.10.3"`.
- Levy: `EngageRange: Fix.Ratio(1, 2)`, add `NoticeRange: Fix.FromInt(2)`. Man-at-arms: `EngageRange: Fix.Ratio(1, 2)`, `NoticeRange: Fix.Ratio(3, 2)`. Zealot unchanged (`NoticeRange` default 0).
- `Leader`: add `LureLeash: Fix.Ratio(5, 2)`.

`Battle.cs`:
- Rename the body of `PositionOf` into `public FixVector2 RoadPointOf(Enemy enemy)` (same code), and make `PositionOf` return `RoadPointOf(enemy) + enemy.Offset` with doc "Where it really is: its road point plus how far it stepped off the road."
- Replace `MoveEnemies` and `CanEngageLeader`:

```csharp
    private void MoveEnemies()
    {
        var engaged = 0;
        foreach (var enemy in _enemies)
        {
            enemy.IsFightingLeader = false;
            if (!enemy.IsActive)
            {
                continue;
            }

            var stats = _balance.Enemies[enemy.Type];
            var step = stats.TilesPerSecond / BalanceTable.TicksPerSecond;
            if (enemy.IsSlowed(Tick))
            {
                step *= enemy.SlowMultiplier;
            }

            if (enemy.IsFrightened(Tick))
            {
                // Scramble back onto the road first, then flee back up it.
                if (enemy.IsOffRoad)
                {
                    enemy.StepOffsetToward(default, step);
                }
                else
                {
                    enemy.PushBack(step);
                }

                continue;
            }

            if (engaged < _balance.Leader.MaxEngaged && WantsLeader(enemy, stats))
            {
                engaged++;
                if (PositionOf(enemy).DistanceSquaredTo(Leader.Position) <= stats.EngageRange * stats.EngageRange)
                {
                    FightLeader(enemy, stats);
                }
                else
                {
                    enemy.StepOffsetToward(Leader.Position - RoadPointOf(enemy), step);
                }

                continue;
            }

            if (enemy.IsOffRoad)
            {
                enemy.StepOffsetToward(default, step); // walk back to the road before marching on
                continue;
            }

            enemy.Advance(step);
            if (enemy.DistanceAlongPath < PathLength)
            {
                continue;
            }

            enemy.ReachHearth();
            HearthHealth = Math.Max(0, HearthHealth - stats.HearthDamage);
            _tickEvents.Add(new HearthDamaged(Tick, enemy.Id, enemy.Type, stats.HearthDamage, HearthHealth));
        }
    }

    /// <summary>
    /// Melee raiders (NoticeRange &gt; 0) leave the road when he is within notice range of their road point and keep
    /// chasing until he is beyond the leash; the rest fight from the road within reach.
    /// </summary>
    private bool WantsLeader(Enemy enemy, EnemyStats stats)
    {
        if (stats.EngageRange <= Fix.Zero || Leader.IsIgnoredByEnemies(Tick, _balance.Leader.ReviveGraceTicks))
        {
            return false;
        }

        if (stats.NoticeRange <= Fix.Zero)
        {
            return PositionOf(enemy).DistanceSquaredTo(Leader.Position) <= stats.EngageRange * stats.EngageRange;
        }

        var range = enemy.IsOffRoad ? Fix.Max(stats.NoticeRange, _balance.Leader.LureLeash) : stats.NoticeRange;
        return RoadPointOf(enemy).DistanceSquaredTo(Leader.Position) <= range * range;
    }
```

- `FightLeader` doc: "Stops (on or off the road) and swings or throws at the Prophet."
- `HashState`: after `_hasher.Add(enemy.DistanceAlongPath.Raw);` add `_hasher.Add(enemy.Offset.X.Raw);` and `_hasher.Add(enemy.Offset.Y.Raw);`.

- [ ] **Step 4: Run them to verify they pass**

Run: `dotnet test --filter ProphetLureTests 2>&1 | tail -5`. Expected: all 12 pass.

- [ ] **Step 5: Full suite; adjust tests whose behaviour legitimately changed**

Run: `dotnet test 2>&1 | tail -15`. Expected: `CultLeaderTests.Step_PhantomVoiceNearEnemies_FrightenedEnemiesWalkBackwards` may now fail — with the Prophet at (3,0) the first Levy is lured off the road, and frightened raiders first walk back to the road. Change its Act to `StepUntil(battle, battle.Tick + 40)` and keep the assertions; ledger the ruling. Any other failure: read it — if it is the old "fights from the road" expectation for melee, update to the new rule with a ruling; if it is anything else, debug (systematic-debugging).

- [ ] **Step 6: Re-pin the demo hash**

```bash
dotnet build src/CultOfComfort.Simulation 2>&1 | tail -2      # copies the DLL into Unity
dotnet run --project tools/CultOfComfort.Sandbox 2>&1 | grep "State hash"
```

The Unity probe uses its own demo layout (`DeterminismProbe.Run`), so read its hash from a PlayMode run instead: run PlayMode `-testFilter Bootstrap_SceneLoaded_CreatesGameInDusk` — Expected: FAIL with "Probe hash X should equal … 12c3fe1f0e23fb59". Put X into `DeterminismProbe.ExpectedHash`, rerun → PASS.

- [ ] **Step 7: Commit**

```bash
git add src tests client/CultOfComfort.Unity/Assets/Scripts/DeterminismProbe.cs
git commit -m "Raiders leave the road to fight the Prophet (balance 2026.10.3): offset from the road, notice range, leash; demo hash re-pinned"
```

---

### Task 2: Status icons `mud` and `swords`

**Files:**
- Create: `art/blender/icons/mud.py`, `art/blender/icons/swords.py`
- Modify: `art/blender/tests/test_icons.py` (`EXPECTED` += `"mud", "swords"`), `client/.../Scripts/Hud/UiAssets.cs` (`Icons` += `"mud", "swords"`)
- Generate: `Assets/Resources/UI/Icons/mud.png`, `swords.png`

**Interfaces:** Produces icon names `mud`, `swords` (and reuses `speed`, `voice`).

- [ ] **Step 1: Failing tests** — add the two names to `EXPECTED` in `test_icons.py` and to `UiAssets.Icons`. Run `./art/build.sh --test` → Expected: `test_icon_modules_match_the_hud_list` FAIL.

- [ ] **Step 2: Modules**

```python
# icons/mud.py
"""Muddy: a mud splat."""
from models.fx_splat import TRIANGLE_BUDGET, build  # noqa: F401
```

```python
# icons/swords.py
"""Fighting the Prophet: two crossed blades."""
from lib.mesh import Part, root

TRIANGLE_BUDGET = 300


def build():
    icon = root("icon_swords")
    part = Part("Prop", parent=icon)
    for side in (-1, 1):
        part.box((0.04, 0.02, 0.44), (0, 0, 0.26), "mail", bevel=0, rotation=(0, side * 35, 0))
        part.box((0.14, 0.03, 0.03), (side * -0.1, -0.01, 0.1), "old_wood", bevel=0, rotation=(0, side * 35, 0))
        part.cylinder(0.02, 0.1, (side * -0.14, -0.01, 0.05), "tarred_wood", segments=6, rotation=(0, side * 35, 0))
    part.build()
    return icon
```

- [ ] **Step 3: Pass** — `./art/build.sh --test` → OK (38); `./art/build.sh --icons mud swords`; look at both PNGs; EditMode `-testFilter UiAssetsTests` → PASS 3/3.

- [ ] **Step 4: Commit** — `git commit -m "Icons: mud and crossed swords for raider status"`.

---

### Task 3: On-screen feedback (status icons, floating notes, flood wave)

**Files:**
- Create: `client/.../Scripts/Views/RaiderStatus.cs`
- Modify: `client/.../Scripts/Views/BattleView.cs`, `client/.../Scripts/Hud/HudView.cs`, `client/.../Resources/UI/Hud.uss`, `client/.../Scripts/GameController.cs`
- Test: `Assets/Tests/EditMode/RaiderStatusTests.cs` (new), `Assets/Tests/PlayMode/HudFrameTests.cs` (add)

**Interfaces:**
- Produces: `RaiderStatus.Select(bool muddy, bool slowed, bool frightened, bool fighting, string[] into) : int` (writes ≤ 2 icon names in priority mud, speed, voice, swords; `into.Length >= 2`); `HudView.Float(string text, Vector2 panelPosition, float now)`; `BattleView.Floater : event Action<Vector3, string>` (world position, text).

- [ ] **Step 1: Failing tests**

```csharp
// Assets/Tests/EditMode/RaiderStatusTests.cs
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class RaiderStatusTests
    {
        private readonly string[] _icons = new string[2];

        [Test]
        public void Select_NothingGoingOn_ShowsNoIcons()
        {
            // Act
            var count = RaiderStatus.Select(false, false, false, false, _icons);

            // Assert
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Select_EverythingAtOnce_ShowsTheTwoMostImportantInOrder()
        {
            // Act
            var count = RaiderStatus.Select(true, true, true, true, _icons);

            // Assert
            Assert.AreEqual(2, count);
            Assert.AreEqual("mud", _icons[0]);
            Assert.AreEqual("speed", _icons[1]);
        }

        [Test]
        public void Select_FrightenedAndFighting_ShowsVoiceThenSwords()
        {
            // Act
            var count = RaiderStatus.Select(false, false, true, true, _icons);

            // Assert
            Assert.AreEqual(2, count);
            Assert.AreEqual("voice", _icons[0]);
            Assert.AreEqual("swords", _icons[1]);
        }
    }
}
```

Add to `HudFrameTests`:

```csharp
        [UnityTest]
        public IEnumerator Float_WashedBack_ShowsThenFades()
        {
            // Arrange
            var (hud, root) = PhoneHud(portrait: true);
            hud.ShowPhase(GamePhase.Night);

            // Act
            hud.Float("washed back!", new Vector2(100f, 300f), Time.time);
            hud.Tick(Time.time);
            yield return null;
            var shown = root.Query<Label>(className: "floater").Where(l => l.resolvedStyle.display == DisplayStyle.Flex && l.text == "washed back!").ToList().Count;
            hud.Tick(Time.time + 2f);
            yield return null;
            var after = root.Query<Label>(className: "floater").Where(l => l.resolvedStyle.display == DisplayStyle.Flex).ToList().Count;

            // Assert
            Assert.AreEqual(1, shown);
            Assert.AreEqual(0, after);
        }
```

Run EditMode `-testFilter RaiderStatusTests` and PlayMode `-testFilter Float_WashedBack_ShowsThenFades` → Expected: compile errors (`RaiderStatus`, `HudView.Float` missing).

- [ ] **Step 2: `RaiderStatus`**

```csharp
// Assets/Scripts/Views/RaiderStatus.cs
namespace CultOfComfort.Client
{
    /// <summary>Which state icons float over a raider: at most two, most important first (spec: Status icons).</summary>
    public static class RaiderStatus
    {
        public const int MaxIcons = 2;

        /// <returns>How many names were written into <paramref name="into"/> (allocation-free; call per frame).</returns>
        public static int Select(bool muddy, bool slowed, bool frightened, bool fighting, string[] into)
        {
            var count = 0;
            Add(muddy, "mud", into, ref count);
            Add(slowed, "speed", into, ref count);
            Add(frightened, "voice", into, ref count);
            Add(fighting, "swords", into, ref count);
            return count;
        }

        private static void Add(bool active, string icon, string[] into, ref int count)
        {
            if (active && count < MaxIcons)
            {
                into[count++] = icon;
            }
        }
    }
}
```

- [ ] **Step 3: HUD floaters**

In `HudView`: a pool of 12 labels (class `floater`, `pickingMode = Ignore`, absolutely positioned, hidden) added to the root last; `Float` takes the next pooled label (round-robin), sets text, `style.left/top = panelPosition - (40, 0)`, start time; `Tick` moves each active one up 36 px over 1.2 s and fades `style.opacity` 1 → 0, hiding it after 1.2 s.

```csharp
        private const float FloatSeconds = 1.2f;
        private readonly Label[] _floaters = new Label[12];
        private readonly float[] _floatStart = new float[12];
        private readonly Vector2[] _floatAt = new Vector2[12];
        private int _nextFloater;

        // in the constructor, after the overlays:
        for (var i = 0; i < _floaters.Length; i++)
        {
            _floaters[i] = Text(string.Empty, "floater", "hidden");
            _floaters[i].pickingMode = PickingMode.Ignore;
            _floatStart[i] = float.NegativeInfinity;
            _root.Add(_floaters[i]);
        }

        /// <summary>A marker-pen note that rises and fades above a raider (panel coordinates).</summary>
        public void Float(string text, Vector2 panelPosition, float now)
        {
            var i = _nextFloater;
            _nextFloater = (_nextFloater + 1) % _floaters.Length;
            _floaters[i].text = text;
            _floatStart[i] = now;
            _floatAt[i] = panelPosition;
        }

        // in Tick(now), before ApplySafeArea():
        for (var i = 0; i < _floaters.Length; i++)
        {
            var age = now - _floatStart[i];
            var live = age >= 0f && age < FloatSeconds;
            _floaters[i].EnableInClassList("hidden", !live);
            if (live)
            {
                var t = age / FloatSeconds;
                _floaters[i].style.left = _floatAt[i].x - 40f;
                _floaters[i].style.top = _floatAt[i].y - 36f * t;
                _floaters[i].style.opacity = 1f - t * t;
            }
        }
```

USS:

```css
.floater {
    position: absolute; width: 80px; -unity-text-align: middle-center; padding: 0;
    -unity-font-definition: resource("UI/Fonts/PermanentMarker");
    color: #ffe066; font-size: 15px; text-shadow: 0 1px 2px rgba(0, 0, 0, 0.95);
}
```

- [ ] **Step 4: BattleView — status billboards, floaters, wave**

- `EnemyVisual` gains `SpriteRenderer[] Status = new SpriteRenderer[2]` and `string[] Icons = new string[2]`. When a raider visual is created, add two child GameObjects `Status0/1` with `SpriteRenderer` (sorting order 10), positioned above the health bar (y + 0.25, x ∓ 0.18), disabled.
- A static sprite cache: `Dictionary<string, Sprite>`; `Sprite.Create(Resources.Load<Texture2D>("UI/Icons/" + name), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 300f)`; missing texture → no icon.
- In `Render`, per visual: `var count = RaiderStatus.Select(visual.Muddied, visual.Slowed, visual.Frightened, visual.Fighting, visual.Icons)`; for k < count set sprite + enable, else disable; set each enabled status renderer's `transform.rotation = cam.transform.rotation` (billboard; `cam = Camera.main` cached in `Begin`).
- `AfterStep` fills `visual.Slowed = enemy.IsSlowed(_battle.Tick)` (new field).
- `public event Action<Vector3, string> Floater;` In `HandleEvent`: on `InterventionUsed{OpenSluiceGate}` → for each active raider visual: `Floater?.Invoke(visual.Current + Vector3.up * 1.2f, "washed back!")`; on `InterventionUsed{PhantomVoice}` → the same for visuals with `Frightened` after the step (use `enemy.IsFrightened(battle.Tick)` from `_battle.Enemies`), text `"?!"`; on `HotStonesThrown` → visuals within the stones radius of the target tile, text `"ouch!"`.
- `SpawnFlood`: replace the delay with a sweep from the Hearth end: `var index = i (path index); var delay = (_battle.Path.Count - 1 - index) / (float)_battle.Path.Count * 0.8f;` lifetime 1.6 s; height `Mathf.Sin(Mathf.Clamp01((t * 1.6f - delay) / 0.6f) * Mathf.PI) * 0.3f`.

- [ ] **Step 5: GameController wiring**

```csharp
            _battleView.Floater += (world, text) =>
            {
                var screen = _camera.WorldToScreenPoint(world);
                if (screen.z <= 0f) return;
                var panel = RuntimePanelUtils.ScreenToPanel(_document.rootVisualElement.panel, new Vector2(screen.x, Screen.height - screen.y));
                _hud.Float(text, panel, Time.time);
            };
```

(`_camera` = the rig's camera; keep the `UIDocument` from `SetUpHud` in a field `_document`.)

- [ ] **Step 6: Pass** — EditMode `-testFilter RaiderStatusTests` → 3/3; PlayMode (all) → all pass. Run `HudCapture` and view the night shot; then view on device in Task 5.

- [ ] **Step 7: Commit** — `git commit -m "Skill feedback: raider status icons, rising notes for sluice/voice/stones, flood sweeps down the road"`.

---

### Task 4: First-use tips + Show tips again

**Files:**
- Create: `client/.../Scripts/Hud/TipBook.cs`
- Modify: `HudView.cs` (settings button + `ResetTipsClicked`), `BattleView.cs` (`SkillUsed` event), `GameController.cs`
- Test: `Assets/Tests/EditMode/TipBookTests.cs` (new), `Assets/Tests/PlayMode/HudFrameTests.cs` (add)

**Interfaces:**
- Produces: `interface ITipStore { bool HasSeen(string id); void MarkSeen(string id); void Reset(IEnumerable<string> ids); }`, `PlayerPrefsTipStore : ITipStore`, `TipBook(ITipStore)` with `string Take(PlayerActionType skill)` (null if seen or no tip) and `void Reset()`; `BattleView.SkillUsed : event Action<PlayerActionType>` (only for accepted uses: `InterventionUsed` and `HotStonesThrown`); `HudView.ResetTipsClicked : event Action`.

- [ ] **Step 1: Failing tests**

```csharp
// Assets/Tests/EditMode/TipBookTests.cs
using System.Collections.Generic;
using CultOfComfort.Simulation.Combat;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class TipBookTests
    {
        private sealed class MemoryStore : ITipStore
        {
            private readonly HashSet<string> _seen = new HashSet<string>();
            public bool HasSeen(string id) => _seen.Contains(id);
            public void MarkSeen(string id) => _seen.Add(id);
            public void Reset(IEnumerable<string> ids) => _seen.Clear();
        }

        [Test]
        public void Take_FirstSluice_ReturnsTheSluiceTip()
        {
            // Arrange
            var tips = new TipBook(new MemoryStore());

            // Act
            var tip = tips.Take(PlayerActionType.OpenSluiceGate);

            // Assert
            StringAssert.Contains("washed three tiles back", tip);
        }

        [Test]
        public void Take_SecondSluice_ReturnsNull()
        {
            // Arrange
            var tips = new TipBook(new MemoryStore());
            tips.Take(PlayerActionType.OpenSluiceGate);

            // Act
            var tip = tips.Take(PlayerActionType.OpenSluiceGate);

            // Assert
            Assert.IsNull(tip);
        }

        [Test]
        public void Take_MoveLeader_HasNoTip()
        {
            Assert.IsNull(new TipBook(new MemoryStore()).Take(PlayerActionType.MoveLeader));
        }

        [Test]
        public void Reset_AfterAllSeen_ShowsTipsAgain()
        {
            // Arrange
            var tips = new TipBook(new MemoryStore());
            tips.Take(PlayerActionType.PhantomVoice);

            // Act
            tips.Reset();

            // Assert
            StringAssert.Contains("megaphone", tips.Take(PlayerActionType.PhantomVoice));
        }
    }
}
```

Add to `HudFrameTests`:

```csharp
        [UnityTest]
        public IEnumerator Settings_ShowTipsAgain_RaisesReset()
        {
            // Arrange
            var (hud, root) = PhoneHud(portrait: true);
            var raised = false;
            hud.ResetTipsClicked += () => raised = true;
            yield return null;

            // Act
            Click(root.Q<Button>("reset-tips"));

            // Assert
            Assert.IsTrue(raised);
        }
```

(Review Focus 4 — rejected interventions never tip — is guaranteed by `SkillUsed` firing only from `InterventionUsed`/`HotStonesThrown`; the sim emits `InputRejected` instead for an empty cistern. Pin it with a PlayMode check in Step 4.)

Run EditMode `-testFilter TipBookTests` → Expected: compile errors.

- [ ] **Step 2: `TipBook`**

```csharp
// Assets/Scripts/Hud/TipBook.cs
using System.Collections.Generic;
using CultOfComfort.Simulation.Combat;
using UnityEngine;

namespace CultOfComfort.Client
{
    public interface ITipStore
    {
        bool HasSeen(string id);
        void MarkSeen(string id);
        void Reset(IEnumerable<string> ids);
    }

    /// <summary>Remembers seen tips on the device.</summary>
    public sealed class PlayerPrefsTipStore : ITipStore
    {
        private const string Prefix = "tip.";

        public bool HasSeen(string id) => PlayerPrefs.GetInt(Prefix + id, 0) == 1;

        public void MarkSeen(string id)
        {
            PlayerPrefs.SetInt(Prefix + id, 1);
            PlayerPrefs.Save();
        }

        public void Reset(IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                PlayerPrefs.DeleteKey(Prefix + id);
            }

            PlayerPrefs.Save();
        }
    }

    /// <summary>First-use explanations of the Prophet's skills, each shown once ever (spec: First-use tips).</summary>
    public sealed class TipBook
    {
        private static readonly Dictionary<PlayerActionType, (string Id, string Text)> Tips = new Dictionary<PlayerActionType, (string, string)>
        {
            [PlayerActionType.OpenSluiceGate] = ("sluice", "Opened the cistern: everyone got washed three tiles back down the road and is muddy for a bit — Sapling Flingers love muddy targets. Two per night."),
            [PlayerActionType.PhantomVoice] = ("voice", "Tin-can megaphone: raiders near me panic and walk backwards for two seconds."),
            [PlayerActionType.HotStones] = ("stones", "Hot stones: hurts and muddies everyone in the circle. Muddy raiders take extra damage from Sapling Flingers."),
        };

        private readonly ITipStore _store;

        public TipBook(ITipStore store) => _store = store;

        /// <returns>The tip the first time this skill is used, otherwise null.</returns>
        public string Take(PlayerActionType skill)
        {
            if (!Tips.TryGetValue(skill, out var tip) || _store.HasSeen(tip.Id))
            {
                return null;
            }

            _store.MarkSeen(tip.Id);
            return tip.Text;
        }

        public void Reset()
        {
            var ids = new List<string>();
            foreach (var tip in Tips.Values)
            {
                ids.Add(tip.Id);
            }

            _store.Reset(ids);
        }
    }
}
```

- [ ] **Step 3: Wiring**
- `HudView.BuildSettings`: after the sound line, `var tips = ButtonWith("Show tips again", () => ResetTipsClicked?.Invoke(), "plain"); tips.name = "reset-tips"; sheet.Add(tips);` and `public event Action ResetTipsClicked;`.
- `BattleView`: `public event Action<PlayerActionType> SkillUsed;` raised in `HandleEvent` for `InterventionUsed used` (`used.Type`) and for `HotStonesThrown` (`PlayerActionType.HotStones`).
- `GameController`: `_tips = new TipBook(new PlayerPrefsTipStore());` `_battleView.SkillUsed += skill => { var tip = _tips.Take(skill); if (tip != null) _hud.Announce(tip, Time.time, 7f, prophet: true); };` `_hud.ResetTipsClicked += () => { _tips.Reset(); Announce("Tips are back. The Prophet clears his throat."); };`.

- [ ] **Step 4: Pass** — EditMode `-testFilter TipBookTests` → 4/4; PlayMode all → pass. In `GameSmokeTests`-style PlayMode test `Tips_SluiceWhileCisternEmpty_NoSecondTip` is not needed — `SkillUsed` is only raised for accepted events (checked by reading; ledger).

- [ ] **Step 5: Commit** — `git commit -m "First-use skill tips in the Prophet's voice, Show tips again in Settings"`.

---

### Task 5: Device check and docs

- [ ] **Step 1: Full suites** — `dotnet test` (all, +12); `./art/build.sh --test`; kit tests; EditMode; PlayMode.
- [ ] **Step 2: Build + install** (as in the HUD plan); Expected: probe MATCH with the new hash; Dusk/busy Night ~60 fps.
- [ ] **Step 3: Screenshots** to `art/previews/gameplay/`: Prophet beside the road with a lured raider and its swords icon; the sluice wave with "washed back!" notes; the first-use sluice tip. Look at each; fix only clipped/unreadable items.
- [ ] **Step 4: Docs** — README Night bullet: "Melee raiders leave the road to fight a Prophet standing within their notice range (Levy 2, Man-at-arms 1½ tiles) — pull them into tower range; Zealots throw from the road." GDD `docs/gdd/01-core-loop.md` "The Prophet on the battlefield": add the lure rule and balance 2026.10.3. Backlog: done line.
- [ ] **Step 5: Commit and push** `gameplay/prophet-bait`.
