using System.Collections.Generic;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;

namespace CultOfComfort.Client
{
    /// <summary>What the Dawn card shows (spec 2026-11-17 fun first, A2): the stars big, what the Night earned, and what's next.</summary>
    public sealed class DawnCardModel
    {
        public string Title;
        public int Stars;
        public string Summary;
        public string Flavour;

        /// <summary>What the Night brought in: salvage, a stage's reward, the Prophet's experience. One line each.</summary>
        public List<string> Earned = new List<string>();

        /// <summary>The second button ("Next: the Pilgrims' Way · stage 2"), or null.</summary>
        public string Next;
    }

    /// <summary>Builds the Dawn card. Pure: tested without Unity.</summary>
    public static class DawnCard
    {
        /// <param name="report">The village's record of a real Night (null for a Pilgrims' stage, or if it didn't come back).</param>
        /// <param name="trialStage">The Pilgrims' stage just walked, or null for a real Night.</param>
        /// <param name="trialPaid">What the stage paid into the stores.</param>
        /// <param name="nextStage">The Pilgrims' stage that can be walked now, or null if the Way isn't open.</param>
        public static DawnCardModel Build(BattleResult result, NightReport report, int? trialStage, ResourceBag trialPaid, int? nextStage, string ruler)
        {
            var won = result.Outcome == BattleOutcome.Won;
            var model = new DawnCardModel
            {
                Stars = result.Stars,
                Title = trialStage != null
                    ? won ? $"Stage {trialStage} cleared" : $"The pilgrims turned back at stage {trialStage}"
                    : won ? "The Hearth stands" : "The Hearth went cold",
                Summary = $"Hearth {result.HearthHealth}/{result.HearthMaxHealth} · {result.EnemiesDefeated} driven off",
                Flavour = won
                    ? "The attackers left damp, confused and noticeably cleaner."
                    : $"{ruler}'s men warmed their hands on the embers and left. Tomorrow: repairs, and a strongly worded diary entry.",
            };

            if (trialStage != null)
            {
                if (!trialPaid.IsZero)
                {
                    model.Earned.Add($"The pilgrims leave {VillagePresenter.ShortCost(trialPaid)}");
                }
            }
            else
            {
                if (report != null && report.Salvage > 0)
                {
                    model.Earned.Add($"+{report.Salvage} Scrap salvaged");
                }

                if (report != null && report.Wrecked.Count > 0)
                {
                    model.Earned.Add(report.Wrecked.Count == 1 ? "1 building wrecked: repair it from its card" : $"{report.Wrecked.Count} buildings wrecked: Repair all");
                }

                model.Earned.Add($"The Prophet: +{ProphetBook.XpForNight(result.Stars)} experience");
            }

            if (nextStage != null)
            {
                model.Next = $"Next: the Pilgrims' Way · stage {nextStage}";
            }

            return model;
        }
    }
}
