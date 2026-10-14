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

        /// <summary>The game server (docs/adrs/0005; deploy/README).</summary>
        public const string ServerUrl = "https://api.cultofcomfort.lechdigital.nl";

        /// <summary>
        /// The server holds the village; the device keeps a cache and a queue so it plays offline. Set false (or
        /// <see cref="Override"/>) to run the rules on the device alone, as before the server existed.
        /// </summary>
        public static bool UseServer { get; set; } = true;

        /// <summary>
        /// The owner's testing gate: a key file pushed with adb (deploy/README: dev key) next to the save. Present and
        /// long enough: the dev tools show and the server accepts development commands from this device.
        /// </summary>
        public static string DevKey()
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, "dev.key");
                var key = File.Exists(path) ? File.ReadAllText(path).Trim() : null;
                return key != null && key.Length >= 24 ? key : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public static IBaseGateway CreateGateway()
        {
            if (Override != null)
            {
                return Override();
            }

            void Warn(string message) => Debug.LogWarning("[CoC] " + message);
            if (UseServer)
            {
                var devKey = DevKey();
                return new RemoteBaseGateway(new HttpVillageApi(ServerUrl, devKey), new PlayerPrefsCredentialStore(),
                    new FileRemoteCache(Path.Combine(Application.persistentDataPath, "village-cache.json")),
                    new SystemClock(), BuildingTable.Default, VillageMap.Demo, Warn, developer: devKey != null);
            }

            return new LocalBaseGateway(new FileBaseStore(Path.Combine(Application.persistentDataPath, "village.json")),
                new SystemClock(), BuildingTable.Default, VillageMap.Demo, Warn);
        }
    }
}
