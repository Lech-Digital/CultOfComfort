using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CultOfComfort.Client.Tests
{
    public class HudFrameTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            if (_host != null)
            {
                Object.Destroy(_host);
            }
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            yield return null;
        }

        private static VisualElement Root() => Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

        private static bool Visible(VisualElement e) => e.resolvedStyle.display == DisplayStyle.Flex && e.worldBound.width > 0f;

        private static void Click(Button button)
        {
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        /// <summary>A bare HudView on its own panel, for tests that drive it directly.</summary>
        private (HudView Hud, VisualElement Root) StandaloneHud()
        {
            _host = new GameObject("HudHost");
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/CocTheme");
            var document = _host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            var root = document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Hud"));
            var hud = new HudView(root, new Dictionary<TowerType, (string, int)> { [TowerType.SaplingFlinger] = ("Sapling Flinger", 70) });
            return (hud, root);
        }

        /// <summary>A HudView rendered at Pixel 10 size and density (2424×1080 px at 2.625×), portrait or landscape.</summary>
        private (HudView Hud, VisualElement Root) PhoneHud(bool portrait)
        {
            var (hud, root) = StandaloneHud();
            var settings = _host.GetComponent<UIDocument>().panelSettings;
            settings.targetTexture = portrait ? new RenderTexture(1080, 2424, 0) : new RenderTexture(2424, 1080, 0);
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 2.625f;
            hud.SetPortrait(portrait);
            return (hud, root);
        }

        [UnityTest]
        public IEnumerator Landscape_DawnCard_BackToDuskFitsOnScreen()
        {
            // Arrange
            var (hud, root) = PhoneHud(portrait: false);
            hud.UpdateFrame(DemoEconomy.Frame(380, 1));
            var result = new BattleResult(BattleOutcome.Lost, 0, 0, 100, 2000, 3, 10, 0x0123456789abcdefUL);

            // Act
            hud.ShowPhase(GamePhase.Dawn);
            hud.ShowDawn(result, result, 10);
            yield return null;
            yield return null;

            // Assert
            var button = root.Q("card").Q<Button>(className: "primary");
            Assert.LessOrEqual(button.worldBound.yMax, root.worldBound.yMax + 0.5f, "Back to Dusk must be reachable in landscape.");
        }

        [UnityTest]
        public IEnumerator Landscape_SettingsAtNight_CloseIsAboveTheBar()
        {
            // Arrange
            var (hud, root) = PhoneHud(portrait: false);
            hud.ShowPhase(GamePhase.Night);
            yield return null;

            // Act
            Click(root.Q<Button>("settings-button"));
            yield return null;
            yield return null;

            // Assert
            var close = root.Q("settings-sheet").Query<Button>(className: "plain").Last();
            Assert.LessOrEqual(close.worldBound.yMax, root.Q(className: "bar").worldBound.yMin + 0.5f, "The night bar must not cover Close.");
        }

        [UnityTest]
        public IEnumerator Portrait_HearthBadge_SitsOutsideTheClippedFace()
        {
            // Arrange & Act
            var (_, root) = PhoneHud(portrait: true);
            yield return null;

            // Assert
            var badge = root.Q("hearth-badge");
            Assert.IsNotNull(root.Q("portrait").Q(className: "avatar-clip"), "The face is clipped by its own wrapper.");
            Assert.IsFalse(badge.parent.ClassListContains("avatar-clip"), "The badge must not be inside the clip.");
        }

        [UnityTest]
        public IEnumerator Frame_TapOnAPillsDrawnEdge_HitsTheUi()
        {
            // Arrange
            var (hud, root) = PhoneHud(portrait: true);
            hud.UpdateFrame(DemoEconomy.Frame(380, 1));
            yield return null;
            yield return null;

            // Act
            var pill = root.Q("pill-timber").worldBound;
            var picked = root.panel.Pick(new Vector2(pill.xMin + 2f, pill.center.y));

            // Assert
            Assert.IsNotNull(picked, "A tap on a pill must not fall through to the board.");
        }

        [UnityTest]
        public IEnumerator LockedNote_LongNote_TextFitsInsideTheNote()
        {
            // Arrange
            var (hud, root) = PhoneHud(portrait: true);
            hud.ShowPhase(GamePhase.Dusk);

            // Act
            hud.ShowLockedNote("research", Time.time);
            hud.Tick(Time.time);
            yield return null;
            yield return null;

            // Assert
            var note = root.Q<Label>("locked-note");
            var content = note.contentRect;
            var text = note.MeasureTextSize(note.text, content.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
            Assert.LessOrEqual(text.y, content.height + 0.5f);
        }

        [UnityTest]
        public IEnumerator Float_WashedBack_ShowsThenFades()
        {
            // Arrange
            var (hud, root) = PhoneHud(portrait: true);
            hud.ShowPhase(GamePhase.Night);

            // Act
            hud.Float("washed back!", new Vector2(100f, 300f), Time.time);
            hud.Tick(Time.time);
            yield return null;
            var shown = root.Query<Label>(className: "floater").Where(l => l.resolvedStyle.display == DisplayStyle.Flex && l.text == "washed back!").ToList().Count;
            hud.Tick(Time.time + 2f);
            yield return null;
            var after = root.Query<Label>(className: "floater").Where(l => l.resolvedStyle.display == DisplayStyle.Flex).ToList().Count;

            // Assert
            Assert.AreEqual(1, shown);
            Assert.AreEqual(0, after);
        }

        [UnityTest]
        public IEnumerator Frame_Dusk_ShowsPillsPortraitTensionAndShortcuts()
        {
            // Arrange & Act
            yield return LoadGame();
            var root = Root();

            // Assert
            foreach (var name in new[] { "frame", "pill-timber", "pill-scrap", "pill-peat", "pill-devotion", "pill-pennies", "portrait",
                         "settings-button", "tension-strip", "side-left", "side-right", "locked-map", "dusk-bar", "start-night" })
            {
                Assert.IsTrue(Visible(root.Q(name)), name);
            }

            Assert.IsFalse(Visible(root.Q("wave-strip")), "No wave strip in Dusk.");
        }

        [UnityTest]
        public IEnumerator DemoPills_Dusk_MarkedDemoExceptScrap()
        {
            // Arrange & Act
            yield return LoadGame();
            var root = Root();

            // Assert
            foreach (var name in new[] { "pill-timber", "pill-peat", "pill-devotion", "pill-pennies", "comfort" })
            {
                Assert.IsTrue(root.Q(name).ClassListContains("demo"), name);
            }

            StringAssert.Contains("demo", root.Q<Label>("comfort").text, "Comfort's demo mark must be visible, not just a colour.");

            Assert.IsFalse(root.Q("pill-scrap").ClassListContains("demo"));
        }

        [UnityTest]
        public IEnumerator LockedButton_Tapped_ShowsItsNote()
        {
            // Arrange
            yield return LoadGame();
            var root = Root();

            // Act
            Click(root.Q<Button>("locked-research"));
            yield return null; // Tick un-hides the note
            yield return null; // UI Toolkit lays it out

            // Assert
            var note = root.Q<Label>("locked-note");
            Assert.IsTrue(Visible(note));
            StringAssert.Contains("Hearth 3", note.text);
        }

        [UnityTest]
        public IEnumerator LockedButton_SecondTapOnAnother_ReplacesTheNote()
        {
            // Arrange
            yield return LoadGame();
            var root = Root();
            Click(root.Q<Button>("locked-research"));
            yield return null;

            // Act
            Click(root.Q<Button>("locked-tithes"));
            yield return null;

            // Assert
            Assert.AreEqual(1, root.Query<Label>("locked-note").ToList().Count);
            StringAssert.Contains("Hearth 4", root.Q<Label>("locked-note").text);
        }

        [UnityTest]
        public IEnumerator Night_StartNight_SwapsTensionForWaveAndHidesShortcuts()
        {
            // Arrange
            yield return LoadGame();
            var game = Object.FindAnyObjectByType<GameController>();
            var root = Root();

            // Act
            game.StartNight();
            yield return null;
            yield return null;

            // Assert
            Assert.IsTrue(Visible(root.Q("wave-strip")));
            Assert.IsFalse(Visible(root.Q("tension-strip")));
            Assert.IsFalse(Visible(root.Q("side-left")));
            StringAssert.StartsWith("Wave", root.Q<Label>("wave-label").text);
            Assert.IsTrue(Visible(root.Q("frame")), "The frame stays at night.");
        }

        [UnityTest]
        public IEnumerator Settings_Opened_ShowsRecenterAndBlocksWorldTaps()
        {
            // Arrange
            yield return LoadGame();
            var root = Root();

            // Act
            Click(root.Q<Button>("settings-button"));
            yield return null;
            yield return null;

            // Assert
            var sheet = root.Q("settings-sheet");
            Assert.IsTrue(Visible(sheet));
            Assert.IsTrue(Visible(root.Q("recenter")));
            var centre = sheet.worldBound.center;
            var panelToScreen = new Vector2(centre.x / root.worldBound.width * Screen.width, Screen.height - centre.y / root.worldBound.height * Screen.height);
            var hud = typeof(GameController).GetField("_hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(Object.FindAnyObjectByType<GameController>()) as HudView;
            Assert.IsTrue(hud.IsOverUi(panelToScreen), "Taps on the sheet must not reach the board.");
        }

        [UnityTest]
        public IEnumerator Orientation_PortraitAndLandscape_RestackTheDuskBar()
        {
            // Arrange
            var (hud, root) = StandaloneHud();
            hud.ShowPhase(GamePhase.Dusk);

            // Act
            hud.SetPortrait(true);
            yield return null;
            var portrait = root.Q("dusk-bar").resolvedStyle.flexDirection;
            hud.SetPortrait(false);
            yield return null;
            var landscape = root.Q("dusk-bar").resolvedStyle.flexDirection;

            // Assert
            Assert.AreEqual(FlexDirection.Column, portrait);
            Assert.AreEqual(FlexDirection.Row, landscape);
        }

        [UnityTest]
        public IEnumerator Orientation_Portrait_RootStillFillsTheScreen()
        {
            // Arrange: the root carries the orientation class "portrait" — no other style may hijack it.
            var (hud, root) = StandaloneHud();
            hud.ShowPhase(GamePhase.Dusk);

            // Act
            hud.SetPortrait(true);
            yield return null;
            yield return null;

            // Assert
            var panel = root.panel.visualTree.worldBound;
            Assert.AreEqual(panel.width, root.worldBound.width, 1f);
            Assert.AreEqual(panel.height, root.worldBound.height, 1f);
        }

        [UnityTest]
        public IEnumerator Announce_VeryLongText_WrapsInsideTheScreen()
        {
            // Arrange
            var (hud, root) = StandaloneHud();
            hud.ShowPhase(GamePhase.Dusk);

            // Act
            hud.Announce(new string('x', 40) + " " + string.Join(" ", new string[30]).Replace(" ", "mud ") , Time.time, 10f, prophet: true);
            hud.Tick(Time.time);
            yield return null;
            yield return null;

            // Assert
            var toast = root.Q("toast");
            Assert.IsTrue(Visible(toast));
            Assert.LessOrEqual(toast.worldBound.xMax, root.worldBound.xMax + 0.5f);
            Assert.GreaterOrEqual(toast.worldBound.xMin, root.worldBound.xMin - 0.5f);
        }

        [UnityTest]
        public IEnumerator Meters_NegativeOrZeroHearth_ShowAnEmptyBar()
        {
            // Arrange
            var (hud, root) = StandaloneHud();
            hud.ShowPhase(GamePhase.Night);

            // Act
            hud.UpdateNight(new NightHudState(1, -5, 100, 0, 110, "Open Sluice · 2", true, "Phantom Voice", true, "Hot Stones", true, false, 1, 1, 4, 0, "hint"));
            yield return null;
            yield return null;

            // Assert
            Assert.AreEqual(0f, root.Q("hearth-fill").resolvedStyle.width, 0.01f);
            Assert.AreEqual(0f, root.Q("prophet-fill").resolvedStyle.width, 0.01f);
        }
    }
}
