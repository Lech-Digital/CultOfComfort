using System;
using System.IO;
using CultOfComfort.Simulation.Base;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>Composition root for the village: picks the gateway. Tests set <see cref="Override"/>.</summary>
    public static class VillageComposition
    {
        public static Func<IBaseGateway> Override { get; set; }

        public static IBaseGateway CreateGateway() =>
            Override?.Invoke() ?? new LocalBaseGateway(
                new FileBaseStore(Path.Combine(Application.persistentDataPath, "village.json")),
                new SystemClock(), BuildingTable.Default, VillageMap.Demo, message => Debug.LogWarning("[CoC] " + message));
    }
}
