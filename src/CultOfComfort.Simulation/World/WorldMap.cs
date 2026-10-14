using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Numerics;

namespace CultOfComfort.Simulation.World;

public enum AuthorityKind
{
    Warlord,
    Bishop,
}

/// <summary>A place in the shared world (axial hex <see cref="Q"/>, <see cref="R"/>), in an Authority's domain.</summary>
public sealed record WorldNode(int Id, string Name, NodeType Type, int Q, int R, int Squalor, int DomainId)
{
    public int DistanceTo(int q, int r) => Hex.Distance(Q, R, q, r);
}

/// <summary>An Authority's domain: the places nearest its seat.</summary>
public sealed record WorldDomain(int Id, string Name, AuthorityKind Authority, int SeatNodeId);

public static class Hex
{
    public static int Distance(int q1, int r1, int q2, int r2) => (Math.Abs(q1 - q2) + Math.Abs(r1 - r2) + Math.Abs(q1 + r1 - q2 - r2)) / 2;

    /// <summary>Every hex within <paramref name="radius"/> of the centre, in a fixed order.</summary>
    public static IEnumerable<(int Q, int R)> Disc(int radius)
    {
        for (var q = -radius; q <= radius; q++)
        {
            for (var r = Math.Max(-radius, -q - radius); r <= Math.Min(radius, -q + radius); r++)
            {
                yield return (q, r);
            }
        }
    }
}

/// <summary>How a world is generated (spec 2026-10-14 shared world).</summary>
public sealed record WorldShape(int Radius, int PlacePercent, int Domains, int Capacity, int VillageSpacing, int CrowdRadius)
{
    /// <summary>
    /// Server 1: radius 46 (6 487 hexes), ~35% places (~2 300), 60 domains, 500 villages at least 3 hexes apart.
    /// Measured: random-but-fair placement packs ~568 at that spacing, so 500 always fit; a full world has ~8 rival
    /// cults within 6 hexes of each village.
    /// </summary>
    public static WorldShape Default { get; } = new(46, 35, 60, 500, 3, 6);
}

/// <summary>A generated world: the same seed and shape always give the same map (server and phone alike).</summary>
public sealed class WorldMap
{
    private readonly Dictionary<(int, int), WorldNode> _byHex;
    private readonly Dictionary<int, WorldNode> _byId;

    internal WorldMap(ulong seed, WorldShape shape, IReadOnlyList<WorldNode> nodes, IReadOnlyList<WorldDomain> domains)
    {
        Seed = seed;
        Shape = shape;
        Nodes = nodes;
        Domains = domains;
        _byHex = nodes.ToDictionary(n => (n.Q, n.R));
        _byId = nodes.ToDictionary(n => n.Id);
    }

    public ulong Seed { get; }

    public WorldShape Shape { get; }

    public IReadOnlyList<WorldNode> Nodes { get; }

    public IReadOnlyList<WorldDomain> Domains { get; }

    public WorldNode? Node(int id) => _byId.TryGetValue(id, out var node) ? node : null;

    public WorldNode? NodeAt(int q, int r) => _byHex.TryGetValue((q, r), out var node) ? node : null;

    public bool Contains(int q, int r) => Hex.Distance(0, 0, q, r) <= Shape.Radius;

    /// <summary>The places within <paramref name="radius"/> hexes of a spot, nearest first.</summary>
    public IEnumerable<WorldNode> Near(int q, int r, int radius) =>
        Nodes.Where(n => n.DistanceTo(q, r) <= radius).OrderBy(n => n.DistanceTo(q, r)).ThenBy(n => n.Id);

    /// <summary>
    /// Where a new village goes (spec: random but fair): a free hex that is no place, at least
    /// <see cref="WorldShape.VillageSpacing"/> from every village, among those with the fewest villages around; ties
    /// broken by <paramref name="seed"/> (the player's). Null when the world is full.
    /// </summary>
    public (int Q, int R)? PlaceVillage(IReadOnlyCollection<(int Q, int R)> villages, ulong seed)
    {
        if (villages.Count >= Shape.Capacity)
        {
            return null;
        }

        var candidates = new List<(int Q, int R, int Crowd)>();
        foreach (var (q, r) in Hex.Disc(Shape.Radius - 1))
        {
            if (_byHex.ContainsKey((q, r)))
            {
                continue;
            }

            var crowd = 0;
            var tooClose = false;
            foreach (var v in villages)
            {
                var d = Hex.Distance(q, r, v.Q, v.R);
                if (d < Shape.VillageSpacing)
                {
                    tooClose = true;
                    break;
                }

                if (d <= Shape.CrowdRadius)
                {
                    crowd++;
                }
            }

            if (!tooClose)
            {
                candidates.Add((q, r, crowd));
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var least = candidates.Min(c => c.Crowd);
        var fair = candidates.Where(c => c.Crowd == least).ToList();
        var pick = fair[new PcgRandom(seed, RandomStreams.Placement).Next(0, fair.Count)];
        return (pick.Q, pick.R);
    }
}
