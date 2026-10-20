using System.Collections.Generic;

namespace CultOfComfort.Client
{
    /// <summary>A shortcut whose screen does not exist yet. Tapping it shows <see cref="Note"/> on a sticky note.</summary>
    public readonly struct LockedFeature
    {
        public LockedFeature(string id, string label, string icon, string note)
        {
            Id = id;
            Label = label;
            Icon = icon;
            Note = note;
        }

        public string Id { get; }
        public string Label { get; }
        public string Icon { get; }
        public string Note { get; }
    }

    /// <summary>The frame's not-yet-built shortcuts (spec: Locked features). An entry leaves this table when its screen lands.</summary>
    public static class LockedFeatures
    {
        public static readonly IReadOnlyList<LockedFeature> All = new[]
        {
            new LockedFeature("map", "Map", "map", "The Marchlands open at Hearth 2. The Prophet is still arguing with the cartographer."),
            new LockedFeature("research", "Research", "research", "Research opens at Hearth 2. He's still looking for the manual in the glovebox."),
            new LockedFeature("missionaries", "Missionaries", "missionaries", "Missionaries set out at Hearth 2. Nobody has volunteered yet."),
            new LockedFeature("pilgrims", "Pilgrims", "pilgrims", "The Pilgrims' Way opens at Hearth 2. The pilgrims are still lacing their boots."),
            new LockedFeature("tithes", "Tithes", "tithes", "Tithes open at Hearth 2. Wulfric hasn't sent the invoice yet."),
            new LockedFeature("shop", "Shop", "shop", "Shop: the till is a bucket. Coming soon."),
            new LockedFeature("cult", "Cult", "cult", "Your cult: five people and a duck. Coming with buildings."),
        };

        public static LockedFeature Get(string id)
        {
            foreach (var feature in All)
            {
                if (feature.Id == id)
                {
                    return feature;
                }
            }

            throw new KeyNotFoundException($"No locked feature '{id}'.");
        }
    }
}
