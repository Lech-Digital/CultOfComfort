using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CultOfComfort.Client.Tests
{
    /// <summary>
    /// Review tool, not a test: renders the live HUD at Pixel 10 resolution into art/previews/hud/editor_*.png.
    /// Run explicitly: -testPlatform PlayMode -testFilter HudCapture.
    /// </summary>
    [Explicit, Category("Capture")]
    public class HudCapture
    {
        private const int Width = 1080;
        private const int Height = 2424;
        private const float Scale = 2.625f; // ~420 dpi / 160 reference dpi

        [SetUp]
        public void SetUp() => TestVillage.Use();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            TestVillage.Reset();
        }

        [UnityTest]
        public IEnumerator HudCapture_Portrait_WritesPngs() => Capture(true);

        [UnityTest]
        public IEnumerator HudCapture_Landscape_WritesPngs() => Capture(false);

        private static IEnumerator Capture(bool portrait)
        {
            var suffix = portrait ? "portrait" : "landscape";
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            var document = Object.FindAnyObjectByType<UIDocument>();
            var target = portrait ? new RenderTexture(Width, Height, 24) : new RenderTexture(Height, Width, 24);
            document.panelSettings.targetTexture = target;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0.25f, 0.3f, 0.2f, 1f);
            var hud = (HudView)typeof(GameController).GetField("_hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
            hud.SetPortrait(portrait);
            yield return Frames(4);
            Save(target, "editor_dusk_" + suffix);

            var root = document.rootVisualElement;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                var button = root.Q<Button>("locked-research");
                submit.target = button;
                button.SendEvent(submit);
            }

            yield return Frames(3);
            Save(target, "editor_locked_" + suffix);

            game.StartNight();
            yield return Frames(30);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                var button = root.Q<Button>("settings-button");
                submit.target = button;
                button.SendEvent(submit);
            }

            yield return Frames(3);
            Save(target, "editor_settings_" + suffix);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                var button = root.Q<Button>("settings-button");
                submit.target = button;
                button.SendEvent(submit);
            }

            yield return Frames(3);
            Save(target, "editor_night_" + suffix);

            Time.timeScale = 20f;
            var timeout = Time.realtimeSinceStartup + 120f;
            while (game.Phase != GamePhase.Dawn && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            yield return Frames(3);
            Save(target, "editor_dawn_" + suffix);
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static void Save(RenderTexture target, string name)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "hud"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
