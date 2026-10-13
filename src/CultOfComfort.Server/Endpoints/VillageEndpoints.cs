using CultOfComfort.Contracts;
using CultOfComfort.Server.Application;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Server.Endpoints;

internal static class RateLimits
{
    public const string Guest = "guest";
    public const string Village = "village";
}

/// <summary>/api/auth/guest, /api/base and /api/base/sync (blueprint: Identity + Base, first slice).</summary>
internal static class VillageEndpoints
{
    private const int MaxCommandsPerSync = 50;

    public static void MapVillageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/guest", async (IVillageService villages, CancellationToken ct) =>
        {
            var guest = await villages.StartGuestAsync(ct);
            return Results.Ok(new GuestDto { playerId = guest.PlayerId.ToString(), token = guest.Token });
        }).RequireRateLimiting(RateLimits.Guest);

        var api = app.MapGroup("/api/base").RequireRateLimiting(RateLimits.Village);

        api.MapGet("/", async (HttpContext http, IVillageService villages, CancellationToken ct) =>
        {
            var player = await PlayerAsync(http, villages, ct);
            if (player == null)
            {
                return Results.Unauthorized();
            }

            var view = await villages.GetAsync(player.Value, ct);
            return view == null ? Results.NotFound() : Results.Ok(Response(view, Array.Empty<(Guid, CommandOutcome)>()));
        });

        api.MapPost("/sync", async (HttpContext http, SyncRequestDto request, IVillageService villages, CancellationToken ct) =>
        {
            var player = await PlayerAsync(http, villages, ct);
            if (player == null)
            {
                return Results.Unauthorized();
            }

            var dtos = request?.commands ?? new List<CommandDto>();
            if (dtos.Count > MaxCommandsPerSync)
            {
                return Results.BadRequest(new { error = $"At most {MaxCommandsPerSync} commands per sync." });
            }

            List<BaseCommand> commands;
            try
            {
                commands = dtos.Select(CommandMapping.ToCommand).ToList();
            }
            catch (ArgumentException e)
            {
                return Results.BadRequest(new { error = e.Message });
            }

            var result = await villages.SyncAsync(player.Value, request!.expectedVersion, commands, ct);
            if (result == null)
            {
                return Results.NotFound();
            }

            // 409 carries the current village, so the client can re-pull and retry in one round trip.
            var body = Response(result.View, result.Outcomes);
            return result.Conflict ? Results.Conflict(body) : Results.Ok(body);
        });
    }

    internal static async Task<Guid?> PlayerAsync(HttpContext http, IVillageService villages, CancellationToken ct)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.Ordinal) ? await villages.AuthenticateAsync(header["Bearer ".Length..].Trim(), ct) : null;
    }

    private static VillageResponseDto Response(VillageView view, IReadOnlyList<(Guid CommandId, CommandOutcome Outcome)> outcomes) => new()
    {
        version = view.Village.Version,
        serverTimeTicks = view.ServerTimeUtc.UtcTicks,
        village = VillageSaveMapping.ToDto(view.Village),
        outcomes = outcomes.Select(o => CommandMapping.ToDto(o.CommandId, o.Outcome)).ToList(),
    };
}
