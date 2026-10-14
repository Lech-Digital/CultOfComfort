using CultOfComfort.Contracts;
using CultOfComfort.Server.Application;
using CultOfComfort.Server.Application.World;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Server.Endpoints;

/// <summary>/api/world: join, the window around you, collecting offerings (spec 2026-10-14 shared world).</summary>
internal static class WorldEndpoints
{
    public static void MapWorldEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/world").RequireRateLimiting(RateLimits.Village);

        api.MapPost("/join", async (HttpContext http, IVillageService villages, IWorldService world, CancellationToken ct) =>
        {
            var player = await VillageEndpoints.PlayerAsync(http, villages, ct);
            if (player == null)
            {
                return Results.Unauthorized();
            }

            var cult = await world.JoinAsync(player.Value, ct);
            if (cult == null)
            {
                return Results.Problem("Every world is full.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var window = await world.WindowAsync(player.Value, null, null, 8, ct);
            return window == null ? Results.NotFound() : Results.Ok(ToDto(window));
        });

        api.MapGet("/", async (HttpContext http, int? q, int? r, int? radius, IVillageService villages, IWorldService world, CancellationToken ct) =>
        {
            var player = await VillageEndpoints.PlayerAsync(http, villages, ct);
            if (player == null)
            {
                return Results.Unauthorized();
            }

            var window = await world.WindowAsync(player.Value, q, r, radius ?? 8, ct);
            return window == null ? Results.NotFound() : Results.Ok(ToDto(window));
        });

        api.MapPost("/collect", async (HttpContext http, IVillageService villages, IWorldService world, CancellationToken ct) =>
        {
            var player = await VillageEndpoints.PlayerAsync(http, villages, ct);
            if (player == null)
            {
                return Results.Unauthorized();
            }

            var collect = await world.CollectAsync(player.Value, ct);
            if (collect == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new WorldCollectDto
            {
                timber = collect.Taken.Timber,
                peat = collect.Taken.Peat,
                scrap = collect.Taken.Scrap,
                devotion = collect.Taken.Devotion,
                reason = collect.Reason ?? string.Empty,
                village = collect.Village == null ? null! : VillageEndpoints.Response(collect.Village, Array.Empty<(Guid, CommandOutcome)>()),
            });
        });
    }

    private static WorldDto ToDto(WorldWindow w) => new()
    {
        worldId = w.WorldId,
        seed = unchecked((long)w.Seed),
        cultId = w.Me.Id,
        q = w.Me.Q,
        r = w.Me.R,
        viewQ = w.ViewQ,
        viewR = w.ViewR,
        viewRadius = w.ViewRadius,
        cults = w.Cults.Select(c => new WorldCultDto { id = c.Id, name = c.Name, q = c.Q, r = c.R, colour = c.Colour, bot = c.IsBot }).ToList(),
        holdings = w.Holdings.Select(h => new WorldHoldingDto { node = h.NodeId, cult = h.CultId, milli = h.Milli, patron = h.Patron }).ToList(),
        purseTimber = w.Purse.Timber,
        pursePeat = w.Purse.Peat,
        purseScrap = w.Purse.Scrap,
        purseDevotion = w.Purse.Devotion,
        perHourTimber = w.OfferingsPerHour.Timber,
        perHourPeat = w.OfferingsPerHour.Peat,
        perHourScrap = w.OfferingsPerHour.Scrap,
        perHourDevotion = w.OfferingsPerHour.Devotion,
        tensionPerHourMilli = w.TensionPerHourMilli,
        serverTimeTicks = w.ServerTimeUtc.UtcTicks,
    };
}
