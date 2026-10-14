using System.Security.Cryptography;
using System.Text;
using CultOfComfort.Contracts;
using CultOfComfort.Server.Application.World;
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

    /// <summary>
    /// A secret that unlocks the development commands for one request (header X-Dev-Key): the owner's testing gate
    /// on the live server. Empty or shorter than 24 characters: no gate.
    /// </summary>
    public string DevKey { get; set; } = string.Empty;

    /// <summary>True when <paramref name="presented"/> is the configured dev key (compared in constant time).</summary>
    public bool IsDevKey(string? presented) =>
        DevKey.Length >= 24 && !string.IsNullOrEmpty(presented) &&
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(DevKey), Encoding.UTF8.GetBytes(presented));
}

/// <summary>Inbound port: everything the API does with a village.</summary>
public interface IVillageService
{
    Task<GuestAccount> StartGuestAsync(CancellationToken ct);

    Task<Guid?> AuthenticateAsync(string token, CancellationToken ct);

    Task<VillageView?> GetAsync(Guid playerId, CancellationToken ct);

    Task<SyncResult?> SyncAsync(Guid playerId, long expectedVersion, IReadOnlyList<BaseCommand> commands, CancellationToken ct, bool developer = false);
}

/// <summary>
/// Runs the village rules with the server as the authority: the client sends intents (commands), never amounts; the
/// server applies them at its own clock and replays Nights from their input logs (ADR 0002/0003).
/// </summary>
internal sealed class VillageService(
    IPlayerRepository players, IVillageRepository villages, BuildingTable table, VillageMap map, TimeProvider clock, VillageOptions options,
    IWorldService? world = null)
    : IVillageService
{
    public async Task<GuestAccount> StartGuestAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var token = Tokens.NewToken();
        var player = new Player(Guid.NewGuid(), Tokens.Hash(token), now);
        await players.AddAsync(player, ct);
        await villages.AddAsync(player.Id, BaseState.CreateNew(table, map, now).ToData(), ct);
        return new GuestAccount(player.Id, token);
    }

    public Task<Guid?> AuthenticateAsync(string token, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(token) ? Task.FromResult<Guid?>(null) : players.FindByTokenHashAsync(Tokens.Hash(token), ct);

    public async Task<VillageView?> GetAsync(Guid playerId, CancellationToken ct)
    {
        var village = await villages.FindAsync(playerId, ct);
        return village == null ? null : new VillageView(village, clock.GetUtcNow());
    }

    public async Task<SyncResult?> SyncAsync(Guid playerId, long expectedVersion, IReadOnlyList<BaseCommand> commands, CancellationToken ct, bool developer = false)
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
        var sent = new List<SendWorldMissionary>();
        foreach (var command in commands)
        {
            var refusal = CommandMapping.IsServerOnly(command) ? "Only the world gives that."
                : CommandMapping.IsDevelopmentOnly(command) && !options.AllowDevelopmentCommands && !developer ? "Not on a real server."
                : command is SendWorldMissionary send ? world == null ? "No shared world here." : await world.MissionaryProblemAsync(playerId, send, ct)
                : null;
            var outcome = refusal != null ? new CommandOutcome(CommandStatus.Rejected, state.Version, refusal) : state.Apply(command, now);
            if (outcome.Status == CommandStatus.Accepted && command is SendWorldMissionary accepted)
            {
                sent.Add(accepted);
            }

            outcomes.Add((command.CommandId, outcome));
        }

        var updated = state.ToData();
        if (!await villages.TryReplaceAsync(playerId, stored.Version, updated, ct))
        {
            // Another request wrote first: nothing of this batch counts; the client re-pulls and retries.
            var current = await villages.FindAsync(playerId, ct) ?? stored;
            return new SyncResult(new VillageView(current, now), Array.Empty<(Guid, CommandOutcome)>(), Conflict: true);
        }

        foreach (var send in sent)
        {
            await world!.MissionarySentAsync(playerId, updated, send, now, ct); // stored first: the villager left whatever happens next
        }

        return new SyncResult(new VillageView(updated, now), outcomes, Conflict: false);
    }

}

/// <summary>Bearer tokens and recovery codes: random, shown once, stored only as SHA-256 hashes.</summary>
internal static class Tokens
{
    /// <summary>No 0/O or 1/I: a code is read off one screen and typed on another.</summary>
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>"ABCD-EFGH-JKLM": 12 letters from 32, 60 bits (and the recover endpoint is rate-limited).</summary>
    public static string NewRecoveryCode()
    {
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        var raw = new string(chars);
        return $"{raw[..4]}-{raw[4..8]}-{raw[8..]}";
    }

    /// <summary>What the player typed, as stored: upper case, no dashes or spaces.</summary>
    public static string NormalizeCode(string? code) =>
        new string((code ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
}
