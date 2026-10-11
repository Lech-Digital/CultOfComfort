using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Offsets from a character's rest pose. Angles in degrees, conventions for a model facing +Z:
    /// BodyTilt/HeadTilt &gt; 0 lean forward; *Yaw &gt; 0 turn to his right; *Roll &gt; 0 lean to his left;
    /// Arm/LegForward &gt; 0 swing the limb forward (up in front of him at 90–180); ArmOut &gt; 0 lifts it out sideways.
    /// </summary>
    public struct Pose
    {
        public float BodyTilt, BodyRoll, BodyYaw, Lift;
        public float HeadTilt, HeadYaw, HeadRoll;
        public float ArmLForward, ArmLOut, ArmRForward, ArmROut;
        public float LegLForward, LegRForward;

        /// <summary>Vertical scale of the body (1 = normal). Stored as an offset so default(Pose) means "no squash".</summary>
        public float Squash { get => 1f + _squashOffset; set => _squashOffset = value - 1f; }

        private float _squashOffset;

        public static Pose Rest => default;

        public static Pose Add(Pose a, Pose b) => new Pose
        {
            BodyTilt = a.BodyTilt + b.BodyTilt, BodyRoll = a.BodyRoll + b.BodyRoll, BodyYaw = a.BodyYaw + b.BodyYaw, Lift = a.Lift + b.Lift,
            HeadTilt = a.HeadTilt + b.HeadTilt, HeadYaw = a.HeadYaw + b.HeadYaw, HeadRoll = a.HeadRoll + b.HeadRoll,
            ArmLForward = a.ArmLForward + b.ArmLForward, ArmLOut = a.ArmLOut + b.ArmLOut,
            ArmRForward = a.ArmRForward + b.ArmRForward, ArmROut = a.ArmROut + b.ArmROut,
            LegLForward = a.LegLForward + b.LegLForward, LegRForward = a.LegRForward + b.LegRForward,
            Squash = a.Squash * b.Squash,
        };

        public static Pose Scale(Pose p, float k) => new Pose
        {
            BodyTilt = p.BodyTilt * k, BodyRoll = p.BodyRoll * k, BodyYaw = p.BodyYaw * k, Lift = p.Lift * k,
            HeadTilt = p.HeadTilt * k, HeadYaw = p.HeadYaw * k, HeadRoll = p.HeadRoll * k,
            ArmLForward = p.ArmLForward * k, ArmLOut = p.ArmLOut * k, ArmRForward = p.ArmRForward * k, ArmROut = p.ArmROut * k,
            LegLForward = p.LegLForward * k, LegRForward = p.LegRForward * k,
            Squash = 1f + (p.Squash - 1f) * k,
        };

        public static Pose Lerp(Pose a, Pose b, float t) => Add(Scale(a, 1f - t), Scale(b, t));
    }

    /// <summary>
    /// Comic, deliberately over-acted animation curves for rigid-part characters (owner: "dramatic, ridiculous").
    /// Pure functions of progress/time, so every beat is unit-tested. One-shots take progress 0→1 and start and end
    /// at rest so they can be layered on top of walking or idling.
    /// </summary>
    public static class PartPose
    {
        /// <summary>Degrees he tips backwards when he faints (not quite 90: he lands on his belt bag).</summary>
        public const float SwoonAngle = 88f;

        public const float BoredCycleSeconds = 6f;

        // ---------------- continuous ----------------

        /// <summary>A waddle: big bob, side-to-side roll, exaggerated arm and leg swing.</summary>
        public static Pose Walk(float phase)
        {
            var s = Mathf.Sin(phase);
            return new Pose
            {
                Lift = Mathf.Abs(s) * 0.08f,
                BodyRoll = s * 7f,
                BodyTilt = 6f,
                HeadRoll = -s * 4f,
                ArmLForward = -s * 50f,
                ArmRForward = s * 50f,
                ArmLOut = 8f,
                ArmROut = 8f,
                LegLForward = s * 35f,
                LegRForward = -s * 35f,
            };
        }

        /// <summary>
        /// Looping boredom, <paramref name="seconds"/> since he got bored: a huge sigh, checking the (dead) smartwatch,
        /// then tapping his foot and lolling his head.
        /// </summary>
        public static Pose Bored(float seconds)
        {
            var t = Mathf.Repeat(seconds, BoredCycleSeconds) / BoredCycleSeconds;
            var pose = new Pose { HeadTilt = 8f };

            // 0–0.25: the sigh. Inhale (stretch), then deflate (squash, slump, arms droop).
            var sigh = Window(t, 0f, 0.25f);
            var inhale = Mathf.Sin(Mathf.Clamp01(t / 0.25f) * Mathf.PI);
            pose.Squash = 1f + 0.06f * Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(t / 0.1f) * Mathf.PI)) - 0.16f * Smooth(Mathf.Clamp01((t - 0.06f) / 0.1f)) * sigh;
            pose.BodyTilt = 12f * inhale * sigh;
            pose.HeadTilt += 18f * sigh;
            pose.ArmLOut -= 6f * sigh;
            pose.ArmROut -= 6f * sigh;

            // 0.28–0.58: lifts the wrist to his face, stares at the smartwatch, head cocked.
            var watch = Window(t, 0.28f, 0.58f);
            pose.ArmLForward += 95f * watch;
            pose.ArmLOut -= 20f * watch;
            pose.HeadYaw -= 28f * watch;
            pose.HeadTilt += 22f * watch;

            // 0.62–1: taps his right foot, head lolls side to side.
            var tap = Window(t, 0.62f, 1f);
            pose.LegRForward += Mathf.Max(0f, Mathf.Sin(seconds * Mathf.PI * 2f * 3f)) * 16f * tap;
            pose.HeadRoll += Mathf.Sin(seconds * 1.6f) * 14f * tap;
            pose.BodyRoll += Mathf.Sin(seconds * 1.6f) * 4f * tap;
            return pose;
        }

        // ---------------- one-shots (progress 0→1) ----------------

        /// <summary>Umbrella raised high over the head, a beat of hang-time, then a full-body smash forward.</summary>
        public static Pose Attack(float progress)
        {
            var p = Mathf.Clamp01(progress);
            var raise = Smooth(Mathf.Clamp01(p / 0.35f));
            var smash = Smooth(Mathf.Clamp01((p - 0.4f) / 0.12f));
            var recover = Smooth(Mathf.Clamp01((p - 0.6f) / 0.4f));
            var arm = Mathf.Lerp(Mathf.Lerp(0f, 170f, raise), 45f, smash);
            return new Pose
            {
                ArmRForward = arm * (1f - recover),
                ArmROut = 10f * raise * (1f - recover),
                ArmLOut = 35f * raise * (1f - recover),
                BodyTilt = (Mathf.Lerp(-12f * raise, 20f, smash)) * (1f - recover),
                Lift = 0.06f * Mathf.Sin(Mathf.Clamp01((p - 0.35f) / 0.2f) * Mathf.PI),
                HeadTilt = -10f * raise * (1f - smash) * (1f - recover),
                Squash = 1f - 0.1f * smash * (1f - recover),
            };
        }

        /// <summary>Knocked back: leans away, squashes, arms flail, head snaps.</summary>
        public static Pose Hit(float progress)
        {
            var k = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            return new Pose
            {
                BodyTilt = -25f * k,
                HeadTilt = -25f * k,
                ArmLOut = 55f * k,
                ArmROut = 55f * k,
                ArmLForward = 30f * k,
                ArmRForward = 30f * k,
                Squash = 1f - 0.2f * k,
            };
        }

        /// <summary>A tantrum: stomping hops, one hand on the hip, the umbrella shaken above his head, chin jutting.</summary>
        public static Pose Annoyed(float progress)
        {
            var p = Mathf.Clamp01(progress);
            var env = Envelope(p, 0.12f);
            var stomp = Mathf.Sin(p * Mathf.PI * 6f);
            return new Pose
            {
                Lift = Mathf.Abs(stomp) * 0.06f * env,
                LegLForward = Mathf.Max(0f, stomp) * 30f * env,
                LegRForward = Mathf.Max(0f, -stomp) * 30f * env,
                ArmRForward = (172f + Mathf.Sin(p * 60f) * 12f) * env, // chibi arms are short: straight up, or it never clears his head
                ArmROut = 15f * env,
                ArmLOut = 45f * env,
                ArmLForward = -20f * env,
                HeadTilt = 15f * env,
                HeadRoll = Mathf.Sin(p * 45f) * 8f * env,
                BodyRoll = stomp * 5f * env,
                BodyTilt = 8f * env,
            };
        }

        /// <summary>Arms crossed, leaning back, a slow disappointed head shake.</summary>
        public static Pose Unimpressed(float progress)
        {
            var p = Mathf.Clamp01(progress);
            var env = Envelope(p, 0.15f);
            return new Pose
            {
                ArmLForward = 75f * env,
                ArmRForward = 75f * env,
                ArmLOut = -30f * env,
                ArmROut = -30f * env,
                BodyTilt = -8f * env,
                HeadTilt = -10f * env,
                HeadYaw = Mathf.Sin(p * Mathf.PI * 4f) * 28f * env,
                Squash = 1f - 0.05f * env,
            };
        }

        /// <summary>
        /// The faint, as theatre: staggers side to side with the back of a hand to his forehead, falls backwards
        /// like a plank with arms flailing, bounces twice off the mud with legs kicking up, ends spread-eagled.
        /// </summary>
        public static Pose Swoon(float progress)
        {
            var p = Mathf.Clamp01(progress);
            var stagger = Mathf.Clamp01(p / 0.3f);
            var fall = Mathf.Clamp01((p - 0.3f) / 0.35f);
            var land = Mathf.Clamp01((p - 0.65f) / 0.35f);
            var standing = 1f - Smooth(fall);

            var tilt = -SwoonAngle * fall * fall;
            if (p > 0.65f)
            {
                tilt = -SwoonAngle + Mathf.Abs(Mathf.Sin(land * Mathf.PI * 2f)) * Mathf.Lerp(14f, 0f, land);
            }

            var kick = p > 0.65f ? Mathf.Sin(land * Mathf.PI) * 55f + 10f * land : 30f * fall;
            return new Pose
            {
                BodyRoll = Mathf.Sin(stagger * Mathf.PI * 3f) * 16f * stagger * standing,
                BodyTilt = tilt,
                HeadTilt = -22f * stagger,
                HeadRoll = 15f * Smooth(land),
                ArmLForward = 150f * Smooth(stagger) * standing,
                ArmLOut = 20f * Smooth(stagger) * standing + 75f * Smooth(fall),
                ArmROut = 50f * Smooth(stagger) + 25f * Smooth(fall),
                ArmRForward = 25f * Mathf.Sin(fall * Mathf.PI),
                LegLForward = kick,
                LegRForward = kick * 0.8f,
            };
        }

        /// <summary>Springs up from the faint past upright, then brushes himself off with sharp little shakes.</summary>
        public static Pose Revive(float progress)
        {
            var p = Mathf.Clamp01(progress);
            var up = Mathf.Clamp01(p / 0.3f);
            var pose = Pose.Lerp(Swoon(1f), Pose.Rest, Smooth(up));
            pose.BodyTilt = up < 1f ? Mathf.Lerp(-SwoonAngle, 12f, Smooth(up)) : 12f * (1f - Smooth(Mathf.Clamp01((p - 0.3f) / 0.15f)));
            pose.Squash = 1f + 0.1f * Mathf.Sin(up * Mathf.PI);

            var brush = Mathf.Clamp01((p - 0.35f) / 0.65f);
            var shake = Mathf.Sin(brush * Mathf.PI * 8f) * (1f - brush);
            pose.BodyYaw += shake * 15f;
            pose.ArmLForward += Mathf.Abs(shake) * 45f;
            pose.ArmRForward += Mathf.Abs(Mathf.Sin(brush * Mathf.PI * 8f + 1f)) * 40f * (1f - brush);
            return pose;
        }

        // ---------------- helpers ----------------

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>1 inside [from, to] with soft 15% edges, 0 outside.</summary>
        private static float Window(float t, float from, float to)
        {
            var edge = (to - from) * 0.15f;
            return Smooth(Mathf.Clamp01((t - from) / edge)) * Smooth(Mathf.Clamp01((to - t) / edge));
        }

        /// <summary>Ramps in and out over <paramref name="edge"/> of the progress so one-shots start and end at rest.</summary>
        private static float Envelope(float p, float edge) => Smooth(Mathf.Clamp01(p / edge)) * Smooth(Mathf.Clamp01((1f - p) / edge));
    }
}
