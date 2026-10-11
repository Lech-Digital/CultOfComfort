using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>The village save on the device: JSON via plain DTOs (the save format never leaks into the domain).</summary>
    public sealed class FileBaseStore : IBaseStore
    {
        private readonly string _path;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public FileBaseStore(string path) => _path = path;

        public async Task<BaseData> LoadAsync(CancellationToken ct)
        {
            // A save interrupted between removing the old file and moving the new one in leaves only the complete .tmp.
            var path = File.Exists(_path) ? _path : File.Exists(_path + ".tmp") ? _path + ".tmp" : null;
            if (path == null)
            {
                return null;
            }

            using (var reader = new StreamReader(path))
            {
                var json = await reader.ReadToEndAsync();
                ct.ThrowIfCancellationRequested();
                return VillageSaveMapper.FromJson(json);
            }
        }

        public Task QuarantineAsync(CancellationToken ct)
        {
            var path = File.Exists(_path) ? _path : File.Exists(_path + ".tmp") ? _path + ".tmp" : null;
            if (path != null)
            {
                File.Copy(path, $"{_path}.unreadable-{DateTime.UtcNow:yyyyMMddHHmmss}", overwrite: true);
            }

            return Task.CompletedTask;
        }

        public async Task SaveAsync(BaseData data, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var temp = _path + ".tmp";
                var bytes = System.Text.Encoding.UTF8.GetBytes(VillageSaveMapper.ToJson(data));
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length, ct);
                    stream.Flush(true); // on the disk before the old save goes away
                }

                if (File.Exists(_path))
                {
                    File.Delete(_path); // File.Replace is unreliable on Mono/Android; LoadAsync falls back to the .tmp
                }

                File.Move(temp, _path);
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    /// <summary>Anti-corruption mapping between <see cref="BaseData"/> and the JSON save.</summary>
    public static class VillageSaveMapper
    {
        private const int CurrentFormat = 1;

        public static string ToJson(BaseData data) => JsonUtility.ToJson(new VillageSaveDto
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
        });

        private static NightDto ToDto(NightPlan plan) => new NightDto
        {
            night = plan.Night, seed = plan.Seed.ToString(), budget = plan.Budget, waves = plan.Waves, extraSluice = plan.ExtraSluiceCharges, scheduled = plan.Scheduled,
            towers = plan.Towers.Select(t => new TowerDto { type = t.Type.ToString(), x = t.Position.X, y = t.Position.Y }).ToList(),
            obstacles = plan.Obstacles.Select(o => new TileDto { x = o.X, y = o.Y }).ToList(),
            structures = plan.Structures.Select(st => new StructureDto { id = st.Id, x = st.Position.X, y = st.Position.Y, max = st.MaxHealth, health = st.Health }).ToList(),
        };

        private static NightPlan FromDto(NightDto d)
        {
            var towers = new List<PlacedTower>();
            foreach (var t in d.towers ?? new List<TowerDto>())
            {
                if (!Enum.TryParse<CultOfComfort.Simulation.Balance.TowerType>(t.type, out var type))
                {
                    throw new InvalidBaseDataException($"unknown tower type '{t.type}'");
                }

                towers.Add(new PlacedTower(type, new GridPosition(t.x, t.y)));
            }

            return new NightPlan(d.night, ParseSeed(d.seed), d.budget, d.waves, d.extraSluice, d.scheduled, towers,
                (d.obstacles ?? new List<TileDto>()).Select(o => new GridPosition(o.x, o.y)).ToList(),
                (d.structures ?? new List<StructureDto>()).Select(st => new PlacedStructure(st.id, new GridPosition(st.x, st.y), st.max, st.health)).ToList());
        }

        private static ReportDto ToDto(NightReport r) => new ReportDto
        {
            night = r.Night, scheduled = r.Scheduled, whileAway = r.WhileAway, stars = r.Stars, hearth = r.HearthHealth, hearthMax = r.HearthMaxHealth,
            wrecked = r.Wrecked.ToList(), salvage = r.Salvage, hash = r.StateHashHex,
        };

        private static NightReport FromDto(ReportDto d) =>
            new NightReport(d.night, d.scheduled, d.whileAway, d.stars, d.hearth, d.hearthMax, d.wrecked ?? new List<int>(), d.salvage, d.hash ?? string.Empty);

        private static ulong ParseSeed(string text) =>
            ulong.TryParse(text, out var seed) ? seed : throw new InvalidBaseDataException("bad seed");

        public static BaseData FromJson(string json)
        {
            VillageSaveDto dto;
            try
            {
                dto = JsonUtility.FromJson<VillageSaveDto>(json);
            }
            catch (ArgumentException e)
            {
                throw new InvalidBaseDataException("not JSON: " + e.Message);
            }

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

                buildings.Add(new BuildingData(b.id, type, b.level, b.x, b.y, b.upgradeEndsAtTicks < 0 ? (long?)null : b.upgradeEndsAtTicks, b.bufferMilli, b.asOfTicks, b.wrecked, b.rotated));
            }

            var recent = (dto.recentCommands ?? new List<string>()).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
            return new BaseData(dto.version, new ResourceBag(dto.timber, dto.peat, dto.scrap, dto.devotion), dto.nextId, dto.lastNight, buildings, recent,
                dto.tensionMilli, dto.tensionAsOfTicks, dto.shieldEndsAtTicks < 0 ? (long?)null : dto.shieldEndsAtTicks, Math.Max(1, dto.nextRaidId),
                dto.hasRaid ? new RaidData(dto.raid.id, ParseSeed(dto.raid.seed), dto.raid.warningEndsAtTicks) : null,
                dto.hasNight ? FromDto(dto.night) : null,
                dto.nightStartedAtTicks,
                dto.hasReport ? FromDto(dto.report) : null,
                (dto.cleared ?? new List<TileDto>()).Select(t => new GridPosition(t.x, t.y)).ToList(),
                (dto.treeCuts ?? new List<TreeCutDto>()).Select(c => new TreeCutData(c.x, c.y, c.ticks)).ToList(),
                dto.layoutVersion,
                dto.population,
                dto.populationAsOfTicks,
                dto.upkeepAsOfTicks,
                dto.upkeepMilli,
                dto.cold);
        }

        [Serializable]
        private sealed class VillageSaveDto
        {
            public int format = CurrentFormat;
            public long version;
            public int timber;
            public int peat;
            public int scrap;
            public int devotion;
            public int nextId;
            public int lastNight;
            public List<BuildingDto> buildings;
            public List<string> recentCommands;
            public long tensionMilli;
            public long tensionAsOfTicks;
            public long shieldEndsAtTicks = -1;
            public int nextRaidId = 1;
            public bool hasRaid;
            public RaidDto raid;
            public bool hasNight;
            public NightDto night;
            public long nightStartedAtTicks;
            public bool hasReport;
            public ReportDto report;
            public List<TileDto> cleared;
            public List<TreeCutDto> treeCuts;
            public int layoutVersion; // 0 in saves from before building shapes: the village migrates them
            public int population;
            public long populationAsOfTicks;
            public long upkeepAsOfTicks;
            public long upkeepMilli;
            public bool cold;
        }

        [Serializable]
        private sealed class TreeCutDto
        {
            public int x;
            public int y;
            public long ticks;
        }

        [Serializable]
        private sealed class RaidDto
        {
            public int id;
            public string seed = "0";
            public long warningEndsAtTicks;
        }

        [Serializable]
        private sealed class NightDto
        {
            public int night;
            public string seed = "0";
            public int budget;
            public int waves;
            public int extraSluice;
            public bool scheduled;
            public List<TowerDto> towers;
            public List<TileDto> obstacles;
            public List<StructureDto> structures;
        }

        [Serializable]
        private sealed class TowerDto
        {
            public string type;
            public int x;
            public int y;
        }

        [Serializable]
        private sealed class TileDto
        {
            public int x;
            public int y;
        }

        [Serializable]
        private sealed class StructureDto
        {
            public int id;
            public int x;
            public int y;
            public int max;
            public int health;
        }

        [Serializable]
        private sealed class ReportDto
        {
            public int night;
            public bool scheduled;
            public bool whileAway;
            public int stars;
            public int hearth;
            public int hearthMax;
            public List<int> wrecked;
            public int salvage;
            public string hash;
        }

        [Serializable]
        private sealed class BuildingDto
        {
            public int id;
            public string type;
            public int level;
            public int x;
            public int y;
            public long upgradeEndsAtTicks = -1;
            public long bufferMilli;
            public long asOfTicks;
            public bool wrecked;
            public bool rotated;
        }
    }
}
