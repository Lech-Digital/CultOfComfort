using System;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client.Tests
{
    /// <summary>A fresh in-memory village per PlayMode test, so no test reads or writes the real device save.</summary>
    internal static class TestVillage
    {
        public static void Use() => VillageComposition.Override = () => new LocalBaseGateway(new Store(), new SystemClock(), BuildingTable.Default, VillageMap.Demo, _ => { });

        public static void Reset() => VillageComposition.Override = null;

        private sealed class Store : IBaseStore
        {
            private BaseData _saved;
            public Task<BaseData> LoadAsync(CancellationToken ct) => Task.FromResult(_saved);
            public Task SaveAsync(BaseData data, CancellationToken ct) { _saved = data; return Task.CompletedTask; }
        }
    }
}
