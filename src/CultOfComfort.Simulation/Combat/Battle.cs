using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;
using CultOfComfort.Simulation.Waves;

namespace CultOfComfort.Simulation.Combat;

/// <summary>
/// Deterministic Night simulation: f(layout, seed, budget, inputs) → result + state hash.
/// The client steps it live; the server replays the recorded input log and compares hashes (ADR 0002).
/// </summary>
public sealed class Battle
{
    private readonly BalanceTable _balance;
    private readonly Relic _relics;
    private readonly BattleLayout _layout;
    private readonly PcgRandom _combatRandom;
    private readonly ulong _seed;
    private readonly List<(int Index, Structure Gate)> _roadBlockers = new(); // barriers standing on the road, nearest first
    private readonly IReadOnlyList<SpawnEntry> _spawns;
    private readonly List<PlayerInput> _pendingInputs;
    private readonly List<PlayerInput> _inputLog = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<Tower> _towers = new();
    private readonly List<Structure> _structures = new();
    private readonly List<BattleEvent> _tickEvents = new();
    private readonly Fnv1aHasher _hasher = new();
    private readonly List<Defender> _defenders = new();
    private readonly bool _autoRelics;
    private int _nextSpawnIndex;
    private int _nextEnemyId = 1;

    public Battle(BattleSetup setup)
    {
        if (setup is null)
        {
            throw new ArgumentNullException(nameof(setup));
        }

        if (setup.Inputs.Any(input => input.Tick < 0))
        {
            throw new ArgumentException("Input ticks must be non-negative.", nameof(setup));
        }

        setup.Layout.Validate();

        _balance = setup.Balance ?? BalanceTable.Default;
        _relics = setup.Relics;
        _autoRelics = setup.AutoRelics;
        _layout = setup.Layout;
        var defenderId = 1;
        foreach (var post in setup.Defenders ?? Array.Empty<GridPosition>())
        {
            _defenders.Add(new Defender(defenderId++, post, (setup.Balance ?? BalanceTable.Default).Defender.MaxHealth));
        }

        Leader = new CultLeader(setup.Layout.LeaderStart, _balance.Leader.MaxHealth);
        _combatRandom = new PcgRandom(setup.Seed, RandomStreams.Combat);
        _seed = setup.Seed;
        _spawns = WaveGenerator.Generate(setup.Budget, setup.WaveCount, setup.Seed, _balance, setup.Roster);
        _pendingInputs = setup.Inputs.OrderBy(input => input.Tick).ToList();

        Path = setup.Layout.ResolveEnemyPath();
        PathLength = Fix.FromInt(Path.Count - 1);
        WaveCount = setup.WaveCount;
        TotalEnemies = _spawns.Count;
        HearthHealth = _balance.HearthMaxHealth;
        SluiceChargesLeft = _balance.SluiceGate.Charges + setup.ExtraSluiceCharges;

        var towerId = 1;
        foreach (var placed in setup.Layout.Towers)
        {
            _towers.Add(new Tower(towerId++, placed.Type, placed.Position, placed.Level, placed.Manned));
        }

        foreach (var s in setup.Layout.Structures)
        {
            _structures.Add(new Structure(s.Id, s.Position, s.MaxHealth, s.Health, s.Tiles, s.Barrier));
        }

        for (var i = 1; i < Path.Count; i++)
        {
            var gate = _structures.FirstOrDefault(st => st.IsBarrier && st.Tiles.Contains(Path[i]));
            if (gate != null)
            {
                _roadBlockers.Add((i, gate));
            }
        }
    }

    /// <summary>The relics fire themselves tonight (Nights planned since 2026-10-15).</summary>
    public bool AutoRelics => _autoRelics;

    /// <summary>The cultists out defending (none for Nights planned before they did).</summary>
    public IReadOnlyList<Defender> Defenders => _defenders;

    /// <summary>The village's buildings and towers as they stand (or lie) right now.</summary>
    public IReadOnlyList<Structure> Structures => _structures;

    /// <summary>The next tick to be simulated.</summary>
    public int Tick { get; private set; }
    public BattleResult? Result { get; private set; }
    public bool IsFinished => Result is not null;
    public int HearthHealth { get; private set; }
    public int HearthMaxHealth => _balance.HearthMaxHealth;
    public int SluiceChargesLeft { get; private set; }
    public int SluiceReadyAtTick { get; private set; }
    public bool IsSluiceReady => SluiceChargesLeft > 0 && Tick >= SluiceReadyAtTick;
    public CultLeader Leader { get; }
    public bool IsVoiceReady => Tick >= Leader.VoiceReadyAtTick && !Leader.IsSwooning(Tick);
    public bool IsHotStonesReady => Tick >= Leader.HotStonesReadyAtTick && !Leader.IsSwooning(Tick);

    /// <summary>The relics he can use tonight.</summary>
    public Relic Relics => _relics;

    public bool IsLaserReady => Has(Relic.LaserPointer) && Tick >= Leader.LaserReadyAtTick && !Leader.IsSwooning(Tick);

    public bool IsTorchReady => Has(Relic.DynamoTorch) && Tick >= Leader.TorchReadyAtTick && !Leader.IsSwooning(Tick);

    /// <summary>The magnifying glass needs the sun, and the sun sets after the first wave.</summary>
    public bool IsSunUp => CurrentWave <= 1;

    public bool IsSunFireReady => Has(Relic.Magnifier) && IsSunUp && Tick >= Leader.SunFireReadyAtTick && !Leader.IsSwooning(Tick);

    private bool Has(Relic relic) => (_relics & relic) == relic;
    public int CurrentWave { get; private set; }
    public int WaveCount { get; }
    public int TotalEnemies { get; }
    public IReadOnlyList<GridPosition> Path { get; }
    public Fix PathLength { get; }
    public IReadOnlyList<Enemy> Enemies => _enemies;
    public IReadOnlyList<Tower> Towers => _towers;

    /// <summary>Events produced by the most recent <see cref="Step"/>.</summary>
    public IReadOnlyList<BattleEvent> LastTickEvents => _tickEvents;

    /// <summary>Every input that was processed, in order. Submit this to /api/combat/resolve.</summary>
    public IReadOnlyList<PlayerInput> InputLog => _inputLog;

    public static BattleResult Simulate(BattleSetup setup) => new Battle(setup).RunToCompletion();

    public BattleResult RunToCompletion()
    {
        while (!IsFinished)
        {
            Step();
        }

        return Result!;
    }

