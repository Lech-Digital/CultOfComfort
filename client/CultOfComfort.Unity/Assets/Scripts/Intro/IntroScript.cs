using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Client
{
    /// <summary>Where a shot is staged: the 2024 garage, the Marchlands in 947, or the title card.</summary>
    public enum IntroSet
    {
        Garage,
        Marchlands,
        Title,
    }

    /// <summary>One shot of the intro: what is staged, what the caption says, and how long it holds.</summary>
    public sealed class IntroShot
    {
        public IntroShot(string id, IntroSet set, string caption, float seconds)
        {
            Id = id;
            Set = set;
            Caption = caption;
            Seconds = seconds;
        }

        public string Id { get; }

        public IntroSet Set { get; }

        public string Caption { get; }

        public float Seconds { get; }
    }

    /// <summary>
    /// The first-launch intro (GDD 05: The Boot): a wonky inventor, a hatchback time machine, no way back.
    /// About forty seconds; a tap moves on, Skip ends it.
    /// </summary>
    public static class IntroScript
    {
        public const string Garage = "garage";
        public const string Machine = "machine";
        public const string Jump = "jump";
        public const string Marchlands = "marchlands";
        public const string Landing = "landing";
        public const string NoSockets = "no-sockets";
        public const string Boot = "boot";
        public const string Cult = "cult";
        public const string Title = "title";

        public static IReadOnlyList<IntroShot> Shots { get; } = new[]
        {
            new IntroShot(Garage, IntroSet.Garage, "Somewhere in the Netherlands, 2024. A garage. A man who should not be allowed tools.", 5.5f),
            new IntroShot(Machine, IntroSet.Garage, "He turned his hatchback into a time machine. On a Thursday. Before lunch.", 5f),
            new IntroShot(Jump, IntroSet.Garage, "He did not build a way back.", 3.5f),
            new IntroShot(Marchlands, IntroSet.Marchlands, "The Marchlands, 947 AD. Mud. More mud. A Roman bath nobody has used in five hundred years.", 5f),
            new IntroShot(Landing, IntroSet.Marchlands, "Until now.", 3f),
            new IntroShot(NoSockets, IntroSet.Marchlands, "No sockets. No signal. Battery: 3%… 2%…", 5f),
            new IntroShot(Boot, IntroSet.Marchlands, "Everything he owned fit in the boot. Everything in the boot is now holy.", 6.5f),
            new IntroShot(Cult, IntroSet.Marchlands, "The locals decided he was a prophet. He decided not to correct them.", 6f),
            new IntroShot(Title, IntroSet.Title, "Keep the faithful warm. Keep the raiders wet.", 4f),
        };

        public static float TotalSeconds => Shots.Sum(s => s.Seconds);

        /// <summary>The relics that fly out of the boot, in order (model names; GDD 05 The Boot).</summary>
        public static IReadOnlyList<string> BootRelics { get; } = new[]
        {
            "rubber_duck", "traffic_cone", "relic_speaker", "relic_dynamo_torch", "relic_laser_pointer",
            "relic_magnifier", "relic_hivis_vest", "relic_warning_triangle", "relic_beach_bag", "shopping_trolley",
        };

        /// <summary>The Marchlands locals who peek out of the bushes and end up kneeling.</summary>
        public static IReadOnlyList<string> Locals { get; } = new[] { "villager_woman", "herbwife", "blacksmith", "friar_odo", "cultist" };
    }

    /// <summary>Remembers on the device whether the intro has played.</summary>
    public interface IIntroProgress
    {
        bool Seen { get; }

        void MarkSeen();
    }

    public sealed class PlayerPrefsIntroProgress : IIntroProgress
    {
        private const string Key = "intro.seen";

        public bool Seen => UnityEngine.PlayerPrefs.GetInt(Key, 0) == 1;

        public void MarkSeen()
        {
            UnityEngine.PlayerPrefs.SetInt(Key, 1);
            UnityEngine.PlayerPrefs.Save();
        }

        public static void Forget()
        {
            UnityEngine.PlayerPrefs.DeleteKey(Key);
            UnityEngine.PlayerPrefs.Save();
        }
    }
}
