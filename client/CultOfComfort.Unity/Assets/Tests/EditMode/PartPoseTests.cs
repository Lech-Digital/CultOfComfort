using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class PartPoseTests
    {
        private static void AssertRest(Pose pose, string what)
        {
            Assert.AreEqual(0f, pose.BodyTilt, 0.01f, $"{what}: body tilt");
            Assert.AreEqual(0f, pose.ArmRForward, 0.01f, $"{what}: right arm");
            Assert.AreEqual(0f, pose.ArmLForward, 0.01f, $"{what}: left arm");
            Assert.AreEqual(0f, pose.HeadYaw, 0.01f, $"{what}: head yaw");
            Assert.AreEqual(1f, pose.Squash, 0.001f, $"{what}: squash");
        }

        private static float Peak(System.Func<float, float> f)
        {
            var peak = 0f;
            for (var i = 0; i <= 200; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(f(i / 200f)));
            }

            return peak;
        }

        [Test]
        public void OneShots_StartAndEnd_ReturnToRest()
        {
            AssertRest(PartPose.Attack(0f), "attack start");
            AssertRest(PartPose.Attack(1f), "attack end");
            AssertRest(PartPose.Hit(0f), "hit start");
            AssertRest(PartPose.Hit(1f), "hit end");
            AssertRest(PartPose.Annoyed(0f), "annoyed start");
            AssertRest(PartPose.Annoyed(1f), "annoyed end");
            AssertRest(PartPose.Unimpressed(0f), "unimpressed start");
            AssertRest(PartPose.Unimpressed(1f), "unimpressed end");
            AssertRest(PartPose.Revive(1f), "revive end");
        }

        [Test]
        public void Swoon_Ends_FlatOnHisBackWithArmsSplayed()
        {
            // Act
            var lying = PartPose.Swoon(1f);

            // Assert — negative tilt is backwards for a model facing +Z.
            Assert.AreEqual(-PartPose.SwoonAngle, lying.BodyTilt, 0.01f);
            Assert.Greater(lying.ArmLOut, 60f);
            Assert.Greater(lying.ArmROut, 60f);
            AssertRest(PartPose.Swoon(0f), "swoon start");
        }

        [Test]
        public void Swoon_IsTheatrical_StaggersHandToForeheadBouncesAndKicks()
        {
            // Act & Assert — stagger sideways before falling, a hand to the forehead, legs kick on impact.
            Assert.Greater(Peak(p => p < 0.3f ? PartPose.Swoon(p).BodyRoll : 0f), 10f, "stagger");
            Assert.Greater(Peak(p => p < 0.3f ? PartPose.Swoon(p).ArmLForward : 0f), 130f, "hand to forehead");
            Assert.Greater(Peak(p => p > 0.6f ? PartPose.Swoon(p).LegLForward : 0f), 40f, "legs kick");
            Assert.LessOrEqual(Peak(p => PartPose.Swoon(p).BodyTilt), PartPose.SwoonAngle + 0.01f, "never past flat");

            var bounced = false;
            for (var i = 70; i < 100; i++)
            {
                bounced |= PartPose.Swoon(i / 100f).BodyTilt > -PartPose.SwoonAngle + 6f;
            }

            Assert.IsTrue(bounced, "he bounces off the mud");
        }

        [Test]
        public void Revive_SpringsUpPastUprightThenSettles()
        {
            // Act
            var overshoot = 0f;
            for (var i = 0; i <= 100; i++)
            {
                overshoot = Mathf.Max(overshoot, PartPose.Revive(i / 100f).BodyTilt);
            }

            // Assert — starts lying down, pops up a little past upright (forward), ends at rest.
            Assert.AreEqual(-PartPose.SwoonAngle, PartPose.Revive(0f).BodyTilt, 0.01f);
            Assert.Greater(overshoot, 5f);
        }

        [Test]
        public void Attack_RaisesTheUmbrellaOverheadThenSmashesForward()
        {
            Assert.Greater(Peak(p => PartPose.Attack(p).ArmRForward), 150f, "overhead wind-up");
            Assert.Greater(Peak(p => p > 0.35f ? PartPose.Attack(p).BodyTilt : 0f), 12f, "lunge");
        }

        [Test]
        public void Annoyed_StompsAndShakesTheUmbrellaAboveHisHead()
        {
            Assert.Greater(Peak(p => PartPose.Annoyed(p).ArmRForward), 130f, "umbrella raised");
            Assert.Greater(Peak(p => PartPose.Annoyed(p).Lift), 0.04f, "stomping hops");
            Assert.Greater(Peak(p => PartPose.Annoyed(p).ArmLOut), 30f, "hand on hip");
        }

        [Test]
        public void Unimpressed_ShakesHisHeadWithArmsCrossed()
        {
            Assert.Greater(Peak(p => PartPose.Unimpressed(p).HeadYaw), 20f, "head shake");
            Assert.Greater(Peak(p => PartPose.Unimpressed(p).ArmLForward), 60f, "arms crossed");
        }

        [Test]
        public void Bored_OverOneCycle_SighsChecksHisWatchAndTapsHisFoot()
        {
            // Arrange
            var cycle = PartPose.BoredCycleSeconds;

            // Act & Assert
            Assert.Less(PartPose.Bored(0.7f).Squash, 0.93f, "deep sigh");
            Assert.Greater(PartPose.Bored(cycle * 0.42f).ArmLForward, 70f, "looks at the smartwatch");
            Assert.Greater(Peak(p => PartPose.Bored(cycle * (0.65f + p * 0.3f)).LegRForward), 10f, "foot tap");
        }

        [Test]
        public void Walk_IsAWaddle()
        {
            Assert.Greater(Peak(p => PartPose.Walk(p * Mathf.PI * 2f).BodyRoll), 5f);
            Assert.Greater(Peak(p => PartPose.Walk(p * Mathf.PI * 2f).ArmLForward), 40f);
        }

        [Test]
        public void Add_CombinesOffsetsAndMultipliesSquash()
        {
            // Arrange
            var a = new Pose { BodyTilt = 10f, Squash = 0.9f };
            var b = new Pose { BodyTilt = 5f, Squash = 0.5f };

            // Act
            var sum = Pose.Add(a, b);

            // Assert
            Assert.AreEqual(15f, sum.BodyTilt, 0.001f);
            Assert.AreEqual(0.45f, sum.Squash, 0.001f);
        }
    }
}
