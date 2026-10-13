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
            upgradeEndsAtTicks = b.UpgradeEndsAtUtcTicks ?? -1, bufferMilli = b.BufferMilli, asOfTicks = b.AsOfUtcTicks, wrecked = b.Wrecked, rotated = b.Rotated,
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
        cold = data.Cold,
        errandIndex = data.ErrandIndex,
        errandEventDone = data.ErrandEventDone,
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

            buildings.Add(new BuildingData(b.id, type, b.level, b.x, b.y, b.upgradeEndsAtTicks < 0 ? null : b.upgradeEndsAtTicks, b.bufferMilli, b.asOfTicks, b.wrecked, b.rotated));
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
            dto.errandEventDone);
    }

    internal static NightDto ToDto(NightPlan plan) => new()
    {
        night = plan.Night, seed = plan.Seed.ToString(), budget = plan.Budget, waves = plan.Waves, extraSluice = plan.ExtraSluiceCharges, scheduled = plan.Scheduled,
        roster = plan.Roster,
        towers = plan.Towers.Select(t => new TowerDto { type = t.Type.ToString(), x = t.Position.X, y = t.Position.Y }).ToList(),
        obstacles = plan.Obstacles.Select(o => new TileDto { x = o.X, y = o.Y }).ToList(),
        structures = plan.Structures.Select(st => new StructureDto
        {
            id = st.Id, x = st.Position.X, y = st.Position.Y, max = st.MaxHealth, health = st.Health, barrier = st.Barrier, gate = st.Gate,
            tiles = st.Tiles?.Select(t => new TileDto { x = t.X, y = t.Y }).ToList() ?? new List<TileDto>(),
        }).ToList(),
    };

    /// <summary>A Night plan from its wire form (the server's answer to BeginNight).</summary>
    public static NightPlan PlanFromDto(NightDto d) => FromDto(d);

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

            towers.Add(new PlacedTower(type, new GridPosition(t.x, t.y)));
        }

        return new NightPlan(d.night, ParseSeed(d.seed), d.budget, d.waves, d.extraSluice, d.scheduled, towers,
            (d.obstacles ?? new List<TileDto>()).Select(o => new GridPosition(o.x, o.y)).ToList(),
            (d.structures ?? new List<StructureDto>()).Select(st => new PlacedStructure(st.id, new GridPosition(st.x, st.y), st.max, st.health)
            {
                // An empty list means "no footprint"; the plan means that as null (just Position).
                Tiles = st.tiles == null || st.tiles.Count == 0 ? null : st.tiles.Select(t => new GridPosition(t.x, t.y)).ToList(),
                Barrier = st.barrier, Gate = st.gate,
            }).ToList(),
            d.roster);
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