    /// <summary>Queues a live player action for the upcoming tick.</summary>
    /// <param name="target">Tile for <see cref="PlayerActionType.MoveLeader"/> and <see cref="PlayerActionType.HotStones"/>.</param>
    public void Intervene(PlayerActionType type, GridPosition target = default)
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("The battle is already finished.");
        }

        var insertAt = _pendingInputs.FindLastIndex(input => input.Tick <= Tick) + 1;
        _pendingInputs.Insert(insertAt, new PlayerInput(Tick, type, target));
    }

    public void Step()
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("The battle is already finished.");
        }

        _tickEvents.Clear();
        SpawnEnemies();
        ReviveLeader();
        ApplyInputs();
        if (_autoRelics)
        {
            FireReadyRelics();
        }

        FireTowers();
        BurnEnemies();
        LeaderAttack();
        DefendersAct();
        MoveEnemies();
        if (!Leader.IsSwooning(Tick))
        {
            Leader.Walk(_balance.Leader.TilesPerSecond / BalanceTable.TicksPerSecond);
        }

        Leader.Regenerate(Tick, _balance.Leader.RegenDelayTicks, _balance.Leader.RegenIntervalTicks);
        HashState();
        CheckForEnd();
        Tick++;
    }

    /// <summary>Where it really is: its point on the road plus how far it stepped off it.</summary>
    public FixVector2 PositionOf(Enemy enemy) => RoadPointOf(enemy) + enemy.Offset;

    /// <summary>Its point on the road (ignoring any offset).</summary>
    public FixVector2 RoadPointOf(Enemy enemy)
    {
        var distance = Fix.Clamp(enemy.DistanceAlongPath, Fix.Zero, PathLength);
        var index = distance.FloorToInt();
        if (index >= Path.Count - 1)
        {
            return Path[Path.Count - 1].ToVector();
        }

        var fraction = distance - Fix.FromInt(index);
        var from = Path[index];
        var to = Path[index + 1];
        return new FixVector2(
            Fix.FromInt(from.X) + fraction * (to.X - from.X),
            Fix.FromInt(from.Y) + fraction * (to.Y - from.Y));
    }

    private void SpawnEnemies()
    {
        while (_nextSpawnIndex < _spawns.Count && _spawns[_nextSpawnIndex].Tick <= Tick)
        {
            var spawn = _spawns[_nextSpawnIndex++];
            if (spawn.Wave != CurrentWave)
            {
                CurrentWave = spawn.Wave;
                _tickEvents.Add(new WaveStarted(Tick, spawn.Wave));
            }

            var enemy = new Enemy(_nextEnemyId++, spawn.Type, spawn.Wave, _balance.Enemies[spawn.Type].MaxHealth);
            enemy.WallBreaker = Roll(enemy.Id) < _balance.Enemies[spawn.Type].WallBreakerPercent;
            _enemies.Add(enemy);
            _tickEvents.Add(new EnemySpawned(Tick, enemy.Id, enemy.Type, spawn.Wave));
        }
    }

    private void ApplyInputs()
    {
        while (_pendingInputs.Count > 0 && _pendingInputs[0].Tick <= Tick)
        {
            var input = _pendingInputs[0];
            _pendingInputs.RemoveAt(0);
            _inputLog.Add(input);

            switch (input.Type)
            {
                case PlayerActionType.OpenSluiceGate:
                    OpenSluiceGate();
                    break;
                case PlayerActionType.MoveLeader:
                    OrderLeader(input.Target);
                    break;
                case PlayerActionType.PhantomVoice:
                    UsePhantomVoice();
                    break;
                case PlayerActionType.HotStones:
                    ThrowHotStones(input.Target);
                    break;
                case PlayerActionType.LaserPointer:
                    PointLaser(input.Target);
                    break;
                case PlayerActionType.DynamoTorch:
                    CrankTorch();
                    break;
                case PlayerActionType.SunFire:
                    FocusSunFire(input.Target);
                    break;
                default:
                    _tickEvents.Add(new InputRejected(Tick, input.Type, "Unknown intervention."));
                    break;
            }
        }
    }

    private void OpenSluiceGate()
    {
        var stats = _balance.SluiceGate;
        if (SluiceChargesLeft == 0)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.OpenSluiceGate, "The cistern is empty."));
            return;
        }

        if (Tick < SluiceReadyAtTick)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.OpenSluiceGate, "The cistern is still refilling."));
            return;
        }

        SluiceChargesLeft--;
        SluiceReadyAtTick = Tick + stats.CooldownTicks;

        var affected = 0;
        foreach (var enemy in _enemies.Where(e => e.IsActive))
        {
            enemy.EndRaid(); // washed out of the field, too: it walks back to the road and starts again
            enemy.PushBack(stats.PushbackTiles);
            enemy.CancelSwing(); // washed off mid-swing
            enemy.ApplyMud(Tick + stats.MuddiedTicks);
            affected++;
        }

        _tickEvents.Add(new InterventionUsed(Tick, PlayerActionType.OpenSluiceGate, affected));
    }

    private void ReviveLeader()
    {
        if (Leader.TryRevive(Tick))
        {
            _tickEvents.Add(new LeaderRevived(Tick, Leader.Health));
        }
    }

    /// <summary>Orders are accepted while he is swooning; he sets off the moment he gets up.</summary>
    private void OrderLeader(GridPosition target)
    {
        if (!_layout.IsWalkableForLeader(target))
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.MoveLeader, "He is not walking into that."));
            return;
        }

        var route = Pathfinder.FindPath(
            _layout.Width, _layout.Height, Leader.RouteOrigin, target, p => !_layout.IsWalkableForLeader(p));
        if (route is null)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.MoveLeader, "There is no way through."));
            return;
        }

        Leader.SetRoute(route, target);
        _tickEvents.Add(new LeaderMoveOrdered(Tick, target));
    }

    /// <summary>Tin-can megaphone: nearby enemies hear a disembodied voice and walk back the way they came.</summary>
    private void UsePhantomVoice()
    {
        var stats = _balance.Leader;
        if (Leader.IsSwooning(Tick))
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.PhantomVoice, "He is face down in the mud."));
            return;
        }

        if (Tick < Leader.VoiceReadyAtTick)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.PhantomVoice, "He is still hoarse."));
            return;
        }

        Leader.VoiceReadyAtTick = Tick + stats.VoiceCooldownTicks;
        var radiusSquared = stats.VoiceRadius * stats.VoiceRadius;
        var affected = 0;
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || PositionOf(enemy).DistanceSquaredTo(Leader.Position) > radiusSquared)
            {
                continue;
            }

            enemy.Frighten(Tick + stats.VoiceFleeTicks);
            affected++;
        }

        _tickEvents.Add(new InterventionUsed(Tick, PlayerActionType.PhantomVoice, affected));
    }

    /// <summary>Hot Stone Therapy: heated river stones on a tile within reach. Burns, and muddies whoever survives.</summary>
    private void ThrowHotStones(GridPosition target)
    {
        var stats = _balance.HotStones;
        if (Leader.IsSwooning(Tick))
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.HotStones, "He is in no state to throw anything."));
            return;
        }

        if (Tick < Leader.HotStonesReadyAtTick)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.HotStones, "The stones are still heating."));
            return;
        }

        if (!_layout.IsInBounds(target) || target.ToVector().DistanceSquaredTo(Leader.Position) > stats.Reach * stats.Reach)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.HotStones, "He can't throw that far. He has tried."));
            return;
        }

        Leader.HotStonesReadyAtTick = Tick + stats.CooldownTicks;
        var center = target.ToVector();
        var radiusSquared = stats.Radius * stats.Radius;
        var affected = 0;
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || PositionOf(enemy).DistanceSquaredTo(center) > radiusSquared)
            {
                continue;
            }

            affected++;
            enemy.ApplyMud(Tick + stats.MuddiedTicks);
            DealDamage(enemy, ApplyResistance(enemy, stats.Damage));
        }

        _tickEvents.Add(new HotStonesThrown(Tick, target, affected));
    }

    /// <summary>A relic he can use right now, or why not (null: go ahead).</summary>
    private string? RelicProblem(PlayerActionType type, int readyAtTick)
    {
        if (!Has(RelicBook.For(type)))
        {
            return "He hasn't found that in the car yet.";
        }

        if (Leader.IsSwooning(Tick))
        {
            return "He is face down in the mud.";
        }

        return Tick < readyAtTick ? "Not yet: give it a moment." : null;
    }

    /// <summary>The Finger of God: a red dot near <paramref name="target"/>, and the raider nearest it is blinded where it stands.</summary>
    private void PointLaser(GridPosition target)
    {
        var stats = _balance.Relics;
        var problem = RelicProblem(PlayerActionType.LaserPointer, Leader.LaserReadyAtTick);
        if (problem == null && target.ToVector().DistanceSquaredTo(Leader.Position) > stats.LaserReach * stats.LaserReach)
        {
            problem = "The dot doesn't reach that far.";
        }

        Enemy? victim = null;
        if (problem == null)
        {
            var best = stats.LaserPick * stats.LaserPick;
            foreach (var enemy in _enemies)
            {
                var d = enemy.IsActive ? PositionOf(enemy).DistanceSquaredTo(target.ToVector()) : best + Fix.One;
                if (d <= best && (victim == null || d < best))
                {
                    victim = enemy;
                    best = d;
                }
            }

            problem = victim == null ? "Nobody there to dazzle." : null;
        }

        if (problem != null)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.LaserPointer, problem));
            return;
        }

        Leader.LaserReadyAtTick = Tick + stats.LaserCooldownTicks;
        victim!.CancelSwing();
        if (victim.Type == EnemyType.Bishop)
        {
            victim.EndRaid();
            victim.Frighten(Tick + stats.LaserBlindTicks * 2); // "It's a miracle." He leaves to report it.
        }
        else
        {
            victim.Blind(Tick + stats.LaserBlindTicks);
        }
        _tickEvents.Add(new RelicUsed(Tick, PlayerActionType.LaserPointer, target, victim.Id, 1));
    }

    /// <summary>Light without fire: while it shines, raiders near him are dazzled (slow, and can't see to fight him).</summary>
    private void CrankTorch()
    {
        var stats = _balance.Relics;
        var problem = RelicProblem(PlayerActionType.DynamoTorch, Leader.TorchReadyAtTick);
        if (problem != null)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.DynamoTorch, problem));
            return;
        }

        Leader.TorchReadyAtTick = Tick + stats.TorchCooldownTicks;
        Leader.TorchLitUntilTick = Tick + stats.TorchLitTicks;
        _tickEvents.Add(new RelicUsed(Tick, PlayerActionType.DynamoTorch, default, -1, 0));
    }

    /// <summary>Sun-fire: the magnifying glass on a tile within reach sets the raiders there alight. Needs the sun.</summary>
    private void FocusSunFire(GridPosition target)
    {
        var stats = _balance.Relics;
        var problem = RelicProblem(PlayerActionType.SunFire, Leader.SunFireReadyAtTick);
        if (problem == null && !IsSunUp)
        {
            problem = "The sun has set. It's a magnifying glass, not a miracle.";
        }

        if (problem == null && (!_layout.IsInBounds(target) || target.ToVector().DistanceSquaredTo(Leader.Position) > stats.SunFireReach * stats.SunFireReach))
        {
            problem = "Too far to focus.";
        }

        if (problem != null)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.SunFire, problem));
            return;
        }

        Leader.SunFireReadyAtTick = Tick + stats.SunFireCooldownTicks;
        var radiusSquared = stats.SunFireRadius * stats.SunFireRadius;
        var affected = 0;
        foreach (var enemy in _enemies)
        {
            if (enemy.IsActive && PositionOf(enemy).DistanceSquaredTo(target.ToVector()) <= radiusSquared)
            {
                enemy.Ignite(Tick + stats.SunFireBurnTicks);
                affected++;
            }
        }

        _tickEvents.Add(new RelicUsed(Tick, PlayerActionType.SunFire, target, -1, affected));
    }

    /// <summary>Burning raiders take fire damage every tick (resistances apply, blessings soften it).</summary>
    private void BurnEnemies()
    {
        if (_relics == Relic.None)
        {
            return;
        }

        var perTick = _balance.Relics.SunFireBurnPerSecond / BalanceTable.TicksPerSecond;
        foreach (var enemy in _enemies)
        {
            if (enemy.IsActive && enemy.IsBurning(Tick))
            {
                DealDamage(enemy, perTick);
            }
        }
    }

    /// <summary>In the dynamo torch's light: slowed, and no fighting the Prophet.</summary>
    private bool IsDazzled(Enemy enemy) =>
        Leader.IsTorchLit(Tick) && PositionOf(enemy).DistanceSquaredTo(Leader.Position) <= _balance.Relics.TorchRadius * _balance.Relics.TorchRadius;

    /// <summary>He hits whatever is closest to the Hearth within reach, with a 2024 umbrella.</summary>
    private void LeaderAttack()
    {
        if (Leader.AttackCooldownTicksRemaining > 0)
        {
            Leader.AttackCooldownTicksRemaining--;
        }

        if (Leader.AttackCooldownTicksRemaining > 0 || Leader.IsSwooning(Tick))
        {
            return;
        }

        var stats = _balance.Leader;
        var rangeSquared = stats.AttackRange * stats.AttackRange;
        Enemy? target = null;
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || PositionOf(enemy).DistanceSquaredTo(Leader.Position) > rangeSquared)
            {
                continue;
            }

            if (target is null || enemy.DistanceAlongPath > target.DistanceAlongPath)
            {
                target = enemy;
            }
        }

        if (target is null)
        {
            return;
        }

        Leader.AttackCooldownTicksRemaining = stats.AttackCooldownTicks;
        var damage = ApplyResistance(target, stats.AttackDamage);
        _tickEvents.Add(new LeaderAttacked(Tick, target.Id, damage));
        DealDamage(target, damage);
    }

    private void FireTowers()
    {
        foreach (var tower in _towers)
        {
            if (IsWreckedAt(tower.Position))
            {
                continue; // a wrecked tower is firewood
            }

            if (tower.CooldownTicksRemaining > 0)
            {
                tower.CooldownTicksRemaining--;
            }

            if (tower.CooldownTicksRemaining > 0)
            {
                continue;
            }

            var stats = _balance.TowerAt(tower.Type, tower.Level, tower.Manned);
            var target = FindTarget(tower, stats.Range);
            if (target is null)
            {
                continue;
            }

            tower.CooldownTicksRemaining = stats.CooldownTicks;
            if (stats.SplashRadius > Fix.Zero)
            {
                FireSplash(tower, stats, target);
            }
            else
            {
                FireSingle(tower, stats, target);
            }
        }
    }

    /// <summary>Targets the enemy furthest along the path; ties go to the lowest id (spawn order).</summary>
    private Enemy? FindTarget(Tower tower, Fix range)
    {
        var towerPosition = tower.Position.ToVector();
        var rangeSquared = range * range;
        Enemy? best = null;

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || PositionOf(enemy).DistanceSquaredTo(towerPosition) > rangeSquared)
            {
                continue;
            }

            if (best is null || enemy.DistanceAlongPath > best.DistanceAlongPath)
            {
                best = enemy;
            }
        }

        return best;
    }

    private void FireSingle(Tower tower, TowerStats stats, Enemy target)
    {
        var roll = Fix.Ratio(_combatRandom.Next(80, 121), 100);
        var critical = target.IsMuddied(Tick) && stats.MuddiedCritMultiplier > Fix.One;
        var damage = stats.Damage * roll;
        if (critical)
        {
            damage *= stats.MuddiedCritMultiplier;
        }

        damage = ApplyResistance(target, damage);
        _tickEvents.Add(new TowerFired(Tick, tower.Id, target.Id, damage, critical));
        DealDamage(target, damage);
        if (stats.PushbackTiles > Fix.Zero && target.IsActive)
        {
            target.EndRaid(); // washed back to the road, off whatever it was smashing or swinging at
            target.CancelSwing();
            target.PushBack(stats.PushbackTiles);
        }
    }

    private void FireSplash(Tower tower, TowerStats stats, Enemy target)
    {
        var center = PositionOf(target);
        var radiusSquared = stats.SplashRadius * stats.SplashRadius;
        _tickEvents.Add(new TowerFired(Tick, tower.Id, target.Id, stats.Damage, Critical: false));

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || PositionOf(enemy).DistanceSquaredTo(center) > radiusSquared)
            {
                continue;
            }

            enemy.ApplySlow(stats.SlowMultiplier, Tick + stats.SlowTicks);
            enemy.ApplyMud(Tick + stats.MuddiedTicks);
            DealDamage(enemy, ApplyResistance(enemy, stats.Damage));
        }
    }

    private Fix ApplyResistance(Enemy enemy, Fix damage) =>
        damage * (Fix.One - _balance.Enemies[enemy.Type].BluntResistance);

    private void DealDamage(Enemy enemy, Fix damage)
    {
        if (IsBlessed(enemy))
        {
            damage = damage * (Fix.One - _balance.BlessingReduction);
        }

        if (enemy.TakeDamage(damage))
        {
            _tickEvents.Add(new EnemyDefeated(Tick, enemy.Id, enemy.Type));
        }
    }

    /// <summary>A priest (or a bishop, further) within blessing range (not the raider himself) softens every blow.</summary>
    private bool IsBlessed(Enemy enemy)
    {
        var priest = _balance.BlessingRadius * _balance.BlessingRadius;
        var bishop = _balance.BishopBlessingRadius * _balance.BishopBlessingRadius;
        foreach (var other in _enemies)
        {
            if (other == enemy || !other.IsActive || (other.Type != EnemyType.Priest && other.Type != EnemyType.Bishop))
            {
                continue;
            }

            if (PositionOf(other).DistanceSquaredTo(PositionOf(enemy)) <= (other.Type == EnemyType.Bishop ? bishop : priest))
            {
                return true;
            }
        }

        return false;
    }

    private void MoveEnemies()
    {
        AssignLeaderSlots();
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

            if (_relics != Relic.None)
            {
                if (enemy.IsBlinded(Tick))
                {
                    enemy.CancelSwing(); // rubbing its eyes, going nowhere
                    continue;
                }

                if (IsDazzled(enemy))
                {
                    step *= _balance.Relics.TorchSlow;
                }
            }

            if (enemy.IsFrightened(Tick))
            {
                enemy.CancelSwing();
                enemy.EndRaid();
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

            if (enemy.HasLeaderSlot)
            {
                enemy.CancelSwing();
                enemy.EndRaid();
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

            // A cultist in its way: it stops and fights them (they're easier than the Prophet, and they're right there).
            if (_defenders.Count > 0 && DefenderInReach(enemy, stats) is { } defender)
            {
                enemy.CancelSwing();
                FightDefender(enemy, stats, defender);
                continue;
            }

            if (enemy.IsRaiding)
            {
                FollowRaid(enemy, stats, step);
                continue;
            }

            if (!enemy.IsOffRoad && (TrySmash(enemy, stats) || TryStartRaid(enemy, stats)))
            {
                continue;
            }

            if (enemy.IsOffRoad)
            {
                enemy.StepOffsetToward(default, step); // walk back to the road before marching on
                continue;
            }

            // A gate across the road: walk up to it, then hack until it falls.
            var blocker = RoadBlockerAhead(enemy);
            if (blocker != null)
            {
                var stop = Fix.FromInt(blocker.Value.Index - 1);
                if (enemy.DistanceAlongPath + step >= stop)
                {
                    if (enemy.DistanceAlongPath < stop)
                    {
                        enemy.Advance(stop - enemy.DistanceAlongPath);
                    }

                    BashRoadGate(enemy, stats, blocker.Value.Gate);
                    continue;
                }
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
    /// A raider on the road stops for one swing at each intact structure next to the road tile it is on (whole tiles,
    /// diagonals included; one at a time, each at most once), then marches on. Returns true while it is busy swinging.
    /// </summary>
    private bool TrySmash(Enemy enemy, EnemyStats stats)
    {
        if (stats.StructureDamage <= 0 || _structures.Count == 0)
        {
            return false;
        }

        var tile = RoadTileOf(enemy);
        if (enemy.SmashTargetId < 0)
        {
            foreach (var structure in _structures)
            {
                if (!structure.IsWrecked && !enemy.StructuresHit.Contains(structure.Id) && IsNextTo(tile, structure))
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

        var target = FindStructure(enemy.SmashTargetId);
        if (target == null || target.IsWrecked || !IsNextTo(tile, target))
        {
            enemy.CancelSwing(); // someone else wrecked it, or it was moved away: no point finishing the swing
            return false;
        }

        if (--enemy.SmashTicksRemaining > 0)
        {
            return true;
        }

        enemy.StructuresHit.Add(target.Id);
        enemy.CancelSwing();
        var wrecked = target.TakeHit(stats.StructureDamage);
        _tickEvents.Add(new StructureHit(Tick, target.Id, enemy.Id, stats.StructureDamage, target.Health));
        if (wrecked)
        {
            _tickEvents.Add(new StructureWrecked(Tick, target.Id));
        }

        return true;
    }

    /// <summary>
    /// From the road, a raider eyes the nearest intact structure it hasn't hit within <see cref="BalanceTable.RaidRange"/>
    /// and walks round whatever is in the way to stand beside it. If only barriers stand in the way, it goes for the first
    /// barrier on its way instead (one blow per barrier per raider). Returns true when it sets off.
    /// </summary>
    private bool TryStartRaid(Enemy enemy, EnemyStats stats)
    {
        if (stats.StructureDamage <= 0 || _structures.Count == 0)
        {
            return false;
        }

        var from = RoadTileOf(enemy);
        var range = _balance.RaidRange;
        Structure? best = null;
        var bestDistance = int.MaxValue;
        foreach (var structure in _structures)
        {
            if (structure.IsWrecked || enemy.StructuresHit.Contains(structure.Id) || (structure.IsBarrier && !enemy.WallBreaker))
            {
                continue; // only wall-breakers go out of their way for a fence or a wall
            }

            var distance = DistanceTo(from, structure);
            if (distance >= 2 && distance <= range && (distance < bestDistance || (distance == bestDistance && structure.Id < best!.Id)))
            {
                best = structure;
                bestDistance = distance;
            }
        }

        if (best == null)
        {
            return false;
        }

        var maxSteps = range * 3;
        var route = FindRaidRoute(from, best, t => IsPassable(t), maxSteps);
        if (route != null)
        {
            enemy.StartRaid(best.Id, route);
            return true;
        }

        // Walled off: walk up to the first barrier this raider hasn't hit yet on the way, and hack at that.
        var through = FindRaidRoute(from, best, t => IsPassable(t) || IsUnhitBarrier(enemy, t), maxSteps);
        if (through == null)
        {
            return false;
        }

        var cut = through.FindIndex(t => !IsPassable(t));
        var barrier = BarrierAt(through[cut])!;
        enemy.StartRaid(barrier.Id, through.GetRange(0, cut));
        return true;
    }

    /// <summary>The nearest intact barrier standing on the road ahead of the raider, with its index on the path.</summary>
    private (Structure Gate, int Index)? RoadBlockerAhead(Enemy enemy)
    {
        foreach (var (index, gate) in _roadBlockers)
        {
            if (!gate.IsWrecked && Fix.FromInt(index) > enemy.DistanceAlongPath)
            {
                return (gate, index);
            }
        }

        return null;
    }

    /// <summary>Hacks at a gate across the road: every swing lands, again and again, until it falls.</summary>
    private void BashRoadGate(Enemy enemy, EnemyStats stats, Structure gate)
    {
        if (enemy.SmashTargetId != gate.Id)
        {
            enemy.SmashTargetId = gate.Id;
            enemy.SmashTicksRemaining = _balance.StructureSwingTicks;
        }

        if (--enemy.SmashTicksRemaining > 0)
        {
            return;
        }

        enemy.CancelSwing();
        var wrecked = gate.TakeHit(stats.StructureDamage);
        _tickEvents.Add(new StructureHit(Tick, gate.Id, enemy.Id, stats.StructureDamage, gate.Health));
        if (wrecked)
        {
            _tickEvents.Add(new StructureWrecked(Tick, gate.Id));
        }
    }

    /// <summary>A number 0–99 from the seed and an id: the same raider rolls the same on every replay.</summary>
    private int Roll(int id)
    {
        var h = _seed ^ ((ulong)id * 0x9E3779B97F4A7C15UL);
        h ^= h >> 33;
        h *= 0xFF51AFD7ED558CCDUL;
        h ^= h >> 33;
        return (int)(h % 100);
    }

    /// <summary>Walks the raid route, swings once at the target, then walks the route back and steps onto the road.</summary>
    private void FollowRaid(Enemy enemy, EnemyStats stats, Fix step)
    {
        var route = enemy.RaidRoute!;
        var target = FindStructure(enemy.RaidTargetId);
        if (!enemy.RaidReturning && (target == null || target.IsWrecked))
        {
            enemy.CancelSwing();
            enemy.RaidReturning = true;
            enemy.RaidStep = Math.Min(enemy.RaidStep, route.Count) - 1;
        }

        if (!enemy.RaidReturning)
        {
            if (enemy.RaidStep < route.Count)
            {
                WalkTo(enemy, route[enemy.RaidStep], step, () => enemy.RaidStep++);
                return;
            }

            // Only swing standing beside it: anything that moved it (the road under it) sends it back to its spot.
            var spot = route.Count == 0 ? RoadPointOf(enemy) : route[route.Count - 1].ToVector();
            if (RoadPointOf(enemy) + enemy.Offset != spot || (route.Count == 0 && !IsNextTo(RoadTileOf(enemy), target!)))
            {
                enemy.CancelSwing();
                if (route.Count == 0)
                {
                    enemy.EndRaid();
                }
                else
                {
                    enemy.RaidStep = route.Count - 1;
                }

                return;
            }

            if (enemy.SmashTargetId != target!.Id)
            {
                enemy.SmashTargetId = target.Id;
                enemy.SmashTicksRemaining = _balance.StructureSwingTicks;
            }

            if (--enemy.SmashTicksRemaining > 0)
            {
                return;
            }

            enemy.StructuresHit.Add(target.Id);
            enemy.CancelSwing();
            var wrecked = target.TakeHit(stats.StructureDamage);
            _tickEvents.Add(new StructureHit(Tick, target.Id, enemy.Id, stats.StructureDamage, target.Health));
            if (wrecked)
            {
                _tickEvents.Add(new StructureWrecked(Tick, target.Id));
            }

            enemy.RaidReturning = true;
            enemy.RaidStep = route.Count - 2; // it stands on the last tile; walk back from the one before
            return;
        }

        if (enemy.RaidStep >= 0)
        {
            WalkTo(enemy, route[enemy.RaidStep], step, () => enemy.RaidStep--);
            return;
        }

        enemy.StepOffsetToward(default, step);
        if (!enemy.IsOffRoad)
        {
            enemy.EndRaid();
        }
    }

    private void WalkTo(Enemy enemy, GridPosition tile, Fix step, Action arrived)
    {
        var offset = tile.ToVector() - RoadPointOf(enemy);
        enemy.StepOffsetToward(offset, step);
        if (enemy.Offset == offset)
        {
            arrived();
        }
    }

    /// <summary>Shortest walk (4-way, at most <paramref name="maxSteps"/>) from a road tile to a tile beside the structure.</summary>
    private List<GridPosition>? FindRaidRoute(GridPosition from, Structure target, Func<GridPosition, bool> passable, int maxSteps)
    {
        var previous = new Dictionary<GridPosition, GridPosition> { [from] = from };
        var frontier = new Queue<(GridPosition Tile, int Steps)>();
        frontier.Enqueue((from, 0));
        while (frontier.Count > 0)
        {
            var (tile, steps) = frontier.Dequeue();
            if (tile != from && IsNextTo(tile, target) && passable(tile) && !InFootprint(tile, target))
            {
                var route = new List<GridPosition>();
                for (var t = tile; t != from; t = previous[t])
                {
                    route.Add(t);
                }

                route.Reverse();
                return route;
            }

            if (steps == maxSteps)
            {
                continue;
            }

            foreach (var next in new[] { new GridPosition(tile.X + 1, tile.Y), new GridPosition(tile.X, tile.Y + 1), new GridPosition(tile.X, tile.Y - 1), new GridPosition(tile.X - 1, tile.Y) })
            {
                if (!previous.ContainsKey(next) && _layout.IsInBounds(next) && next != _layout.Hearth && passable(next))
                {
                    previous[next] = tile;
                    frontier.Enqueue((next, steps + 1));
                }
            }
        }

        return null;
    }

    /// <summary>Open ground, the road, or a barrier someone already broke.</summary>
    private bool IsPassable(GridPosition tile) => !_layout.IsBlocked(tile) || BarrierAt(tile) is { IsWrecked: true };

    private bool IsUnhitBarrier(Enemy enemy, GridPosition tile) => BarrierAt(tile) is { IsWrecked: false } b && !enemy.StructuresHit.Contains(b.Id);

    private Structure? BarrierAt(GridPosition tile)
    {
        foreach (var structure in _structures)
        {
            if (structure.IsBarrier && InFootprint(tile, structure))
            {
                return structure;
            }
        }

        return null;
    }

    private static bool InFootprint(GridPosition tile, Structure structure)
    {
        foreach (var part in structure.Tiles)
        {
            if (part == tile)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whole-tile distance (diagonals count one) from a tile to the nearest tile of a structure.</summary>
    private static int DistanceTo(GridPosition tile, Structure structure)
    {
        var best = int.MaxValue;
        foreach (var part in structure.Tiles)
        {
            best = Math.Min(best, Math.Max(Math.Abs(part.X - tile.X), Math.Abs(part.Y - tile.Y)));
        }

        return best;
    }

    /// <summary>The road tile a raider is on (its distance along the path, rounded to the nearest tile).</summary>
    private GridPosition RoadTileOf(Enemy enemy)
    {
        var index = (enemy.DistanceAlongPath + Fix.Ratio(1, 2)).FloorToInt();
        return Path[Math.Min(Math.Max(index, 0), Path.Count - 1)];
    }

    private static bool IsNextTo(GridPosition a, GridPosition b) => Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;

    /// <summary>Next to any tile of the structure's footprint.</summary>
    private static bool IsNextTo(GridPosition tile, Structure structure)
    {
        foreach (var part in structure.Tiles)
        {
            if (IsNextTo(tile, part))
            {
                return true;
            }
        }

        return false;
    }

    private Structure? FindStructure(int id)
    {
        foreach (var structure in _structures)
        {
            if (structure.Id == id)
            {
                return structure;
            }
        }

        return null;
    }

    private bool IsWreckedAt(GridPosition tile)
    {
        foreach (var structure in _structures)
        {
            if (structure.IsWrecked && structure.Position == tile)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Hands out the Prophet's engaged slots (at most MaxEngaged): raiders that held one last tick keep it first, then
    /// newcomers in list order — so a raider already fighting is never bumped by one still walking over.
    /// </summary>
    private void AssignLeaderSlots()
    {
        var engaged = 0;
        foreach (var enemy in _enemies)
        {
            var held = enemy.HasLeaderSlot;
            enemy.HasLeaderSlot = false;
            if (held && Contends(enemy) && engaged < _balance.Leader.MaxEngaged)
            {
                enemy.HasLeaderSlot = true;
                engaged++;
            }
        }

        foreach (var enemy in _enemies)
        {
            if (engaged >= _balance.Leader.MaxEngaged)
            {
                return;
            }

            if (!enemy.HasLeaderSlot && Contends(enemy))
            {
                enemy.HasLeaderSlot = true;
                engaged++;
            }
        }
    }

    private bool Contends(Enemy enemy) =>
        enemy.IsActive && !enemy.IsFrightened(Tick) && (_relics == Relic.None || (!enemy.IsBlinded(Tick) && !IsDazzled(enemy)))
        && WantsLeader(enemy, _balance.Enemies[enemy.Type]);

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

    /// <summary>Stops (on or off the road) and swings or throws at the Prophet. Holding them still is what towers like.</summary>
    // ---------------- the cultists out defending (spec 2026-10-15) ----------------

    /// <summary>
    /// Each cultist up and about goes for the raider nearest them within guard range of their post, hits it when in
    /// reach, and otherwise walks over (re-planning a couple of times a second); with nobody to fight, back to the post.
    /// </summary>
    private void DefendersAct()
    {
        if (_defenders.Count == 0)
        {
            return;
        }

        var stats = _balance.Defender;
        var guard = stats.GuardRadius * stats.GuardRadius;
        var reach = stats.AttackRange * stats.AttackRange;
        foreach (var defender in _defenders)
        {
            if (defender.TryRevive(Tick))
            {
                _tickEvents.Add(new DefenderBack(Tick, defender.Id));
            }

            if (!defender.IsUp)
            {
                continue;
            }

            if (defender.AttackCooldownTicksRemaining > 0)
            {
                defender.AttackCooldownTicksRemaining--;
            }

            Enemy? target = null;
            var best = Fix.Zero;
            var post = defender.Post.ToVector();
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsActive || enemy.IsFrightened(Tick))
                {
                    continue;
                }

                var at = PositionOf(enemy);
                if (at.DistanceSquaredTo(post) > guard)
                {
                    continue;
                }

                var d = at.DistanceSquaredTo(defender.Position);
                if (target == null || d < best)
                {
                    target = enemy;
                    best = d;
                }
            }

            defender.TargetId = target?.Id ?? -1;
            if (target != null && best <= reach)
            {
                defender.Stop();
                if (defender.AttackCooldownTicksRemaining == 0)
                {
                    defender.AttackCooldownTicksRemaining = stats.AttackCooldownTicks;
                    var damage = ApplyResistance(target, stats.AttackDamage);
                    _tickEvents.Add(new DefenderAttacked(Tick, defender.Id, target.Id, damage));
                    DealDamage(target, damage);
                }
            }
            else
            {
                var goal = target != null ? TileOf(PositionOf(target)) : defender.Post;
                var home = target == null && !defender.IsMoving && TileOf(defender.Position) == defender.Post;
                if (!home && (goal != defender.RoutedTo || Tick - defender.RoutedAtTick >= 10))
                {
                    var route = Pathfinder.FindPath(_layout.Width, _layout.Height, defender.RouteOrigin, goal, p => !_layout.IsWalkableForLeader(p) && p != goal);
                    if (route != null)
                    {
                        defender.SetRoute(route);
                    }

                    defender.RoutedTo = goal;
                    defender.RoutedAtTick = Tick;
                }

                defender.Walk(stats.TilesPerSecond / BalanceTable.TicksPerSecond);
            }
        }
    }

    /// <summary>The first cultist (lowest id) standing within the raider's swing, or null.</summary>
    private Defender? DefenderInReach(Enemy enemy, EnemyStats stats)
    {
        if (stats.EngageRange <= Fix.Zero)
        {
            return null; // archers and the like don't brawl
        }

        var range = Fix.Max(stats.EngageRange, _balance.Defender.AttackRange);
        var at = PositionOf(enemy);
        foreach (var defender in _defenders)
        {
            if (defender.IsUp && at.DistanceSquaredTo(defender.Position) <= range * range)
            {
                return defender;
            }
        }

        return null;
    }

    private void FightDefender(Enemy enemy, EnemyStats stats, Defender defender)
    {
        if (enemy.LeaderAttackCooldownTicksRemaining > 0)
        {
            enemy.LeaderAttackCooldownTicksRemaining--;
            return;
        }

        enemy.LeaderAttackCooldownTicksRemaining = stats.LeaderAttackCooldownTicks;
        var downed = defender.TakeHit(Tick, stats.LeaderDamage, _balance.Defender.DownTicks);
        _tickEvents.Add(new DefenderHit(Tick, defender.Id, enemy.Id, stats.LeaderDamage, defender.Health));
        if (downed)
        {
            _tickEvents.Add(new DefenderDowned(Tick, defender.Id));
        }
    }

    private static GridPosition TileOf(FixVector2 at) =>
        new((at.X + Fix.Ratio(1, 2)).FloorToInt(), (at.Y + Fix.Ratio(1, 2)).FloorToInt());

    // ---------------- relics that fire themselves (owner 2026-10-15) ----------------

    /// <summary>
    /// Ready relics fire when it's worth it: the laser at the raider within reach doing the most harm (at a building,
    /// else furthest along the road), the torch when raiders crowd him, the magnifier on the thickest knot of raiders.
    /// </summary>
    private void FireReadyRelics()
    {
        if (Leader.IsSwooning(Tick))
        {
            return;
        }

        var stats = _balance.Relics;
        if (IsLaserReady)
        {
            var reach = (stats.LaserReach - Fix.Ratio(1, 2)) * (stats.LaserReach - Fix.Ratio(1, 2));
            Enemy? pick = null;
            var pickHarm = -1;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsActive || enemy.IsBlinded(Tick) || enemy.IsFrightened(Tick) || PositionOf(enemy).DistanceSquaredTo(Leader.Position) > reach)
                {
                    continue;
                }

                var harm = enemy.SmashTargetId >= 0 || enemy.IsRaiding ? 1 : 0;
                if (pick == null || harm > pickHarm || (harm == pickHarm && enemy.DistanceAlongPath > pick.DistanceAlongPath))
                {
                    pick = enemy;
                    pickHarm = harm;
                }
            }

            if (pick != null)
            {
                PointLaser(TileOf(PositionOf(pick)));
            }
        }

        if (IsTorchReady && !Leader.IsTorchLit(Tick))
        {
            var radius = stats.TorchRadius * stats.TorchRadius;
            var crowd = 0;
            foreach (var enemy in _enemies)
            {
                if (enemy.IsActive && PositionOf(enemy).DistanceSquaredTo(Leader.Position) <= radius)
                {
                    crowd++;
                }
            }

            if (crowd >= _balance.TorchAutoCrowd)
            {
                CrankTorch();
            }
        }

        if (IsSunFireReady)
        {
            var reach = (stats.SunFireReach - Fix.Ratio(1, 2)) * (stats.SunFireReach - Fix.Ratio(1, 2));
            var radius = stats.SunFireRadius * stats.SunFireRadius;
            Enemy? centre = null;
            var most = 0;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsActive || enemy.IsBurning(Tick) || PositionOf(enemy).DistanceSquaredTo(Leader.Position) > reach)
                {
                    continue;
                }

                var count = 0;
                foreach (var other in _enemies)
                {
                    if (other.IsActive && PositionOf(other).DistanceSquaredTo(PositionOf(enemy)) <= radius)
                    {
                        count++;
                    }
                }

                if (count > most)
                {
                    centre = enemy;
                    most = count;
                }
            }

            if (centre != null && _layout.IsInBounds(TileOf(PositionOf(centre))))
            {
                FocusSunFire(TileOf(PositionOf(centre)));
            }
        }
    }

    /// <summary>
    /// Where Hot Stones would do the most (the client's aim for a tap on the card): the tile within his reach with the
    /// most raiders under the splash, or null when nobody is in reach.
    /// </summary>
    public GridPosition? BestHotStonesTarget()
    {
        var stats = _balance.HotStones;
        var reach = (stats.Reach - Fix.Ratio(1, 2)) * (stats.Reach - Fix.Ratio(1, 2));
        var radius = stats.Radius * stats.Radius;
        GridPosition? best = null;
        var most = 0;
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsActive || PositionOf(enemy).DistanceSquaredTo(Leader.Position) > reach)
            {
                continue;
            }

            var tile = TileOf(PositionOf(enemy));
            var count = 0;
            foreach (var other in _enemies)
            {
                if (other.IsActive && PositionOf(other).DistanceSquaredTo(tile.ToVector()) <= radius)
                {
                    count++;
                }
            }

            if (count > most && _layout.IsInBounds(tile))
            {
                best = tile;
                most = count;
            }
        }

        return best;
    }

    private void FightLeader(Enemy enemy, EnemyStats stats)
    {
        enemy.IsFightingLeader = true;
        if (enemy.LeaderAttackCooldownTicksRemaining > 0)
        {
            enemy.LeaderAttackCooldownTicksRemaining--;
            return;
        }

        enemy.LeaderAttackCooldownTicksRemaining = stats.LeaderAttackCooldownTicks;
        var swooned = Leader.TakeHit(Tick, stats.LeaderDamage, _balance.Leader.SwoonTicks);
        _tickEvents.Add(new LeaderHit(Tick, enemy.Id, enemy.Type, stats.LeaderDamage, Leader.Health));
        if (swooned)
        {
            _tickEvents.Add(new LeaderSwooned(Tick));
        }
    }

    private void HashState()
    {
        _hasher.Add(Tick);
        _hasher.Add(HearthHealth);
        _hasher.Add(SluiceChargesLeft);
        foreach (var enemy in _enemies)
        {
            _hasher.Add(enemy.Id);
            _hasher.Add((int)enemy.State);
            _hasher.Add(enemy.Health.Raw);
            _hasher.Add(enemy.DistanceAlongPath.Raw);
            _hasher.Add(enemy.Offset.X.Raw);
            _hasher.Add(enemy.Offset.Y.Raw);
            _hasher.Add(enemy.MuddiedUntilTick);
            _hasher.Add(enemy.SlowedUntilTick);
            _hasher.Add(enemy.FrightenedUntilTick);
            _hasher.Add(enemy.IsFightingLeader ? 1 : 0);
            _hasher.Add(enemy.HasLeaderSlot ? 1 : 0);
            if (_structures.Count > 0)
            {
                // Only villages have structures: layouts without them (the demo) keep their historic hashes.
                _hasher.Add(enemy.SmashTicksRemaining);
                _hasher.Add(enemy.SmashTargetId);
                _hasher.Add(enemy.StructuresHit.Count);
                _hasher.Add(enemy.RaidTargetId);
                _hasher.Add(enemy.RaidStep);
                _hasher.Add(enemy.RaidReturning ? 1 : 0);
                _hasher.Add(enemy.WallBreaker ? 1 : 0);
            }
            _hasher.Add(enemy.LeaderAttackCooldownTicksRemaining);
            if (_relics != Relic.None)
            {
                _hasher.Add(enemy.BlindedUntilTick);
                _hasher.Add(enemy.BurningUntilTick);
            }
        }

        Leader.AddToHash(_hasher);
        if (_relics != Relic.None)
        {
            _hasher.Add((int)_relics);
            Leader.AddRelicsToHash(_hasher);
        }

        foreach (var tower in _towers)
        {
            _hasher.Add(tower.CooldownTicksRemaining);
        }

        foreach (var defender in _defenders)
        {
            defender.AddToHash(_hasher); // none in Nights planned before: their hashes are as they were
        }

        foreach (var structure in _structures)
        {
            _hasher.Add(structure.Health);
        }
    }

    private void CheckForEnd()
    {
        BattleOutcome outcome;
        if (HearthHealth <= 0)
        {
            outcome = BattleOutcome.Lost;
        }
        else if ((_nextSpawnIndex == _spawns.Count && !_enemies.Any(e => e.IsActive)) || Tick + 1 >= _balance.MaxTicks)
        {
            outcome = BattleOutcome.Won;
        }
        else
        {
            return;
        }

        var stars = CalculateStars(outcome);
        Result = new BattleResult(
            outcome,
            stars,
            HearthHealth,
            HearthMaxHealth,
            TicksElapsed: Tick + 1,
            EnemiesDefeated: _enemies.Count(e => e.State == EnemyState.Defeated),
            EnemiesLeaked: _enemies.Count(e => e.State == EnemyState.ReachedHearth),
            _hasher.Value);
        _tickEvents.Add(new BattleEnded(Tick, outcome, stars));
    }

    /// <summary>GDD 01: 3★ ≥ 80% Hearth, 2★ ≥ 40%, 1★ survived. Morale is not simulated yet.</summary>
    private int CalculateStars(BattleOutcome outcome)
    {
        if (outcome == BattleOutcome.Lost)
        {
            return 0;
        }

        var percent = HearthHealth * 100 / HearthMaxHealth;
        return percent >= 80 ? 3 : percent >= 40 ? 2 : 1;
    }
}
