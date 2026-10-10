using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>Animation curves for rigid-part characters. Pure functions of time/progress, so they are unit-tested.</summary>
    public static class PartPose
    {
        /// <summary>Degrees he tips backwards when he faints (not quite 90: he lands on his belt bag).</summary>
        public const float SwoonAngle = 88f;

        private const float FallPortion = 0.7f;
        private const float BounceDegrees = 7f;

        /// <param name="progress">0 = standing, 1 = flat in the mud.</param>
        public static float SwoonTilt(float progress)
        {
            var p = Mathf.Clamp01(progress);
            if (p < FallPortion)
            {
                var t = p / FallPortion;
                return SwoonAngle * t * t; // accelerates like a falling plank
            }

            var settle = (p - FallPortion) / (1f - FallPortion);
            return SwoonAngle - Mathf.Sin(settle * Mathf.PI) * BounceDegrees;
        }

        public static float WalkBob(float phase) => Mathf.Abs(Mathf.Sin(phase)) * 0.05f;

        public static float ArmSwing(float phase, bool moving) => moving ? Mathf.Sin(phase) * 35f : 0f;

        /// <param name="remaining">1 when the attack starts, counting down to 0.</param>
        /// <returns>Forward swing in degrees: a short wind-up back, then a big strike, then rest.</returns>
        public static float AttackSwing(float remaining)
        {
            var t = 1f - Mathf.Clamp01(remaining);
            if (t < 0.25f)
            {
                return -30f * Mathf.Sin(t / 0.25f * Mathf.PI);
            }

            return 110f * Mathf.Sin((t - 0.25f) / 0.75f * Mathf.PI);
        }

        public static float HitSquash(float remaining) => 1f - 0.12f * Mathf.Sin(Mathf.Clamp01(remaining) * Mathf.PI);

        /// <summary>Brush-off shake after getting up (degrees of body yaw); <paramref name="remaining"/> goes 1 → 0.</summary>
        public static float ReviveShake(float remaining)
        {
            var r = Mathf.Clamp01(remaining);
            return Mathf.Sin(r * Mathf.PI * 6f) * 9f * r;
        }
    }
}
