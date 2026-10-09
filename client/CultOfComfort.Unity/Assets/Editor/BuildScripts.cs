using System;
using System.Linq;
using UnityEditor;
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
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;

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
    }
}
