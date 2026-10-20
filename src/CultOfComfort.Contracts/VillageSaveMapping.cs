using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Contracts;

/// <summary>
/// Anti-corruption mapping between <see cref="BaseData"/> and the village document (device save and server wire format).
/// Serialising the DTO to text is left to each side: JsonUtility on the device, System.Text.Json on the server.
/// </summary>
public static class VillageSaveMapping
{
    public const int CurrentFormat = 1;

    public static VillageSaveDto ToDto(BaseData data) => new()
    {
        version = data.Version,
        timber = data.Stock.Timber,
        peat = data.Stock.Peat,
        scrap = data.Stock.Scrap,
        devotion = data.Stock.Devotion,
        nextId = data.NextId,
        lastNight = data.LastNightRecorded,
        buildings = data.Buildings.Select(b => new BuildingDto
        {
            id = b.Id, type = b.Type.ToString(), level = b.Level, x = b.X, y = b.Y,
            upgradeEndsAtTicks = b.UpgradeEndsAtUtcTicks ?? -1, bufferMilli = b.BufferMilli, asOfTicks = b.AsOfUtcTicks, wrecked = b.Wrecked, rotated = b.Rotated, flipped = b.Flipped, patrol = b.Patrol, crew = b.Crew ?? -1,
        }).ToList(),
        recentCommands = data.RecentCommands.Select(g => g.ToString()).ToList(),
        tensionMilli = data.TensionMilli,
        tensionAsOfTicks = data.TensionAsOfUtcTicks,
        shieldEndsAtTicks = data.ShieldEndsAtUtcTicks ?? -1,
        nextRaidId = data.NextRaidId,
        hasRaid = data.Raid != null,
        raid = data.Raid == null ? new RaidDto() : new RaidDto { id = data.Raid.Id, seed = data.Raid.Seed.ToString(), warningEndsAtTicks = data.Raid.WarningEndsAtUtcTicks },
        hasNight = data.NightInProgress != null,
        night = data.NightInProgress == null ? new NightDto() : ToDto(data.NightInProgress),
        nightStartedAtTicks = data.NightStartedAtUtcTicks,
        hasReport = data.UnseenReport != null,
        report = data.UnseenReport == null ? new ReportDto() : ToDto(data.UnseenReport),
        cleared = (data.ClearedTerrain ?? Array.Empty<GridPosition>()).Select(t => new TileDto { x = t.X, y = t.Y }).ToList(),
        treeCuts = (data.TreeCuts ?? Array.Empty<TreeCutData>()).Select(c => new TreeCutDto { x = c.X, y = c.Y, ticks = c.CutAtUtcTicks }).ToList(),
        layoutVersion = data.LayoutVersion,
        population = data.Population,
        populationAsOfTicks = data.PopulationAsOfUtcTicks,
        upkeepAsOfTicks = data.UpkeepAsOfUtcTicks,
        upkeepMilli = data.UpkeepMilli,
        fractionsMilli = (data.StockFractions ?? new long[4]).ToList(),
        cold = data.Cold,
        errandIndex = data.ErrandIndex,
        errandEventDone = data.ErrandEventDone,
        errandId = data.ErrandId ?? string.Empty,
        raidersRoad = data.RaidersRoad,
        answeredVisitorSlot = data.AnsweredVisitorSlot,
        prophetXp = data.ProphetXp,
        prophetPerks = (data.ProphetPerks ?? Array.Empty<string>()).ToList(),
        trialsCleared = data.TrialsCleared,
        hasTrial = data.TrialInProgress != null,
        trial = data.TrialInProgress == null ? new NightDto() : ToDto(data.TrialInProgress),
        almsAsOfTicks = data.AlmsAsOfUtcTicks,
        festivalDay = data.FestivalDay,
        festivalPoints = data.FestivalPoints,
        festivalClaimed = (data.FestivalClaimed ?? Array.Empty<bool>()).ToList(),
        notableIds = (data.Notables ?? Array.Empty<(string, int)>()).Select(n => n.Item1).ToList(),
        notableBuildings = (data.Notables ?? Array.Empty<(string, int)>()).Select(n => n.Item2).ToList(),
        stowed = (data.Stowed ?? Array.Empty<StowedTower>()).Select(t => new StowedDto { type = t.Type.ToString(), level = t.Level, wrecked = t.Wrecked }).ToList(),
        researched = data.Research?.Done.ToList() ?? new List<string>(),
        researching = data.Research?.Current ?? string.Empty,
        researchEndsTicks = data.Research?.EndsAtUtcTicks ?? 0,
        hasDaily = data.Daily != null,
        daily = data.Daily == null ? new DailyDto() : new DailyDto
        {
            day = data.Daily.Day, offsetMinutes = data.Daily.OffsetMinutes, ids = data.Daily.Ids.ToList(), progress = data.Daily.Progress.ToList(),
            claimed = data.Daily.Claimed.ToList(), thermos = data.Daily.Thermos, streak = data.Daily.Streak, lastThermosDay = data.Daily.LastThermosDay,
            towels = data.Daily.Towels, cosmetics = data.Daily.Cosmetics.ToList(),
        },
        hasDomain = data.Domain != null,
        domain = data.Domain == null ? new DomainDto() : new DomainDto
        {
            influence = data.Domain.Influence.Select(i => new InfluenceDto { node = i.NodeId, milli = i.InfluenceMilli }).ToList(),
            missionaries = data.Domain.Missionaries.Select(m => new MissionaryDto { id = m.Id, node = m.NodeId, hours = m.Hours, departedTicks = m.DepartedAtUtcTicks, preached = m.Preached, distance = m.Distance ?? 0, preacher = m.PreacherId }).ToList(),
            offeringsMilli = data.Domain.OfferingsMilli.ToList(),
            asOfTicks = data.Domain.AsOfUtcTicks,
            nextDecayTicks = data.Domain.NextDecayUtcTicks ?? -1,
            lastTitheTicks = data.Domain.LastTitheUtcTicks ?? -1,
            nextMissionaryId = data.Domain.NextMissionaryId,
            inWorld = data.Domain.World != null,
            worldQ = data.Domain.World?.Q ?? 0,
            worldR = data.Domain.World?.R ?? 0,
            worldTensionMilli = data.Domain.World?.TensionPerHourMilli ?? 0,
            worldPatronages = data.Domain.World?.Patronages ?? 0,
            preachers = (data.Domain.PreacherXp ?? Array.Empty<(int, int)>()).Select(p => new PreacherXpDto { id = p.Item1, xp = p.Item2 }).ToList(),
            reports = (data.Domain.Reports ?? Array.Empty<RoadReport>()).Select(r => new RoadReportDto
            {
                preacher = r.PreacherId, node = r.NodeId, inWorld = r.InWorld, hours = r.Hours, find = r.Find.ToString(),
                tale = r.Tale?.ToString() ?? string.Empty, choice = r.Choice,
                timber = r.Gift.Timber, peat = r.Gift.Peat, scrap = r.Gift.Scrap, devotion = r.Gift.Devotion, homeTicks = r.HomeAtUtcTicks,
            }).ToList(),
            hasDemand = data.Domain.Demand != null,
            demandId = data.Domain.Demand?.Id ?? 0,
            demandTimber = data.Domain.Demand?.Amount.Timber ?? 0,
            demandPeat = data.Domain.Demand?.Amount.Peat ?? 0,
            demandScrap = data.Domain.Demand?.Amount.Scrap ?? 0,
            demandDevotion = data.Domain.Demand?.Amount.Devotion ?? 0,
            demandDueTicks = data.Domain.Demand?.DueAtUtcTicks ?? 0,
            nextDemandTicks = data.Domain.NextDemandUtcTicks ?? -1,
            nextDemandId = data.Domain.NextDemandId,
            demandStalled = data.Domain.Demand?.Stalled ?? false,
            demandHaggle = (data.Domain.Demand?.Haggle ?? DemandHaggle.None).ToString(),
            demandFlattered = data.Domain.Demand?.Flattered ?? false,
            tales = (data.Domain.Tales ?? Array.Empty<TaleData>()).Select(t => new TaleDto
            {
                id = t.Id, preacher = t.PreacherId, node = t.NodeId, inWorld = t.InWorld, hours = t.Hours, tale = t.Tale.ToString(), homeTicks = t.HomeAtUtcTicks,
            }).ToList(),
        },
    };

