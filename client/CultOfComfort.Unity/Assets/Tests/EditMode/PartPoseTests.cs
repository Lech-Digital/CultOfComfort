using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class PartPoseTests
    {
        [Test]
        public void SwoonTilt_StartAndEnd_UprightThenFlatOnHisBack()
        {
            // Act & Assert
            Assert.AreEqual(0f, PartPose.SwoonTilt(0f), 0.001f);
            Assert.AreEqual(PartPose.SwoonAngle, PartPose.SwoonTilt(1f), 0.001f);
        }

        [Test]
        public void SwoonTilt_NearTheEnd_BouncesButNeverPassesFlat()
        {
            // Arrange
            var max = 0f;
            var bounced = false;

            // Act
            for (var i = 0; i <= 100; i++)
            {
                var tilt = PartPose.SwoonTilt(i / 100f);
                max = UnityEngine.Mathf.Max(max, tilt);
                bounced |= i > 75 && tilt < PartPose.SwoonAngle - 2f;
            }

            // Assert
            Assert.LessOrEqual(max, PartPose.SwoonAngle + 0.001f);
            Assert.IsTrue(bounced, "He should bounce once when he hits the mud.");
        }

        [Test]
        public void ArmSwing_Standing_HangsStill()
        {
            Assert.AreEqual(0f, PartPose.ArmSwing(1.3f, moving: false));
        }

        [Test]
        public void AttackSwing_RestAtBothEnds_StrikesInBetween()
        {
            // Act
            var peak = 0f;
            for (var i = 0; i <= 100; i++)
            {
                peak = UnityEngine.Mathf.Max(peak, PartPose.AttackSwing(i / 100f));
            }

            // Assert
            Assert.AreEqual(0f, PartPose.AttackSwing(1f), 0.001f);
            Assert.AreEqual(0f, PartPose.AttackSwing(0f), 0.001f);
            Assert.Greater(peak, 80f);
        }

        [Test]
        public void ReviveShake_StartAndEnd_StillInBetweenShakes()
        {
            // Arrange
            var peak = 0f;
            for (var i = 0; i <= 100; i++)
            {
                peak = UnityEngine.Mathf.Max(peak, UnityEngine.Mathf.Abs(PartPose.ReviveShake(i / 100f)));
            }

            // Assert
            Assert.AreEqual(0f, PartPose.ReviveShake(0f), 0.001f);
            Assert.AreEqual(0f, PartPose.ReviveShake(1f), 0.001f);
            Assert.Greater(peak, 4f);
        }

        [Test]
        public void HitSquash_NoHit_FullHeight()
        {
            Assert.AreEqual(1f, PartPose.HitSquash(0f), 0.0001f);
            Assert.Less(PartPose.HitSquash(0.5f), 0.95f);
        }
    }
}
