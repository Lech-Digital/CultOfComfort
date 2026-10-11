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
    /// Contract: <see cref="LoadAsync"/> is called often (twice a second, and before every tap) and must be cheap — a
    /// remote gateway serves it from a local projection (a <c>BaseState</c> mirror fed by server snapshots) and syncs
    /// in the background. Everything else the client needs (Night layout, sluice bonus) is derived from the snapshot.
    /// </summary>
    public interface IBaseGateway
    {
        Task<BaseSnapshot> LoadAsync(CancellationToken ct);

        Task<CommandResult> SendAsync(BaseCommand command, CancellationToken ct);
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

        /// <summary>Sets an unreadable save aside (a copy) before a fresh village replaces it. Stores without files need nothing.</summary>
        Task QuarantineAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
