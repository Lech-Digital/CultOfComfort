using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Contracts;
using UnityEngine;
using UnityEngine.Networking;

namespace CultOfComfort.Client
{
    /// <summary>The village API over HTTPS with UnityWebRequest; JSON by JsonUtility on the shared contract DTOs.</summary>
    public sealed class HttpVillageApi : IVillageApi
    {
        private const int TimeoutSeconds = 10;
        private readonly string _baseUrl;
        private readonly string _devKey;

        /// <param name="devKey">The owner's testing key (sent as X-Dev-Key), or null.</param>
        public HttpVillageApi(string baseUrl, string devKey = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _devKey = devKey;
        }

        public async Task<GuestDto> StartGuestAsync(CancellationToken ct)
        {
            var (status, text) = await SendAsync("POST", "/api/auth/guest", null, null, ct);
            return status == 200 ? JsonUtility.FromJson<GuestDto>(text) : null;
        }

        public Task<ApiReply> GetAsync(string token, CancellationToken ct) => VillageAsync("GET", "/api/base", token, null, ct);

        public async Task<string> RecoveryCodeAsync(string token, CancellationToken ct)
        {
            var (status, text) = await SendAsync("POST", "/api/auth/recovery-code", token, null, ct);
            return status == 200 ? JsonUtility.FromJson<RecoveryCodeDto>(text)?.code : null;
        }

        public async Task<GuestDto> RecoverAsync(string code, CancellationToken ct)
        {
            var (status, text) = await SendAsync("POST", "/api/auth/recover", null, JsonUtility.ToJson(new RecoverRequestDto { code = code }), ct);
            return status == 200 ? JsonUtility.FromJson<GuestDto>(text) : null;
        }

        public async Task<WorldDto> JoinWorldAsync(string token, CancellationToken ct)
        {
            var (status, text) = await SendAsync("POST", "/api/world/join", token, null, ct);
            return status == 200 ? JsonUtility.FromJson<WorldDto>(text) : null;
        }

        public async Task<WorldDto> GetWorldAsync(string token, int q, int r, int radius, CancellationToken ct)
        {
            var (status, text) = await SendAsync("GET", $"/api/world?q={q}&r={r}&radius={radius}", token, null, ct);
            return status == 200 ? JsonUtility.FromJson<WorldDto>(text) : null;
        }

        public async Task<WorldCollectDto> CollectWorldAsync(string token, CancellationToken ct)
        {
            var (status, text) = await SendAsync("POST", "/api/world/collect", token, null, ct);
            return status == 200 ? JsonUtility.FromJson<WorldCollectDto>(text) : null;
        }

        public Task<ApiReply> SyncAsync(string token, SyncRequestDto request, CancellationToken ct) =>
            VillageAsync("POST", "/api/base/sync", token, JsonUtility.ToJson(request), ct);

        private async Task<ApiReply> VillageAsync(string method, string path, string token, string body, CancellationToken ct)
        {
            var (status, text) = await SendAsync(method, path, token, body, ct);
            if (status == 0)
            {
                return ApiReply.Offline;
            }

            VillageResponseDto village = null;
            if ((status == 200 || status == 409) && !string.IsNullOrEmpty(text))
            {
                try
                {
                    village = JsonUtility.FromJson<VillageResponseDto>(text);
                }
                catch (ArgumentException)
                {
                    village = null;
                }
            }

            return new ApiReply(status, village);
        }

        private async Task<(int Status, string Text)> SendAsync(string method, string path, string token, string body, CancellationToken ct)
        {
            using (var request = new UnityWebRequest(_baseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                if (!string.IsNullOrEmpty(token))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                }

                if (!string.IsNullOrEmpty(_devKey))
                {
                    request.SetRequestHeader("X-Dev-Key", _devKey);
                }

                request.timeout = TimeoutSeconds;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (ct.IsCancellationRequested)
                    {
                        request.Abort();
                        ct.ThrowIfCancellationRequested();
                    }

                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    return (0, null);
                }

                return ((int)request.responseCode, request.downloadHandler.text);
            }
        }
    }

    /// <summary>The guest token in PlayerPrefs (device-local; a real account comes later).</summary>
    public sealed class PlayerPrefsCredentialStore : ICredentialStore
    {
        private const string Key = "remote.token";

        public string Token => PlayerPrefs.GetString(Key, null);

        public void Save(string token)
        {
            PlayerPrefs.SetString(Key, token);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>The offline cache as a JSON file next to the old device save.</summary>
    public sealed class FileRemoteCache : IRemoteCache
    {
        private readonly string _path;

        public FileRemoteCache(string path) => _path = path;

        public RemoteCacheDto Load()
        {
            try
            {
                return System.IO.File.Exists(_path) ? JsonUtility.FromJson<RemoteCacheDto>(System.IO.File.ReadAllText(_path)) : null;
            }
            catch (Exception e) when (e is System.IO.IOException || e is ArgumentException)
            {
                Debug.LogWarning($"[CoC] Village cache unreadable ({e.Message}); the server's copy will do.");
                return null;
            }
        }

        public void Save(RemoteCacheDto cache)
        {
            var temp = _path + ".tmp";
            System.IO.File.WriteAllText(temp, JsonUtility.ToJson(cache));
            if (System.IO.File.Exists(_path))
            {
                System.IO.File.Delete(_path);
            }

            System.IO.File.Move(temp, _path);
        }
    }
}
