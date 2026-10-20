using System.Collections.Generic;

namespace CultOfComfort.Simulation.Base;

/// <summary>What an errand asks for. Build, upgrade and Hearth goals read the village; the others are events.</summary>
public enum ErrandGoal
{
    /// <summary>Have <c>Target</c> finished buildings of <c>Type</c>.</summary>
    Build,

    /// <summary>Collect from a building (of <c>Type</c>, when given). Retired with auto-collect: kept so old books still read.</summary>
    Collect,

    /// <summary>Have a <c>Type</c> at level <c>Target</c> or more.</summary>
    UpgradeTo,

    /// <summary>Have the Hearth at level <c>Target</c> or more.</summary>
    HearthLevel,

    /// <summary>Have <c>Target</c> buildings of <c>Type</c> standing on the raiders' road.</summary>
    BuildOnRoad,

    /// <summary>See a Night through, whatever the result.</summary>
    SurviveNight,

    /// <summary>Win a Night with <c>Target</c> stars or more.</summary>
    WinRaid,

    /// <summary>Have a missionary out, or influence somewhere in the Marchlands.</summary>
    SendMissionary,

    /// <summary>Be Patron of <c>Target</c> places in the Marchlands.</summary>
    Patron,

    /// <summary>Have <c>Target</c> studies finished.</summary>
    Study,
}

/// <summary>One of the Prophet's errands: a goal and the reward paid when it is claimed.</summary>
public sealed record Errand(string Id, string Text, ErrandGoal Goal, BuildingType? Type, int Target, ResourceBag Reward)
{
    public bool IsEvent => Goal is ErrandGoal.Collect or ErrandGoal.SurviveNight or ErrandGoal.WinRaid;
}

/// <summary>The current errand as the client shows it.</summary>
public sealed record ErrandView(int Index, int Count, string Id, string Text, int Progress, int Target, bool Done, ResourceBag Reward,
    ErrandGoal Goal = ErrandGoal.Build, BuildingType? Type = null);

