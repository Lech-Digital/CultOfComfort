using System.Threading.RateLimiting;
using CultOfComfort.Server.Application;
using CultOfComfort.Server.Endpoints;
using CultOfComfort.Server.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ---- Composition root ------------------------------------------------------------------------
var villageOptions = builder.Configuration.GetSection(VillageOptions.SectionName).Get<VillageOptions>() ?? new VillageOptions();
builder.Services.AddVillageGame(villageOptions);
builder.Services.AddVillageStorage(builder.Configuration.GetConnectionString("Db") ?? "Data Source=/data/cultofcomfort.db");
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.IncludeFields = true); // the shared contract DTOs are fields
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 256 * 1024); // a sync batch is small; a Night's input log too

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // A guest account is free to make, so making them is what gets limited.
    options.AddPolicy(RateLimits.Guest, PerClient(limit: 10, window: TimeSpan.FromHours(1)));
    options.AddPolicy(RateLimits.Village, PerClient(limit: 240, window: TimeSpan.FromMinutes(1))); // the client reads twice a second

    static Func<HttpContext, RateLimitPartition<string>> PerClient(int limit, TimeSpan window) =>
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = window, QueueLimit = 0 });
});

var app = builder.Build();

// Caddy terminates TLS and proxies here over the shared Docker network (as for PurePrep): trust its forwarded
// client address, or every player would share one rate-limit bucket. The container is not published otherwise.
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, ForwardLimit = 1 };
forwarded.KnownIPNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);
app.UseRateLimiter();

await app.Services.EnsureVillageStorageAsync(app.Lifetime.ApplicationStopping);

app.MapGet("/health", () => Results.Ok(new { status = "ok", game = "cult-of-comfort" }));
app.MapVillageEndpoints();

app.Run();

public partial class Program;
