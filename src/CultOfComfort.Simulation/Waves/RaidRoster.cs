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

    /// <summary>The south road at Hearth 2: Friar Odo's flock and the pilgrims who tag along.</summary>
    public static RaidRoster OdosFlock { get; } = new(new[]
    {
        (EnemyType.Zealot, 40), (EnemyType.Peasant, 30), (EnemyType.Priest, 15), (EnemyType.Levy, 15),
    }, null);

    /// <summary>The south road from Hearth 3: the Bishop's pilgrimage, the Bishop at its head.</summary>
    public static RaidRoster BishopsPilgrimage { get; } = new(new[]
    {
        (EnemyType.Zealot, 30), (EnemyType.Priest, 25), (EnemyType.Peasant, 18), (EnemyType.Levy, 15), (EnemyType.Bishop, 12),
    }, EnemyType.Bishop);

    /// <summary>The east road at Hearth 3: whatever lives in the far woods, driven out by the noise.</summary>
    public static RaidRoster WildBeasts { get; } = new(new[]
    {
        (EnemyType.Boar, 35), (EnemyType.Goat, 30), (EnemyType.Goose, 20), (EnemyType.Peasant, 15),
    }, null);

    /// <summary>The east road from Hearth 4: the beasts, and the lord's huntsmen after them.</summary>
    public static RaidRoster TheHunt { get; } = new(new[]
    {
        (EnemyType.Boar, 35), (EnemyType.Goat, 20), (EnemyType.Goose, 15), (EnemyType.ManAtArms, 20), (EnemyType.Levy, 10),
    }, null);

    /// <summary>
    /// Who comes by a road (spec 2026-10-20 across the river): the north road is the lord's (by Hearth, as always), the
    /// south road the church's, the east road the wild's. Road 0 is exactly <see cref="ForHearth"/>, so old Nights replay.
    /// </summary>
    public static RaidRoster? ForRoad(int hearthLevel, int road) => road switch
    {
        1 when hearthLevel >= 2 => hearthLevel >= 3 ? BishopsPilgrimage : OdosFlock,
        2 when hearthLevel >= 3 => hearthLevel >= 4 ? TheHunt : WildBeasts,
        _ => ForHearth(hearthLevel),
    };

    /// <summary>What the scout says is coming down a road, in a word or two (it fits on the Tension strip).</summary>
    public static string Who(int hearthLevel, int road) => road switch
    {
        1 when hearthLevel >= 3 => "the Bishop",
        1 when hearthLevel >= 2 => "Odo's flock",
        2 when hearthLevel >= 4 => "the hunt",
        2 when hearthLevel >= 3 => "wild beasts",
        _ => hearthLevel <= 1 ? "locals" : hearthLevel == 2 ? "the levy" : "Wulfric",
    };

    /// <summary>The roster for a Hearth level (0 or less: none — the classic demo mix).</summary>
    public static RaidRoster? ForHearth(int hearthLevel) => hearthLevel switch
    {
        <= 0 => null,
        1 => CuriousLocals,
        2 => WulfricsLevy,
        _ => LordsAndChurch,
    };
}
