using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Contracts;
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

    /// <summary>
    /// The JSON save on the device. The document and its mapping are shared with the server (CultOfComfort.Contracts),
    /// so the device save and the server's wire format are the same JSON; this only does the text, with JsonUtility.
    /// </summary>
    public static class VillageSaveMapper
    {
        public static string ToJson(BaseData data) => JsonUtility.ToJson(VillageSaveMapping.ToDto(data));

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

            return VillageSaveMapping.FromDto(dto);
        }
    }
}
