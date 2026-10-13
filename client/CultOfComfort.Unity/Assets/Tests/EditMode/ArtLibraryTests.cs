using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class ArtLibraryTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
            }
        }

        [Test]
        public void Instantiate_UnknownName_ReturnsNull()
        {
            // Act
            _instance = ArtLibrary.Instantiate("beige_of_regret", null);

            // Assert
            Assert.IsNull(_instance);
        }

        [Test]
        public void Instantiate_PipelineCube_UsesPaletteMaterialEverywhere()
        {
            // Act
            _instance = ArtLibrary.Instantiate("pipeline_cube", null);

            // Assert
            Assert.IsNotNull(_instance, "Run ./art/build.sh pipeline_cube first.");
            Assert.IsNotNull(ArtLibrary.PaletteMaterial, "Run ProjectSetup.EnsureAssets to create Palette.mat.");
            foreach (var renderer in _instance.GetComponentsInChildren<Renderer>())
            {
                Assert.AreEqual(ArtLibrary.PaletteMaterial, renderer.sharedMaterial, renderer.name);
            }

            Assert.IsNotNull(ArtLibrary.FindPart(_instance.transform, "Cube"));
            Assert.IsNull(ArtLibrary.FindPart(_instance.transform, "Umbrella"));
        }

        [Test]
        public void PaletteTexture_ImportSettings_KeepSwatchesCrisp()
        {
            // Act
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Art/Palette.png");

            // Assert
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
        }

        [Test]
        public void Instantiate_Prophet_IsAboutOneMetreTallAndUpright()
        {
            // Arrange
            _instance = ArtLibrary.Instantiate("prophet", null);
            Assert.IsNotNull(_instance, "Run ./art/build.sh prophet first.");
            var bounds = new Bounds(_instance.transform.position, Vector3.zero);

            // Act
            foreach (var renderer in _instance.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }

            // Assert
            // Renderer bounds box the tilted head's rotated local bounds, so they read a few cm tall; the Blender-side
            // test measures real vertices. This one guards against wrong scale or a model lying down.
            Assert.AreEqual(1.0f, bounds.size.y, 0.1f, "height");
            Assert.AreEqual(0f, bounds.min.y, 0.02f, "feet on the ground");
            Assert.Greater(bounds.size.y, bounds.size.x * 1.5f, "upright, not lying down");
        }

        [Test]
        public void Instantiate_Prophet_FacesPlusZ()
        {
            // Act
            _instance = ArtLibrary.Instantiate("prophet", null);

            // Assert
            Assert.Greater(ArtLibrary.FindPart(_instance.transform, "Front").position.z, 0.2f,
                "Unity moves units with LookRotation(heading), so models must face +Z.");
        }

        [Test]
        public void Instantiate_Prophet_PivotsAtFeetAndShoulders()
        {
            // Act
            _instance = ArtLibrary.Instantiate("prophet", null);
            var root = _instance.transform;

            // Assert
            Assert.AreEqual(0f, ArtLibrary.FindPart(root, "Body").position.y, 0.01f);
            Assert.AreEqual(0.56f, ArtLibrary.FindPart(root, "ArmR").position.y, 0.02f);
            Assert.AreEqual(0.35f, ArtLibrary.FindPart(root, "Umbrella").position.y, 0.02f);
            Assert.AreEqual("ArmR", ArtLibrary.FindPart(root, "Umbrella").parent.name);
        }

        [Test]
        public void PartAnimator_Swooning_TipsTheBodyBackwards()
        {
            // Arrange
            _instance = ArtLibrary.Instantiate("prophet", null);
            var animator = _instance.AddComponent<PartAnimator>();
            animator.Bind();
            var head = ArtLibrary.FindPart(_instance.transform, "Head");
            var standingHeadZ = head.position.z;

            // Act
            animator.IsSwooning = true;
            for (var i = 0; i < 60; i++)
            {
                animator.Tick(1f / 30f, i / 30f);
            }

            // Assert — he faces +Z, so falling on his back moves his head towards -Z and down near the ground.
            Assert.Less(head.position.z, standingHeadZ - 0.4f);
            Assert.Less(head.position.y, 0.35f);
        }

        [Test]
        public void PartAnimator_Attack_SwingsTheUmbrellaForward()
        {
            // Arrange — reviewer finding: a positive X rotation swings a hanging arm backwards.
            _instance = ArtLibrary.Instantiate("prophet", null);
            var animator = _instance.AddComponent<PartAnimator>();
            animator.Bind();
            var umbrella = ArtLibrary.FindPart(_instance.transform, "Umbrella");
            var restZ = umbrella.position.z;

            // Act — run into the smash (about half way through the attack).
            animator.Attack();
            for (var i = 0; i < 12; i++)
            {
                animator.Tick(1f / 30f, i / 30f);
            }

            // Assert — the model faces +Z, so a forward smash moves the umbrella towards +Z.
            Assert.Greater(umbrella.position.z, restZ + 0.15f);
        }

        [Test]
        public void PartAnimator_LeftIdle_GetsBored()
        {
            // Arrange
            _instance = ArtLibrary.Instantiate("prophet", null);
            var animator = _instance.AddComponent<PartAnimator>();
            animator.Bind();

            // Act — six seconds of nothing.
            for (var i = 0; i < 180; i++)
            {
                animator.Tick(1f / 30f, i / 30f);
            }

            // Assert
            Assert.IsTrue(animator.IsBored);
        }

        [Test]
        public void PartAnimator_Annoyed_ShakesTheUmbrellaAboveHisHead()
        {
            // Arrange
            _instance = ArtLibrary.Instantiate("prophet", null);
            var animator = _instance.AddComponent<PartAnimator>();
            animator.Bind();
            var umbrella = ArtLibrary.FindPart(_instance.transform, "Umbrella").GetComponent<Renderer>();

            // Act — half way through the tantrum.
            animator.Annoyed();
            for (var i = 0; i < 24; i++)
            {
                animator.Tick(1f / 30f, i / 30f);
            }

            // Assert — the head tops out at ~1.0 m; the shaken umbrella must poke above it.
            Assert.Greater(umbrella.bounds.max.y, 1.05f);
        }

        [TestCase("levy")]
        [TestCase("manatarms")]
        [TestCase("zealot")]
        public void Instantiate_Enemy_IsALocalSizedPersonFacingPlusZWithAWeapon(string name)
        {
            // Act
            _instance = ArtLibrary.Instantiate(name, null);

            // Assert
            Assert.IsNotNull(_instance, $"Run ./art/build.sh {name} first.");
            var bounds = new Bounds(_instance.transform.position, Vector3.zero);
            foreach (var renderer in _instance.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }

            Assert.AreEqual(0.85f, bounds.size.y, 0.15f, "locals are about 0.8 m");
            Assert.Greater(ArtLibrary.FindPart(_instance.transform, "Front").position.z, 0.15f);
            Assert.IsNotNull(ArtLibrary.FindPart(_instance.transform, "Weapon"));
            Assert.AreEqual(0f, ArtLibrary.FindPart(_instance.transform, "Body").position.y, 0.01f);
        }

        [Test]
        public void Exists_KnownAndUnknownModels_ReportsCorrectly()
        {
            Assert.IsTrue(ArtLibrary.Exists("prophet"));
            Assert.IsFalse(ArtLibrary.Exists("beige_of_regret"));
        }

        [TestCase("hearth_l1", "Fire")]
        [TestCase("sapling_flinger", "Arm")]
        [TestCase("slurry_chute", "Gate")]
        [TestCase("torch", "Flame")]
        public void Instantiate_Scenery_HasItsAnimatedPart(string name, string part)
        {
            // Act
            _instance = ArtLibrary.Instantiate(name, null);

            // Assert
            Assert.IsNotNull(_instance, $"Run ./art/build.sh {name} first.");
            Assert.IsNotNull(ArtLibrary.FindPart(_instance.transform, part));
        }

        private PartAnimator ProphetAnimator()
        {
            _instance = ArtLibrary.Instantiate("prophet", null);
            var animator = _instance.AddComponent<PartAnimator>();
            animator.Bind();
            return animator;
        }

        [Test]
        public void PartAnimator_TickWithEmotesRunning_AllocatesNothing()
        {
            // Arrange — reviewer finding: method-group delegates were allocated per one-shot per frame.
            var animator = ProphetAnimator();
            animator.IsMoving = true;
            animator.Attack();
            animator.Annoyed();
            animator.Tick(1f / 30f, 0f); // warm up
            var before = System.GC.GetAllocatedBytesForCurrentThread();

            // Act
            for (var i = 1; i < 30; i++)
            {
                animator.Tick(1f / 30f, i / 30f);
            }

            // Assert
            Assert.AreEqual(0L, System.GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void PartAnimator_EmoteWhileSwooning_IsIgnored()
        {
            // Arrange — a rejected order while he lies in the mud must not queue a tantrum for after he gets up.
            var animator = ProphetAnimator();
            animator.IsSwooning = true;
            animator.Tick(1f / 30f, 0f);

            // Act
            animator.Annoyed();
            animator.Unimpressed();

            // Assert
            Assert.IsFalse(animator.IsEmoting);
        }

        [Test]
        public void PartAnimator_RefiringAnEmoteMidway_DoesNotSnapBackToRest()
        {
            // Arrange — half way through a tantrum the umbrella is above his head.
            var animator = ProphetAnimator();
            var umbrella = ArtLibrary.FindPart(_instance.transform, "Umbrella").GetComponent<Renderer>();
            animator.Annoyed();
            for (var i = 0; i < 24; i++)
            {
                animator.Tick(1f / 30f, i / 30f);
            }

            // Act — another refusal arrives.
            animator.Annoyed();
            animator.Tick(1f / 30f, 0.8f);

            // Assert
            Assert.Greater(umbrella.bounds.max.y, 1.05f);
        }

        [Test]
        public void EnemyModel_EveryRaiderType_HasAModelThatExists()
        {
            foreach (CultOfComfort.Simulation.Balance.EnemyType type in System.Enum.GetValues(typeof(CultOfComfort.Simulation.Balance.EnemyType)))
            {
                // Act
                var name = BattleView.EnemyModel(type, 1);

                // Assert
                Assert.IsTrue(ArtLibrary.Exists(name), $"{type} → {name}");
            }

            Assert.AreEqual("lord_wulfric", BattleView.EnemyModel(CultOfComfort.Simulation.Balance.EnemyType.Lord, 1));
        }
    }
}
