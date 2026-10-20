using System;
using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>One of the Prophet's perks: chosen at its level, one of a pair.</summary>
public sealed record ProphetPerk(string Id, int Level, string Name, string Text, int HealthPercent = 0, int DamagePercent = 0, int SpeedPercent = 0,
    int VoicePercent = 0, int StonesPercent = 0);

/// <summary>The Prophet as shown: his level, how far to the next, his perks, and the pair to choose from (if one's due).</summary>
public sealed record ProphetView(int Level, int Xp, int XpForLevel, int XpForNext, IReadOnlyList<ProphetPerk> Perks, IReadOnlyList<ProphetPerk>? Choice,
    int HealthPercent, int DamagePercent);

/// <summary>
/// The Prophet grows (spec 2026-10-20 the Prophet grows; owner: "a reason to fight well"): every Night earns him
/// experience, each level a little more health and bite, and at levels 3, 5, 7 and 9 a choice of two perks. It all goes
/// into the Night plan, so the server fights the same Prophet.
/// </summary>
public static class ProphetBook
{
    public const int MaxLevel = 10;
    public const int HealthPercentPerLevel = 4;
    public const int DamagePercentPerLevel = 5;

    /// <summary>Experience for a Night: something for turning up, more for every star.</summary>
    public static int XpForNight(int stars) => 20 + 25 * Math.Max(0, stars);

    /// <summary>Experience needed to reach a level (level 1: none): 50, 150, 300, 500…</summary>
    public static int XpFor(int level) => level <= 1 ? 0 : 25 * level * (level - 1);

    public static int LevelOf(int xp)
    {
        var level = 1;
        while (level < MaxLevel && xp >= XpFor(level + 1))
        {
            level++;
        }

        return level;
    }

    public static IReadOnlyList<ProphetPerk> Perks { get; } = new[]
    {
        new ProphetPerk("bathrobe", 3, "A thicker bathrobe", "Health +20%", HealthPercent: 20),
        new ProphetPerk("slippers", 3, "Fluffier slippers", "He walks 20% faster", SpeedPercent: 20),
        new ProphetPerk("megaphone", 5, "A louder megaphone", "The Voice reaches 40% further", VoicePercent: 40),
        new ProphetPerk("hot-stones", 5, "Hotter stones", "Hot Stones burn 40% harder", StonesPercent: 40),
        new ProphetPerk("velvet-knuckles", 7, "Velvet knuckle-dusters", "He hits 25% harder", DamagePercent: 25),
        new ProphetPerk("second-thermos", 7, "A second thermos", "Health +25%", HealthPercent: 25),
        new ProphetPerk("calm", 9, "Unshakeable calm", "Health +30%", HealthPercent: 30),
        new ProphetPerk("righteous-slap", 9, "The righteous slap", "He hits 35% harder", DamagePercent: 35),
    };

    public static ProphetPerk? Perk(string id) => Perks.FirstOrDefault(p => p.Id == id);
}
