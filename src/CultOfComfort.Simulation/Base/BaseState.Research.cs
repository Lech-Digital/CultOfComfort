using System;
using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>Research at the Hearth (spec 2026-10-14 world map, slice 2): one study at a time, bonuses for ever.</summary>
public sealed partial class BaseState
{
    private readonly HashSet<string> _researched = new();
    private Tech? _studying;
    private DateTimeOffset _studyEndsAt;

    private ResearchBook Research => _table.Research;

    /// <summary>The sum of a kind of bonus over everything researched.</summary>
    private int Bonus(TechEffect effect) => _researched.Select(Research.Get).Where(t => t != null && t.Effect == effect).Sum(t => t!.Amount);

    private CommandOutcome StartStudy(StartResearch start, DateTimeOffset now)
    {
        var tech = Research.Get(start.TechId);
        var problem = tech == null ? "There's no such page in the manual." : StudyProblem(tech);
        if (problem != null)
        {
            return CommandOutcome.Reject(problem);
        }

        _stock -= tech!.Cost;
        _studying = tech;
        _studyEndsAt = now.AddMinutes(tech.Minutes);
        return CommandOutcome.Accept();
    }

    private string? StudyProblem(Tech tech)
    {
        if (_researched.Contains(tech.Id))
        {
            return "He's already read that bit.";
        }

        if (HearthLevel < tech.UnlockHearth)
        {
            return $"Opens at Hearth {tech.UnlockHearth}.";
        }

        if (tech.Needs != null && !_researched.Contains(tech.Needs))
        {
            return $"Needs {Research.Get(tech.Needs)?.Name} first.";
        }

        if (_studying != null)
        {
            return $"He's still reading about {_studying.Name}.";
        }

        return _stock.Covers(tech.Cost) ? null : $"Needs {tech.Cost}.";
    }

    /// <summary>When the study under way finishes (an Advance event), or null.</summary>
    private DateTimeOffset? StudyEnds() => _studying == null ? null : _studyEndsAt;

    /// <summary>Completes the study at its own end time: everything up to then was earned without the bonus.</summary>
    private void CompleteStudy(DateTimeOffset at)
    {
        MaterializeTension(at);
        MaterializeDomain(at);
        _researched.Add(_studying!.Id);
        _studying = null;
        NoteDaily(DailyGoal.Study, 1, at);
    }

    private IReadOnlyList<TechView> ResearchViews() => Research.Techs.Select(t =>
    {
        if (_researched.Contains(t.Id))
        {
            return new TechView(t, TechState.Done, null, null);
        }

        if (_studying?.Id == t.Id)
        {
            return new TechView(t, TechState.Studying, null, _studyEndsAt);
        }

        var locked = HearthLevel < t.UnlockHearth || (t.Needs != null && !_researched.Contains(t.Needs));
        return new TechView(t, locked ? TechState.Locked : TechState.Available, StudyProblem(t), null);
    }).ToList();

    private ResearchData ResearchToData() => new(_researched.OrderBy(id => id).ToList(), _studying?.Id, _studying == null ? null : _studyEndsAt.UtcTicks);

    private void RestoreResearch(ResearchData? data)
    {
        if (data == null)
        {
            return;
        }

        foreach (var id in data.Done)
        {
            if (Research.Get(id) == null)
            {
                throw new InvalidBaseDataException($"unknown research '{id}'");
            }

            _researched.Add(id);
        }

        if (data.Current != null)
        {
            _studying = Research.Get(data.Current) ?? throw new InvalidBaseDataException($"unknown research '{data.Current}'");
            _studyEndsAt = new DateTimeOffset(data.EndsAtUtcTicks ?? 0, TimeSpan.Zero);
        }
    }
}