    /// <exception cref="InvalidBaseDataException">The document is empty, from another format, or names things that don't exist.</exception>
    public static BaseData FromDto(VillageSaveDto? dto)
    {
        if (dto == null || dto.buildings == null)
        {
            throw new InvalidBaseDataException("empty save");
        }

        if (dto.format != CurrentFormat)
        {
            throw new InvalidBaseDataException($"save format {dto.format} is not {CurrentFormat}");
        }

        var buildings = new List<BuildingData>();
        foreach (var b in dto.buildings)
        {
            if (!Enum.TryParse<BuildingType>(b.type, out var type))
            {
                throw new InvalidBaseDataException($"unknown building type '{b.type}'");
            }

            buildings.Add(new BuildingData(b.id, type, b.level, b.x, b.y, b.upgradeEndsAtTicks < 0 ? null : b.upgradeEndsAtTicks, b.bufferMilli, b.asOfTicks, b.wrecked, b.rotated, b.crew < 0 ? null : b.crew, b.flipped, b.patrol));
        }

        var recent = (dto.recentCommands ?? new List<string>()).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
        return new BaseData(dto.version, new ResourceBag(dto.timber, dto.peat, dto.scrap, dto.devotion), dto.nextId, dto.lastNight, buildings, recent,
            dto.tensionMilli, dto.tensionAsOfTicks, dto.shieldEndsAtTicks < 0 ? null : dto.shieldEndsAtTicks, Math.Max(1, dto.nextRaidId),
            dto.hasRaid && dto.raid != null ? new RaidData(dto.raid.id, ParseSeed(dto.raid.seed), dto.raid.warningEndsAtTicks) : null,
            dto.hasNight && dto.night != null ? FromDto(dto.night) : null,
            dto.nightStartedAtTicks,
            dto.hasReport && dto.report != null ? FromDto(dto.report) : null,
            (dto.cleared ?? new List<TileDto>()).Select(t => new GridPosition(t.x, t.y)).ToList(),
            (dto.treeCuts ?? new List<TreeCutDto>()).Select(c => new TreeCutData(c.x, c.y, c.ticks)).ToList(),
            dto.layoutVersion,
            dto.population,
            dto.populationAsOfTicks,
            dto.upkeepAsOfTicks,
            dto.upkeepMilli,
            dto.cold,
            dto.errandIndex,
            dto.errandEventDone,
            dto.hasDomain && dto.domain != null ? FromDto(dto.domain) : null,
            new ResearchData(dto.researched ?? new List<string>(), string.IsNullOrEmpty(dto.researching) ? null : dto.researching,
                string.IsNullOrEmpty(dto.researching) ? null : dto.researchEndsTicks),
            dto.fractionsMilli != null && dto.fractionsMilli.Count == 4 && dto.fractionsMilli.TrueForAll(f => f >= 0 && f < 1000) ? dto.fractionsMilli : null,
            dto.hasDaily && dto.daily != null
                ? new DailyData(dto.daily.day, dto.daily.offsetMinutes, dto.daily.ids ?? new List<string>(), dto.daily.progress ?? new List<int>(),
                    dto.daily.claimed ?? new List<bool>(), dto.daily.thermos, dto.daily.streak, dto.daily.lastThermosDay, dto.daily.towels, dto.daily.cosmetics ?? new List<string>())
                : null,
            string.IsNullOrEmpty(dto.errandId) ? null : dto.errandId,
            dto.raidersRoad,
            (dto.stowed ?? new List<StowedDto>()).Select(t => new StowedTower(
                Enum.TryParse<BuildingType>(t.type, out var type) ? type : throw new InvalidBaseDataException($"unknown tower type '{t.type}'"), t.level, t.wrecked)).ToList(),
            dto.answeredVisitorSlot,
            dto.prophetXp,
            dto.prophetPerks ?? new List<string>(),
            (dto.notableIds ?? new List<string>()).Zip(dto.notableBuildings ?? new List<int>(), (n, b) => (n, b)).ToList(),
            dto.trialsCleared,
            dto.hasTrial && dto.trial != null ? FromDto(dto.trial) : null,
            dto.almsAsOfTicks,
            dto.festivalDay,
            dto.festivalPoints,
            dto.festivalClaimed ?? new List<bool>());
    }

