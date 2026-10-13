using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Contracts;

namespace CultOfComfort.Client
{
    /// <summary>What the server said: an HTTP status (0: no connection at all) and, when there is one, the village.</summary>
    public sealed class ApiReply
    {
        public ApiReply(int status, VillageResponseDto village)
        {
            Status = status;
            Village = village;
        }

        public int Status { get; }

        public VillageResponseDto Village { get; }

        public bool Ok => Status == 200 && Village != null;

        public bool Conflict => Status == 409 && Village != null;

        public static ApiReply Offline { get; } = new ApiReply(0, null);
    }

    /// <summary>Outbound port: the game server's village API (docs/adrs/0005).</summary>
    public interface IVillageApi
    {
        /// <summary>A new guest account, or null without a connection.</summary>
        Task<GuestDto> StartGuestAsync(CancellationToken ct);

        Task<ApiReply> GetAsync(string token, CancellationToken ct);

        Task<ApiReply> SyncAsync(string token, SyncRequestDto request, CancellationToken ct);
    }

    /// <summary>Outbound port: where the guest token lives on the device.</summary>
    public interface ICredentialStore
    {
        string Token { get; }

        void Save(string token);

        void Clear();
    }

    /// <summary>Outbound port: the last village the server sent plus the commands it has not confirmed yet, so the game starts (and plays) offline.</summary>
    public interface IRemoteCache
    {
        RemoteCacheDto Load();

        void Save(RemoteCacheDto cache);
    }

    /// <summary>The offline cache document (JsonUtility).</summary>
    [Serializable]
    public sealed class RemoteCacheDto
    {
        public bool hasVillage;
        public VillageSaveDto village;
        public long version = -1;
        public long skewTicks;
        public List<CommandDto> pending = new List<CommandDto>();
    }
}
