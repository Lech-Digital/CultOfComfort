using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The cult's builder at whatever is under construction (owner 2026-10-14: "a hammer and a piece of wood"): he
    /// swings his mallet on a steady beat, and every blow knocks off a few chips and a fleck of mud. Lives under the
    /// building's visual, so it goes when the building is done.
    /// </summary>
    public sealed class BuilderAtWork : MonoBehaviour
    {
        private const float BeatSeconds = 0.95f;
        private const float BlowAt = 0.34f; // seconds into the swing when the mallet lands (PartPose.Attack's smash)
        private const float ChipSeconds = 0.45f;

        private PartAnimator _animator;
        private Transform _strike;
        private float _clock;
        private bool _struck;
        private readonly Transform[] _chips = new Transform[4];
        private readonly Vector3[] _velocity = new Vector3[4];
        private float _chipAge = ChipSeconds;

        /// <summary>Puts a builder and his pile at <paramref name="parent"/>'s front edge, facing <paramref name="towards"/>.</summary>
        public static GameObject Create(Transform parent, Vector3 standAt, Vector3 pileAt, Vector3 towards)
        {
            var model = ArtLibrary.Instantiate("builder", parent);
            if (model == null)
            {
                return null;
            }

            model.name = "Builder";
            model.transform.localPosition = standAt;
            var look = towards - standAt;
            look.y = 0f;
            model.transform.localRotation = look.sqrMagnitude > 0.001f ? Quaternion.LookRotation(look) : Quaternion.identity;
            var pile = ArtLibrary.Instantiate("build_pile", model.transform.parent);
            if (pile != null)
            {
                pile.name = "Build pile";
                pile.transform.localPosition = pileAt;
                pile.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
                pile.transform.SetParent(model.transform, true); // goes with him
            }

            var animator = model.AddComponent<PartAnimator>();
            animator.Bind();
            var builder = model.AddComponent<BuilderAtWork>();
            builder._animator = animator;
            builder._strike = new GameObject("Strike").transform;
            builder._strike.SetParent(model.transform, false);
            builder._strike.localPosition = new Vector3(0f, 0.05f, 0.35f); // just in front of him, on the ground
            builder._clock = Random.value * BeatSeconds; // two sites never hammer in step
            return model;
        }

        private void Update()
        {
            _clock += Time.deltaTime;
            if (_clock >= BeatSeconds)
            {
                _clock -= BeatSeconds;
                _struck = false;
                _animator.Attack();
            }

            if (!_struck && _clock >= BlowAt)
            {
                _struck = true;
                Chips();
            }

            if (_chipAge < ChipSeconds)
            {
                _chipAge += Time.deltaTime;
                var gravity = new Vector3(0f, -6f, 0f) * Time.deltaTime;
                for (var i = 0; i < _chips.Length; i++)
                {
                    _velocity[i] += gravity;
                    _chips[i].position += _velocity[i] * Time.deltaTime;
                    _chips[i].Rotate(500f * Time.deltaTime, 300f * Time.deltaTime, 0f);
                    _chips[i].gameObject.SetActive(_chipAge < ChipSeconds && _chips[i].position.y > 0f);
                }
            }
        }

        /// <summary>A few wood chips and a fleck of mud fly off the blow.</summary>
        private void Chips()
        {
            for (var i = 0; i < _chips.Length; i++)
            {
                if (_chips[i] == null)
                {
                    var colour = i == 0 ? Palette.Mud : Palette.Wood;
                    _chips[i] = Shapes.Create(PrimitiveType.Cube, colour, Vector3.zero, Vector3.one * (i == 0 ? 0.06f : 0.045f), transform, "Chip").transform;
                }

                _chips[i].position = _strike.position;
                _chips[i].gameObject.SetActive(true);
                var side = Random.insideUnitCircle * 1.1f;
                _velocity[i] = new Vector3(side.x, 1.6f + Random.value * 0.8f, side.y);
            }

            _chipAge = 0f;
        }
    }
}
