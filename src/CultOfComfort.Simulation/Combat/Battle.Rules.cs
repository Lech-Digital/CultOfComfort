using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Numerics;
using CultOfComfort.Simulation.Waves;

namespace CultOfComfort.Simulation.Combat;

/// <summary>
/// A Night that needs you (spec 2026-11-17 fun first, C), for Nights fought by rules 1 (balance 2026.11.1): a wave waits
/// for the last to be cleared; in the gap three boons are on offer and the next wave can be called early; stars are earned,
/// the third with the Night's objective; the first Night wrecks one building at most. Rules 0 is untouched, so every
/// Night planned before replays as it was.
/// </summary>
public sealed partial class Battle
{
    /// <summary>The rules this file adds (the balance version 2026.11.1).</summary>
    public const int FunFirstRules = 1;

    /// <summary>The breather between waves (C5: 8 seconds before, 10 now).</summary>
    public const int GapTicks = 10 * BalanceTable.TicksPerSecond;

    /// <summary>A wave's raiders come further apart than before, so it takes a while and the towers do the killing (C4).</summary>
    public const int SpawnStretch = 2;

    /// <summary>Each early call pays this much more salvage (C3).</summary>
    public const int EarlyCallSalvagePercent = 10;

    /// <summary>The umbrellas patch a tenth of a building every two seconds.</summary>
    private const int MendEveryTicks = 2 * BalanceTable.TicksPerSecond;

    private static readonly Boon[] AllBoons = { Boon.ChuteTape, Boon.FullVolume, Boon.Umbrellas, Boon.HotterStones };

    private int _rules;
    private NightObjective _objective;
    private int _maxWrecks;
    private readonly Dictionary<int, int> _waveFirstTick = new();
    private int _waveStartTick;
    private bool _gapOpen;
    private int _nextWaveAt;
    private readonly List<Boon> _offer = new();
    private Boon? _picked;
    private bool _swooned;
    private int _bestStones;

    /// <summary>Which rules tonight is fought by (0: as before).</summary>
    public int Rules => _rules;

    /// <summary>The third star's condition tonight.</summary>
    public NightObjective Objective => _objective;

    /// <summary>Between waves: the next one waits (rules 1+). Before the first wave too, without boons.</summary>
    public bool IsGapOpen => _rules >= FunFirstRules && _gapOpen && CurrentWave >= 1;

    /// <summary>When the next wave comes if nobody calls it.</summary>
    public int NextWaveAtTick => _nextWaveAt;

    /// <summary>The boons on offer in this gap (empty once one is picked, or outside a gap).</summary>
    public IReadOnlyList<Boon> BoonOffer => _picked == null ? _offer : Array.Empty<Boon>();

    /// <summary>The boon picked for the coming wave, or the one helping the wave now on.</summary>
    public Boon? PickedBoon => _picked;

    public Boon? ActiveBoon { get; private set; }

    /// <summary>How many waves were called early tonight.</summary>
    public int EarlyCalls { get; private set; }

    /// <summary>Whether the objective is (still) met as things stand.</summary>
    public bool ObjectiveMet => _objective switch
    {
        NightObjective.NothingWrecked => !_structures.Any(s => s.IsWrecked && !s.IsBarrier),
        NightObjective.ProphetNeverSwoons => !_swooned,
        NightObjective.ThreeInOneStone => _bestStones >= 3,
        _ => true,
    };

    /// <summary>The objective a Night has, from its number: they take turns, so the Dusk can say it in advance.</summary>
    public static NightObjective ObjectiveFor(int night) => ((Math.Max(1, night) - 1) % 3) switch
    {
        0 => NightObjective.NothingWrecked,
        1 => NightObjective.ProphetNeverSwoons,
        _ => NightObjective.ThreeInOneStone,
    };

    /// <summary>The three boons offered before <paramref name="wave"/>: drawn from the seed, so replays agree.</summary>
    public static IReadOnlyList<Boon> BoonsFor(ulong seed, int wave)
    {
        var random = new PcgRandom(seed + (ulong)wave * 7919UL, RandomStreams.Boons);
        var pool = AllBoons.ToList();
        var offer = new List<Boon>();
        while (offer.Count < 3 && pool.Count > 0)
        {
            var i = random.Next(0, pool.Count);
            offer.Add(pool[i]);
            pool.RemoveAt(i);
        }

        return offer;
    }

    private void InitRules(BattleSetup setup)
    {
        _rules = setup.Rules;
        _objective = setup.Rules >= FunFirstRules ? setup.Objective : NightObjective.None;
        _maxWrecks = setup.MaxWrecks;
        if (_rules < FunFirstRules)
        {
            return;
        }

        foreach (var spawn in _spawns)
        {
            if (!_waveFirstTick.ContainsKey(spawn.Wave))
            {
                _waveFirstTick[spawn.Wave] = spawn.Tick;
            }
        }

        _gapOpen = true; // before the first wave: it comes when it always did
        _nextWaveAt = _spawns.Count > 0 ? _spawns[0].Tick : 0;
    }

