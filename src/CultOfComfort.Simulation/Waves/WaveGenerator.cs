using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.Waves;

public sealed record SpawnEntry(int Tick, EnemyType Type, int Wave);

/// <summary>Deterministic wave composition from (budget, seed). Shared by client preview, client play and server replay.</summary>
public static class WaveGenerator
{
    private const int FirstWaveStartTick = 2 * BalanceTable.TicksPerSecond;
    private const int WaveGapTicks = 8 * BalanceTable.TicksPerSecond;
    private const int MinSpawnIntervalTicks = 12;
    private const int MaxSpawnIntervalTicks = 25;

    public static IReadOnlyList<SpawnEntry> Generate(int budget, int waveCount, ulong seed, BalanceTable balance)
    {
        if (budget <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(budget), "Raid budget must be positive.");
        }

        if (waveCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waveCount), "A raid needs at least one wave.");
        }

        var random = new PcgRandom(seed, RandomStreams.Waves);
        var levyCost = balance.Enemies[EnemyType.Levy].WaveCost;
        var heavyCost = balance.Enemies[EnemyType.ManAtArms].WaveCost;
        var zealotCost = balance.Enemies[EnemyType.Zealot].WaveCost;
        var weightTotal = waveCount * (waveCount + 1) / 2;
        var spawns = new List<SpawnEntry>();
        var tick = FirstWaveStartTick;
        var spent = 0;

        for (var wave = 1; wave <= waveCount; wave++)
        {
            // Later waves get a larger share; the last wave absorbs rounding.
            var waveBudget = wave == waveCount ? budget - spent : budget * wave / weightTotal;
            spent += waveBudget;

            var remaining = waveBudget;
            var heavyChancePercent = 15 + 10 * wave;

            // Zealots join from wave 2: by then the Prophet has usually wandered somewhere they can see him.
            var zealotChancePercent = wave >= 2 ? 20 : 0;
            var isFirstInWave = true;

            while (remaining >= levyCost)
            {
                var roll = random.Next(0, 100);
                var type = roll < heavyChancePercent && remaining >= heavyCost ? EnemyType.ManAtArms
                    : roll >= 100 - zealotChancePercent && remaining >= zealotCost ? EnemyType.Zealot
                    : EnemyType.Levy;
                remaining -= balance.Enemies[type].WaveCost;

                if (!isFirstInWave)
                {
                    tick += random.Next(MinSpawnIntervalTicks, MaxSpawnIntervalTicks);
                }

                spawns.Add(new SpawnEntry(tick, type, wave));
                isFirstInWave = false;
            }

            tick += WaveGapTicks;
        }

        return spawns;
    }
}
