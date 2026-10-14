using CultOfComfort.Server.Application.World;

namespace CultOfComfort.Server;

/// <summary>Runs the shared worlds' clock: every few seconds, sermons that ended land, influence fades, Tension reaches villages.</summary>
internal sealed class WorldTicker(IWorldService world, WorldOptions options, ILogger<WorldTicker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.TickSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await world.TickAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e, "World tick failed; trying again next tick");
            }
        }
    }
}
