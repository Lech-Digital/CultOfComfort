using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Server.Application;

/// <summary>A player: for now a guest, known only by the hash of their bearer token.</summary>
public sealed record Player(Guid Id, string TokenHash, DateTimeOffset CreatedAtUtc);

/// <summary>Outbound port: who owns which token.</summary>
public interface IPlayerRepository
{
    Task AddAsync(Player player, CancellationToken ct);

    Task<Guid?> FindByTokenHashAsync(string tokenHash, CancellationToken ct);
}

/// <summary>Outbound port: each player's village, stored as a whole and replaced with an optimistic version check.</summary>
public interface IVillageRepository
{
    Task<BaseData?> FindAsync(Guid playerId, CancellationToken ct);

    Task AddAsync(Guid playerId, BaseData village, CancellationToken ct);

    /// <summary>Replaces the village only if it is still at <paramref name="expectedVersion"/>; false when someone else wrote first.</summary>
    Task<bool> TryReplaceAsync(Guid playerId, long expectedVersion, BaseData village, CancellationToken ct);
}
