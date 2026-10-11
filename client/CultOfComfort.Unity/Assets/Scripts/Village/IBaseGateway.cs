using System;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The one door to the village (inbound port). <see cref="LocalBaseGateway"/> runs the rules on the device; a
    /// remote gateway will post the same commands to /api/base/sync. Nothing above this interface changes then.
    /// </summary>
    public interface IBaseGateway
    {
        Task<BaseSnapshot> LoadAsync(CancellationToken ct);

        Task<CommandResult> SendAsync(BaseCommand command, CancellationToken ct);

        /// <summary>The Night layout for the village as last loaded or changed.</summary>
        BattleLayout CurrentLayout { get; }

        /// <summary>Extra Open Sluice charges from Cisterns.</summary>
        int SluiceBonus { get; }
    }

    public sealed class CommandResult
    {
        public CommandResult(CommandOutcome outcome, BaseSnapshot snapshot)
        {
            Outcome = outcome;
            Snapshot = snapshot;
        }

        public CommandOutcome Outcome { get; }
        public BaseSnapshot Snapshot { get; }
    }

    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    /// <summary>Outbound port for the device save. <see cref="LoadAsync"/> returns null when there is none.</summary>
    public interface IBaseStore
    {
        Task<BaseData> LoadAsync(CancellationToken ct);

        Task SaveAsync(BaseData data, CancellationToken ct);
    }
}
