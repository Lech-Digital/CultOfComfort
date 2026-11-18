using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Client
{
    /// <summary>The preachers' tray on the Map (spec 2026-11-17 fun first, D1): who's out, and a few good places to send the next one.</summary>
    public sealed class PreacherTrayModel
    {
        /// <summary>"Brother Aldo · preaching at Muddlecombe, 2h left", one per preacher out; or how many wait at home.</summary>
        public List<string> Preachers = new List<string>();

        /// <summary>A preacher came home with a story: the Mission House sheet has its choice.</summary>
        public string TaleLine;

        /// <summary>Three to five places worth a walk: id (the Map flies there and opens its card), name, why.</summary>
        public List<(int Id, string Name, string Why)> Places = new List<(int, string, string)>();
    }

    /// <summary>
    /// Builds the tray from the Mission House's model (spec 2026-11-17 fun first, D1): instead of a board of twenty places at
    /// 0%, the few a preacher should walk to now: in reach, not already won, nearest first, the least contested. Pure.
    /// </summary>
    public static class PreacherTray
    {
        public const int MaxPlaces = 3; // the spec says three to five; three keeps the map in view on a phone

        public static PreacherTrayModel Build(MissionHouseModel house)
        {
            var tray = new PreacherTrayModel();
            if (house == null || !house.Unlocked)
            {
                return tray;
            }

            var out_ = house.Preachers.Where(p => !p.AtHome).ToList();
            foreach (var p in out_)
            {
                tray.Preachers.Add($"{p.Name} · {p.Status}");
            }

            var home = house.Preachers.Count(p => p.AtHome);
            if (home > 0)
            {
                tray.Preachers.Add(home == 1 ? "1 preacher ready: tap a place to send them." : $"{home} preachers ready: tap a place to send one.");
            }

            if (house.Tales.Count > 0)
            {
                tray.TaleLine = house.Tales.Count == 1 ? "A preacher is back with a story ›" : $"{house.Tales.Count} stories from the road ›";
            }

            var bound = new HashSet<int>(out_.Select(p => p.NodeId));
            foreach (var p in house.Places
                         .Where(p => p.Reachable && !p.Patron && p.Mine < 100 && !bound.Contains(p.Id))
                         .OrderBy(p => p.Gives != null && p.Gives.StartsWith("No offerings") ? 1 : 0) // a good payoff first
                         .ThenBy(p => p.Distance).ThenBy(p => p.Rival).ThenByDescending(p => p.Mine)
                         .Take(MaxPlaces))
            {
                var why = $"{p.Kind} · {p.Walk} walk" + (string.IsNullOrEmpty(p.Gives) ? string.Empty : $" · {p.Gives}")
                    + (p.Mine > 0 ? $" · {p.Mine}% yours" : string.Empty) + (p.Rival > 0 ? $" · rivals {p.Rival}%" : string.Empty);
                tray.Places.Add((p.Id, p.Name, why));
            }

            return tray;
        }
    }
}
