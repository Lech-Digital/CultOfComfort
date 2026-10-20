using System;
using System.Threading.Tasks;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The Google OAuth Web client id the server accepts ID tokens for (ADR 0007). Empty: Google sign-in is off and the
    /// button stays hidden. The same id goes in the server's Auth__GoogleClientIds__0.
    /// </summary>
    public static class GoogleAuthConfig
    {
        public const string WebClientId = "306329299380-ldti7e8ln5afc69r6mm3g64ldjgfipfu.apps.googleusercontent.com";

        public static bool Enabled => !string.IsNullOrEmpty(WebClientId) && Application.platform == RuntimePlatform.Android;
    }

    /// <summary>
    /// Sign in with Google on Android (ADR 0007, 2026-10-20): the native Credential Manager sheet, through
    /// GoogleSignInBridge.java. Resolves to the Google ID token, null when the player backs out, or throws with why.
    /// </summary>
    public static class GoogleSignIn
    {
        private sealed class Listener : AndroidJavaProxy
        {
            private readonly TaskCompletionSource<string> _done;

            public Listener(TaskCompletionSource<string> done) : base("com.lechdigital.cultofcomfort.auth.GoogleSignInBridge$Listener") => _done = done;

            public void onToken(string idToken) => _done.TrySetResult(idToken);

            public void onCancelled() => _done.TrySetResult(null);

            public void onError(string message) => _done.TrySetException(new InvalidOperationException(message));
        }

        public static Task<string> IdTokenAsync()
        {
            var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!GoogleAuthConfig.Enabled)
            {
                done.SetException(new InvalidOperationException("Google sign-in isn't set up in this build."));
                return done.Task;
            }

            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.lechdigital.cultofcomfort.auth.GoogleSignInBridge"))
            {
                bridge.CallStatic("signIn", activity, GoogleAuthConfig.WebClientId, new Listener(done));
            }

            return done.Task;
        }
    }
}
