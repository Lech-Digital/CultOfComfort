using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client
{
    /// <summary>One line of today's list on the note.</summary>
    public sealed class DailyLine
    {
        public int Slot;
        public string Text;
        public string Progress;
        public string Reward;
        public bool CanClaim;
        public bool Claimed;
        public bool CanSwap;
    }

    /// <summary>
    /// The Prophet's daily list on the errand note (spec 2026-10-15 daily errands): today's three, the Thermos, the
    /// streak and the Towels. Pure: tested without Unity's UI.
    /// </summary>
    public sealed class DailyNote
    {
        public string Heading;
        public List<DailyLine> Lines = new List<DailyLine>();
        public bool ThermosReady;
        public string Footer;
        public int Towels;

        public static DailyNote From(DailyView daily, DateTimeOffset now)
        {
            if (daily == null)
            {
                return null;
            }

            var note = new DailyNote
            {
                Towels = daily.Towels,
                Heading = $"Today's list · {daily.Towels} Towel{(daily.Towels == 1 ? string.Empty : "s")}" + (daily.Streak > 0 ? $" · {daily.Streak}-day streak" : string.Empty),
                ThermosReady = daily.ThermosReady,
            };
            foreach (var e in daily.Errands)
            {
                note.Lines.Add(new DailyLine
                {
                    Slot = e.Slot, Text = e.Text, Progress = e.Target > 1 ? $"{e.Progress}/{e.Target}" : string.Empty,
                    Reward = ErrandNote.Rewards(e.Reward), CanClaim = e.Done && !e.Claimed, Claimed = e.Claimed,
                    CanSwap = !e.Claimed && !e.Done && daily.Towels >= DailyBook.RerollCost,
                });
            }

            var left = VillagePresenter.Duration((long)Math.Max(0, (daily.NextListAtUtc - now).TotalSeconds));
            var toWeek = DailyBook.StreakLength - daily.Streak % DailyBook.StreakLength;
            note.Footer = daily.ThermosClaimed
                ? $"Thermos drunk. A new list in {left}." + (daily.Streak % DailyBook.StreakLength == 0 && daily.Streak > 0 ? " A whole week: something new by the Hearth!" : $" {toWeek} more day{(toWeek == 1 ? string.Empty : "s")} for the week's prize.")
                : daily.ThermosReady ? $"All three done: the Thermos is yours (+{DailyBook.ThermosTowels} Towels)."
                : $"Do all three for the Prophet's Thermos. A new list in {left}.";
            return note;
        }
    }
}
