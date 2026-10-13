using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Server.Application;

/// <summary>A player: for now a guest, known only by the hash of their bearer token.</summary>
public sealed record Player(Guid Id, string TokenHash, DateTimeOffset CreatedAtUtc);

/// <summary>Outbound port: who owns which token.</summary>
public interface IPlayerRepository
{
    Task AddAsync(Player player, CancellationToken ct);

    Task<Guid?> FindByTokenHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>Gives the player a new bearer token (the old one stops working: one device at a time).</summary>
    Task SetTokenHashAsync(Guid playerId, string tokenHash, CancellationToken ct);

    Task SetRecoveryCodeHashAsync(Guid playerId, string codeHash, CancellationToken ct);

    Task<Guid?> FindByRecoveryCodeHashAsync(string codeHash, CancellationToken ct);

    Task<Guid?> FindByGoogleSubjectAsync(string subject, CancellationToken ct);

    /// <summary>Links a Google account to the player; false when that account is already someone's.</summary>
    Task<bool> LinkGoogleSubjectAsync(Guid playerId, string subject, CancellationToken ct);
}

/// <summary>Outbound port: checks a Google ID token; returns its subject (the stable account id), or null if it isn't valid for us.</summary>
public interface IGoogleTokenVerifier
{
    Task<string?> SubjectAsync(string idToken, IReadOnlyList<string> audiences, CancellationToken ct);
}

/// <summary>Outbound port: each player's village, stored as a whole and replaced with an optimistic version check.</summary>
public interface IVillageRepository
{
    Task<BaseData?> FindAsync(Guid playerId, CancellationToken ct);

    Task AddAsync(Guid playerId, BaseData village, CancellationToken ct);

    /// <summary>Replaces the village only if it is still at <paramref name="expectedVersion"/>; false when someone else wrote first.</summary>
    Task<bool> TryReplaceAsync(Guid playerId, long expectedVersion, BaseData village, CancellationToken ct);
}
