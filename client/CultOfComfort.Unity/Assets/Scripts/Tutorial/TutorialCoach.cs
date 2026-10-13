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
    }

    /// <summary>One step of the tutorial: where the arrow goes and what the Prophet says about it.</summary>
    public sealed class CoachStep
    {
        public static readonly CoachStep None = new CoachStep(CoachTarget.None, null, null, null);

        public CoachStep(CoachTarget target, string line, BuildingType? type = null, int? buildingId = null)
        {
            Target = target;
            Line = line;
            Type = type;
            BuildingId = buildingId;
        }

        public CoachTarget Target { get; }

        public string Line { get; }

        public BuildingType? Type { get; }

        public int? BuildingId { get; }

        /// <summary>True when there is something to point at (a wait shows only the line).</summary>
        public bool HasArrow => Target != CoachTarget.None;

        public override string ToString() => $"{Target} {Type} {BuildingId}: {Line}";
    }

    /// <summary>
    /// The first-player tutorial (GDD 05): rides on the first errands and points at the next tap — build card, ✓,
    /// the building, the note — until the player has built, collected and claimed. Pure: the HUD draws it.
    /// </summary>
    public static class TutorialCoach
    {
        /// <summary>The coach follows errands up to and including this one (0: Lean-to, 1: collect, 2: Cottage).</summary>
        public const int LastErrand = 2;

        public static CoachStep Step(ErrandView errand, BuildingType? placing, IReadOnlyList<BuildingView> buildings, BuildingTable table)
        {
            if (errand == null || errand.Index > LastErrand)
            {
                return CoachStep.None;
            }

            if (errand.Done)
            {
                return new CoachStep(CoachTarget.ErrandNote, errand.Index == LastErrand
                    ? "Done! Claim it. After this you're on your own. (I'll be on the note.)"
                    : "Done! Tap the note to claim your reward.");
            }

            var type = errand.Type;
            if (type == null)
            {
                return CoachStep.None;
            }

            var name = table.Spec(type.Value).Name;
            switch (errand.Goal)
            {
                case ErrandGoal.Build:
                    if (placing == type)
                    {
                        return new CoachStep(CoachTarget.GhostConfirm, "Drag it somewhere next to a road, then tap ✓.", type);
                    }

                    var going = buildings.FirstOrDefault(b => b.Type == type && (b.Level == 0 || b.IsUpgrading));
                    if (going != null)
                    {
                        return new CoachStep(CoachTarget.None, "The faithful are on it. Building takes a moment.", type, going.Id);
                    }

                    return new CoachStep(CoachTarget.BuildCard, $"Tap the {name} card.", type);
                case ErrandGoal.Collect:
                    var producer = buildings.Where(b => b.Type == type && b.Level > 0).OrderByDescending(b => b.Ready).FirstOrDefault();
                    if (producer == null)
                    {
                        return new CoachStep(CoachTarget.BuildCard, $"Tap the {name} card.", type);
                    }

                    return producer.Ready >= 1
                        ? new CoachStep(CoachTarget.Building, $"Tap the {name} to collect.", type, producer.Id)
                        : new CoachStep(CoachTarget.None, "It's working. Collect when the icon shows.", type, producer.Id);
                default:
                    return CoachStep.None;
            }
        }
    }
}
