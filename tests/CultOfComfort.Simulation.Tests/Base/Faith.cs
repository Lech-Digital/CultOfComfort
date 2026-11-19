using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Test helper: a village that already holds faith somewhere, so the lord's demands come (owner 2026-11-19).</summary>
internal static class Faith
{
    public static BaseState Held(BaseState state, BuildingTable table, int percent = 20)
    {
        var data = state.ToData();
        var domain = data.Domain!;
        var influence = domain.Influence.Where(i => i.NodeId != 1).Append((1, percent * 1000L)).ToList();
        return BaseState.Restore(table, VillageMap.Demo, data with { Domain = domain with { Influence = influence } });
    }
}
