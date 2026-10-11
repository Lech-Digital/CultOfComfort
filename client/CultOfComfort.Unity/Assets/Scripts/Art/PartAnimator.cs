using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Code-driven, over-acted animation for Blender-pipeline characters made of named rigid parts
    /// (Body, Head, ArmL/R, LegL/R). Views set state (moving, swooning) and fire one-shots (attack, hit, annoyed,
    /// unimpressed); left alone for a while he gets visibly bored. Poses come from <see cref="PartPose"/>.
    /// </summary>
    public sealed class PartAnimator : MonoBehaviour
    {
        private const float BoredAfterSeconds = 4f;
        private const float SwoonSeconds = 1.7f;
        private const float ReviveSeconds = 1.3f;

        private Transform _body, _head, _armL, _armR, _legL, _legR;
        private Quaternion _bodyRest, _headRest, _armLRest, _armRRest, _legLRest, _legRRest;
        private Vector3 _bodyPosition, _bodyScale;
        private float _phase, _walkWeight, _idleSeconds, _boredWeight;
        private float _swoon, _revive = 1f;
        private bool _wasSwooning;
        private readonly OneShot _attack = new OneShot(0.75f);
        private readonly OneShot _hit = new OneShot(0.45f);
        private readonly OneShot _annoyed = new OneShot(1.6f);
        private readonly OneShot _unimpressed = new OneShot(2f);

        public bool IsMoving { get; set; }
        public bool IsSwooning { get; set; }

        /// <summary>True while he is visibly bored (for tests and the showcase).</summary>
        public bool IsBored => _boredWeight > 0.5f;

        /// <summary>True while any one-shot (attack, hit, annoyed, unimpressed) is playing.</summary>
        public bool IsEmoting => _attack.Active || _hit.Active || _annoyed.Active || _unimpressed.Active;

        public void Bind()
        {
            _body = Find("Body", out _bodyRest);
            _head = Find("Head", out _headRest);
            _armL = Find("ArmL", out _armLRest);
            _armR = Find("ArmR", out _armRRest);
            _legL = Find("LegL", out _legLRest);
            _legR = Find("LegR", out _legRRest);
            _bodyPosition = _body != null ? _body.localPosition : Vector3.zero;
            _bodyScale = _body != null ? _body.localScale : Vector3.one;
        }

        public void Attack() => _attack.Fire();

        public void Hit() => _hit.Fire();

        /// <summary>Ignored while he is fainted or getting up: a tantrum must not queue up for after the revive.</summary>
        public void Annoyed()
        {
            if (!IsDown)
            {
                _annoyed.Fire();
            }
        }

        public void Unimpressed()
        {
            if (!IsDown)
            {
                _unimpressed.Fire();
            }
        }

        private bool IsDown => IsSwooning || _revive < 1f;

        private void Update() => Tick(Time.deltaTime, Time.time);

        /// <summary>Advances the pose. Public so tools and tests can drive it frame by frame without the player loop.</summary>
        public void Tick(float dt, float time)
        {
            if (_body == null)
            {
                return;
            }

            var busy = _attack.Active || _hit.Active || _annoyed.Active || _unimpressed.Active;
            _idleSeconds = IsMoving || IsSwooning || busy ? 0f : _idleSeconds + dt;
            _boredWeight = Mathf.MoveTowards(_boredWeight, _idleSeconds > BoredAfterSeconds ? 1f : 0f, dt * 2f);
            _walkWeight = Mathf.MoveTowards(_walkWeight, IsMoving ? 1f : 0f, dt * 6f);
            _phase += dt * 11f * _walkWeight;

            if (IsSwooning && !_wasSwooning)
            {
                _swoon = 0f;
            }

            if (!IsSwooning && _wasSwooning)
            {
                _revive = 0f; // springs up and brushes himself off
            }

            _wasSwooning = IsSwooning;
            _swoon = IsSwooning ? Mathf.Min(1f, _swoon + dt / SwoonSeconds) : _swoon;
            _revive = Mathf.Min(1f, _revive + dt / ReviveSeconds);

            Pose pose;
            if (IsSwooning)
            {
                pose = PartPose.Swoon(_swoon);
            }
            else if (_revive < 1f)
            {
                pose = PartPose.Revive(_revive);
            }
            else
            {
                pose = Pose.Add(Pose.Scale(PartPose.Walk(_phase), _walkWeight),
                    Pose.Scale(PartPose.Bored(_idleSeconds - BoredAfterSeconds), _boredWeight));
                pose = Pose.Add(pose, _attack.Sample(dt, PartPose.Attack));
                pose = Pose.Add(pose, _annoyed.Sample(dt, PartPose.Annoyed));
                pose = Pose.Add(pose, _unimpressed.Sample(dt, PartPose.Unimpressed));
            }

            pose = Pose.Add(pose, _hit.Sample(dt, PartPose.Hit));
            Apply(pose);
        }

        private void Apply(Pose pose)
        {
            _body.localRotation = _bodyRest * Quaternion.Euler(pose.BodyTilt, pose.BodyYaw, pose.BodyRoll);
            _body.localPosition = _bodyPosition + Vector3.up * pose.Lift;
            var bulge = 1f + (1f - pose.Squash) * 0.5f; // squash and stretch keeps the volume, cartoon-style
            _body.localScale = Vector3.Scale(_bodyScale, new Vector3(bulge, pose.Squash, bulge));
            if (_head != null)
            {
                _head.localRotation = _headRest * Quaternion.Euler(pose.HeadTilt, pose.HeadYaw, pose.HeadRoll);
            }

            // Limbs hang down (−Y), so a forward swing is a negative X rotation. "Out" is away from the body:
            // the left arm (−X side) goes out with a negative Z rotation, the right arm with a positive one.
            Limb(_armL, _armLRest, pose.ArmLForward, -pose.ArmLOut);
            Limb(_armR, _armRRest, pose.ArmRForward, pose.ArmROut);
            Limb(_legL, _legLRest, pose.LegLForward, 0f);
            Limb(_legR, _legRRest, pose.LegRForward, 0f);
        }

        private Transform Find(string part, out Quaternion rest)
        {
            var found = ArtLibrary.FindPart(transform, part);
            rest = found != null ? found.localRotation : Quaternion.identity;
            return found;
        }

        private static void Limb(Transform part, Quaternion rest, float forward, float outward)
        {
            if (part != null)
            {
                part.localRotation = rest * Quaternion.Euler(-forward, 0f, outward);
            }
        }

        /// <summary>A timed one-shot animation: progress runs 0→1 over its duration after <see cref="Fire"/>.</summary>
        private sealed class OneShot
        {
            private readonly float _seconds;
            private float _progress = 1f;

            public OneShot(float seconds) => _seconds = seconds;

            public bool Active => _progress < 1f;

            /// <summary>Starts the one-shot; a repeat trigger while it is playing is ignored rather than snapping it back to rest.</summary>
            public void Fire()
            {
                if (!Active)
                {
                    _progress = 0f;
                }
            }

            public Pose Sample(float dt, System.Func<float, Pose> curve)
            {
                if (!Active)
                {
                    return Pose.Rest;
                }

                _progress = Mathf.Min(1f, _progress + dt / _seconds);
                return curve(_progress);
            }
        }
    }
}
