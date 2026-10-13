using CultOfComfort.Simulation.Combat;

namespace CultOfComfort.Client
{
    /// <summary>Which sound a Night event makes (art/audio/make_sounds.py). Pure, so it is testable; null: silence.</summary>
    public static class SoundBook
    {
        public static readonly string[] All =
        {
            "tap", "error", "place", "build_done", "collect", "claim", "upgrade_pop", "lamps", "wave_horn", "hearth_hit", "enemy_down",
            "prophet_hit", "swoon", "swish", "sluice", "voice", "stones", "laser", "torch", "sunfire", "victory", "defeat",
        };

        public const string DayMusic = "music_day";
        public const string NightMusic = "music_night";

        public static string For(BattleEvent battleEvent) => battleEvent switch
        {
            WaveStarted _ => "wave_horn",
            HearthDamaged _ => "hearth_hit",
            EnemyDefeated _ => "enemy_down",
            LeaderHit _ => "prophet_hit",
            LeaderSwooned _ => "swoon",
            LeaderAttacked _ => "swish",
            InterventionUsed { Type: PlayerActionType.OpenSluiceGate } => "sluice",
            InterventionUsed { Type: PlayerActionType.PhantomVoice } => "voice",
            HotStonesThrown _ => "stones",
            RelicUsed { Type: PlayerActionType.LaserPointer } => "laser",
            RelicUsed { Type: PlayerActionType.DynamoTorch } => "torch",
            RelicUsed { Type: PlayerActionType.SunFire } => "sunfire",
            InputRejected _ => "error",
            BattleEnded { Outcome: BattleOutcome.Won } => "victory",
            BattleEnded { Outcome: BattleOutcome.Lost } => "defeat",
            _ => null,
        };
    }
}
