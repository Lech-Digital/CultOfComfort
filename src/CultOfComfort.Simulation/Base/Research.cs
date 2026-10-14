using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>What a study improves (each tech has one effect of <see cref="Tech.Amount"/>).</summary>
public enum TechEffect
{
    /// <summary>Timber producers make Amount% more.</summary>
    TimberPercent,

    /// <summary>Peat producers make Amount% more.</summary>
    PeatPercent,

    /// <summary>Missionaries win Amount% more influence per sermon.</summary>
    MissionaryPercent,

    /// <summary>Buildings and towers stand Amount% more smashing in a Night.</summary>
    HealthPercent,

    /// <summary>Bathhouses give Amount more Comfort.</summary>
    BathComfort,

    /// <summary>Tithes calm Wulfric Amount% more.</summary>
    TithePercent,

    /// <summary>Influence fades Amount percentage points a day slower.</summary>
    SlowerDecay,

    /// <summary>Every store holds Amount% more.</summary>
    StoragePercent,

    /// <summary>Amount Comfort for the whole village.</summary>
    Comfort,
}

/// <summary>A study at the Hearth: the Prophet reads the car's manual and works out something useful (GDD 03).</summary>
public sealed record Tech(string Id, string Name, string Joke, int Era, int UnlockHearth, ResourceBag Cost, int Minutes, TechEffect Effect, int Amount, string? Needs = null);

/// <summary>The research tree (spec 2026-10-14 world map, slice 2). One study at a time; bonuses last for ever.</summary>
public sealed class ResearchBook
{
    public static ResearchBook Default { get; } = new(new[]
    {
        new Tech("sharper-axes", "Sharper Axes", "The manual has a page on whetstones. It is the only useful page.", 1, 2, new ResourceBag(300, 0, 40, 30), 60, TechEffect.TimberPercent, 15),
        new Tech("peat-stacking", "Peat Stacking", "Stack it so the wind dries it. Revolutionary, says the Prophet.", 1, 2, new ResourceBag(250, 40, 20, 30), 60, TechEffect.PeatPercent, 15),
        new Tech("sermon-notes", "Sermon Notes", "Bullet points. The locals have never seen a bullet, or a point.", 1, 2, new ResourceBag(200, 0, 0, 60), 90, TechEffect.MissionaryPercent, 25),
        new Tech("thicker-walls", "Thicker Walls", "Twice the mud, half the draught.", 1, 2, new ResourceBag(400, 0, 80, 20), 120, TechEffect.HealthPercent, 20, "sharper-axes"),
        new Tech("plumbing", "Plumbing, Probably", "Water goes down. Usually.", 2, 3, new ResourceBag(900, 200, 200, 100), 240, TechEffect.BathComfort, 20, "peat-stacking"),
        new Tech("tithe-accounting", "Tithe Accounting", "Wulfric can't count. That's the trick.", 2, 3, new ResourceBag(600, 0, 150, 120), 180, TechEffect.TithePercent, 25, "sermon-notes"),
        new Tech("long-sermons", "Long Sermons", "Nobody remembers what he said, so nobody forgets.", 2, 3, new ResourceBag(500, 100, 0, 150), 240, TechEffect.SlowerDecay, 1, "sermon-notes"),
        new Tech("bigger-stores", "Bigger Storehouses", "Shelves. Actual shelves.", 2, 3, new ResourceBag(1200, 100, 250, 50), 300, TechEffect.StoragePercent, 20, "thicker-walls"),
        new Tech("hot-water", "Hot Water for All", "The faith's central promise, finally kept.", 2, 3, new ResourceBag(1500, 300, 300, 200), 480, TechEffect.Comfort, 15, "plumbing"),
    });

    private readonly Dictionary<string, Tech> _byId;

    public ResearchBook(IReadOnlyList<Tech> techs)
    {
        Techs = techs;
        _byId = techs.ToDictionary(t => t.Id);
    }

    public IReadOnlyList<Tech> Techs { get; }

    public Tech? Get(string id) => _byId.TryGetValue(id, out var tech) ? tech : null;
}

/// <summary>Research as stored: what is done, and the study under way.</summary>
public sealed record ResearchData(IReadOnlyList<string> Done, string? Current, long? EndsAtUtcTicks);

public enum TechState
{
    Done,
    Studying,
    Available,
    Locked,
}

public sealed record TechView(Tech Tech, TechState State, string? Reason, System.DateTimeOffset? EndsAtUtc);
