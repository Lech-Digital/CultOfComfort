using System;

namespace CultOfComfort.Simulation.Base;

public enum BuildingType
{
    Hearth,
    Woodcutter,
    PeatCut,
    StorageHut,
    CultistHut,
    Cistern,
    SlurryChute,
    SaplingFlinger,
    WattleFence,
    Stone,
    Tree,
    WoodenWall,
    Gate,
    DirtTrack,
    PlankWalk,
    CobbledRoad,
    Cottage,
}

public enum ResourceType
{
    Timber,
    Peat,
    Scrap,
    Devotion,
}

/// <summary>An amount of each of the four core resources (costs, stock, caps). Value object.</summary>
public readonly record struct ResourceBag(int Timber, int Peat, int Scrap, int Devotion)
{
    public bool IsZero => this == default;

    public int Get(ResourceType type) => type switch
    {
        ResourceType.Timber => Timber,
        ResourceType.Peat => Peat,
        ResourceType.Scrap => Scrap,
        _ => Devotion,
    };

    public static ResourceBag Of(ResourceType type, int amount) => type switch
    {
        ResourceType.Timber => new ResourceBag(amount, 0, 0, 0),
        ResourceType.Peat => new ResourceBag(0, amount, 0, 0),
        ResourceType.Scrap => new ResourceBag(0, 0, amount, 0),
        _ => new ResourceBag(0, 0, 0, amount),
    };

    public static ResourceBag operator +(ResourceBag a, ResourceBag b) =>
        new(a.Timber + b.Timber, a.Peat + b.Peat, a.Scrap + b.Scrap, a.Devotion + b.Devotion);

    public static ResourceBag operator -(ResourceBag a, ResourceBag b) =>
        new(a.Timber - b.Timber, a.Peat - b.Peat, a.Scrap - b.Scrap, a.Devotion - b.Devotion);

    public bool Covers(ResourceBag cost) =>
        Timber >= cost.Timber && Peat >= cost.Peat && Scrap >= cost.Scrap && Devotion >= cost.Devotion;

    /// <summary>How much more of each resource <paramref name="cost"/> needs.</summary>
    public ResourceBag Missing(ResourceBag cost) => new(
        Math.Max(0, cost.Timber - Timber), Math.Max(0, cost.Peat - Peat), Math.Max(0, cost.Scrap - Scrap), Math.Max(0, cost.Devotion - Devotion));

    public ResourceBag Half() => new(Timber / 2, Peat / 2, Scrap / 2, Devotion / 2);

    public override string ToString()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (Timber != 0) parts.Add($"{Timber} Timber");
        if (Peat != 0) parts.Add($"{Peat} Peat");
        if (Scrap != 0) parts.Add($"{Scrap} Scrap");
        if (Devotion != 0) parts.Add($"{Devotion} Devotion");
        return parts.Count == 0 ? "free" : string.Join(" · ", parts);
    }
}
