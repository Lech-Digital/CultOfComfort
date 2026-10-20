using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client
{
    /// <summary>What the coach's arrow points at.</summary>
    public enum CoachTarget
    {
        /// <summary>Nothing: the tutorial is over (or there is nothing to tap right now).</summary>
        None,

        /// <summary>A card in the build drawer (<see cref="CoachStep.Type"/>).</summary>
        BuildCard,

        /// <summary>The ✓ under a ghost being placed.</summary>
        GhostConfirm,

        /// <summary>A building on the board (<see cref="CoachStep.BuildingId"/>).</summary>
        Building,

        /// <summary>The errand note (claim the reward).</summary>
        ErrandNote,

        /// <summary>Light the Lamps (start a Night).</summary>
        StartNight,

        /// <summary>A shortcut on the side of the screen (<see cref="CoachStep.Feature"/>: map, research…).</summary>
        Feature,
    }

    /// <summary>One step of the tutorial: where the arrow goes and what the Prophet says about it.</summary>
    public sealed class CoachStep
    {
        public static readonly CoachStep None = new CoachStep(CoachTarget.None, null, null, null);

        public CoachStep(CoachTarget target, string line, BuildingType? type = null, int? buildingId = null, string feature = null)
        {
            Target = target;
            Line = line;
            Type = type;
            BuildingId = buildingId;
            Feature = feature;
        }

        /// <summary>The shortcut's id (<see cref="LockedFeatures"/>) for a <see cref="CoachTarget.Feature"/> step.</summary>
        public string Feature { get; }

        public CoachTarget Target { get; }

        public string Line { get; }

        public BuildingType? Type { get; }

        public int? BuildingId { get; }

        /// <summary>True when there is something to point at (a wait shows only the line).</summary>
        public bool HasArrow => Target != CoachTarget.None;

        public override string ToString() => $"{Target} {Type} {BuildingId}: {Line}";
    }

    /// <summary>
    /// The coach (GDD 05): rides on the Prophet's errands and points at the next tap — build card, ✓, the building,
    /// Light the Lamps, a shortcut, the note — for the whole errand chain, so a new player always has one thing to do
    /// (owner's playtest 2026-10: "no idea how to play"). Pure: the HUD draws it.
    /// </summary>
    public static class TutorialCoach
    {
        /// <param name="studying">A study is under way (the study errand counts only a finished one).</param>
        public static CoachStep Step(ErrandView errand, BuildingType? placing, IReadOnlyList<BuildingView> buildings, BuildingTable table, bool studying = false)
        {
            if (errand == null)
            {
                return CoachStep.None;
            }

            if (errand.Done)
            {
                return new CoachStep(CoachTarget.ErrandNote, null); // the note says "Done! Tap to claim": the arrow alone (one voice)
            }

            switch (errand.Goal)
            {
                case ErrandGoal.Build when errand.Type != null:
                    return BuildStep(errand.Type.Value, placing, buildings, table, table.Spec(errand.Type.Value).IsTower
                        ? "Drag it right beside the raiders' road, then tap ✓." // away from it, a tower stands unconnected
                        : "Drag it somewhere next to a road, then tap ✓.");
                case ErrandGoal.BuildOnRoad when errand.Type != null:
                    return BuildStep(errand.Type.Value, placing, buildings, table, "Drag it onto the raiders' road, then tap ✓.");
                case ErrandGoal.UpgradeTo when errand.Type != null:
                case ErrandGoal.HearthLevel:
                    return UpgradeStep(errand.Type ?? BuildingType.Hearth, errand.Target, buildings, table);
                case ErrandGoal.SurviveNight:
                case ErrandGoal.WinRaid:
                    return new CoachStep(CoachTarget.StartNight, "When you're ready, light the Lamps. The raiders come at Night.");
                case ErrandGoal.SendMissionary:
                case ErrandGoal.Patron:
                    // The Mission House lists the places in reach with their walks; the map makes you hunt for them.
                    return new CoachStep(CoachTarget.Feature, "Open the Missionaries.", feature: "missionaries");
                case ErrandGoal.Study when studying:
                    return new CoachStep(CoachTarget.None, "He's reading. The errand is done when he finishes the page.");
                case ErrandGoal.Study:
                    return new CoachStep(CoachTarget.Feature, "Open Research.", feature: "research");
                default:
                    return CoachStep.None;
            }
        }

        private static CoachStep BuildStep(BuildingType type, BuildingType? placing, IReadOnlyList<BuildingView> buildings, BuildingTable table, string place)
        {
            if (placing == type)
            {
                return new CoachStep(CoachTarget.GhostConfirm, place, type);
            }

            var going = buildings.FirstOrDefault(b => b.Type == type && (b.Level == 0 || b.IsUpgrading));
            if (going != null)
            {
                return new CoachStep(CoachTarget.None, "The faithful are on it. Building takes a moment.", type, going.Id);
            }

            return new CoachStep(CoachTarget.BuildCard, $"Tap the {table.Spec(type).Name} card.", type);
        }

        private static CoachStep UpgradeStep(BuildingType type, int level, IReadOnlyList<BuildingView> buildings, BuildingTable table)
        {
            if (buildings.Any(b => b.Type == type && b.IsUpgrading))
            {
                return CoachStep.None; // an upgrade can take hours: the coach is quiet and the note comes back
            }

            var next = buildings.Where(b => b.Type == type && b.Level > 0 && b.Level < level && !b.Wrecked).OrderByDescending(b => b.Level).FirstOrDefault();
            return next == null
                ? CoachStep.None
                : new CoachStep(CoachTarget.Building, $"Tap your {table.Spec(type).Name}, then Upgrade.", type, next.Id);
        }
    }
}
