using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;

namespace CultOfComfort.Client
{
    public enum BarkKind
    {
        RaiderArrives,
        RaiderBashesGate,
        RaiderHacksWall,
        RaiderWashedBack,
        RaiderReachesHearth,
        ProphetWave,
        ProphetSluice,
        ProphetVoice,
        ProphetStones,
        ProphetSwoons,
        CultistBuilt,
        CultistRaidComing,
        CultistCold,
        CultistNewcomer,
    }

    /// <summary>
    /// What everyone shouts (style guide: muddy satire — silly, never cruel). One file, so the jokes are easy to write;
    /// the pick is a plain function of a roll, so the same Night says the same things.
    /// </summary>
    public static class BarkBook
    {
        private static readonly Dictionary<BarkKind, string[]> Lines = new Dictionary<BarkKind, string[]>
        {
            [BarkKind.RaiderArrives] = new[]
            {
                "For Lord Wulfric! …probably.", "Is this the spa?", "I was told there'd be towels.", "Smash the hot tubs!",
                "Mum said I'd be home by supper.", "Which way to the heretics?",
            },
            [BarkKind.RaiderBashesGate] = new[]
            {
                "Open up, in the name of the Bishop!", "Knock knock!", "Who builds a gate on a road?!", "Put your back into it!",
            },
            [BarkKind.RaiderHacksWall] = new[] { "This wall looks smug.", "Chop chop!", "Nobody fences me in!", "Splinters! Ow!" },
            [BarkKind.RaiderWashedBack] = new[] { "My boots! My good boots!", "It's in my helmet!", "Is this… warm?", "Blub." },
            [BarkKind.RaiderReachesHearth] = new[] { "Found the fireplace!", "Cosy in here, innit?", "Ooh, a bathhouse." },
            [BarkKind.ProphetWave] = new[] { "More guests! Light more lamps!", "Stay calm. Stay moisturised.", "Here they come, lads." },
            [BarkKind.ProphetSluice] = new[] { "Bath time!", "Rinse cycle!", "Cleanliness is next to Prophet-ness!" },
            [BarkKind.ProphetVoice] = new[] { "THIS IS YOUR CONSCIENCE. GO HOME.", "Testing, testing… repent!", "BOO." },
            [BarkKind.ProphetStones] = new[] { "Hot stones! Mind your toes!", "Sauna special!", "Catch!" },
            [BarkKind.ProphetSwoons] = new[] { "I'll just… lie here a moment.", "Tell my followers… I was fluffy.", "Ow." },
            [BarkKind.CultistBuilt] = new[] { "Another fine hut for the Prophet!", "Built it with my own hands. Mostly.", "Smells of fresh timber!" },
            [BarkKind.CultistRaidComing] = new[] { "Lamps! Somebody fetch the lamps!", "Wulfric's lot are coming!", "Hide the good soap!" },
            [BarkKind.CultistCold] = new[] { "Brr. Who let the fire go out?", "No Peat, no work. Union rules.", "My toes are on strike." },
            [BarkKind.CultistNewcomer] = new[] { "A new face! Hand them a towel.", "Welcome, friend. Shoes off.", "Another seeker of comfort!" },
        };

        private static readonly Dictionary<EnemyType, string[]> Arrivals = new Dictionary<EnemyType, string[]>
        {
            [EnemyType.ManAtArms] = new[] { "Form up! Or at least stand near each other.", "Armour on. Brain optional.", "Clank clank, heretics!" },
            [EnemyType.Zealot] = new[] { "Repent, ye soaky sinners!", "Page forty-two, brothers! Aim for his head!", "Bathing is a slippery slope!" },
            [EnemyType.Goose] = new[] { "HONK.", "HONK HONK.", "Hssssss!" },
            [EnemyType.Goat] = new[] { "Meh-eh-eh!", "*chews a fence post thoughtfully*", "Baaah. (That wall is mine.)" },
            [EnemyType.Boar] = new[] { "SNORT!", "Oink. Menacingly.", "*charges at nothing in particular*" },
            [EnemyType.Peasant] = new[] { "Just having a look, dear.", "Ooh, what's this lever do?", "Our Margaret says you lot bathe twice a week!" },
            [EnemyType.Priest] = new[] { "Bless you all. Now hit harder.", "The Bishop sends his regards. And soldiers.", "Holy water only! None of your hot nonsense!" },
            [EnemyType.Lord] = new[] { "I am Lord Wulfric, and I am NOT amused.", "Who taxed my peasants' patience? You did!", "Bring me the Prophet. And a towel." },
        };

        public static string Line(BarkKind kind, int roll) => Pick(Lines[kind], roll);

        /// <summary>What a raider shouts on the road in: each type has its own voice; levies fall back to the general crowd.</summary>
        public static string Arrival(EnemyType type, int roll) =>
            Pick(Arrivals.TryGetValue(type, out var lines) ? lines : Lines[BarkKind.RaiderArrives], roll);

        private static string Pick(string[] lines, int roll) => lines[(roll % lines.Length + lines.Length) % lines.Length];
    }

    /// <summary>Keeps the chatter readable: at most three bubbles at once, each ~2.5 s, and nobody repeats within 8 s.</summary>
    public sealed class BubbleDirector
    {
        public const float BubbleSeconds = 2.5f;
        private const float SpeakerRestSeconds = 8f;
        private const int MaxBubbles = 3;

        private readonly List<float> _showing = new List<float>();
        private readonly Dictionary<string, float> _lastSpoke = new Dictionary<string, float>();

        public bool TrySpeak(string speaker, float now)
        {
            _showing.RemoveAll(started => now - started >= BubbleSeconds);
            if (_showing.Count >= MaxBubbles || (_lastSpoke.TryGetValue(speaker, out var last) && now - last < SpeakerRestSeconds))
            {
                return false;
            }

            _showing.Add(now);
            _lastSpoke[speaker] = now;
            return true;
        }
    }
}
