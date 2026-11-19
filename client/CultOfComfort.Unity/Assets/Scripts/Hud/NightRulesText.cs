using CultOfComfort.Simulation.Combat;

namespace CultOfComfort.Client
{
    /// <summary>The words for a Night that needs you (spec 2026-11-17 fun first, C): boons, objectives, the gap. Pure.</summary>
    public static class NightRulesText
    {
        public static string BoonTitle(Boon boon) => boon switch
        {
            Boon.ChuteTape => "Duct-tape the chute",
            Boon.FullVolume => "Bluetooth at full volume",
            Boon.Umbrellas => "Hand out umbrellas",
            _ => "Hotter stones",
        };

        public static string BoonText(Boon boon) => boon switch
        {
            Boon.ChuteTape => "Towers fire half as fast again",
            Boon.FullVolume => "The Voice cools down twice as fast",
            Boon.Umbrellas => "Cultists patch the buildings",
            _ => "Hot Stones heat up twice as fast",
        };

        /// <summary>The objective as the third star's condition, or null when the Night has none (planned before).</summary>
        public static string Objective(NightObjective objective) => objective switch
        {
            NightObjective.NothingWrecked => "no building wrecked",
            NightObjective.ProphetNeverSwoons => "the Prophet never swoons",
            NightObjective.ThreeInOneStone => "three raiders in one Hot Stone",
            _ => null,
        };

        /// <summary>
        /// The line under the meters at Night: "Third star: no building wrecked ✓", or "… (missed)", or null. Words, not ✗:
        /// the HUD's font has no ✗, and it showed as an empty box on the phone (playtest 2026-11-18).
        /// </summary>
        public static string ObjectiveLine(NightObjective objective, bool met, int bestStones = 0)
        {
            var text = Objective(objective);
            if (text == null)
            {
                return null;
            }

            // Something still to do is "not yet", with how close, never "(missed)" before it could be met (review
            // 2026-11-19: "three in one Hot Stone (missed)" from the first second).
            return objective == NightObjective.ThreeInOneStone && !met
                ? $"Third star: {text} · best {bestStones}/3"
                : $"Third star: {text} {Mark(met)}";
        }

        public static string Mark(bool met) => met ? "✓" : "(missed)";

        /// <summary>The wave line in a gap: "Wave 2/3 in 8s", with the tally if any were driven off.</summary>
        public static string GapLine(int nextWave, int waveCount, int secondsLeft, int defeated)
        {
            var text = $"Wave {nextWave}/{waveCount} in {secondsLeft}s";
            return defeated > 0 ? $"{text} · {defeated} driven off" : text;
        }
    }
}