    private static DomainData FromDto(DomainDto d) => new(
        (d.influence ?? new List<InfluenceDto>()).Select(i => (i.node, i.milli)).ToList(),
        (d.missionaries ?? new List<MissionaryDto>()).Select(m => new MissionaryData(m.id, m.node, m.hours, m.departedTicks, m.preached, m.distance > 0 ? m.distance : null, m.preacher)).ToList(),
        d.offeringsMilli != null && d.offeringsMilli.Count == 4 ? d.offeringsMilli : new List<long> { 0, 0, 0, 0 },
        d.asOfTicks,
        d.nextDecayTicks < 0 ? null : d.nextDecayTicks,
        d.lastTitheTicks < 0 ? null : d.lastTitheTicks,
        d.nextMissionaryId,
        d.inWorld ? new WorldLink(d.worldQ, d.worldR, d.worldTensionMilli, d.worldPatronages) : null,
        (d.preachers ?? new List<PreacherXpDto>()).Select(p => (p.id, p.xp)).ToList(),
        (d.reports ?? new List<RoadReportDto>()).Select(r => new RoadReport(r.preacher, r.node, r.inWorld, r.hours,
            Enum.TryParse<RoadFind>(r.find, out var find) ? find : RoadFind.Nothing, new ResourceBag(r.timber, r.peat, r.scrap, r.devotion), r.homeTicks,
            Enum.TryParse<RoadTale>(r.tale, out var tale) ? tale : null, r.choice)).ToList(),
        d.hasDemand
            ? new DemandData(d.demandId, new ResourceBag(d.demandTimber, d.demandPeat, d.demandScrap, d.demandDevotion), d.demandDueTicks, d.demandStalled,
                Enum.TryParse<DemandHaggle>(d.demandHaggle, out var haggle) ? haggle : DemandHaggle.None, d.demandFlattered)
            : null,
        d.nextDemandTicks < 0 ? null : d.nextDemandTicks,
        Math.Max(1, d.nextDemandId),
        (d.tales ?? new List<TaleDto>()).Select(t => new TaleData(t.id, t.preacher, t.node, t.inWorld, t.hours,
            Enum.TryParse<RoadTale>(t.tale, out var told) ? told : throw new InvalidBaseDataException($"unknown road tale '{t.tale}'"), t.homeTicks)).ToList());

