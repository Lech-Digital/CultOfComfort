using System;
using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>The notables' work (spec 2026-10-20 notables): see <see cref="NotableBook"/>.</summary>
public sealed partial class BaseState
{
    private readonly Dictionary<string, int> _notableAt = new(); // notable id → the building they work

    /// <summary>The notable working a building, if any (and still standing, and still of the cult).</summary>
    private Notable? NotableAt(Building b)
    {
        foreach (var (id, building) in _notableAt)
        {
            if (building == b.Id && NotableBook.Get(id) is { } n && n.JoinsAtHearth <= HearthLevel)
            {
                return n;
            }
        }

        return null;
    }

    /// <summary>A notable's extra output at a workplace, in percent.</summary>
    private int NotableBonus(Building b) => NotableAt(b) is { Knack: NotableKnack.Work } n ? n.Percent : 0;

    private IReadOnlyList<NotableView> NotableViews() =>
        NotableBook.JoinedAt(HearthLevel).Select(n => new NotableView(n, _notableAt.TryGetValue(n.Id, out var b) && Find(b) != null ? b : null)).ToList();

    private CommandOutcome Assign(AssignNotable assign, DateTimeOffset now)
    {
        var notable = NotableBook.Get(assign.NotableId);
        if (notable == null || notable.JoinsAtHearth > HearthLevel)
        {
            return CommandOutcome.Reject("Nobody by that name here.");
        }

        MaterializeEconomy(now); // what was made so far was made at the old rate
        if (assign.BuildingId < 0)
        {
            _notableAt.Remove(notable.Id); // off to put the kettle on
            return CommandOutcome.Accept();
        }

        var building = Find(assign.BuildingId);
        if (building == null || !notable.CanWork(building.Type, _table))
        {
            return CommandOutcome.Reject($"{notable.Name} isn't much use there.");
        }

        foreach (var other in _notableAt.Where(kv => kv.Value == building.Id).Select(kv => kv.Key).ToList())
        {
            _notableAt.Remove(other); // one notable to a building: the other goes home
        }

        _notableAt[notable.Id] = building.Id;
        return CommandOutcome.Accept();
    }

    private void RestoreNotables(BaseData data)
    {
        foreach (var (id, building) in data.Notables ?? Array.Empty<(string, int)>())
        {
            if (NotableBook.Get(id) == null)
            {
                throw new InvalidBaseDataException($"An unknown notable '{id}'.");
            }

            _notableAt[id] = building;
        }
    }
}
