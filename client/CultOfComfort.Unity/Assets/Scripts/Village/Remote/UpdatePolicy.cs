using System;

namespace CultOfComfort.Client
{
    /// <summary>The server's word on versions (<c>/api/app-version</c>): below Minimum the game must update; below Latest it is asked to.</summary>
    public sealed class AppVersionPolicy
    {
        public int Latest;
        public int Minimum;
    }

    public enum UpdateAction
    {
        None,

        /// <summary>Downloads in the background while the game is played; a restart installs it.</summary>
        Flexible,

        /// <summary>Play's full-screen update, which the game can't be played past.</summary>
        Immediate,

        /// <summary>Play has no in-app update to offer (not from Play, or not rolled out yet) but the server requires one: to the store.</summary>
        OpenStore,
    }

    /// <summary>
    /// When to update (as in Coo, 2026-11-21): from what Play offers, its priority (0–5, set per release) and the
    /// server's minimum. Play's in-app update can lag a release by hours or not come at all, so the server's Latest
    /// suggests a newer version once a day besides. Pure: tested without Unity.
    /// </summary>
    public static class UpdatePolicy
    {
        /// <summary>Play's update priority from which an update is immediate.</summary>
        public const int ImmediatePriority = 4;

        /// <summary>How often a newer version is suggested, when nothing else asks for it.</summary>
        public static readonly TimeSpan SuggestEvery = TimeSpan.FromDays(1);

        public static UpdateAction Decide(long versionCode, AppVersionPolicy server, bool playOffersUpdate, int playPriority, bool immediateAllowed,
            bool flexibleAllowed, bool flexibleOfferedThisSession)
        {
            var required = server != null && versionCode < server.Minimum;
            if (playOffersUpdate)
            {
                if ((required || playPriority >= ImmediatePriority) && immediateAllowed)
                {
                    return UpdateAction.Immediate;
                }

                if (flexibleAllowed && !flexibleOfferedThisSession)
                {
                    return UpdateAction.Flexible;
                }

                return required && !immediateAllowed && !flexibleAllowed ? UpdateAction.OpenStore : UpdateAction.None;
            }

            return required ? UpdateAction.OpenStore : UpdateAction.None;
        }

        /// <summary>When <see cref="Decide"/> asks nothing but the server knows a newer version: "a new version is ready", once a day.</summary>
        public static bool ShouldSuggest(long versionCode, AppVersionPolicy server, DateTimeOffset? lastSuggested, DateTimeOffset now) =>
            server != null && versionCode < server.Latest
            && (lastSuggested == null || now - lastSuggested.Value >= SuggestEvery || now < lastSuggested.Value);
    }
}
