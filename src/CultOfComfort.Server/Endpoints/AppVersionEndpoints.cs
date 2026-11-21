namespace CultOfComfort.Server.Endpoints;

/// <summary>
/// The server's word on app versions (2026-11-21, as in Coo): <c>latest</c> is the newest version code on Play (the
/// game suggests it once a day when Play's own in-app update lags), <c>minimum</c> the oldest this server can serve
/// (below it the update is required). Set both with each release: AppUpdates__Latest and AppUpdates__Minimum.
/// </summary>
internal static class AppVersionEndpoints
{
    public static void MapAppVersionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/app-version", (IConfiguration configuration) => Results.Ok(new
        {
            latest = configuration.GetValue("AppUpdates:Latest", 3),
            minimum = configuration.GetValue("AppUpdates:Minimum", 2),
        }));
    }
}
