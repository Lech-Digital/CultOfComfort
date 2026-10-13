using CultOfComfort.Simulation.Base;
using Microsoft.Extensions.DependencyInjection;

namespace CultOfComfort.Server.Application;

public static class DependencyInjection
{
    /// <summary>Registers the village use cases with the rules tables the server plays by.</summary>
    public static IServiceCollection AddVillageGame(this IServiceCollection services, VillageOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(BuildingTable.Default);
        services.AddSingleton(VillageMap.Demo);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IVillageService, VillageService>();
        return services;
    }
}