    internal static NightDto ToDto(NightPlan plan) => new()
    {
        night = plan.Night, seed = plan.Seed.ToString(), budget = plan.Budget, waves = plan.Waves, extraSluice = plan.ExtraSluiceCharges, scheduled = plan.Scheduled,
        roster = plan.Roster,
        towers = plan.Towers.Select(t => new TowerDto { type = t.Type.ToString(), x = t.Position.X, y = t.Position.Y, level = t.Level, manned = t.Manned }).ToList(),
        obstacles = plan.Obstacles.Select(o => new TileDto { x = o.X, y = o.Y }).ToList(),
        structures = plan.Structures.Select(st => new StructureDto
        {
            id = st.Id, x = st.Position.X, y = st.Position.Y, max = st.MaxHealth, health = st.Health, barrier = st.Barrier, gate = st.Gate, decoy = st.Decoy,
            tiles = st.Tiles?.Select(t => new TileDto { x = t.X, y = t.Y }).ToList() ?? new List<TileDto>(),
        }).ToList(),
        autoRelics = plan.AutoRelics,
        defenders = (plan.Defenders ?? Array.Empty<GridPosition>()).Select(t => new TileDto { x = t.X, y = t.Y }).ToList(),
        towerDamage = plan.TowerDamagePercent,
        prophetHealth = plan.ProphetHealthPercent,
        road = plan.Road,
        prophetDamage = plan.ProphetDamagePercent,
        prophetSpeed = plan.ProphetSpeedPercent,
        voice = plan.VoicePercent,
        stones = plan.StonesPercent,
        enemySpeed = plan.EnemySpeedPercent,
        salvage = plan.SalvagePercent,
        defenderDamage = plan.DefenderDamagePercent,
        traps = (plan.Traps ?? Array.Empty<GridPosition>()).Select(t => new TileDto { x = t.X, y = t.Y }).ToList(),
    };

