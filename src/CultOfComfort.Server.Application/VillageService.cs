using System.Security.Cryptography;
using System.Text;
using CultOfComfort.Contracts;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Server.Application;

/// <summary>The village as the server holds it, and the server's clock.</summary>
public sealed record VillageView(BaseData Village, DateTimeOffset ServerTimeUtc);

/// <summary>What a sync did. <see cref="Conflict"/>: the client's version was stale and nothing was applied.</summary>
public sealed record SyncResult(VillageView View, IReadOnlyList<(Guid CommandId, CommandOutcome Outcome)> Outcomes, bool Conflict);

/// <summary>A new guest: their id and the bearer token (shown once; only its hash is stored).</summary>
public sealed record GuestAccount(Guid PlayerId, string Token);

public sealed class VillageOptions
{
    public const string SectionName = "Village";

    /// <summary>Lets "finish now", "raise Tension" and "record a Night" through. Never on a public server.</summary>
    public bool AllowDevelopmentCommands { get; set; }
}

/// <summary>Inbound port: everything the API does with a village.</summary>
public interface IVillageService
{
    Task<GuestAccount> StartGuestAsync(CancellationToken ct);

    Task<Guid?> AuthenticateAsync(string token, CancellationToken ct);

    Task<VillageView?> GetAsync(Guid playerId, CancellationToken ct);

    Task<SyncResult?> SyncAsync(Guid playerId, long expectedVersion, IReadOnlyList<BaseCommand> commands, CancellationToken ct);
}

/// <summary>
/// Runs the village rules with the server as the authority: the client sends intents (commands), never amounts; the
/// server applies them at its own clock and replays Nights from their input logs (ADR 0002/0003).
/// </summary>
internal sealed class VillageService(
    IPlayerRepository players, IVillageRepository villages, BuildingTable table, VillageMap map, TimeProvider clock, VillageOptions options)
    : IVillageService
{
    public async Task<GuestAccount> StartGuestAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var player = new Player(Guid.NewGuid(), Hash(token), now);
        await players.AddAsync(player, ct);
        await villages.AddAsync(player.Id, BaseState.CreateNew(table, map, now).ToData(), ct);
        return new GuestAccount(player.Id, token);
    }

    public Task<Guid?> AuthenticateAsync(string token, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(token) ? Task.FromResult<Guid?>(null) : players.FindByTokenHashAsync(Hash(token), ct);

    public async Task<VillageView?> GetAsync(Guid playerId, CancellationToken ct)
    {
        var village = await villages.FindAsync(playerId, ct);
        return village == null ? null : new VillageView(village, clock.GetUtcNow());
    }

    public async Task<SyncResult?> SyncAsync(Guid playerId, long expectedVersion, IReadOnlyList<BaseCommand> commands, CancellationToken ct)
    {
        var stored = await villages.FindAsync(playerId, ct);
        if (stored == null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        if (stored.Version != expectedVersion)
        {
            return new SyncResult(new VillageView(stored, now), Array.Empty<(Guid, CommandOutcome)>(), Conflict: true);
        }

        var state = BaseState.Restore(table, map, stored);
        var outcomes = new List<(Guid, CommandOutcome)>(commands.Count);
        foreach (var command in commands)
        {
            var outcome = CommandMapping.IsDevelopmentOnly(command) && !options.AllowDevelopmentCommands
                ? new CommandOutcome(CommandStatus.Rejected, state.Version, "Not on a real server.")
                : state.Apply(command, now);
            outcomes.Add((command.CommandId, outcome));
        }

        var updated = state.ToData();
        if (!await villages.TryReplaceAsync(playerId, stored.Version, updated, ct))
        {
            // Another request wrote first: nothing of this batch counts; the client re-pulls and retries.
            var current = await villages.FindAsync(playerId, ct) ?? stored;
            return new SyncResult(new VillageView(current, now), Array.Empty<(Guid, CommandOutcome)>(), Conflict: true);
        }

        return new SyncResult(new VillageView(updated, now), outcomes, Conflict: false);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
