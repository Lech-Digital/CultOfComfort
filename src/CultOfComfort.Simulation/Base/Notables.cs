using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>What a notable is good at.</summary>
public enum NotableKnack
{
    /// <summary>Makes a producer of the given kinds produce more (any producer when none named).</summary>
    Work,

    /// <summary>Works a tower: it fires faster (like a warden).</summary>
    Tower,
}

/// <summary>A named villager with a trait (spec 2026-10-20 notables): joins at a Hearth level, works one building.</summary>
public sealed record Notable(string Id, string Name, string Trait, string Text, int JoinsAtHearth, NotableKnack Knack, int Percent = 0,
    IReadOnlyList<BuildingType>? Suits = null)
{
    public bool CanWork(BuildingType type, BuildingTable table) => Knack == NotableKnack.Tower
        ? table.Spec(type).IsTower
        : table.Spec(type).Produces != null && (Suits == null || Suits.Contains(type));
}

/// <summary>A notable as shown: who, where they work (null: idle).</summary>
public sealed record NotableView(Notable Notable, int? BuildingId);

/// <summary>
/// The notables (spec 2026-10-20 notables; owner: "villagers with names and traits"): two join the cult at each Hearth
/// level from 2, each good at something. Put one to work at a building from its card.
/// </summary>
public static class NotableBook
{
    public static IReadOnlyList<Notable> All { get; } = new[]
    {
        new Notable("agnes", "Agnes the Axe", "Loves trees, to death", "A Lean-to she works makes 40% more Timber.", 2, NotableKnack.Work, 40, new[] { BuildingType.Woodcutter }),
        new Notable("ember", "Sister Ember", "A little too fond of fire", "A tower she works fires much faster.", 2, NotableKnack.Tower),
        new Notable("willy", "Wet Willy", "Happiest up to his knees", "A Peat Cut he works makes 40% more Peat.", 3, NotableKnack.Work, 40, new[] { BuildingType.PeatCut }),
        new Notable("hammer", "Brother Hammer", "Tireless", "Any workplace he joins makes 25% more.", 3, NotableKnack.Work, 25),
        new Notable("drip", "Deacon Drip", "Steams gently", "A Bathhouse or Sauna he works brings 40% more Devotion.", 4, NotableKnack.Work, 40, new[] { BuildingType.Bathhouse, BuildingType.Sauna }),
        new Notable("tom", "Little Tom", "Never misses", "A tower he works fires much faster.", 4, NotableKnack.Tower),
        new Notable("mags", "Old Mags", "Sorts anything", "A Midden Sorter or Blacksmith she works makes 40% more Scrap.", 5, NotableKnack.Work, 40, new[] { BuildingType.MiddenSorter, BuildingType.Blacksmith }),
        new Notable("bede", "Bede the Bellows", "Hums while he works", "Any workplace he joins makes 30% more.", 5, NotableKnack.Work, 30),
    };

    public static Notable? Get(string id) => All.FirstOrDefault(n => n.Id == id);

    public static IEnumerable<Notable> JoinedAt(int hearthLevel) => All.Where(n => n.JoinsAtHearth <= hearthLevel);
}
