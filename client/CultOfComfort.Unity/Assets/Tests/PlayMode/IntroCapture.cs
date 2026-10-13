using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CultOfComfort.Client.Tests
{
    /// <summary>
    /// Review tool, not a test: plays the intro at 30 fps and writes one still per moment (3D and captions composited)
    /// into art/previews/intro/. Run explicitly: -testPlatform PlayMode -testFilter IntroCapture.
    /// </summary>
    [Explicit, Category("Capture")]
    public class IntroCapture
    {
        private const int Width = 540;
        private const int Height = 1212;
        private const float Scale = 1.3125f; // half the Pixel 10's ~420 dpi / 160

        /// <summary>(shot, seconds into it) worth a still.</summary>
        private static readonly (string Shot, float At)[] Moments =
        {
            (IntroScript.Garage, 2.5f), (IntroScript.Machine, 3.8f), (IntroScript.Jump, 0.5f), (IntroScript.Jump, 2.5f),
            (IntroScript.Marchlands, 3f), (IntroScript.Landing, 0.7f), (IntroScript.Landing, 1.3f), (IntroScript.NoSockets, 1.8f),
            (IntroScript.NoSockets, 3.5f), (IntroScript.Boot, 2.5f), (IntroScript.Boot, 6f), (IntroScript.Cult, 1.5f), (IntroScript.Cult, 4.5f),
            (IntroScript.Title, 3f),
        };

        private FakeProgress _progress;

        [SetUp]
        public void SetUp()
        {
            TestVillage.Use();
            _progress = new FakeProgress();
            VillageComposition.IntroOverride = () => _progress;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            VillageComposition.IntroOverride = null;
            TestVillage.Reset();
        }

        [UnityTest]
        public IEnumerator IntroCapture_EveryShot_WritesPngs()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            var intro = game.Intro;
            Assert.IsNotNull(intro);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "intro"));
            Directory.CreateDirectory(output);

            var ui = new RenderTexture(Width, Height, 24);
            var panel = game.GetComponent<UIDocument>().panelSettings;
            panel.targetTexture = ui;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = Scale;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var world = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            var camera = Camera.main;
            camera.aspect = Width / (float)Height;

            Time.captureDeltaTime = 1f / 30f;
            var pending = new Queue<(string Shot, float At)>(Moments);
            var index = 0;
            var guard = 0;
            while (pending.Count > 0 && intro != null && intro.IsPlaying && guard++ < 3000)
            {
                yield return null;
                var next = pending.Peek();
                if (intro.CurrentShot == next.Shot && intro.ShotTime >= next.At)
                {
                    pending.Dequeue();
                    Save(camera, world, ui, Path.Combine(output, $"{index++:00}_{next.Shot}_{next.At:0.0}.png"));
                }
            }

            Assert.AreEqual(0, pending.Count, "every moment was captured");
        }

        [UnityTest]
        public IEnumerator IntroCapture_TutorialCoach_WritesPngs()
        {
            _progress.MarkSeen();
            yield return SceneManager.LoadSceneAsync("SampleScene");
            for (var f = 0; f < 6; f++)
            {
                yield return null;
            }

            var game = Object.FindAnyObjectByType<GameController>();
            var document = game.GetComponent<UIDocument>();
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "intro"));
            Directory.CreateDirectory(output);
            var ui = new RenderTexture(Width, Height, 24);
            document.panelSettings.targetTexture = ui;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var hud = (HudView)typeof(GameController).GetField("_hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
            hud.SetPortrait(true);
            var world = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            Camera.main.aspect = Width / (float)Height;
            for (var f = 0; f < 8; f++)
            {
                yield return null;
            }

            Save(Camera.main, world, ui, Path.Combine(output, "coach_1_card.png"));
            var card = document.rootVisualElement.Q<Button>("card-Woodcutter");
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = card;
                card.SendEvent(submit);
            }

            for (var f = 0; f < 8; f++)
            {
                yield return null;
            }

            Save(Camera.main, world, ui, Path.Combine(output, "coach_2_confirm.png"));
        }

        private static void Save(Camera camera, RenderTexture world, RenderTexture ui, string path)
        {
            camera.targetTexture = world;
            camera.Render();
            camera.targetTexture = null;
            var image = Read(world);
            var overlay = Read(ui);
            var pixels = image.GetPixels();
            var top = overlay.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Lerp(pixels[i], new Color(top[i].r, top[i].g, top[i].b, 1f), top[i].a);
            }

            image.SetPixels(pixels);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(overlay);
        }

        private static Texture2D Read(RenderTexture source)
        {
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            return texture;
        }

        private sealed class FakeProgress : IIntroProgress
        {
            public bool Seen { get; private set; }

            public void MarkSeen() => Seen = true;
        }
    }
}
