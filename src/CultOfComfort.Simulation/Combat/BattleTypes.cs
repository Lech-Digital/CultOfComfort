using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Combat;

public enum PlayerActionType
{
    OpenSluiceGate,
    MoveLeader,
    PhantomVoice,
    HotStones,

    /// <summary>Relic: the Finger of God. Target: a tile; the raider nearest it is blinded where it stands.</summary>
    LaserPointer,

    /// <summary>Relic: light without fire. Raiders near the Prophet are dazzled while it burns.</summary>
    DynamoTorch,

    /// <summary>Relic: sun-fire. Target: a tile; raiders there catch fire. Only while the sun is up (the first wave).</summary>
    SunFire,

    /// <summary>Between waves: bring the next one on now, for more salvage (spec 2026-11-17 fun first, C3). Rules 1+.</summary>
    CallWave,

    /// <summary>Between waves: take one of the three boons on offer; Target.X is its index (C2). Rules 1+.</summary>
    PickBoon,
}

/// <summary>The relics from the boot the Prophet has worked out how to use (GDD 05: The Boot).</summary>
[System.Flags]
public enum Relic
{
    None = 0,
    LaserPointer = 1,
    DynamoTorch = 2,
    Magnifier = 4,
}

/// <summary>Which relics he can use at each Hearth level (the village grows; he finds things in the car).</summary>
public static class RelicBook
{
    public static Relic ForHearth(int hearthLevel) => hearthLevel switch
    {
        <= 1 => Relic.None,
        2 => Relic.LaserPointer | Relic.DynamoTorch,
        _ => Relic.LaserPointer | Relic.DynamoTorch | Relic.Magnifier,
    };

    public static Relic For(PlayerActionType action) => action switch
    {
        PlayerActionType.LaserPointer => Relic.LaserPointer,
        PlayerActionType.DynamoTorch => Relic.DynamoTorch,
        PlayerActionType.SunFire => Relic.Magnifier,
        _ => Relic.None,
    };
}

public enum BattleOutcome
{
    Won,
    Lost,
}

/// <summary>One recorded player action. The input log is all the server needs to replay a Night.</summary>
/// <param name="Target">Tile for <see cref="PlayerActionType.MoveLeader"/> and <see cref="PlayerActionType.HotStones"/>; ignored otherwise.</param>
public readonly record struct PlayerInput(int Tick, PlayerActionType Type, GridPosition Target = default);

/// <param name="ExtraSluiceCharges">Open Sluice charges on top of the balance's (the village's Cisterns).</param>
public sealed record BattleSetup(
    BattleLayout Layout,
    ulong Seed,
    int Budget,
    int WaveCount,
    IReadOnlyList<PlayerInput> Inputs,
    BalanceTable? Balance = null,
    int ExtraSluiceCharges = 0)
{
    /// <summary>Who comes (null: the classic demo mix, so old replays and the demo hash stay as they were).</summary>
    public Waves.RaidRoster? Roster { get; init; }

    /// <summary>The relics he can use tonight (none: old replays and the demo hash stay as they were).</summary>
    public Relic Relics { get; init; }

    /// <summary>
    /// The relics fire themselves when they're ready and worth it (owner 2026-10-15: "laser should act automatically").
    /// Off for Nights planned before, so their replays stay as they were.
    /// </summary>
    public bool AutoRelics { get; init; }

    /// <summary>Cultists out defending: one post each (beside their hut). None for Nights planned before.</summary>
    public IReadOnlyList<GridPosition> Defenders { get; init; } = System.Array.Empty<GridPosition>();

    /// <summary>Mud traps on the road (spec 2026-10-20 dusk tricks). None for Nights planned before.</summary>
    public IReadOnlyList<GridPosition> Traps { get; init; } = System.Array.Empty<GridPosition>();

    /// <summary>
    /// Which rules the Night is fought by: 0 as before; 1 (balance 2026.11.1, spec 2026-11-17 fun first, C) waves wait for
    /// the last to be cleared, with a gap for a boon and a call, stars are earned, and there's an objective.
    /// </summary>
    public int Rules { get; init; }

    /// <summary>The third star's condition (rules 1+).</summary>
    public NightObjective Objective { get; init; }

    /// <summary>How many buildings the raiders may wreck (0: no limit). The first Night teaches: one (decision 2).</summary>
    public int MaxWrecks { get; init; }
}

/// <summary>What the third star asks for (spec 2026-11-17 fun first, C1), shown at Dusk.</summary>
public enum NightObjective
{
    None,
    NothingWrecked,
    ProphetNeverSwoons,
    ThreeInOneStone,
}

