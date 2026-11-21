using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace CultOfComfort.Client
{
    /// <summary>What the game has to say about an update (the rest Play shows itself).</summary>
    public enum UpdatePrompt
    {
        /// <summary>A background download is in: "Restart to update".</summary>
        Restart,

        /// <summary>The server knows a newer version that Play hasn't offered: "A new version is on Google Play", once a day.</summary>
        Suggest,

        /// <summary>This version is too old for the server and Play can't update it in-app: to the store.</summary>
        Required,
    }

    /// <summary>
    /// Updates, as in Coo (2026-11-21: the owner opened the game after a release and was never told). On launch and on
    /// coming back, at most once an hour, it asks Play what it offers this copy and the server what it expects
    /// (<c>/api/app-version</c>), then <see cref="UpdatePolicy"/> decides: Play's full-screen update for a release with
    /// priority 4–5 or one the server requires; else Play's background download and a "Restart to update" when it's in;
    /// and when Play offers nothing but the server knows a newer version, the game says so once a day. Android only.
    /// </summary>
    public static class AppUpdates
    {
        private const string Bridge = "com.lechdigital.cultofcomfort.update.AppUpdateBridge";
        private const string SuggestedKey = "update.suggested_at";
        private const int UpdateAvailable = 2;           // UpdateAvailability.UPDATE_AVAILABLE
        private const int UpdateInProgress = 3;         // UpdateAvailability.DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS
        private const int Downloaded = 11;              // InstallStatus.DOWNLOADED
        private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(1);

        private static DateTimeOffset _lastChecked = DateTimeOffset.MinValue;
        private static bool _flexibleOffered;
        private static SynchronizationContext _main;

        /// <summary>Something for the game to show (on the main thread).</summary>
        public static event Action<UpdatePrompt> Prompted;

        /// <summary>Whether this build can ask Play at all (an Android player, not the editor).</summary>
        public static bool Supported => Application.platform == RuntimePlatform.Android && !Application.isEditor;

        private readonly struct PlayInfo
        {
            public readonly int Availability;
            public readonly int Priority;
            public readonly bool Immediate;
            public readonly bool Flexible;
            public readonly int InstallStatus;

            public PlayInfo(int availability, int priority, bool immediate, bool flexible, int installStatus)
            {
                Availability = availability;
                Priority = priority;
                Immediate = immediate;
                Flexible = flexible;
                InstallStatus = installStatus;
            }
        }

        private sealed class Listener : AndroidJavaProxy
        {
            private readonly TaskCompletionSource<PlayInfo?> _info;

            public Listener(TaskCompletionSource<PlayInfo?> info) : base(Bridge + "$Listener") => _info = info;

            public void onInfo(int availability, int availableVersionCode, int priority, bool immediateAllowed, bool flexibleAllowed, int installStatus) =>
                _info.TrySetResult(new PlayInfo(availability, priority, immediateAllowed, flexibleAllowed, installStatus));

            public void onDownloaded() => _main?.Post(_ => Prompted?.Invoke(UpdatePrompt.Restart), null);

            public void onError(string message)
            {
                Debug.Log($"[CoC] Play has no update answer: {message}");
                _info.TrySetResult(null);
            }
        }

        /// <summary>Asks Play and the server, and acts. <paramref name="serverUrl"/>: the game server's base URL.</summary>
        public static async void Check(string serverUrl, bool force = false)
        {
            if (!Supported)
            {
                return;
            }

            _main ??= SynchronizationContext.Current;
            try
            {
                var info = await PlayInfoAsync();
                if (info?.InstallStatus == Downloaded)
                {
                    Prompted?.Invoke(UpdatePrompt.Restart); // downloaded while away: ask on every return until it's in
                    return;
                }

                if (info?.Availability == UpdateInProgress)
                {
                    Start(immediate: true); // a full-screen update left half-way goes on
                    return;
                }

                var now = DateTimeOffset.UtcNow;
                if (!force && now - _lastChecked < CheckEvery)
                {
                    return;
                }

                _lastChecked = now;
                var code = VersionCode();
                var server = await ServerPolicyAsync(serverUrl);
                var action = UpdatePolicy.Decide(code, server, info?.Availability == UpdateAvailable, info?.Priority ?? 0,
                    info?.Immediate ?? false, info?.Flexible ?? false, _flexibleOffered);
                Debug.Log($"[CoC] Update check: version {code}, server {server?.Latest}/{server?.Minimum}, Play {info?.Availability}: {action}.");
                switch (action)
                {
                    case UpdateAction.Immediate:
                        Start(immediate: true);
                        break;
                    case UpdateAction.Flexible:
                        _flexibleOffered = true;
                        Start(immediate: false);
                        break;
                    case UpdateAction.OpenStore:
                        Prompted?.Invoke(UpdatePrompt.Required);
                        break;
                    default:
                        Suggest(code, server, now);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[CoC] Update check failed: {e.Message}"); // never breaks the game; the next return tries again
            }
        }

        public static void OpenStore() => CallBridge("openStore", withActivity: true);

        /// <summary>Installs a downloaded update; the game restarts.</summary>
        public static void Restart() => CallBridge("complete", withActivity: false);

        private static void Suggest(long code, AppVersionPolicy server, DateTimeOffset now)
        {
            var ticks = long.TryParse(PlayerPrefs.GetString(SuggestedKey, string.Empty), out var t) ? t : 0L;
            var last = ticks > 0 ? new DateTimeOffset(ticks, TimeSpan.Zero) : (DateTimeOffset?)null;
            if (!UpdatePolicy.ShouldSuggest(code, server, last, now))
            {
                return;
            }

            PlayerPrefs.SetString(SuggestedKey, now.UtcTicks.ToString());
            PlayerPrefs.Save();
            Prompted?.Invoke(UpdatePrompt.Suggest);
        }

        private static Task<PlayInfo?> PlayInfoAsync()
        {
            var done = new TaskCompletionSource<PlayInfo?>();
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(Bridge))
                {
                    bridge.CallStatic("check", activity, new Listener(done));
                }
            }
            catch (Exception e)
            {
                Debug.Log($"[CoC] Play has no update answer: {e.Message}");
                done.TrySetResult(null);
            }

            return done.Task;
        }

        private static long VersionCode()
        {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass(Bridge))
            {
                return bridge.CallStatic<long>("versionCode", activity);
            }
        }

        private static void Start(bool immediate)
        {
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass(Bridge))
            {
                bridge.CallStatic<bool>("start", activity, immediate);
            }
        }

        private static void CallBridge(string method, bool withActivity)
        {
            if (!Supported)
            {
                return;
            }

            using (var bridge = new AndroidJavaClass(Bridge))
            {
                if (!withActivity)
                {
                    bridge.CallStatic(method);
                    return;
                }

                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    bridge.CallStatic(method, activity);
                }
            }
        }

        [Serializable]
        private sealed class VersionDto
        {
            public int latest;
            public int minimum;
        }

        /// <summary>The server's word, or null when it can't be asked (offline, an old server).</summary>
        private static async Task<AppVersionPolicy> ServerPolicyAsync(string serverUrl)
        {
            using (var request = UnityWebRequest.Get(serverUrl.TrimEnd('/') + "/api/app-version"))
            {
                request.timeout = 10;
                var sent = request.SendWebRequest();
                while (!sent.isDone)
                {
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    return null;
                }

                var dto = JsonUtility.FromJson<VersionDto>(request.downloadHandler.text);
                return dto != null && dto.latest > 0 ? new AppVersionPolicy { Latest = dto.latest, Minimum = dto.minimum } : null;
            }
        }
    }
}