    /// <summary>A Night plan from its wire form (the server's answer to BeginNight).</summary>
    public static NightPlan PlanFromDto(NightDto d) => FromDto(d);

    /// <summary>A Night plan in its wire form.</summary>
    public static NightDto PlanToDto(NightPlan plan) => ToDto(plan);

    /// <summary>A Night report from its wire form (the server's answer to ResolveNight).</summary>
    public static NightReport ReportFromDto(ReportDto d) => FromDto(d);

    private static NightPlan FromDto(NightDto d)
    {
        var towers = new List<PlacedTower>();
        foreach (var t in d.towers ?? new List<TowerDto>())
        {
            if (!Enum.TryParse<TowerType>(t.type, out var type))
            {
                throw new InvalidBaseDataException($"unknown tower type '{t.type}'");
            }

            towers.Add(new PlacedTower(type, new GridPosition(t.x, t.y), Math.Max(1, t.level), t.manned));
        }

        return new NightPlan(d.night, ParseSeed(d.seed), d.budget, d.waves, d.extraSluice, d.scheduled, towers,
            (d.obstacles ?? new List<TileDto>()).Select(o => new GridPosition(o.x, o.y)).ToList(),
            (d.structures ?? new List<StructureDto>()).Select(st => new PlacedStructure(st.id, new GridPosition(st.x, st.y), st.max, st.health)
            {
                // An empty list means "no footprint"; the plan means that as null (just Position).
                Tiles = st.tiles == null || st.tiles.Count == 0 ? null : st.tiles.Select(t => new GridPosition(t.x, t.y)).ToList(),
                Barrier = st.barrier, Gate = st.gate, Decoy = st.decoy,
            }).ToList(),
            d.roster,
            d.autoRelics,
            (d.defenders ?? new List<TileDto>()).Select(t => new GridPosition(t.x, t.y)).ToList(),
            Math.Max(0, d.towerDamage), Math.Max(0, d.prophetHealth), Math.Max(0, d.road),
            (d.traps ?? new List<TileDto>()).Select(t => new GridPosition(t.x, t.y)).ToList(),
            Math.Max(0, d.prophetDamage), Math.Max(0, d.prophetSpeed), Math.Max(0, d.voice), Math.Max(0, d.stones),
            Math.Clamp(d.enemySpeed, -90, 0), Math.Max(0, d.salvage), Math.Max(0, d.defenderDamage));
    }

    internal static ReportDto ToDto(NightReport r) => new()
    {
        night = r.Night, scheduled = r.Scheduled, whileAway = r.WhileAway, stars = r.Stars, hearth = r.HearthHealth, hearthMax = r.HearthMaxHealth,
        wrecked = r.Wrecked.ToList(), salvage = r.Salvage, hash = r.StateHashHex,
    };

    private static NightReport FromDto(ReportDto d) =>
        new(d.night, d.scheduled, d.whileAway, d.stars, d.hearth, d.hearthMax, d.wrecked ?? new List<int>(), d.salvage, d.hash ?? string.Empty);

    private static ulong ParseSeed(string? text) =>
        ulong.TryParse(text, out var seed) ? seed : throw new InvalidBaseDataException("bad seed");
}