/// <summary>What a boon does for the next wave (C2).</summary>
public enum Boon
{
    /// <summary>"Duct-tape the chute": towers fire half as fast again.</summary>
    ChuteTape,

    /// <summary>"Bluetooth at full volume": the Voice cools down in half the time.</summary>
    FullVolume,

    /// <summary>"Hand out umbrellas": cultists patch the buildings as the wave comes.</summary>
    Umbrellas,

    /// <summary>"Hotter stones": Hot Stones heat up in half the time.</summary>
    HotterStones,
}

public sealed record BattleResult(
    BattleOutcome Outcome,
    int Stars,
    int HearthHealth,
    int HearthMaxHealth,
    int TicksElapsed,
    int EnemiesDefeated,
    int EnemiesLeaked,
    ulong StateHash,
    bool ObjectiveMet = false,
    int EarlyCalls = 0)
{
    public string StateHashHex => StateHash.ToString("x16");
}

public abstract record BattleEvent(int Tick);

public sealed record WaveStarted(int Tick, int Wave) : BattleEvent(Tick);

public sealed record EnemySpawned(int Tick, int EnemyId, EnemyType Type, int Wave) : BattleEvent(Tick);

public sealed record TowerFired(int Tick, int TowerId, int TargetId, Fix Damage, bool Critical) : BattleEvent(Tick);

public sealed record EnemyDefeated(int Tick, int EnemyId, EnemyType Type) : BattleEvent(Tick);

public sealed record HearthDamaged(int Tick, int EnemyId, EnemyType Type, int Damage, int HearthHealthRemaining) : BattleEvent(Tick);

public sealed record InterventionUsed(int Tick, PlayerActionType Type, int EnemiesAffected) : BattleEvent(Tick);

public sealed record InputRejected(int Tick, PlayerActionType Type, string Reason) : BattleEvent(Tick);

public sealed record LeaderMoveOrdered(int Tick, GridPosition Target) : BattleEvent(Tick);

public sealed record LeaderAttacked(int Tick, int TargetId, Fix Damage) : BattleEvent(Tick);

/// <summary>An enemy stopped to fight the Prophet and landed a hit.</summary>
public sealed record StructureHit(int Tick, int StructureId, int EnemyId, int Damage, int HealthRemaining) : BattleEvent(Tick);

public sealed record StructureWrecked(int Tick, int StructureId) : BattleEvent(Tick);

public sealed record LeaderHit(int Tick, int EnemyId, EnemyType Type, int Damage, int HealthRemaining) : BattleEvent(Tick);

public sealed record LeaderSwooned(int Tick) : BattleEvent(Tick);

public sealed record LeaderRevived(int Tick, int Health) : BattleEvent(Tick);

public sealed record HotStonesThrown(int Tick, GridPosition Target, int EnemiesAffected) : BattleEvent(Tick);

/// <summary>A wave is cleared and the next waits (rules 1+): the three boons on offer, and when it comes if not called.</summary>
public sealed record WaveGap(int Tick, int NextWave, IReadOnlyList<Boon> Offer, int NextWaveAtTick) : BattleEvent(Tick);

public sealed record BoonPicked(int Tick, Boon Boon) : BattleEvent(Tick);

/// <summary>The umbrellas patched a building (rules 1+).</summary>
public sealed record StructureMended(int Tick, int StructureId, int Health) : BattleEvent(Tick);

/// <summary>The player called the next wave early (rules 1+).</summary>
public sealed record WaveCalled(int Tick, int Wave) : BattleEvent(Tick);

/// <summary>A relic went off: the laser at <paramref name="TargetId"/> (−1: none), the torch lit, the glass focused on <paramref name="Target"/>.</summary>
public sealed record RelicUsed(int Tick, PlayerActionType Type, GridPosition Target, int TargetId, int EnemiesAffected) : BattleEvent(Tick);

public sealed record DefenderAttacked(int Tick, int DefenderId, int EnemyId, Fix Damage) : BattleEvent(Tick);

public sealed record DefenderHit(int Tick, int DefenderId, int EnemyId, int Damage, int HealthRemaining) : BattleEvent(Tick);

public sealed record DefenderDowned(int Tick, int DefenderId) : BattleEvent(Tick);

public sealed record DefenderBack(int Tick, int DefenderId) : BattleEvent(Tick);

/// <summary>A raider stepped in a mud trap: stuck and slowed; <paramref name="ChargesLeft"/> more before it's trodden flat.</summary>
public sealed record TrapSprung(int Tick, GridPosition Tile, int EnemyId, int ChargesLeft) : BattleEvent(Tick);

public sealed record BattleEnded(int Tick, BattleOutcome Outcome, int Stars) : BattleEvent(Tick);