    /// <summary>Rules 1: the current wave's raiders, spaced from when it began; the next wave only after a gap.</summary>
    private void SpawnByRules()
    {
        if (_gapOpen)
        {
            if (Tick < _nextWaveAt || _nextSpawnIndex >= _spawns.Count)
            {
                return;
            }

            CurrentWave = _spawns[_nextSpawnIndex].Wave;
            _waveStartTick = Tick;
            _gapOpen = false;
            ActiveBoon = _picked;
            _picked = null;
            _offer.Clear();
            _tickEvents.Add(new WaveStarted(Tick, CurrentWave));
        }

        while (_nextSpawnIndex < _spawns.Count)
        {
            var spawn = _spawns[_nextSpawnIndex];
            if (spawn.Wave != CurrentWave || _waveStartTick + (spawn.Tick - _waveFirstTick[spawn.Wave]) * SpawnStretch > Tick)
            {
                break;
            }

            _nextSpawnIndex++;
            SpawnOne(spawn);
        }
    }

    /// <summary>After everything moved this tick: open a gap once the wave is spent, mend, and keep the objective's score.</summary>
    private void TrackRules()
    {
        if (_rules < FunFirstRules)
        {
            return;
        }

        foreach (var e in _tickEvents)
        {
            switch (e)
            {
                case LeaderSwooned:
                    _swooned = true;
                    break;
                case HotStonesThrown thrown:
                    _bestStones = Math.Max(_bestStones, thrown.EnemiesAffected);
                    break;
            }
        }

        if (ActiveBoon == Boon.Umbrellas && !_gapOpen && (Tick - _waveStartTick) % MendEveryTicks == MendEveryTicks - 1)
        {
            foreach (var structure in _structures)
            {
                if (!structure.IsWrecked && structure.Health < structure.MaxHealth)
                {
                    structure.Mend(Math.Max(1, structure.MaxHealth / 10));
                    _tickEvents.Add(new StructureMended(Tick, structure.Id, structure.Health));
                }
            }
        }

        var waveSpent = _nextSpawnIndex >= _spawns.Count || _spawns[_nextSpawnIndex].Wave != CurrentWave;
        if (!_gapOpen && waveSpent && _nextSpawnIndex < _spawns.Count && !_enemies.Any(e => e.IsActive))
        {
            _gapOpen = true;
            _nextWaveAt = Tick + GapTicks;
            ActiveBoon = null; // a boon lasts its wave
            _offer.Clear();
            _offer.AddRange(BoonsFor(_seed, CurrentWave + 1));
            _tickEvents.Add(new WaveGap(Tick, CurrentWave + 1, _offer.ToList(), _nextWaveAt));
        }
    }

    private void CallWave()
    {
        if (!IsGapOpen)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.CallWave, "There's a wave on already."));
            return;
        }

        _nextWaveAt = Tick;
        EarlyCalls++;
        _tickEvents.Add(new WaveCalled(Tick, CurrentWave + 1));
    }

    private void PickBoon(int index)
    {
        if (!IsGapOpen || _picked != null || index < 0 || index >= _offer.Count)
        {
            _tickEvents.Add(new InputRejected(Tick, PlayerActionType.PickBoon, "No boon to pick."));
            return;
        }

        _picked = _offer[index];
        _tickEvents.Add(new BoonPicked(Tick, _picked.Value));
    }

    /// <summary>Whether a hit may wreck this structure: not once the first Night's one building is down (gates always may).</summary>
    private bool MayWreck(Structure structure) =>
        _maxWrecks <= 0 || structure.IsBarrier || _structures.Count(s => s.IsWrecked && !s.IsBarrier) < _maxWrecks;

    private int TowerCooldown(int ticks) => ActiveBoon == Boon.ChuteTape ? ticks * 2 / 3 : ticks;

    private int VoiceCooldown(int ticks) => ActiveBoon == Boon.FullVolume ? ticks / 2 : ticks;

    private int StonesCooldown(int ticks) => ActiveBoon == Boon.HotterStones ? ticks / 2 : ticks;

    /// <summary>
    /// Rules 1 (C1): 1★ the Hearth stands; 2★ above 60%; 3★ above 80% and the objective. A Night nobody touched (and so an
    /// auto-resolve) gets one star at most.
    /// </summary>
    private int StarsByRules(BattleOutcome outcome)
    {
        if (outcome == BattleOutcome.Lost)
        {
            return 0;
        }

        if (_inputLog.Count == 0)
        {
            return 1;
        }

        var percent = HearthHealth * 100 / HearthMaxHealth;
        return percent > 80 && ObjectiveMet ? 3 : percent > 60 ? 2 : 1;
    }

    private void HashRules()
    {
        if (_rules < FunFirstRules)
        {
            return;
        }

        _hasher.Add(CurrentWave);
        _hasher.Add(_gapOpen ? 1 : 0);
        _hasher.Add(_nextWaveAt);
        _hasher.Add(_picked.HasValue ? (int)_picked.Value + 1 : 0);
        _hasher.Add(ActiveBoon.HasValue ? (int)ActiveBoon.Value + 1 : 0);
        _hasher.Add(EarlyCalls);
    }
}
