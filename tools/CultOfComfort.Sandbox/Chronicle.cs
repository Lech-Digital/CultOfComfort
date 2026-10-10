using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;

namespace CultOfComfort.Sandbox;

/// <summary>Turns simulation events into the protagonist's running commentary. Presentation only.</summary>
internal sealed class Chronicle
{
    private static readonly string[] WaveLines =
    [
        "Lord Wulfric's levies have finished their ale and found their pitchforks.",
        "More of them. Someone in the village told them we have hot water.",
        "Men-at-arms. Mail shirts, poor hygiene, strong opinions.",
        "The last of them. Probably. Wulfric was never good at counting.",
        "Still coming. At this point it's just stubbornness.",
    ];

    private static readonly string[] HeavyDefeatLines =
    [
        "A man-at-arms has been persuaded. By a rock.",
        "Another man-at-arms down. Chainmail is mostly holes, it turns out.",
        "A man-at-arms slipped in the mud and reconsidered his career.",
        "Man-at-arms defeated. His horse is on our side now.",
        "A man-at-arms has left the conversation.",
    ];

    private static readonly string[] SwoonLines =
    [
        "The leader has fainted. Dramatically, and into the mud. The cult takes notes.",
        "Down again. He claims it's low blood sugar. It's a pitchfork.",
        "He lies face down, muttering about health insurance.",
    ];

    private int _leviesDefeated;
    private int _swoons;
    private bool _firstZealotLogged;
    private int _heavyDefeated;
    private bool _firstCritLogged;

    public string? Describe(BattleEvent battleEvent) => battleEvent switch
    {
        WaveStarted e => $"Wave {e.Wave}. {WaveLines[Math.Min(e.Wave - 1, WaveLines.Length - 1)]}",
        InterventionUsed { Type: PlayerActionType.PhantomVoice } e => e.EnemiesAffected == 0
            ? "The leader shouted into a tin can at nobody. Cultists exchanged looks."
            : $"A disembodied voice told {e.EnemiesAffected} soldiers to go home. They did.",
        InterventionUsed e => e.EnemiesAffected == 0
            ? "Sluice gate opened on an empty road. Very clean road now."
            : $"Sluice gate opened. {e.EnemiesAffected} soldiers rediscover mud at speed.",
        InputRejected e => $"That didn't work. {e.Reason}",
        HotStonesThrown e => e.EnemiesAffected == 0
            ? "Hot stones, thrown at nothing. The grass is now exfoliated."
            : $"Hot Stone Therapy for {e.EnemiesAffected}. Unrequested, unrefundable.",
        LeaderSwooned => SwoonLines[_swoons++ % SwoonLines.Length],
        LeaderRevived => "He's up. Muddy, offended, and back in charge.",
        EnemySpawned { Type: EnemyType.Zealot } when !_firstZealotLogged => FirstZealot(),
        HearthDamaged { Type: EnemyType.Zealot } e => $"A zealot reached the Hearth and blessed it. Aggressively. −{e.Damage}.",
        HearthDamaged { Type: EnemyType.ManAtArms } e => $"A man-at-arms dented the Hearth. −{e.Damage}. Rude.",
        HearthDamaged e => $"A levy reached the Hearth and kicked it. −{e.Damage} warmth.",
        EnemyDefeated { Type: EnemyType.ManAtArms } => HeavyDefeatLines[_heavyDefeated++ % HeavyDefeatLines.Length],
        EnemyDefeated => DescribeLevyDefeat(),
        TowerFired { Critical: true } when !_firstCritLogged => FirstCrit(),
        BattleEnded e => Ending(e),
        _ => null,
    };

    private string? DescribeLevyDefeat()
    {
        _leviesDefeated++;
        return _leviesDefeated switch
        {
            1 => "First levy down. He'll tell his friends. They won't listen.",
            5 => "Five levies sent home early. Their mothers will be relieved.",
            10 => "Ten levies. Wulfric is running out of cousins.",
            _ => null,
        };
    }

    private string FirstZealot()
    {
        _firstZealotLogged = true;
        return "Zealots. Friar Odo's lot. They throw hymnals, and they aim for the heretic.";
    }

    private string FirstCrit()
    {
        _firstCritLogged = true;
        return "Muddied and standing still: the Flinger's favourite kind of customer.";
    }

    private static string Ending(BattleEnded e) => (e.Outcome, e.Stars) switch
    {
        (BattleOutcome.Lost, _) =>
            "The Hearth went cold. Wulfric's men warmed their hands on the embers and left. Tomorrow: repairs, and a strongly worded diary entry.",
        (_, 3) =>
            "Anno Domini 947. The heathen bathhouse stood. The attackers left damp, confused and noticeably cleaner.",
        (_, 2) =>
            "We held. The Hearth has a limp now, but so does Wulfric's reputation.",
        _ =>
            "We survived, technically. The Hearth is held together with daub and spite.",
    };
}
