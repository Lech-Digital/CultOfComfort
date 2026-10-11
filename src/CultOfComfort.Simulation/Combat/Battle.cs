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
    private readonly BattleLayout _layout;
    private readonly PcgRandom _combatRandom;
    private readonly IReadOnlyList<SpawnEntry> _spawns;
    private readonly List<PlayerInput> _pendingInputs;
    private readonly List<PlayerInput> _inputLog = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<Tower> _towers = new();
    private readonly List<BattleEvent> _tickEvents = new();
    private readonly Fnv1aHasher _hasher = new();
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
        _layout = setup.Layout;
        Leader = new CultLeader(setup.Layout.LeaderStart, _balance.Leader.MaxHealth);
        _combatRandom = new PcgRandom(setup.Seed, RandomStreams.Combat);
        _spawns = WaveGenerator.Generate(setup.Budget, setup.WaveCount, setup.Seed, _balance);
        _pendingInputs = setup.Inputs.OrderBy(input => input.Tick).ToList();

        Path = setup.Layout.ResolveEnemyPath();
        PathLength = Fix.FromInt(Path.Count - 1);
        WaveCount = setup.WaveCount;
        TotalEnemies = _spawns.Count;
        HearthHealth = _balance.HearthMaxHealth;
        SluiceChargesLeft = _balance.SluiceGate.Charges;

        var towerId = 1;
        foreach (var placed in setup.Layout.Towers)
        {
            _towers.Add(new Tower(towerId++, placed.Type, placed.Position));
        }
    }

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
        FireTowers();
        LeaderAttack();
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
            enemy.PushBack(stats.PushbackTiles);
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
            if (tower.CooldownTicksRemaining > 0)
            {
                tower.CooldownTicksRemaining--;
            }

            if (tower.CooldownTicksRemaining > 0)
            {
                continue;
            }

            var stats = _balance.Towers[tower.Type];
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
        if (enemy.TakeDamage(damage))
        {
            _tickEvents.Add(new EnemyDefeated(Tick, enemy.Id, enemy.Type));
        }
    }

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

    /// <summary>Stops (on or off the road) and swings or throws at the Prophet. Holding them still is what towers like.</summary>
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
            _hasher.Add(enemy.LeaderAttackCooldownTicksRemaining);
        }

        Leader.AddToHash(_hasher);

        foreach (var tower in _towers)
        {
            _hasher.Add(tower.CooldownTicksRemaining);
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
