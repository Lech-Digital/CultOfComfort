using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client
{
    /// <summary>
    /// When each side shortcut appears (spec 2026-11-17 fun first, A3): one new thing at a time, not six at Hearth 2.
    /// A shortcut opens when the village has earned it <em>and</em> the player has tried the one before it, so the next
    /// arrives after the last has had its moment. Once shown, a shortcut stays. From Hearth 3 the queue no longer waits:
    /// a village that far along gets everything it has earned. Pure: tested without Unity.
    /// </summary>
    public static class FeatureUnlocks
    {
        /// <summary>
        /// The order shortcuts arrive in. One world screen (spec 2026-11-17 fun first, D): the preachers live on the Map,
        /// and the lord's hall opens from the Tension strip, so neither has a shortcut of its own any more.
        /// </summary>
        public static readonly IReadOnlyList<string> Order = new[] { "pilgrims", "map", "research", "festival" };

        /// <summary>The shortcuts that arrive with another, and count as tried when it is (none since D).</summary>
        private static readonly Dictionary<string, string> Riders = new Dictionary<string, string>();

        /// <summary>The lord's temper at which his hall appears, unless a letter comes first.</summary>
        public const int TithesAtTension = 15;

        public const int QueueEndsAtHearth = 3;

        /// <summary>Whether the village has earned the shortcut, regardless of the queue.</summary>
        public static bool Earned(string id, BaseSnapshot s)
        {
            switch (id)
            {
                case "pilgrims":
                    return VillagePresenter.TrialsOpen(s);
                case "map":
                case "missionaries":
                    return s.Domain?.Unlocked == true;
                case "research":
                    return s.HearthLevel >= 2;
                case "tithes":
                    return s.Domain?.Unlocked == true && (s.Domain.Demand != null || s.Tension >= TithesAtTension);
                case "festival":
                    return s.HearthLevel >= 2 && ((s.Trial?.Cleared ?? 0) >= 1 || s.LastNightRecorded >= 2);
                default:
                    return true;
            }
        }

        /// <param name="shown">Shortcuts shown before (they stay).</param>
        /// <param name="tried">Shortcuts the player has opened at least once.</param>
        /// <returns>Every shortcut that should be on screen now, riders included.</returns>
        public static ISet<string> Open(BaseSnapshot s, ICollection<string> shown, ICollection<string> tried)
        {
            var open = new HashSet<string>(shown.Where(id => Order.Contains(id) || Riders.ContainsKey(id)));
            if (NeededBy(s.Errand) is { } needed && Earned(needed, s))
            {
                open.Add(needed); // the errand asks for it: it never waits in the queue
            }

            var queue = s.HearthLevel < QueueEndsAtHearth;
            foreach (var id in Order)
            {
                if (open.Contains(id))
                {
                    if (queue && !Tried(id, tried))
                    {
                        break; // the newest shortcut hasn't been opened yet: the next one waits
                    }

                    continue;
                }

                if (!Earned(id, s))
                {
                    continue; // not earned yet; a later one may already be
                }

                open.Add(id);
                if (queue)
                {
                    break; // one new shortcut at a time
                }
            }

            foreach (var rider in Riders)
            {
                if (open.Contains(rider.Value))
                {
                    open.Add(rider.Key);
                }
            }

            return open;
        }

        /// <summary>The shortcut the errand sends the player to, if any.</summary>
        public static string NeededBy(ErrandView errand)
        {
            switch (errand?.Goal)
            {
                case ErrandGoal.SendMissionary:
                case ErrandGoal.Patron:
                    return "map";
                case ErrandGoal.Study:
                    return "research";
                default:
                    return null;
            }
        }

        private static bool Tried(string id, ICollection<string> tried) =>
            tried.Contains(id) || Riders.Any(r => r.Value == id && tried.Contains(r.Key));
    }
}
