using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

/// <summary>What a study improves (each tech has one effect of <see cref="Tech.Amount"/>).</summary>
public enum TechEffect
{
    /// <summary>Timber producers make Amount% more.</summary>
    TimberPercent,

    /// <summary>Peat producers make Amount% more.</summary>
    PeatPercent,

    /// <summary>Missionaries win Amount% more influence per sermon.</summary>
    MissionaryPercent,

    /// <summary>Buildings and towers stand Amount% more smashing in a Night.</summary>
    HealthPercent,

    /// <summary>Bathhouses give Amount more Comfort.</summary>
    BathComfort,

    /// <summary>Tithes calm Wulfric Amount% more.</summary>
    TithePercent,

    /// <summary>Influence fades Amount percentage points a day slower.</summary>
    SlowerDecay,

    /// <summary>Every store holds Amount% more.</summary>
    StoragePercent,

    /// <summary>Amount Comfort for the whole village.</summary>
    Comfort,

    /// <summary>Scrap producers make Amount% more.</summary>
    ScrapPercent,

    /// <summary>Devotion producers make Amount% more (before Comfort).</summary>
    DevotionPercent,

    /// <summary>Construction and upgrades take Amount% less time (set when the work starts).</summary>
    BuildTimePercent,

    /// <summary>A Night leaves Amount% more Scrap behind.</summary>
    SalvagePercent,

    /// <summary>Amount more Open Sluice charges a Night (into the Night plan, like a Cistern).</summary>
    SluiceCharges,

    /// <summary>Towers hit Amount% harder (into the Night plan).</summary>
    TowerDamagePercent,

    /// <summary>The Prophet has Amount% more health (into the Night plan).</summary>
    ProphetHealthPercent,

    /// <summary>The lord's letters ask for Amount% less.</summary>
    DemandPercent,

    /// <summary>The lord's temper rises Amount% more slowly.</summary>
    TemperPercent,
}

/// <summary>The four rooms of the manual (spec 2026-10-16 first day): a tab each on the Research sheet.</summary>
public enum TechCategory
{
    /// <summary>What the village makes and how fast it builds.</summary>
    Workshop,

    /// <summary>The fight: towers, walls, the sluice, the Prophet.</summary>
    Night,

    /// <summary>Faith, preachers and the lord.</summary>
    Pulpit,

    /// <summary>Comfort, and the baths it all started with.</summary>
    Bath,
}

/// <summary>A study at the Hearth: the Prophet reads the car's manual and works out something useful (GDD 03).</summary>
public sealed record Tech(string Id, string Name, string Joke, TechCategory Category, int UnlockHearth, ResourceBag Cost, int Minutes, TechEffect Effect, int Amount, string? Needs = null);

/// <summary>
/// The research tree (spec 2026-10-14 world map, slice 2; spec 2026-10-16 first day: four rooms). One study at a time;
/// bonuses last for ever. Ids never change: saves hold them.
/// </summary>
public sealed class ResearchBook
{
    private const TechCategory Workshop = TechCategory.Workshop, Night = TechCategory.Night, Pulpit = TechCategory.Pulpit, Bath = TechCategory.Bath;

