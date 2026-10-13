using System.Collections.Generic;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client
{
    /// <summary>What tapping the errand note does.</summary>
    public enum ErrandHint
    {
        /// <summary>The errand is done: take the reward.</summary>
        Claim,

        /// <summary>Show the build card to pick (<see cref="ErrandNote.Type"/>).</summary>
        BuildCard,

        /// <summary>Point at the building to tap or upgrade (<see cref="ErrandNote.Type"/>).</summary>
        Building,

        /// <summary>Point at the Hearth.</summary>
        Hearth,

        /// <summary>Point at Light the Lamps.</summary>
        StartNight,
    }

    /// <summary>The sticky note under the Hearth panel: the Prophet's current errand (spec 2026-10-13 first-hour arc).</summary>
    public sealed class ErrandNote
    {
        private ErrandNote(string heading, string text, string progress, string reward, bool done, ErrandHint hint, BuildingType? type)
        {
            Heading = heading;
            Text = text;
            Progress = progress;
            Reward = reward;
            Done = done;
            Hint = hint;
            Type = type;
        }

        public string Heading { get; }
        public string Text { get; }

        /// <summary>"1/2" for counted errands; null when there is one thing to do, or the target is a level or stars.</summary>
        public string Progress { get; }

        public string Reward { get; }
        public bool Done { get; }
        public ErrandHint Hint { get; }
        public BuildingType? Type { get; }

        public static ErrandNote From(ErrandView view)
        {
            if (view == null)
            {
                return null;
            }

            var counted = view.Target > 1 && view.Goal is ErrandGoal.Build or ErrandGoal.BuildOnRoad;
            var hint = view.Done ? ErrandHint.Claim : view.Goal switch
            {
                ErrandGoal.Build or ErrandGoal.BuildOnRoad => ErrandHint.BuildCard,
                ErrandGoal.UpgradeTo or ErrandGoal.Collect => ErrandHint.Building,
                ErrandGoal.HearthLevel => ErrandHint.Hearth,
                _ => ErrandHint.StartNight,
            };
            return new ErrandNote($"Errand {view.Index + 1} of {view.Count}", view.Text, counted ? $"{view.Progress}/{view.Target}" : null,
                Rewards(view.Reward), view.Done, hint, view.Type);
        }

        /// <summary>Light the Lamps waits (a small locked note) until the first-Night errand comes up.</summary>
        public static bool LampsWait(ErrandView view, IReadOnlyList<Errand> book)
        {
            if (view == null)
            {
                return false;
            }

            for (var i = 0; i < book.Count; i++)
            {
                if (book[i].Goal == ErrandGoal.SurviveNight)
                {
                    return view.Index < i;
                }
            }

            return false;
        }

        private static string Rewards(ResourceBag reward)
        {
            var parts = new List<string>();
            if (reward.Timber > 0) parts.Add($"+{reward.Timber} Timber");
            if (reward.Peat > 0) parts.Add($"+{reward.Peat} Peat");
            if (reward.Scrap > 0) parts.Add($"+{reward.Scrap} Scrap");
            if (reward.Devotion > 0) parts.Add($"+{reward.Devotion} Devotion");
            return string.Join(" · ", parts);
        }
    }
}
