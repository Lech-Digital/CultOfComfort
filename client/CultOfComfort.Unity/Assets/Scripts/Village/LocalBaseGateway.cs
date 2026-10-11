using System;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Client
{
    /// <summary>Holds authority on the device: runs <see cref="BaseState"/> and saves after every accepted command.</summary>
    public sealed class LocalBaseGateway : IBaseGateway
    {
        private readonly IBaseStore _store;
        private readonly IClock _clock;
        private readonly BuildingTable _table;
        private readonly VillageMap _map;
        private readonly Action<string> _warn;
        private BaseState _state;

        public LocalBaseGateway(IBaseStore store, IClock clock, BuildingTable table, VillageMap map, Action<string> warn)
        {
            _store = store;
            _clock = clock;
            _table = table;
            _map = map;
            _warn = warn;
        }

        public BattleLayout CurrentLayout => _state?.ToBattleLayout() ?? _map.ToBattleLayout(Array.Empty<PlacedTower>(), Array.Empty<GridPosition>());

        public int SluiceBonus => _state?.SluiceBonus ?? 0;

        public async Task<BaseSnapshot> LoadAsync(CancellationToken ct)
        {
            await EnsureLoadedAsync(ct);
            return _state.Snapshot(_clock.UtcNow);
        }

        public async Task<CommandResult> SendAsync(BaseCommand command, CancellationToken ct)
        {
            await EnsureLoadedAsync(ct);
            var now = _clock.UtcNow;
            var outcome = _state.Apply(command, now);
            if (outcome.Status == CommandStatus.Accepted)
            {
                await _store.SaveAsync(_state.ToData(), ct);
            }

            return new CommandResult(outcome, _state.Snapshot(now));
        }

        private async Task EnsureLoadedAsync(CancellationToken ct)
        {
            if (_state != null)
            {
                return;
            }

            try
            {
                var data = await _store.LoadAsync(ct);
                _state = data == null ? BaseState.CreateNew(_table, _map, _clock.UtcNow) : BaseState.Restore(_table, _map, data);
            }
            catch (InvalidBaseDataException e)
            {
                _warn($"Village save unreadable ({e.Message}); starting a fresh village.");
                _state = BaseState.CreateNew(_table, _map, _clock.UtcNow);
            }
        }
    }
}
