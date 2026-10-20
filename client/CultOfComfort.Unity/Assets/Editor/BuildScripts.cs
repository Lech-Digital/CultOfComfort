using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CultOfComfort.Editor
{
    /// <summary>
    /// Command-line builds, e.g.:
    /// Unity -batchmode -quit -projectPath client/CultOfComfort.Unity -buildTarget Android
    ///       -executeMethod CultOfComfort.Editor.BuildScripts.BuildAndroid
    /// </summary>
    public static class BuildScripts
    {
        public const string AndroidOutput = "Builds/Android/CultOfComfort.apk";

        public const string AndroidReleaseOutput = "Builds/Android/CultOfComfort.aab";

        /// <summary>The upload key for Google Play (outside the repo, like the other apps' keys in ~/keystores).</summary>
        public static readonly string UploadKeystore =
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "keystores", "cultofcomfort-upload.jks");

        [MenuItem("Cult of Comfort/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigureAndroid();
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false;
            Build(AndroidOutput, BuildOptions.Development);
        }

        /// <summary>
        /// The Google Play build: a signed release App Bundle, no development flags. Needs CULTOFCOMFORT_KEYSTORE_PASS
        /// (the upload key's password); without it the build fails rather than ship something unsigned. Optional
        /// -versionCode N and -versionName x.y.z on the command line; otherwise the Player Settings values are used.
        /// </summary>
        [MenuItem("Cult of Comfort/Build Android release (AAB)")]
        public static void BuildAndroidRelease()
        {
            var password = Environment.GetEnvironmentVariable("CULTOFCOMFORT_KEYSTORE_PASS");
            if (string.IsNullOrEmpty(password) || !System.IO.File.Exists(UploadKeystore))
            {
                Fail($"[CoC] Release build needs {UploadKeystore} and CULTOFCOMFORT_KEYSTORE_PASS.");
                return;
            }

            ConfigureAndroid();
            var versionCode = Argument("-versionCode");
            if (versionCode != null)
            {
                PlayerSettings.Android.bundleVersionCode = int.Parse(versionCode);
            }

            var versionName = Argument("-versionName");
            if (versionName != null)
            {
                PlayerSettings.bundleVersion = versionName;
            }

            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36; // Google Play's target for new apps in 2026
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = UploadKeystore;
            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasName = "upload";
            PlayerSettings.Android.keyaliasPass = password;
            EditorUserBuildSettings.buildAppBundle = true;
            try
            {
                Build(AndroidReleaseOutput, BuildOptions.None);
            }
            finally
            {
                // Never leave the password in ProjectSettings.asset.
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.keystoreName = string.Empty;
                PlayerSettings.Android.keyaliasName = string.Empty;
                AssetDatabase.SaveAssets();
            }
        }

        private static void ConfigureAndroid()
        {
            ProjectSetup.EnsureAssets();
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.lechdigital.cultofcomfort");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP); // Required for ARM64.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            ApplyIcons(android);
        }

        private static void Build(string output, BuildOptions buildOptions)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                options = buildOptions,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[CoC] Android build {summary.result}: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss})");
            if (summary.result != BuildResult.Succeeded)
            {
                Fail("[CoC] Android build failed.");
            }
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static void Fail(string message)
        {
            Debug.LogError(message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// The app icon (art/make_app_icon.py): the hooded duck in its tub on the Hearth's glow. Adaptive layers for
        /// Android 8+, the full composite for round and legacy launchers and as the default icon.
        /// </summary>
        private static void ApplyIcons(NamedBuildTarget android)
        {
            const string folder = "Assets/Branding/";
            AssetDatabase.ImportAsset(folder + "app_icon.png");
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "app_icon.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "app_icon_background.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "app_icon_foreground.png");
            if (full == null || background == null || foreground == null)
            {
                Debug.LogWarning("[CoC] App icon missing from Assets/Branding: run art/make_app_icon.py");
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (var icon in adaptive)
            {
                icon.SetTextures(background, foreground);
            }

            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            foreach (var kind in new[] { AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Legacy })
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var icon in icons)
                {
                    icon.SetTexture(full);
                }

                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
        }
    }
}
