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

        [MenuItem("Cult of Comfort/Build Android APK")]
        public static void BuildAndroid()
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
            EditorUserBuildSettings.buildAppBundle = false;
            ApplyIcons(android);

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = AndroidOutput,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[CoC] Android build {summary.result}: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss})");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
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
