using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;

namespace CultOfComfort.Simulation.Waves;

/// <summary>
/// Who comes on a raid: a weighted mix, plus an optional boss who leads the last wave. The roster grows with the
/// Hearth: curious locals first, then Wulfric's levy, then the lords and the church.
/// </summary>
public sealed record RaidRoster(IReadOnlyList<(EnemyType Type, int Weight)> Mix, EnemyType? Boss)
{
    /// <summary>Hearth 1: wild animals and nosy peasants.</summary>
    public static RaidRoster CuriousLocals { get; } = new(new[]
    {
        (EnemyType.Peasant, 40), (EnemyType.Goose, 25), (EnemyType.Goat, 20), (EnemyType.Boar, 15),
    }, null);

    /// <summary>Hearth 2: Lord Wulfric's levy, with the odd animal that followed them.</summary>
    public static RaidRoster WulfricsLevy { get; } = new(new[]
    {
        (EnemyType.Levy, 45), (EnemyType.ManAtArms, 20), (EnemyType.Zealot, 15), (EnemyType.Boar, 10), (EnemyType.Goat, 10),
    }, null);

    /// <summary>Hearth 3: the lords and the church, led by Lord Wulfric himself.</summary>
    public static RaidRoster LordsAndChurch { get; } = new(new[]
    {
        (EnemyType.Levy, 30), (EnemyType.ManAtArms, 25), (EnemyType.Zealot, 20), (EnemyType.Priest, 15), (EnemyType.Boar, 10), (EnemyType.Bishop, 6),
    }, EnemyType.Lord);

    /// <summary>The roster for a Hearth level (0 or less: none — the classic demo mix).</summary>
    public static RaidRoster? ForHearth(int hearthLevel) => hearthLevel switch
    {
        <= 0 => null,
        1 => CuriousLocals,
        2 => WulfricsLevy,
        _ => LordsAndChurch,
    };
}
