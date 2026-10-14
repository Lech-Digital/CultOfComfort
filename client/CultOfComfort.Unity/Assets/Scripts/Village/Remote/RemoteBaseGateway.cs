using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Contracts;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The village with the server as the authority (ADR 0005), playable offline. Reads come from a local
    /// <see cref="BaseState"/> mirror; a command is applied to the mirror at once (no waiting on the network), queued,
    /// and sent in batches to /api/base/sync. Every server answer replaces the mirror, with the still-unconfirmed
    /// commands replayed on top. Lighting the Lamps needs the server (it deals the raid); everything else waits for a
    /// connection in the queue, which survives a restart in the cache.
    /// </summary>
    public sealed class RemoteBaseGateway : IBaseGateway
    {
        public const int BatchSize = 50;
        public static readonly TimeSpan PullEvery = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan[] Backoff = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30) };

        private readonly IVillageApi _api;
        private readonly ICredentialStore _credentials;
        private readonly IRemoteCache _cache;
        private readonly IClock _clock;
        private readonly BuildingTable _table;
        private readonly VillageMap _map;
        private readonly Action<string> _warn;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _syncGate = new SemaphoreSlim(1, 1);
        private readonly List<BaseCommand> _pending = new List<BaseCommand>();
        private BaseState _mirror;
        private long _version = -1; // the server's village version the mirror is built on; -1: no server village yet
        private TimeSpan _skew;
        private Task _loading;
        private DateTimeOffset _nextAttempt = DateTimeOffset.MinValue;
        private DateTimeOffset _lastHeard = DateTimeOffset.MinValue;
        private int _failures;

        public RemoteBaseGateway(IVillageApi api, ICredentialStore credentials, IRemoteCache cache, IClock clock, BuildingTable table, VillageMap map, Action<string> warn,
            bool developer = false)
        {
            Developer = developer;
            _api = api;
            _credentials = credentials;
            _cache = cache;
            _clock = clock;
            _table = table;
            _map = map;
            _warn = warn;
        }

        /// <summary>This device holds the owner's dev key: development commands go to the server instead of being refused here.</summary>
        public bool Developer { get; }

        /// <summary>True after the last exchange with the server worked.</summary>
        public bool Online { get; private set; }

        /// <summary>Commands applied here that the server has not confirmed yet.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>The village clock: the device clock moved onto the server's.</summary>
        private DateTimeOffset Now => _clock.UtcNow + _skew;

        public async Task<BaseSnapshot> LoadAsync(CancellationToken ct)
        {
            BaseSnapshot snapshot;
            await _gate.WaitAsync(ct);
            try
            {
                await EnsureLoadedAsync(ct);
                snapshot = _mirror.Snapshot(Now);
            }
            finally
            {
                _gate.Release();
            }

            Pump();
            return snapshot;
        }

        public async Task<CommandResult> SendAsync(BaseCommand command, CancellationToken ct)
        {
            if (command is BeginNight)
            {
                return await SendThroughServerAsync(command, ct);
            }

            CommandResult result;
            await _gate.WaitAsync(ct);
            try
            {
                await EnsureLoadedAsync(ct);
                var now = Now;
                if (CommandMapping.IsDevelopmentOnly(command) && !Developer)
                {
                    return new CommandResult(new CommandOutcome(CommandStatus.Rejected, _mirror.Version, "Not on a real server."), _mirror.Snapshot(now));
                }

                var outcome = command is ResolveNight
                    ? await Task.Run(() => _mirror.Apply(command, now), ct) // replays the whole Night; the server replays it too
                    : _mirror.Apply(command, now);
                if (outcome.Status == CommandStatus.Accepted)
                {
                    _pending.Add(command);
                    SaveCache();
                }

                result = new CommandResult(outcome, _mirror.Snapshot(now));
            }
            finally
            {
                _gate.Release();
            }

            Pump();
            return result;
        }

        /// <summary>
        /// One exchange with the server: sends up to <see cref="BatchSize"/> queued commands (or just asks for the
        /// village) and adopts the answer. Returns false without a connection. The game calls it through <see cref="Pump"/>;
        /// tests call it directly.
        /// </summary>
        public async Task<bool> SyncNowAsync(CancellationToken ct)
        {
            await _syncGate.WaitAsync(ct);
            try
            {
                return await SyncOnceAsync(ct);
            }
            finally
            {
                _syncGate.Release();
            }
        }

        /// <summary>Starts a background exchange when one is due: queued commands, or nothing heard for <see cref="PullEvery"/>.</summary>
        public void Pump()
        {
            var now = _clock.UtcNow;
            if (_loading == null || !_loading.IsCompleted || now < _nextAttempt || _syncGate.CurrentCount == 0)
            {
                return;
            }

            if (_pending.Count == 0 && now - _lastHeard < PullEvery)
            {
                return;
            }

            _ = PumpAsync();
        }

        private async Task PumpAsync()
        {
            try
            {
                while (await SyncNowAsync(CancellationToken.None) && _pending.Count > 0)
                {
                    // Keep going while the queue drains (more than one batch, or taps during the last exchange).
                }
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                _warn($"Sync failed: {e.Message}");
            }
        }

        private async Task<bool> SyncOnceAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                await EnsureLoadedAsync(ct);
            }
            finally
            {
                _gate.Release();
            }

            var token = await EnsureAccountAsync(ct);
            if (token == null)
            {
                return Failed();
            }

            List<BaseCommand> batch;
            long version;
            await _gate.WaitAsync(ct);
            try
            {
                batch = _version < 0 ? new List<BaseCommand>() : _pending.Take(BatchSize).ToList();
                version = _version;
            }
            finally
            {
                _gate.Release();
            }

            var reply = batch.Count == 0
                ? await _api.GetAsync(token, ct)
                : await _api.SyncAsync(token, new SyncRequestDto { expectedVersion = version, commands = batch.Select(CommandMapping.ToDto).ToList() }, ct);
            if (reply.Status == 401)
            {
                _warn("The server does not know this device any more; starting a new guest village.");
                _credentials.Clear();
                return Failed();
            }

            if (!reply.Ok && !reply.Conflict)
            {
                return Failed();
            }

            await _gate.WaitAsync(ct);
            try
            {
                var confirmed = reply.Ok ? batch : new List<BaseCommand>(); // a conflict applied nothing: the batch goes again
                foreach (var outcome in reply.Village.outcomes ?? new List<OutcomeDto>())
                {
                    if (outcome.status == nameof(CommandStatus.Rejected))
                    {
                        _warn($"The server turned down a command: {outcome.reason}");
                    }
                }

                _pending.RemoveAll(c => confirmed.Any(b => b.CommandId == c.CommandId));
                Adopt(reply.Village);
            }
            finally
            {
                _gate.Release();
            }

            Online = true;
            _failures = 0;
            _nextAttempt = DateTimeOffset.MinValue;
            return true;
        }

        /// <summary>This village's recovery code (a new one each time; the last one shown is the one that works), or null offline.</summary>
        public async Task<string> RecoveryCodeAsync(CancellationToken ct)
        {
            var token = await EnsureAccountAsync(ct);
            return token == null ? null : await _api.RecoveryCodeAsync(token, ct);
        }

        /// <summary>
        /// Moves this device onto the village a recovery code belongs to: the new token replaces this one, and whatever
        /// this device had queued is dropped (it belonged to the other village). False: unknown code or offline.
        /// </summary>
        public async Task<bool> RecoverAsync(string code, CancellationToken ct)
        {
            var account = await _api.RecoverAsync(code, ct);
            if (account == null || string.IsNullOrEmpty(account.token))
            {
                return false;
            }

            _credentials.Save(account.token);
            await _gate.WaitAsync(ct);
            try
            {
                _pending.Clear();
                _version = -1;
                _lastServerVillage = null;
            }
            finally
            {
                _gate.Release();
            }

            return await SyncNowAsync(ct);
        }

        /// <summary>
        /// The shared world around (q, r) (null: around the village). The first time, the village joins a world, and the
        /// village is pulled again so the mirror knows where it stands. Null offline.
        /// </summary>
        public async Task<WorldDto> WorldAsync(int? q, int? r, int radius, CancellationToken ct)
        {
            var token = await EnsureAccountAsync(ct);
            if (token == null)
            {
                return null;
            }

            var home = (Q: 0, R: 0);
            if (_mirror == null || !_mirror.InWorld)
            {
                var joined = await _api.JoinWorldAsync(token, ct);
                if (joined == null)
                {
                    return null;
                }

                await SyncNowAsync(ct); // the server applied JoinWorld: take the village as it is now
                home = (joined.q, joined.r);
            }
            else
            {
                await _gate.WaitAsync(ct);
                try
                {
                    var link = _mirror.Snapshot(Now).Domain?.World;
                    home = link == null ? home : (link.Q, link.R);
                }
                finally
                {
                    _gate.Release();
                }
            }

            return await _api.GetWorldAsync(token, q ?? home.Q, r ?? home.R, radius, ct);
        }

        /// <summary>Takes the world's offerings into the stores (the server decides how much fits). Null offline.</summary>
        public async Task<WorldCollectDto> CollectWorldAsync(CancellationToken ct)
        {
            var token = await EnsureAccountAsync(ct);
            if (token == null)
            {
                return null;
            }

            while (_pending.Count > 0)
            {
                if (!await SyncNowAsync(ct))
                {
                    return null; // the queue first, so the server's village is this one
                }
            }

            var collect = await _api.CollectWorldAsync(token, ct);
            if (collect?.village?.village != null && collect.village.version > 0)
            {
                await _gate.WaitAsync(ct);
                try
                {
                    Adopt(collect.village);
                }
                finally
                {
                    _gate.Release();
                }
            }

            return collect;
        }

        /// <summary>BeginNight: the server deals the raid, so the queue goes first and the answer is the server's.</summary>
        private async Task<CommandResult> SendThroughServerAsync(BaseCommand command, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                await EnsureLoadedAsync(ct);
            }
            finally
            {
                _gate.Release();
            }

            while (_version < 0 || _pending.Count > 0)
            {
                if (!await SyncNowAsync(ct))
                {
                    return NoConnection();
                }
            }

            var token = await EnsureAccountAsync(ct);
            ApiReply reply = null;
            if (token != null)
            {
                await _syncGate.WaitAsync(ct);
                try
                {
                    reply = await _api.SyncAsync(token, new SyncRequestDto { expectedVersion = _version, commands = new List<CommandDto> { CommandMapping.ToDto(command) } }, ct);
                }
                finally
                {
                    _syncGate.Release();
                }
            }

            if (reply == null || (!reply.Ok && !reply.Conflict))
            {
                Failed();
                return NoConnection();
            }

            await _gate.WaitAsync(ct);
            try
            {
                Adopt(reply.Village);
                Online = true;
                var answer = reply.Ok ? reply.Village.outcomes?.FirstOrDefault(o => o.id == command.CommandId.ToString()) : null;
                var outcome = answer == null
                    ? new CommandOutcome(CommandStatus.Rejected, _mirror.Version, "The village changed on the server; try again.")
                    : ToOutcome(answer, _mirror.Version);
                return new CommandResult(outcome, _mirror.Snapshot(Now));
            }
            finally
            {
                _gate.Release();
            }
        }

        private CommandResult NoConnection()
        {
            var reason = "No connection: the raiders can't find you either. Light the Lamps when you're back online.";
            return new CommandResult(new CommandOutcome(CommandStatus.Rejected, _mirror.Version, reason), _mirror.Snapshot(Now));
        }

        private static CommandOutcome ToOutcome(OutcomeDto dto, long version)
        {
            var status = Enum.TryParse<CommandStatus>(dto.status, out var parsed) ? parsed : CommandStatus.Rejected;
            return new CommandOutcome(status, version, string.IsNullOrEmpty(dto.reason) ? null : dto.reason,
                new ResourceBag(dto.timber, dto.peat, dto.scrap, dto.devotion),
                dto.hasPlan && dto.plan != null ? VillageSaveMapping.PlanFromDto(dto.plan) : null,
                dto.hasReport && dto.report != null ? VillageSaveMapping.ReportFromDto(dto.report) : null);
        }

        /// <summary>The server's village becomes the mirror; the queue is replayed on top of it. Call inside the gate.</summary>
        private void Adopt(VillageResponseDto response)
        {
            _skew = new DateTimeOffset(response.serverTimeTicks, TimeSpan.Zero) - _clock.UtcNow;
            _version = response.version;
            _mirror = BaseState.Restore(_table, _map, VillageSaveMapping.FromDto(response.village));
            var now = Now;
            foreach (var command in _pending)
            {
                _mirror.Apply(command, now);
            }

            _lastHeard = _clock.UtcNow;
            SaveCache(response.village);
        }

        private async Task<string> EnsureAccountAsync(CancellationToken ct)
        {
            var token = _credentials.Token;
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }

            var guest = await _api.StartGuestAsync(ct);
            if (guest == null || string.IsNullOrEmpty(guest.token))
            {
                return null;
            }

            _credentials.Save(guest.token);
            await _gate.WaitAsync(ct);
            try
            {
                _version = -1; // a new account: its village is the server's fresh one, the queue replays onto it
            }
            finally
            {
                _gate.Release();
            }

            return guest.token;
        }

        private bool Failed()
        {
            Online = false;
            _failures++;
            _nextAttempt = _clock.UtcNow + Backoff[Math.Min(_failures, Backoff.Length) - 1];
            return false;
        }

        /// <summary>Loads once: the cache (with its queue), else a fresh local village until the server answers.</summary>
        private Task EnsureLoadedAsync(CancellationToken ct) => _loading ??= Task.Run(LoadOnce, CancellationToken.None);

        private void LoadOnce()
        {
            RemoteCacheDto cache = null;
            try
            {
                cache = _cache.Load();
            }
            catch (Exception e)
            {
                _warn($"Village cache unreadable ({e.Message}).");
            }

            if (cache != null && cache.hasVillage && cache.village != null)
            {
                try
                {
                    _skew = TimeSpan.FromTicks(cache.skewTicks);
                    _version = cache.version;
                    _pending.AddRange((cache.pending ?? new List<CommandDto>()).Select(CommandMapping.ToCommand));
                    _mirror = BaseState.Restore(_table, _map, VillageSaveMapping.FromDto(cache.village));
                    _lastServerVillage = cache.village;
                    var now = Now;
                    foreach (var command in _pending)
                    {
                        _mirror.Apply(command, now);
                    }

                    return;
                }
                catch (Exception e) when (e is InvalidBaseDataException || e is ArgumentException)
                {
                    _warn($"Village cache unreadable ({e.Message}); waiting for the server's copy.");
                    _pending.Clear();
                }
            }

            _version = -1;
            _mirror = BaseState.CreateNew(_table, _map, Now);
        }

        private VillageSaveDto _lastServerVillage;

        private void SaveCache(VillageSaveDto serverVillage = null)
        {
            _lastServerVillage = serverVillage ?? _lastServerVillage;
            try
            {
                _cache.Save(new RemoteCacheDto
                {
                    hasVillage = _lastServerVillage != null,
                    village = _lastServerVillage,
                    version = _version,
                    skewTicks = _skew.Ticks,
                    pending = _pending.Select(CommandMapping.ToDto).ToList(),
                });
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                _warn($"Could not write the village cache ({e.Message}).");
            }
        }
    }
}
