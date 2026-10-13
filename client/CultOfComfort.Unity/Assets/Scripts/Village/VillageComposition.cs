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

        /// <summary>Tests that bring their own village skip the intro unless they set this.</summary>
        public static Func<IIntroProgress> IntroOverride { get; set; }

        public static IIntroProgress CreateIntroProgress() =>
            IntroOverride?.Invoke() ?? (Override != null ? new SeenIntro() : new PlayerPrefsIntroProgress());

        private sealed class SeenIntro : IIntroProgress
        {
            public bool Seen => true;

            public void MarkSeen()
            {
            }
        }

        public static IBaseGateway CreateGateway() =>
            Override?.Invoke() ?? new LocalBaseGateway(
                new FileBaseStore(Path.Combine(Application.persistentDataPath, "village.json")),
                new SystemClock(), BuildingTable.Default, VillageMap.Demo, message => Debug.LogWarning("[CoC] " + message));
    }
}
