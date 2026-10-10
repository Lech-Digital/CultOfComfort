using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Code-driven animation for Blender-pipeline characters made of named rigid parts (Body, Head, ArmL/R, LegL/R).
    /// Views set state (moving, swooning) and fire one-shots (attack, hit); this applies <see cref="PartPose"/> each frame.
    /// Positive X rotation in Unity tips forward, so "backwards" swoon and "forward" swings use the signs below.
    /// </summary>
    public sealed class PartAnimator : MonoBehaviour
    {
        private Transform _body, _head, _armL, _armR, _legL, _legR;
        private Quaternion _bodyRest, _headRest, _armLRest, _armRRest, _legLRest, _legRRest;
        private Vector3 _bodyPosition, _bodyScale;
        private float _phase, _attack, _hit, _swoon, _revive;
        private bool _wasSwooning;

        public bool IsMoving { get; set; }
        public bool IsSwooning { get; set; }

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

        public void Attack() => _attack = 1f;

        public void Hit() => _hit = 1f;

        private void Update() => Tick(Time.deltaTime, Time.time);

        /// <summary>Advances the pose. Public so tools and tests can drive it frame by frame without the player loop.</summary>
        public void Tick(float dt, float time)
        {
            if (_body == null)
            {
                return;
            }

            _phase = IsMoving ? _phase + dt * 12f : Mathf.MoveTowards(_phase, Mathf.Round(_phase / Mathf.PI) * Mathf.PI, dt * 6f);
            _attack = Mathf.MoveTowards(_attack, 0f, dt * 3.5f);
            _hit = Mathf.MoveTowards(_hit, 0f, dt * 5f);
            _swoon = Mathf.MoveTowards(_swoon, IsSwooning ? 1f : 0f, dt * (IsSwooning ? 1.6f : 2.5f));
            if (_wasSwooning && !IsSwooning)
            {
                _revive = 1f; // he gets up and brushes himself off
            }

            _wasSwooning = IsSwooning;
            _revive = _swoon > 0f ? _revive : Mathf.MoveTowards(_revive, 0f, dt * 1.5f);

            var swing = PartPose.ArmSwing(_phase, IsMoving);
            _body.localRotation = _bodyRest * Quaternion.Euler(-PartPose.SwoonTilt(_swoon), PartPose.ReviveShake(_revive), 0f);
            _body.localPosition = _bodyPosition + Vector3.up * PartPose.WalkBob(_phase);
            _body.localScale = Vector3.Scale(_bodyScale, new Vector3(1f, PartPose.HitSquash(_hit), 1f));
            Rotate(_armL, _armLRest, swing);
            Rotate(_armR, _armRRest, -swing + PartPose.AttackSwing(_attack));
            Rotate(_legL, _legLRest, -swing * 0.6f);
            Rotate(_legR, _legRRest, swing * 0.6f);
            if (_head != null)
            {
                var glance = IsMoving || IsSwooning ? 0f : Mathf.Sin(time * 0.7f) * 10f; // idly unimpressed
                _head.localRotation = _headRest * Quaternion.Euler(0f, glance, 0f);
            }
        }

        private Transform Find(string part, out Quaternion rest)
        {
            var found = ArtLibrary.FindPart(transform, part);
            rest = found != null ? found.localRotation : Quaternion.identity;
            return found;
        }

        private static void Rotate(Transform part, Quaternion rest, float forwardDegrees)
        {
            if (part != null)
            {
                part.localRotation = rest * Quaternion.Euler(forwardDegrees, 0f, 0f);
            }
        }
    }
}
