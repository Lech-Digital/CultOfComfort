using System;
using System.IO;
using CultOfComfort.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CultOfComfort.Editor
{
    /// <summary>
    /// Renders a character's code-driven animations frame by frame (the real model + PartAnimator, not a mock-up),
    /// so they can be reviewed as GIFs without a phone. Batch:
    /// Unity -batchmode -quit -projectPath client/CultOfComfort.Unity -executeMethod CultOfComfort.Editor.AnimationShowcase.RenderProphet
    /// Frames land in art/previews/anim/&lt;clip&gt;/; art/make_gifs.py turns them into GIFs.
    /// </summary>
    public static class AnimationShowcase
    {
        private const int Size = 384;
        private const int Fps = 20;

        private static readonly (string Clip, float Seconds, Action<PartAnimator, float> Script)[] AllClips =
        {
            ("bored", 10.5f, (_, __) => { }), // gets bored after 4 s of nothing
            ("walk", 1.6f, (a, _) => a.IsMoving = true),
            ("attack", 1.2f, (a, t) => Once(t, 0.15f, a.Attack)),
            ("hit", 1f, (a, t) => Once(t, 0.15f, a.Hit)),
            ("annoyed", 2f, (a, t) => Once(t, 0.15f, a.Annoyed)),
            ("unimpressed", 2.4f, (a, t) => Once(t, 0.15f, a.Unimpressed)),
            ("swoon_revive", 4.6f, (a, t) => a.IsSwooning = t > 0.3f && t < 2.8f),
        };

        [MenuItem("Cult of Comfort/Render Prophet Animations")]
        public static void RenderProphet() => RenderCharacter("prophet", "bored", "walk", "attack", "hit", "annoyed", "unimpressed", "swoon_revive");

        /// <summary>The rest of the cast: raiders march and fight, animals trot (front legs are "arms").</summary>
        public static void RenderCast()
        {
            RenderCharacter("levy", "walk", "attack", "annoyed");
            RenderCharacter("manatarms", "walk", "attack");
            RenderCharacter("zealot", "walk", "attack", "unimpressed");
            RenderCharacter("lord_wulfric", "annoyed");
            RenderCharacter("pig", "walk");
            RenderCharacter("goose", "walk", "annoyed");
            RenderCharacter("dog", "walk");
        }

        private static void RenderCharacter(string name, params string[] clips)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = SetUpStage();
            var model = ArtLibrary.Instantiate(name, null);
            if (model == null)
            {
                throw new InvalidOperationException($"No {name} model: run ./art/build.sh {name} first.");
            }

            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "anim"));
            foreach (var clip in AllClips)
            {
                if (Array.IndexOf(clips, clip.Clip) >= 0)
                {
                    Render(camera, model, output, $"{name}_{clip.Clip}", clip.Seconds, clip.Script);
                }
            }

            Debug.Log($"[CoC] {name} animation frames written to {output}");
        }

        private static void Once(float t, float at, Action action)
        {
            if (t >= at && t < at + 1f / Fps)
            {
                action();
            }
        }

        private static Camera SetUpStage()
        {
            var camera = new GameObject("Showcase Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 0.75f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.43f, 0.48f, 0.29f); // grass
            camera.transform.position = new Vector3(2.2f, 1.5f, 3.2f);
            camera.transform.LookAt(new Vector3(0f, 0.45f, 0f));

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.88f, 0.7f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.58f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 3f;
            ground.GetComponent<Renderer>().sharedMaterial = Shapes.Material(Palette.Grass);
            return camera;
        }

        private static void Render(Camera camera, GameObject model, string output, string clip, float seconds, Action<PartAnimator, float> script)
        {
            // A fresh animator per clip: rest pose, no leftover idle/bored timers.
            var old = model.GetComponent<PartAnimator>();
            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old);
            }

            var animator = model.AddComponent<PartAnimator>();
            animator.Bind();

            var folder = Path.Combine(output, clip);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            Directory.CreateDirectory(folder);

            var target = new RenderTexture(Size, Size, 24) { antiAliasing = 4 };
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            var frames = Mathf.RoundToInt(seconds * Fps);
            for (var frame = 0; frame < frames; frame++)
            {
                var t = frame / (float)Fps;
                script(animator, t);
                animator.Tick(1f / Fps, t);
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder, $"{frame:000}.png"), image.EncodeToPNG());
            }

            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
