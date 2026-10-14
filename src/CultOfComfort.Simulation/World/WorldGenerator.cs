using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.World;

/// <summary>
/// Builds a world from a seed (spec 2026-10-14 shared world): seats spread over a hex disc, places scattered between
/// them, each place in the domain of its nearest seat, every name a little bit muddy. Pure and deterministic.
/// </summary>
public static class WorldGenerator
{
    private static readonly string[] Starts =
    {
        "Mud", "Bog", "Puddle", "Drizzle", "Soggy", "Damp", "Grumble", "Nettle", "Brack", "Mire", "Sludge", "Fen", "Murk", "Squelch",
        "Wether", "Moss", "Peat", "Gloom", "Slop", "Dribble", "Marsh", "Clag", "Muck", "Soak", "Wallow", "Dank", "Sop", "Sump",
    };

    private static readonly string[] Ends =
    {
        "combe", "wick", "ford", "by", "ton", " End", " Bottom", "ham", "mire", "bridge", "well", "stead", "hollow", "moor", "dale",
        "worth", "field", "pool", "marsh", "thorpe", "ley", "brook",
    };

    private static readonly string[] Saints =
    {
        "Grumble", "Sludgeric", "Dampwyn", "Moaning Mildred", "Wilfred the Wet", "Agnes of the Drain", "Barnaby", "Perpetual Drizzle",
        "Soggy Bede", "Ethelred the Unready", "Cuthbert", "Hilda the Humid", "Oswald", "Bartholomew Bog",
    };

    private static readonly string[] Lords =
    {
        "Wulfric", "Godric", "Aelfric", "Osric", "Baldric", "Cedric", "Leofric", "Sigeric", "Wigmund", "Ealdred", "Thorkel", "Hereward",
        "Eadwig", "Brihtnoth", "Ordgar", "Wulfstan",
    };

    public static WorldMap Generate(ulong seed, WorldShape? shape = null)
    {
        shape ??= WorldShape.Default;
        var random = new PcgRandom(seed, RandomStreams.World);
        var hexes = Hex.Disc(shape.Radius).ToList();
        var area = hexes.Count;

        // Seats: shuffled hexes, each kept only if far enough from the seats so far (an even spread).
        var spacing = (int)System.Math.Floor(System.Math.Sqrt(area / (double)shape.Domains) * 0.95);
        var shuffled = hexes.OrderBy(_ => random.NextUInt()).ToList();
        var seats = new List<(int Q, int R)>();
        foreach (var hex in shuffled)
        {
            if (seats.Count < shape.Domains && Hex.Distance(0, 0, hex.Q, hex.R) <= shape.Radius - 2
                && seats.All(s => Hex.Distance(s.Q, s.R, hex.Q, hex.R) >= spacing))
            {
                seats.Add(hex);
            }
        }

        var nodes = new List<WorldNode>();
        var domains = new List<WorldDomain>();
        var usedNames = new HashSet<string>();
        for (var d = 0; d < seats.Count; d++)
        {
            var authority = d % 2 == 0 ? AuthorityKind.Warlord : AuthorityKind.Bishop;
            var title = authority == AuthorityKind.Warlord ? $"Lord {Pick(random, Lords)}'s Keep" : $"Cathedral of St. {Pick(random, Saints)}";
            var name = Unique(usedNames, title, random);
            var id = nodes.Count + 1;
            nodes.Add(new WorldNode(id, name, authority == AuthorityKind.Warlord ? NodeType.Keep : NodeType.Cathedral, seats[d].Q, seats[d].R,
                5 + random.Next(0, 15), d + 1));
            domains.Add(new WorldDomain(d + 1, authority == AuthorityKind.Warlord ? $"The lands of {name.Replace("'s Keep", string.Empty)}" : $"The see of {name.Replace("Cathedral of ", string.Empty)}",
                authority, id));
        }

        // Places: a share of the remaining hexes, never on top of each other; types weighted like the Marchlands.
        foreach (var hex in hexes)
        {
            if (seats.Contains(hex) || random.Next(0, 100) >= shape.PlacePercent)
            {
                continue;
            }

            var roll = random.Next(0, 100);
            var type = roll < 40 ? NodeType.Hamlet : roll < 73 ? NodeType.Village : roll < 87 ? NodeType.MarketTown : NodeType.Abbey;
            var stem = Pick(random, Starts) + Pick(random, Ends);
            var name = type switch
            {
                NodeType.MarketTown => $"{stem} Market",
                NodeType.Abbey => $"St. {Pick(random, Saints)}'s Abbey",
                _ => stem,
            };
            var domain = 1 + Enumerable.Range(0, seats.Count)
                .OrderBy(i => Hex.Distance(seats[i].Q, seats[i].R, hex.Q, hex.R)).ThenBy(i => i).First();
            var seatDistance = Hex.Distance(seats[domain - 1].Q, seats[domain - 1].R, hex.Q, hex.R);
            var squalor = System.Math.Min(95, 15 + seatDistance * 8 + random.Next(0, 30)); // the further from the seat, the muddier
            nodes.Add(new WorldNode(nodes.Count + 1, Unique(usedNames, name, random), type, hex.Q, hex.R, squalor, domain));
        }

        return new WorldMap(seed, shape, nodes, domains);
    }

    private static string Pick(PcgRandom random, string[] from) => from[random.Next(0, from.Length)];

    /// <summary>Names repeat in a big world; a second "Mudwick" becomes "Mudwick Parva", a third "Mudwick Magna", then numbers.</summary>
    private static string Unique(HashSet<string> used, string name, PcgRandom random)
    {
        if (used.Add(name))
        {
            return name;
        }

        foreach (var suffix in new[] { " Parva", " Magna", " Minor", " Major", "-on-the-Bog", "-under-Water" })
        {
            if (used.Add(name + suffix))
            {
                return name + suffix;
            }
        }

        for (var n = 2; ; n++)
        {
            var numbered = $"{name} {n}";
            if (used.Add(numbered))
            {
                return numbered;
            }
        }
    }
}
