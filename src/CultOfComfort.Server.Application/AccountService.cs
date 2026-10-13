using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Server.Application;

public sealed class AccountOptions
{
    public const string SectionName = "Auth";

    /// <summary>OAuth client ids whose Google ID tokens we accept (the Web client id the Android app asks for). Empty: Google sign-in is off.</summary>
    public List<string> GoogleClientIds { get; set; } = new();
}

/// <summary>Inbound port: keeping a village when the device changes (ADR 0007).</summary>
public interface IAccountService
{
    bool GoogleEnabled { get; }

    /// <summary>A fresh recovery code for the player (the previous one stops working).</summary>
    Task<string> IssueRecoveryCodeAsync(Guid playerId, CancellationToken ct);

    /// <summary>A new bearer token for the village the code belongs to; null for an unknown code.</summary>
    Task<GuestAccount?> RecoverAsync(string code, CancellationToken ct);

    /// <summary>
    /// Signs in with Google: the village already linked to that account (new token), else links it to the
    /// <paramref name="currentPlayer"/>'s village, else starts a new one. Null when the ID token isn't valid for us.
    /// </summary>
    Task<GuestAccount?> SignInWithGoogleAsync(string idToken, Guid? currentPlayer, CancellationToken ct);
}

internal sealed class AccountService(
    IPlayerRepository players, IVillageRepository villages, IGoogleTokenVerifier google, AccountOptions options, BuildingTable table, VillageMap map, TimeProvider clock)
    : IAccountService
{
    public bool GoogleEnabled => options.GoogleClientIds.Count > 0;

    public async Task<string> IssueRecoveryCodeAsync(Guid playerId, CancellationToken ct)
    {
        var code = Tokens.NewRecoveryCode();
        await players.SetRecoveryCodeHashAsync(playerId, Tokens.Hash(Tokens.NormalizeCode(code)), ct);
        return code;
    }

    public async Task<GuestAccount?> RecoverAsync(string code, CancellationToken ct)
    {
        var normalized = Tokens.NormalizeCode(code);
        if (normalized.Length != 12)
        {
            return null;
        }

        var player = await players.FindByRecoveryCodeHashAsync(Tokens.Hash(normalized), ct);
        return player == null ? null : await NewTokenAsync(player.Value, ct);
    }

    public async Task<GuestAccount?> SignInWithGoogleAsync(string idToken, Guid? currentPlayer, CancellationToken ct)
    {
        if (!GoogleEnabled || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var subject = await google.SubjectAsync(idToken, options.GoogleClientIds, ct);
        if (string.IsNullOrEmpty(subject))
        {
            return null;
        }

        var linked = await players.FindByGoogleSubjectAsync(subject, ct);
        if (linked != null)
        {
            return await NewTokenAsync(linked.Value, ct); // this account's village, on this device
        }

        if (currentPlayer != null && await players.LinkGoogleSubjectAsync(currentPlayer.Value, subject, ct))
        {
            return await NewTokenAsync(currentPlayer.Value, ct); // the guest village keeps going, now with a real account
        }

        var now = clock.GetUtcNow();
        var token = Tokens.NewToken();
        var player = new Player(Guid.NewGuid(), Tokens.Hash(token), now);
        await players.AddAsync(player, ct);
        await villages.AddAsync(player.Id, BaseState.CreateNew(table, map, now).ToData(), ct);
        await players.LinkGoogleSubjectAsync(player.Id, subject, ct);
        return new GuestAccount(player.Id, token);
    }

    private async Task<GuestAccount> NewTokenAsync(Guid playerId, CancellationToken ct)
    {
        var token = Tokens.NewToken();
        await players.SetTokenHashAsync(playerId, Tokens.Hash(token), ct);
        return new GuestAccount(playerId, token);
    }
}
