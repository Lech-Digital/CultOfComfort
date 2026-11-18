using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Sandbox;

/// <summary>
/// A player who fights the Night (spec 2026-11-17 fun first, C7, the "active" bot): walks the Prophet out to the raider
/// nearest the Hearth (they stop to fight him, by the towers), throws Hot Stones at the thickest crowd in reach, uses the
/// Voice when two or more are on him and the Sluice when the road fills, takes the boon that fits, and calls the next wave
/// early while the Hearth is healthy. Its input log is what the village replays.
/// </summary>
internal static class NightBot
{
    private static readonly Boon[] Preference = { Boon.ChuteTape, Boon.HotterStones, Boon.FullVolume, Boon.Umbrellas };

    public static (BattleResult Result, IReadOnlyList<PlayerInput> Inputs, Battle Battle) PlayActive(BattleSetup setup)
    {
        var battle = new Battle(setup with { Inputs = Array.Empty<PlayerInput>() });
        var balance = setup.Balance ?? BalanceTable.Default;
        var lastMove = -100;
        while (!battle.IsFinished)
        {
            Act(battle, balance, ref lastMove);
            battle.Step();
        }

        return (battle.Result!, battle.InputLog.ToList(), battle);
    }

    private static void Act(Battle battle, BalanceTable balance, ref int lastMove)
    {
        if (battle.IsGapOpen)
        {
            if (battle.BoonOffer.Count > 0)
            {
                var offer = battle.BoonOffer;
                var best = Preference.First(offer.Contains);
                battle.Intervene(PlayerActionType.PickBoon, new GridPosition(offer.ToList().IndexOf(best), 0));
            }

            var gapAge = battle.Tick - (battle.NextWaveAtTick - Battle.GapTicks);
            if (gapAge >= 3 * BalanceTable.TicksPerSecond && battle.HearthHealth * 100 / battle.HearthMaxHealth >= 85)
            {
                battle.Intervene(PlayerActionType.CallWave);
            }

            return;
        }

        var active = battle.Enemies.Where(e => e.IsActive).ToList();
        if (active.Count == 0)
        {
            return;
        }

        var leader = battle.Leader.Position;
        double Dist(Simulation.Numerics.FixVector2 a, Simulation.Numerics.FixVector2 b) =>
            Math.Sqrt(Math.Pow(a.X.ToDouble() - b.X.ToDouble(), 2) + Math.Pow(a.Y.ToDouble() - b.Y.ToDouble(), 2));
        GridPosition Tile(Simulation.Numerics.FixVector2 p) =>
            new((int)Math.Round(p.X.ToDouble()), (int)Math.Round(p.Y.ToDouble()));

        if (battle.IsHotStonesReady)
        {
            var radius = balance.HotStones.Radius.ToDouble();
            var reach = balance.HotStones.Reach.ToDouble();
            var target = active
                .Select(e => battle.PositionOf(e))
                .Where(p => Dist(p, leader) <= reach)
                .Select(p => (Tile: Tile(p), Count: active.Count(o => Dist(battle.PositionOf(o), Tile(p).ToVector()) <= radius)))
                .OrderByDescending(t => t.Count)
                .FirstOrDefault();
            if (target.Count >= 2)
            {
                battle.Intervene(PlayerActionType.HotStones, target.Tile);
                return;
            }
        }

        if (battle.IsVoiceReady && active.Count(e => Dist(battle.PositionOf(e), leader) <= balance.Leader.VoiceRadius.ToDouble()) >= 2)
        {
            battle.Intervene(PlayerActionType.PhantomVoice);
            return;
        }

        if (battle.IsSluiceReady && active.Count >= 4)
        {
            battle.Intervene(PlayerActionType.OpenSluiceGate);
            return;
        }

        if (battle.Tick - lastMove >= BalanceTable.TicksPerSecond)
        {
            // Hurt: back to the Hearth to mend. Else out to the raider nearest the Hearth.
            var hurt = battle.Leader.Health * 100 / battle.Leader.MaxHealth < 40;
            var lead = active.OrderByDescending(e => e.DistanceAlongPath).First();
            var to = hurt ? battle.Path[^1] : Tile(battle.PositionOf(lead));
            if (to != battle.Leader.Destination)
            {
                battle.Intervene(PlayerActionType.MoveLeader, to);
                lastMove = battle.Tick;
            }
        }
    }
}
