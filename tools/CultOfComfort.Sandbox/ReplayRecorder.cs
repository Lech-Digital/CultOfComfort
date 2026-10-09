using System.Text.Json;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Sandbox;

/// <summary>Captures per-tick frames from a live battle and bakes them into the HTML viewer.</summary>
internal sealed class ReplayRecorder
{
    private readonly List<object> _frames = [];
    private readonly List<object> _log = [];
    private readonly Chronicle _chronicle = new();

    public void Capture(Battle battle)
    {
        var tick = battle.Tick - 1; // Step() has already advanced the counter.
        var shots = new List<double[]>();
        var sluice = false;

        foreach (var battleEvent in battle.LastTickEvents)
        {
            if (battleEvent is TowerFired fired)
            {
                var target = battle.Enemies.First(e => e.Id == fired.TargetId);
                var position = battle.PositionOf(target);
                shots.Add([fired.TowerId, Round(position.X.ToDouble()), Round(position.Y.ToDouble()), fired.Critical ? 1 : 0]);
            }

            sluice |= battleEvent is InterventionUsed { Type: PlayerActionType.OpenSluiceGate };

            var line = _chronicle.Describe(battleEvent);
            if (line is not null)
            {
                _log.Add(new { t = tick, text = line, kind = battleEvent.GetType().Name });
            }
        }

        var enemies = battle.Enemies
            .Where(e => e.IsActive)
            .Select(e =>
            {
                var position = battle.PositionOf(e);
                var flags = (e.IsMuddied(tick) ? 1 : 0) | (e.IsSlowed(tick) ? 2 : 0);
                return new double[]
                {
                    e.Id, (int)e.Type, Round(position.X.ToDouble()), Round(position.Y.ToDouble()),
                    Round(e.Health.ToDouble() / e.MaxHealth.ToDouble()), flags,
                };
            })
            .ToList();

        _frames.Add(new
        {
            hearth = battle.HearthHealth,
            wave = battle.CurrentWave,
            charges = battle.SluiceChargesLeft,
            enemies,
            shots,
            sluice,
        });
    }

    public string BuildHtml(string template, BattleLayout layout, Battle battle, BattleSetup setup, ServerVerdict verdict)
    {
        var result = battle.Result!;
        var replay = new
        {
            width = layout.Width,
            height = layout.Height,
            spawn = new[] { layout.Spawn.X, layout.Spawn.Y },
            hearth = new[] { layout.Hearth.X, layout.Hearth.Y },
            obstacles = layout.Obstacles.Select(o => new[] { o.X, o.Y }),
            towers = battle.Towers.Select(t => new { id = t.Id, type = t.Type.ToString(), x = t.Position.X, y = t.Position.Y }),
            path = battle.Path.Select(p => new[] { p.X, p.Y }),
            hearthMax = battle.HearthMaxHealth,
            waveCount = battle.WaveCount,
            totalEnemies = battle.TotalEnemies,
            ticksPerSecond = Simulation.Balance.BalanceTable.TicksPerSecond,
            seed = setup.Seed,
            budget = setup.Budget,
            layoutHash = layout.ComputeHash().ToString("x16"),
            inputLog = battle.InputLog.Select(i => new { tick = i.Tick, type = i.Type.ToString() }),
            result = new
            {
                outcome = result.Outcome.ToString(),
                stars = result.Stars,
                hearth = result.HearthHealth,
                defeated = result.EnemiesDefeated,
                leaked = result.EnemiesLeaked,
                ticks = result.TicksElapsed,
                hash = result.StateHashHex,
            },
            verdict = new { replayHash = verdict.ReplayHash, match = verdict.Match, tamperedHash = verdict.TamperedHash },
            frames = _frames,
            log = _log,
        };

        var json = JsonSerializer.Serialize(replay);
        return template.Replace("/*__REPLAY__*/null", json);
    }

    private static double Round(double value) => Math.Round(value, 3);
}

internal sealed record ServerVerdict(string ReplayHash, bool Match, string? TamperedHash);
