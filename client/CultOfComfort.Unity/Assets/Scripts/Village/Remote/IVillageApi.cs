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

        /// <summary>A fresh recovery code for this village, or null (no connection, or not signed in).</summary>
        Task<string> RecoveryCodeAsync(string token, CancellationToken ct);

        /// <summary>A token for the village the code belongs to, or null (unknown code, or no connection).</summary>
        Task<GuestDto> RecoverAsync(string code, CancellationToken ct);

        /// <summary>Signs in with a Google ID token (ADR 0007): this account's village, or this guest village linked to it. Null: refused or offline.</summary>
        Task<GuestDto> GoogleSignInAsync(string token, string idToken, CancellationToken ct);

        /// <summary>Puts the village on a shared world (or finds where it already is): the window around it, or null.</summary>
        Task<WorldDto> JoinWorldAsync(string token, CancellationToken ct);

        /// <summary>The shared world around (q, r), or null (offline, or the village isn't on a world yet).</summary>
        Task<WorldDto> GetWorldAsync(string token, int q, int r, int radius, CancellationToken ct);

        /// <summary>Takes the world's offerings purse into the stores, or null offline.</summary>
        Task<WorldCollectDto> CollectWorldAsync(string token, CancellationToken ct);
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
