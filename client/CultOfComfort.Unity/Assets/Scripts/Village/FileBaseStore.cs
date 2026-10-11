using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>The village save on the device: JSON via plain DTOs (the save format never leaks into the domain).</summary>
    public sealed class FileBaseStore : IBaseStore
    {
        private readonly string _path;

        public FileBaseStore(string path) => _path = path;

        public async Task<BaseData> LoadAsync(CancellationToken ct)
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            using (var reader = new StreamReader(_path))
            {
                var json = await reader.ReadToEndAsync();
                ct.ThrowIfCancellationRequested();
                return VillageSaveMapper.FromJson(json);
            }
        }

        public async Task SaveAsync(BaseData data, CancellationToken ct)
        {
            var temp = _path + ".tmp";
            using (var writer = new StreamWriter(temp, false))
            {
                await writer.WriteAsync(VillageSaveMapper.ToJson(data));
            }

            ct.ThrowIfCancellationRequested();
            if (File.Exists(_path))
            {
                File.Delete(_path); // File.Replace is unreliable on Mono/Android; the .tmp is complete before this
            }

            File.Move(temp, _path);
        }
    }

    /// <summary>Anti-corruption mapping between <see cref="BaseData"/> and the JSON save.</summary>
    public static class VillageSaveMapper
    {
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
                upgradeEndsAtTicks = b.UpgradeEndsAtUtcTicks ?? -1, bufferMilli = b.BufferMilli, asOfTicks = b.AsOfUtcTicks,
            }).ToList(),
            recentCommands = data.RecentCommands.Select(g => g.ToString()).ToList(),
        });

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

            var buildings = new List<BuildingData>();
            foreach (var b in dto.buildings)
            {
                if (!Enum.TryParse<BuildingType>(b.type, out var type))
                {
                    throw new InvalidBaseDataException($"unknown building type '{b.type}'");
                }

                buildings.Add(new BuildingData(b.id, type, b.level, b.x, b.y, b.upgradeEndsAtTicks < 0 ? (long?)null : b.upgradeEndsAtTicks, b.bufferMilli, b.asOfTicks));
            }

            var recent = (dto.recentCommands ?? new List<string>()).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
            return new BaseData(dto.version, new ResourceBag(dto.timber, dto.peat, dto.scrap, dto.devotion), dto.nextId, dto.lastNight, buildings, recent);
        }

        [Serializable]
        private sealed class VillageSaveDto
        {
            public int format = 1;
            public long version;
            public int timber;
            public int peat;
            public int scrap;
            public int devotion;
            public int nextId;
            public int lastNight;
            public List<BuildingDto> buildings;
            public List<string> recentCommands;
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
        }
    }
}
