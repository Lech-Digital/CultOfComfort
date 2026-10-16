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
    public static ErrandBook Default { get; } = new("errands-2026.10.3", new Errand[]
    {
        new("woodcutter", "Build a Woodcutter's Lean-to. Comfort starts with firewood.", ErrandGoal.Build, BuildingType.Woodcutter, 1, new ResourceBag(50, 0, 0, 0)),
        new("storage-hut", "Build a Storage Hut. The Timber stacks itself, but only where there's room.", ErrandGoal.Build, BuildingType.StorageHut, 1, new ResourceBag(100, 0, 0, 0)), // pays the hut back: the tutorial stays on its clock
        new("cottage", "Build a Cottage. Seekers need somewhere to sleep.", ErrandGoal.Build, BuildingType.Cottage, 1, new ResourceBag(60, 0, 0, 0)),
        new("peat-cut", "Build a Peat Cut. No Peat, no warmth, no work.", ErrandGoal.Build, BuildingType.PeatCut, 1, new ResourceBag(40, 40, 0, 0)),
        new("cultist-hut", "Build a Cultist Hut. Devotion keeps the Prophet going.", ErrandGoal.Build, BuildingType.CultistHut, 1, new ResourceBag(0, 0, 0, 20)),
        new("first-night", "Light the Lamps and see your first Night through.", ErrandGoal.SurviveNight, null, 1, new ResourceBag(100, 0, 40, 0)),
        new("hearth-2", "Raise the Hearth to level 2.", ErrandGoal.HearthLevel, BuildingType.Hearth, 2, new ResourceBag(0, 0, 0, 30)),
        new("missionary", "Open the Missionaries and send a preacher to a place nearby. The muddy ones listen best.", ErrandGoal.SendMissionary, null, 1, new ResourceBag(100, 0, 0, 20)),
        new("study", "Open Research and study something from the car's manual.", ErrandGoal.Study, null, 1, new ResourceBag(0, 30, 30, 0)),
        new("slurry-chute", "Put a Slurry Chute beside the road. Mud is a defence.", ErrandGoal.Build, BuildingType.SlurryChute, 1, new ResourceBag(0, 0, 50, 0)),
        new("road-gate", "Build a Gate across the raiders' road.", ErrandGoal.BuildOnRoad, BuildingType.Gate, 1, new ResourceBag(100, 0, 0, 0)),
        new("woodcutter-2", "Upgrade a Woodcutter's Lean-to to level 2.", ErrandGoal.UpgradeTo, BuildingType.Woodcutter, 2, new ResourceBag(150, 0, 0, 0)),
        new("cistern", "Build a Cistern. More water for the sluice.", ErrandGoal.Build, BuildingType.Cistern, 1, new ResourceBag(0, 0, 60, 0)),
        new("patron", "Become Patron of a place in the Marchlands (40% faith). Mind Wulfric's temper.", ErrandGoal.Patron, null, 1, new ResourceBag(0, 0, 0, 60)),
        new("hearth-3", "Raise the Hearth to level 3.", ErrandGoal.HearthLevel, BuildingType.Hearth, 3, new ResourceBag(0, 0, 0, 50)),
        new("win-raid", "Win a Night with 2 stars or more. (After a Night the Authority sulks for 12 hours.)", ErrandGoal.WinRaid, null, 2, new ResourceBag(0, 0, 100, 0)),
    });
}
