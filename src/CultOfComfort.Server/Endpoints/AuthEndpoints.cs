using CultOfComfort.Contracts;
using CultOfComfort.Server.Application;

namespace CultOfComfort.Server.Endpoints;

/// <summary>Keeping a village across devices (ADR 0007): recovery codes now, Google sign-in once it is configured.</summary>
internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/recovery-code", async (HttpContext http, IVillageService villages, IAccountService accounts, CancellationToken ct) =>
        {
            var player = await VillageEndpoints.PlayerAsync(http, villages, ct);
            return player == null ? Results.Unauthorized() : Results.Ok(new RecoveryCodeDto { code = await accounts.IssueRecoveryCodeAsync(player.Value, ct) });
        }).RequireRateLimiting(RateLimits.Village);

        // Guessing codes is what gets limited: ten tries an hour per address.
        app.MapPost("/api/auth/recover", async (RecoverRequestDto request, IAccountService accounts, CancellationToken ct) =>
        {
            var account = await accounts.RecoverAsync(request?.code ?? string.Empty, ct);
            return account == null ? Results.NotFound() : Results.Ok(new GuestDto { playerId = account.PlayerId.ToString(), token = account.Token });
        }).RequireRateLimiting(RateLimits.Guest);

        app.MapPost("/api/auth/google", async (HttpContext http, GoogleSignInDto request, IVillageService villages, IAccountService accounts, CancellationToken ct) =>
        {
            if (!accounts.GoogleEnabled)
            {
                return Results.StatusCode(StatusCodes.Status501NotImplemented);
            }

            var current = await VillageEndpoints.PlayerAsync(http, villages, ct);
            var account = await accounts.SignInWithGoogleAsync(request?.idToken ?? string.Empty, current, ct);
            return account == null ? Results.Unauthorized() : Results.Ok(new GuestDto { playerId = account.PlayerId.ToString(), token = account.Token });
        }).RequireRateLimiting(RateLimits.Guest);
    }
}
