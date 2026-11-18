using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Waves;

namespace CultOfComfort.Client
{
    /// <summary>One raider type on the roster card: its name, the scout's line, and whether the player has seen it on a card.</summary>
    public readonly struct RaiderRow
    {
        public RaiderRow(EnemyType type, string name, string line, bool isNew, bool leads)
        {
            Type = type;
            Name = name;
            Line = line;
            IsNew = isNew;
            Leads = leads;
        }

        public EnemyType Type { get; }
        public string Name { get; }
        public string Line { get; }
        public bool IsNew { get; }

        /// <summary>The boss who leads the last wave.</summary>
        public bool Leads { get; }
    }

    /// <summary>
    /// Who may come tonight, read at Dusk (spec 2026-11-17 fun first, B5): the scouting lines left the fight for this card.
    /// Pure: the road's roster (<see cref="RaidRoster.ForRoad"/>), most common first, the boss last.
    /// </summary>
    public static class RosterCard
    {
        public static string Title(int hearthLevel, int road) => $"Tonight: {RaidRoster.Who(hearthLevel, road)}";

        public static List<RaiderRow> Rows(int hearthLevel, int road, ICollection<string> seen)
        {
            var rows = new List<RaiderRow>();
            var roster = RaidRoster.ForRoad(hearthLevel, road);
            if (roster == null)
            {
                return rows;
            }

            var mix = new List<(EnemyType Type, int Weight)>(roster.Mix);
            mix.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            foreach (var (type, _) in mix)
            {
                if (type != roster.Boss)
                {
                    rows.Add(Row(type, seen, false));
                }
            }

            if (roster.Boss is { } boss)
            {
                rows.Add(Row(boss, seen, true));
            }

            return rows;
        }

        /// <summary>Whether the card has a raider the player hasn't read about yet: the strip says so.</summary>
        public static bool HasNew(int hearthLevel, int road, ICollection<string> seen) =>
            Rows(hearthLevel, road, seen).Exists(r => r.IsNew);

        private static RaiderRow Row(EnemyType type, ICollection<string> seen, bool leads) =>
            new RaiderRow(type, Name(type), ScoutLine(type), !seen.Contains(type.ToString()), leads);

        public static string Name(EnemyType type) => type switch
        {
            EnemyType.ManAtArms => "Men-at-arms",
            EnemyType.Zealot => "Zealots",
            EnemyType.Goose => "Geese",
            EnemyType.Goat => "Goats",
            EnemyType.Boar => "Wild boar",
            EnemyType.Peasant => "Nosy locals",
            EnemyType.Priest => "Priests",
            EnemyType.Bishop => "The Bishop",
            EnemyType.Lord => Ruler.Cap(Ruler.Full),
            _ => "Levies",
        };

        /// <summary>The scout's line for a raider type (once the toast the first time it showed up in a Night).</summary>
        public static string ScoutLine(EnemyType type) => type switch
        {
            EnemyType.ManAtArms => "Proper soldiers, in proper tin. Slow to stop; let the towers wear them down.",
            EnemyType.Zealot => "Friar Odo's lot. They throw hymnals, and they aim for the heretic.",
            EnemyType.Goose => "It has chosen violence. Mind the Prophet's ankles.",
            EnemyType.Goat => "They butt fences and walls on principle.",
            EnemyType.Boar => "Tough, quick, and nobody invited it.",
            EnemyType.Peasant => "They won't fight; they just poke your buildings to see what happens.",
            EnemyType.Priest => "Their blessing makes the soldiers around them harder to hurt. Get them first.",
            EnemyType.Lord => "In person, at the head of the last wave. He is not amused.",
            EnemyType.Bishop => "Slow, sure, and blessing everyone near him. Point the laser at him: he'll take it for a miracle.",
            _ => $"{Ruler.Short}'s farmhands with sticks. Ordinary, and plenty of them.",
        };
    }
}