    public static ResearchBook Default { get; } = new(new[]
    {
        // The workshop.
        new Tech("sharper-axes", "Sharper Axes", "The manual has a page on whetstones. It is the only useful page.", Workshop, 2, new ResourceBag(300, 0, 40, 30), 60, TechEffect.TimberPercent, 15),
        new Tech("peat-stacking", "Peat Stacking", "Stack it so the wind dries it. Revolutionary, says the Prophet.", Workshop, 2, new ResourceBag(250, 40, 20, 30), 60, TechEffect.PeatPercent, 15),
        new Tech("midden-sieve", "The Midden Sieve", "Sort the rubbish twice. The second time, it's treasure.", Workshop, 2, new ResourceBag(250, 0, 30, 20), 60, TechEffect.ScrapPercent, 25),
        new Tech("measure-twice", "Measure Twice", "Measure twice, cut once. He measured once, then three more times.", Workshop, 2, new ResourceBag(400, 0, 60, 30), 120, TechEffect.BuildTimePercent, 20, "sharper-axes"),
        new Tech("two-axes", "Two Axes", "One for each hand. Balance, says the Prophet.", Workshop, 3, new ResourceBag(900, 0, 120, 60), 180, TechEffect.TimberPercent, 20, "sharper-axes"),
        new Tech("turf-ricks", "Turf Ricks", "Rick it, roof it, stop it turning into soup.", Workshop, 3, new ResourceBag(700, 150, 80, 60), 180, TechEffect.PeatPercent, 20, "peat-stacking"),
        new Tech("bellows", "Bellows", "Blow on the fire. Harder. With a bag.", Workshop, 4, new ResourceBag(2000, 0, 300, 100), 300, TechEffect.ScrapPercent, 20, "midden-sieve"),
        new Tech("bigger-stores", "Bigger Storehouses", "Shelves. Actual shelves.", Workshop, 3, new ResourceBag(1200, 100, 250, 50), 300, TechEffect.StoragePercent, 20, "midden-sieve"),

        // The Night.
        new Tech("thicker-walls", "Thicker Walls", "Twice the mud, half the draught.", Night, 2, new ResourceBag(400, 0, 80, 20), 120, TechEffect.HealthPercent, 20),
        new Tech("bigger-bucket", "A Bigger Bucket", "A bigger bucket on the sluice. Engineering.", Night, 2, new ResourceBag(300, 0, 60, 20), 90, TechEffect.SluiceCharges, 1),
        new Tech("springier-saplings", "Springier Saplings", "Green wood bends further. The manual calls it 'potential energy'.", Night, 2, new ResourceBag(350, 0, 80, 30), 120, TechEffect.TowerDamagePercent, 20),
        new Tech("finders-keepers", "Finders Keepers", "Raiders drop things when they run. The faithful pick them up.", Night, 2, new ResourceBag(200, 0, 0, 40), 60, TechEffect.SalvagePercent, 50),
        new Tech("padded-cardigan", "A Padded Cardigan", "Quilted with sheep's wool and optimism.", Night, 3, new ResourceBag(800, 0, 150, 80), 180, TechEffect.ProphetHealthPercent, 25),
        new Tech("stickier-mud", "Stickier Mud", "Add straw. Add more straw.", Night, 3, new ResourceBag(1000, 100, 200, 60), 240, TechEffect.TowerDamagePercent, 20, "springier-saplings"),
        new Tech("brass-nozzle", "A Brass Nozzle", "Narrow the hose; widen the eyes of whoever's in front of it.", Night, 4, new ResourceBag(2000, 0, 400, 100), 360, TechEffect.TowerDamagePercent, 20, "stickier-mud"),
        new Tech("wattle-and-daub", "Wattle and Daub", "Twice the mud again. The draught has given up.", Night, 3, new ResourceBag(1000, 0, 200, 40), 240, TechEffect.HealthPercent, 20, "thicker-walls"),

        // The pulpit.
        new Tech("sermon-notes", "Sermon Notes", "Bullet points. The locals have never seen a bullet, or a point.", Pulpit, 2, new ResourceBag(200, 0, 0, 60), 90, TechEffect.MissionaryPercent, 25),
        new Tech("scented-candles", "Scented Candles", "Beeswax and a hint of wet dog. Very holy.", Pulpit, 2, new ResourceBag(250, 30, 0, 40), 90, TechEffect.DevotionPercent, 20),
        new Tech("grovelling", "Grovelling, Properly", "Bow lower. Lower. Not that low.", Pulpit, 2, new ResourceBag(200, 0, 30, 50), 90, TechEffect.DemandPercent, 20),
        new Tech("tithe-accounting", "Tithe Accounting", "The lord can't count. That's the trick.", Pulpit, 3, new ResourceBag(600, 0, 150, 120), 180, TechEffect.TithePercent, 25, "sermon-notes"),
        new Tech("long-sermons", "Long Sermons", "Nobody remembers what he said, so nobody forgets.", Pulpit, 3, new ResourceBag(500, 100, 0, 150), 240, TechEffect.SlowerDecay, 1, "sermon-notes"),
        new Tech("whispered-hymns", "Whispered Hymns", "Hymns, but quietly. The lord hears less to be cross about.", Pulpit, 3, new ResourceBag(700, 0, 100, 150), 240, TechEffect.TemperPercent, 20, "grovelling"),
        new Tech("pamphlets", "Pamphlets", "\"Have You Considered Not Being Cold?\" Illustrated.", Pulpit, 3, new ResourceBag(900, 0, 150, 120), 240, TechEffect.MissionaryPercent, 25, "sermon-notes"),
        new Tech("hymn-books", "Hymn Books", "Everyone sings the same words. Mostly the same words.", Pulpit, 4, new ResourceBag(1800, 0, 200, 300), 360, TechEffect.MissionaryPercent, 25, "pamphlets"),

        // The bath.
        new Tech("woolly-socks", "Woolly Socks", "Socks, with the Duck on them.", Bath, 2, new ResourceBag(250, 40, 0, 30), 60, TechEffect.Comfort, 5),
        new Tech("warm-stones", "Warm Stones", "Hot stones in the beds. Mind the toes.", Bath, 2, new ResourceBag(400, 80, 30, 40), 120, TechEffect.Comfort, 5, "woolly-socks"),
        new Tech("plumbing", "Plumbing, Probably", "Water goes down. Usually.", Bath, 3, new ResourceBag(900, 200, 200, 100), 240, TechEffect.BathComfort, 20, "woolly-socks"),
        new Tech("hot-water", "Hot Water for All", "The faith's central promise, finally kept.", Bath, 3, new ResourceBag(1500, 300, 300, 200), 480, TechEffect.Comfort, 15, "plumbing"),
        new Tech("birch-whisks", "Birch Whisks", "Hit yourself with a twig. It's relaxing, says the Prophet, wincing.", Bath, 4, new ResourceBag(2000, 300, 100, 200), 360, TechEffect.Comfort, 10, "hot-water"),
        new Tech("duck-ascendant", "The Duck Ascendant", "A bigger wooden duck. Faith follows size.", Bath, 3, new ResourceBag(1200, 200, 100, 100), 300, TechEffect.DevotionPercent, 15, "warm-stones"),
    });

    private readonly Dictionary<string, Tech> _byId;

    public ResearchBook(IReadOnlyList<Tech> techs)
    {
        Techs = techs;
        _byId = techs.ToDictionary(t => t.Id);
    }

    public IReadOnlyList<Tech> Techs { get; }

    public Tech? Get(string id) => _byId.TryGetValue(id, out var tech) ? tech : null;
}

/// <summary>Research as stored: what is done, and the study under way.</summary>
public sealed record ResearchData(IReadOnlyList<string> Done, string? Current, long? EndsAtUtcTicks);

public enum TechState
{
    Done,
    Studying,
    Available,
    Locked,
}

public sealed record TechView(Tech Tech, TechState State, string? Reason, System.DateTimeOffset? EndsAtUtc);
