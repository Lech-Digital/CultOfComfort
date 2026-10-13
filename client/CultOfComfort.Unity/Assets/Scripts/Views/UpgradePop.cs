using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The moment an upgrade finishes: the new building springs up out of the ground (squash, stretch, settle) and
    /// a puff of dust rolls off it. Removes itself when done.
    /// </summary>
    public sealed class UpgradePop : MonoBehaviour
    {
        public const float Duration = 0.55f;

        private float _age;
        private Vector3 _rest;

        /// <summary>Scale (horizontal, vertical) at <paramref name="t"/> ∈ [0, 1]: squat, overshoot tall, wobble, rest.</summary>
        public static Vector2 Shape(float t)
        {
            t = Mathf.Clamp01(t);
            var settle = 1f - t;
            var squash = Mathf.Cos(t * Mathf.PI * 2.5f) * settle * settle; // 1 at the start, swings, dies away
            return new Vector2(1f + squash * 0.12f, 1f - squash * 0.4f);
        }

        private void Awake()
        {
            _rest = transform.localScale;
            var poof = ArtLibrary.Instantiate("fx_poof", transform.parent);
            if (poof != null)
            {
                poof.transform.localPosition = transform.localPosition;
                poof.transform.localScale = Vector3.one * 1.6f;
                Destroy(poof, Duration);
            }

            Apply(0f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            Apply(_age / Duration);
            if (_age >= Duration)
            {
                transform.localScale = _rest;
                Destroy(this);
            }
        }

        private void Apply(float t)
        {
            var shape = Shape(t);
            transform.localScale = new Vector3(_rest.x * shape.x, _rest.y * shape.y, _rest.z * shape.x);
        }
    }
}
