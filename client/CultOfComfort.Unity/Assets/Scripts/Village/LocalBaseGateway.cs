using System;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Holds authority on the device: runs <see cref="BaseState"/> and saves after every accepted command. One call at a time:
    /// a Night's replay runs on a worker thread (it would freeze a frame), and nothing may read the village meanwhile.
    /// </summary>
    public sealed class LocalBaseGateway : IBaseGateway
    {
        private readonly IBaseStore _store;
        private readonly IClock _clock;
        private readonly BuildingTable _table;
        private readonly VillageMap _map;
        private readonly Action<string> _warn;
        private BaseState _state;
        private Task _loading;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public LocalBaseGateway(IBaseStore store, IClock clock, BuildingTable table, VillageMap map, Action<string> warn)
        {
            _store = store;
            _clock = clock;
            _table = table;
            _map = map;
            _warn = warn;
        }

        public async Task<BaseSnapshot> LoadAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                await EnsureLoadedAsync(ct);
                return _state.Snapshot(_clock.UtcNow);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<CommandResult> SendAsync(BaseCommand command, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                await EnsureLoadedAsync(ct);
                var now = _clock.UtcNow;
                var outcome = command is ResolveNight
                    ? await Task.Run(() => _state.Apply(command, now), ct) // replays the whole Night
                    : _state.Apply(command, now);
                if (outcome.Status == CommandStatus.Accepted)
                {
                    await _store.SaveAsync(_state.ToData(), ct);
                }

                return new CommandResult(outcome, _state.Snapshot(now));
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Loads once: overlapping first calls share the same load, so a command can't be lost to a second restore.</summary>
        private Task EnsureLoadedAsync(CancellationToken ct) => _loading ??= LoadOnceAsync(ct);

        private async Task LoadOnceAsync(CancellationToken ct)
        {
            try
            {
                var data = await _store.LoadAsync(ct);
                _state = data == null ? BaseState.CreateNew(_table, _map, _clock.UtcNow) : BaseState.Restore(_table, _map, data);
            }
            catch (Exception e) when (e is InvalidBaseDataException || e is System.IO.IOException)
            {
                _warn($"Village save unreadable ({e.Message}); keeping a copy and starting a fresh village.");
                await _store.QuarantineAsync(ct);
                _state = BaseState.CreateNew(_table, _map, _clock.UtcNow);
            }
        }
    }
}
