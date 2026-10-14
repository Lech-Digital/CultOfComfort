using System.Collections.Generic;
using CultOfComfort.Simulation.Combat;

namespace CultOfComfort.Client
{
    /// <summary>What the Prophet says when a bigger Hearth means he finally works out another relic from the boot.</summary>
    public static class RelicNews
    {
        /// <summary>The news for going from Hearth <paramref name="from"/> to <paramref name="to"/>; null on loading (from 0) or when nothing is new.</summary>
        public static string Found(int from, int to)
        {
            if (from <= 0 || to <= from)
            {
                return null;
            }

            var added = RelicBook.ForHearth(to) & ~RelicBook.ForHearth(from);
            var names = new List<string>();
            if (added.HasFlag(Relic.LaserPointer))
            {
                names.Add("the laser pointer");
            }

            if (added.HasFlag(Relic.DynamoTorch))
            {
                names.Add("the dynamo torch");
            }

            if (added.HasFlag(Relic.Magnifier))
            {
                names.Add("the magnifying glass");
            }

            return names.Count == 0 ? null
                : $"Rummaging in the car, I found {string.Join(" and ", names)}. Tonight, {(names.Count == 1 ? "it's a weapon" : "they're weapons")}.";
        }
    }
}
