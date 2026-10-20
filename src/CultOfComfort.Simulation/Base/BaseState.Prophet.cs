using System;
using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>The Prophet's experience and perks (spec 2026-10-20 the Prophet grows): see <see cref="ProphetBook"/>.</summary>
public sealed partial class BaseState
{
    private int _prophetXp;
    private readonly List<string> _perks = new();

    private int ProphetLevel => ProphetBook.LevelOf(_prophetXp);

    private IEnumerable<ProphetPerk> TakenPerks => _perks.Select(ProphetBook.Perk).OfType<ProphetPerk>();

    /// <summary>The first perk level he's reached without choosing yet (null: none due).</summary>
    private int? PerkDue => ProphetBook.Perks.Select(p => p.Level).Distinct().OrderBy(l => l)
        .Where(l => l <= ProphetLevel && !TakenPerks.Any(p => p.Level == l)).Cast<int?>().FirstOrDefault();

    private int ProphetHealthBonus => (ProphetLevel - 1) * ProphetBook.HealthPercentPerLevel + TakenPerks.Sum(p => p.HealthPercent);

    private int ProphetDamageBonus => (ProphetLevel - 1) * ProphetBook.DamagePercentPerLevel + TakenPerks.Sum(p => p.DamagePercent);

    private void GainProphetXp(int stars) => _prophetXp += ProphetBook.XpForNight(stars);

    private ProphetView ProphetViewNow()
    {
        var level = ProphetLevel;
        var due = PerkDue;
        return new ProphetView(level, _prophetXp, ProphetBook.XpFor(level), level >= ProphetBook.MaxLevel ? _prophetXp : ProphetBook.XpFor(level + 1),
            TakenPerks.ToList(), due == null ? null : ProphetBook.Perks.Where(p => p.Level == due).ToList(),
            ProphetHealthBonus, ProphetDamageBonus);
    }

    private CommandOutcome ChoosePerk(ChooseProphetPerk choose)
    {
        var perk = ProphetBook.Perk(choose.PerkId);
        if (perk == null)
        {
            return CommandOutcome.Reject("No such perk.");
        }

        if (perk.Level != PerkDue)
        {
            return CommandOutcome.Reject(perk.Level > ProphetLevel ? $"That comes at level {perk.Level}." : "He's chosen at that level already.");
        }

        _perks.Add(perk.Id);
        return CommandOutcome.Accept();
    }

    private void RestoreProphet(BaseData data)
    {
        _prophetXp = Math.Max(0, data.ProphetXp);
        foreach (var id in data.ProphetPerks ?? Array.Empty<string>())
        {
            if (ProphetBook.Perk(id) == null)
            {
                throw new InvalidBaseDataException($"An unknown perk '{id}'.");
            }

            _perks.Add(id);
        }
    }
}
