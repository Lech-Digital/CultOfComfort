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

        [MenuItem("Cult of Comfort/Render Prophet Animations")]
        public static void RenderProphet()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = SetUpStage();
            var model = ArtLibrary.Instantiate("prophet", null);
            if (model == null)
            {
                throw new InvalidOperationException("No prophet model: run ./art/build.sh prophet first.");
            }

            var animator = model.AddComponent<PartAnimator>();
            animator.Bind();
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "anim"));

            Render(camera, animator, output, "idle", 3f, (_, __) => { });
            Render(camera, animator, output, "walk", 1.6f, (a, _) => a.IsMoving = true);
            Render(camera, animator, output, "attack", 1.4f, (a, t) => Once(t, 0.2f, a.Attack));
            Render(camera, animator, output, "hit", 1.2f, (a, t) => Once(t, 0.2f, a.Hit));
            Render(camera, animator, output, "swoon_revive", 3.6f, (a, t) => a.IsSwooning = t > 0.3f && t < 2f);
            Debug.Log($"[CoC] Animation frames written to {output}");
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

        private static void Render(Camera camera, PartAnimator animator, string output, string clip, float seconds, Action<PartAnimator, float> script)
        {
            var folder = Path.Combine(output, clip);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            Directory.CreateDirectory(folder);
            animator.IsMoving = false;
            animator.IsSwooning = false;
            for (var i = 0; i < 40; i++)
            {
                animator.Tick(1f / Fps, 0f); // settle back to rest between clips
            }

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