/// <summary>
/// The Prophet's errands (spec 2026-10-13 first-hour arc): an ordered chain that walks a new cult from its first
/// Woodcutter to Hearth 3. The Hearth 1 rewards bridge the 600 Timber of Hearth 2, so it comes around minute 20.
/// </summary>
public sealed record ErrandBook(string Version, IReadOnlyList<Errand> Errands)
{
    // Spec 2026-10-16 first day: the defences come before the first Night, and the middle (Hearth 2 → 3) has errands of
    // 10–30 minutes. Defence errands repay what they cost, so the tutorial keeps its clock.
    public static ErrandBook Default { get; } = new("errands-2026.10.16", new Errand[]
    {
        new("woodcutter", "Build a Woodcutter's Lean-to. Comfort starts with firewood.", ErrandGoal.Build, BuildingType.Woodcutter, 1, new ResourceBag(50, 0, 0, 0)),
        new("storage-hut", "Build a Storage Hut. The Timber stacks itself, but only where there's room.", ErrandGoal.Build, BuildingType.StorageHut, 1, new ResourceBag(100, 0, 0, 0)), // pays the hut back: the tutorial stays on its clock
        new("cottage", "Build a Cottage. Seekers need somewhere to sleep.", ErrandGoal.Build, BuildingType.Cottage, 1, new ResourceBag(60, 0, 0, 0)),
        new("peat-cut", "Build a Peat Cut. No Peat, no warmth, no work.", ErrandGoal.Build, BuildingType.PeatCut, 1, new ResourceBag(40, 40, 0, 0)),
        new("cultist-hut", "Build a Cultist Hut. Devotion keeps the Prophet going.", ErrandGoal.Build, BuildingType.CultistHut, 1, new ResourceBag(0, 0, 0, 20)),
        new("slurry-chute", "Put a Slurry Chute right beside the raiders' road. Mud is a defence.", ErrandGoal.Build, BuildingType.SlurryChute, 1, new ResourceBag(20, 0, 50, 0)),
        new("first-night", "Light the Lamps and see your first Night through.", ErrandGoal.SurviveNight, null, 1, new ResourceBag(100, 0, 40, 0)),
        new("hearth-2", "Raise the Hearth to level 2.", ErrandGoal.HearthLevel, BuildingType.Hearth, 2, new ResourceBag(0, 0, 0, 30)),
        new("missionary", "Open the Missionaries and send a preacher to a place nearby. The muddy ones listen best.", ErrandGoal.SendMissionary, null, 1, new ResourceBag(100, 0, 0, 20)),
        new("midden-sorter", "Build a Midden Sorter. Scrap, from rubbish. Keep it away from the homes: it smells.", ErrandGoal.Build, BuildingType.MiddenSorter, 1, new ResourceBag(100, 0, 0, 0)),
        new("study", "Open Research and study something from the car's manual.", ErrandGoal.Study, null, 1, new ResourceBag(0, 30, 30, 0)),
        new("sapling-flinger", "Build a Sapling Flinger beside the road. Mud slows them; saplings hit them.", ErrandGoal.Build, BuildingType.SaplingFlinger, 1, new ResourceBag(40, 0, 70, 0)),
        new("road-gate", "Build a Gate across the raiders' road. They knock; the Flinger answers.", ErrandGoal.BuildOnRoad, BuildingType.Gate, 1, new ResourceBag(60, 0, 10, 0)),
        new("woodcutter-2", "Upgrade a Woodcutter's Lean-to to level 2.", ErrandGoal.UpgradeTo, BuildingType.Woodcutter, 2, new ResourceBag(150, 0, 0, 0)),
        new("cultist-hut-2", "Upgrade a Cultist Hut to level 2. More Devotion, and more cultists out at Night.", ErrandGoal.UpgradeTo, BuildingType.CultistHut, 2, new ResourceBag(150, 30, 0, 0)),
        new("peat-cut-2", "Upgrade a Peat Cut to level 2. Winter is a rumour, but a loud one.", ErrandGoal.UpgradeTo, BuildingType.PeatCut, 2, new ResourceBag(100, 50, 0, 0)),
        new("hearth-3", "Raise the Hearth to level 3.", ErrandGoal.HearthLevel, BuildingType.Hearth, 3, new ResourceBag(0, 0, 0, 50)),
        new("cistern", "Build a Cistern. More water for the sluice.", ErrandGoal.Build, BuildingType.Cistern, 1, new ResourceBag(0, 0, 60, 0)),
        new("study-2", "Finish a second study. Another room of the manual, perhaps.", ErrandGoal.Study, null, 2, new ResourceBag(100, 0, 40, 0)),
        new("patron", "Become Patron of a place in the Marchlands (40% faith). Mind Wulfric's temper.", ErrandGoal.Patron, null, 1, new ResourceBag(0, 0, 0, 60)),
        new("win-raid", "Win a Night with 2 stars or more. (After a Night the Authority sulks for 12 hours.)", ErrandGoal.WinRaid, null, 2, new ResourceBag(0, 0, 100, 0)),
        // Spec 2026-10-16 hearth four: the Comfort Era.
        new("beehive", "Build a Beehive. Bees: Devotion that nobody has to make.", ErrandGoal.Build, BuildingType.Beehive, 1, new ResourceBag(60, 0, 0, 0)),
        new("hearth-4", "Raise the Hearth to level 4. The Comfort Era, at last.", ErrandGoal.HearthLevel, BuildingType.Hearth, 4, new ResourceBag(0, 0, 0, 100)),
        new("sauna", "Build a Sauna. Hot stones, a bucket, and twigs. Trust him.", ErrandGoal.Build, BuildingType.Sauna, 1, new ResourceBag(300, 100, 0, 0)),
        new("pressure-sluice", "Put a Pressure Sluice right beside the raiders' road. Water, but rude.", ErrandGoal.Build, BuildingType.PressureSluice, 1, new ResourceBag(100, 0, 100, 0)),
        new("blacksmith", "Build a Blacksmith. Scrap, by the hammerful.", ErrandGoal.Build, BuildingType.Blacksmith, 1, new ResourceBag(300, 0, 100, 0)),
    });

    /// <summary>
    /// The order of errands-2026.10.3, for saves from before the errand's id was stored (they held only a position):
    /// the position is read through this to the id, so a player keeps the errand they were on.
    /// </summary>
    public static IReadOnlyList<string> LegacyOrder { get; } = new[]
    {
        "woodcutter", "storage-hut", "cottage", "peat-cut", "cultist-hut", "first-night", "hearth-2", "missionary", "study",
        "slurry-chute", "road-gate", "woodcutter-2", "cistern", "patron", "hearth-3", "win-raid",
    };

    /// <summary>Where a saved errand is in this book: by id when the save has one, else through <see cref="LegacyOrder"/>.</summary>
    public int IndexOf(string? id, int legacyIndex)
    {
        id ??= legacyIndex >= 0 && legacyIndex < LegacyOrder.Count ? LegacyOrder[legacyIndex] : null;
        if (id == null)
        {
            return legacyIndex >= LegacyOrder.Count ? Errands.Count : 0; // past the old end: all done
        }

        for (var i = 0; i < Errands.Count; i++)
        {
            if (Errands[i].Id == id)
            {
                return i;
            }
        }

        return Errands.Count; // an errand this book no longer has: treat the chain as done
    }
}
