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
    public void Intervene(InterventionType type)
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("The battle is already finished.");
        }

        var insertAt = _pendingInputs.FindLastIndex(input => input.Tick <= Tick) + 1;
        _pendingInputs.Insert(insertAt, new PlayerInput(Tick, type));
    }

    public void Step()
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("The battle is already finished.");
        }

        _tickEvents.Clear();
        SpawnEnemies();
        ApplyInputs();
        FireTowers();
        MoveEnemies();
        HashState();
        CheckForEnd();
        Tick++;
    }

    public FixVector2 PositionOf(Enemy enemy)
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
                case InterventionType.OpenSluiceGate:
                    OpenSluiceGate();
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
            _tickEvents.Add(new InputRejected(Tick, InterventionType.OpenSluiceGate, "The cistern is empty."));
            return;
        }

        if (Tick < SluiceReadyAtTick)
        {
            _tickEvents.Add(new InputRejected(Tick, InterventionType.OpenSluiceGate, "The cistern is still refilling."));
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

        _tickEvents.Add(new InterventionUsed(Tick, InterventionType.OpenSluiceGate, affected));
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
        foreach (var enemy in _enemies)
        {
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
            _hasher.Add(enemy.MuddiedUntilTick);
            _hasher.Add(enemy.SlowedUntilTick);
        }

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
