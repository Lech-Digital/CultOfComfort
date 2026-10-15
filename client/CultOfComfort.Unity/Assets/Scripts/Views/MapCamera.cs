using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The world map's camera: looks north over the land at a fixed tilt from a variable distance. A drag keeps the
    /// ground under the finger glued to it and a flick glides on and slows down; a pinch zooms about the point between
    /// the fingers; <see cref="FlyTo"/> eases over to a place. The focus stays inside the world's disc.
    /// </summary>
    public sealed class MapCamera
    {
        public const float MinDistance = 9f;
        public const float MaxDistance = 40f;
        public const float HomeDistance = 19f;
        private const float Pitch = 54f;
        private const float GroundHeight = 0.35f; // the ground's typical height: drags glue to it

        private readonly Camera _camera;
        private readonly Vector3 _origin;
        private readonly Vector3 _direction;
        private float _bound;
        private Vector2 _focus;
        private float _distance = HomeDistance;
        private Vector2 _velocity;
        private Vector2? _flyTo;
        private float? _flyDistance;
        private bool _held;

        /// <param name="origin">Where the land's (0, 0) is in the scene.</param>
        public MapCamera(Camera camera, Vector3 origin)
        {
            _camera = camera;
            _origin = origin;
            _direction = new Vector3(0f, Mathf.Sin(Pitch * Mathf.Deg2Rad), -Mathf.Cos(Pitch * Mathf.Deg2Rad));
            Apply();
        }

        /// <summary>The land point (x, z) in the middle of the view.</summary>
        public Vector2 Focus => _focus;

        public float Distance => _distance;

        /// <summary>The focus may roam up to this far from the land's centre.</summary>
        public void SetBound(float radius)
        {
            _bound = radius;
            Clamp();
            Apply();
        }

        public void JumpTo(Vector2 focus, float distance)
        {
            _focus = focus;
            _distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
            _velocity = Vector2.zero;
            _flyTo = null;
            _flyDistance = null;
            Clamp();
            Apply();
        }

        /// <summary>Eases over to a land point (and, optionally, a distance) over about half a second.</summary>
        public void FlyTo(Vector2 focus, float? distance = null)
        {
            _flyTo = focus;
            _flyDistance = distance;
            _velocity = Vector2.zero;
        }

        public bool TryScreenToLand(Vector2 screen, out Vector2 land)
        {
            var ray = _camera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, _origin + Vector3.up * GroundHeight);
            land = default;
            if (!plane.Raycast(ray, out var hit))
            {
                return false;
            }

            var point = ray.GetPoint(hit) - _origin;
            land = new Vector2(point.x, point.z);
            return true;
        }

        public Vector3 ToScene(Vector2 land, float height) => _origin + new Vector3(land.x, height, land.y);

        /// <summary>One frame of a drag: the ground under <paramref name="from"/> moves to <paramref name="to"/>.</summary>
        public void Drag(Vector2 from, Vector2 to, float dt)
        {
            _flyTo = null;
            _held = true;
            if (TryScreenToLand(from, out var a) && TryScreenToLand(to, out var b))
            {
                var delta = a - b;
                _focus += delta;
                var speed = delta / Mathf.Max(dt, 0.008f);
                _velocity = Vector2.Lerp(_velocity, speed, 0.5f);
                Clamp();
                Apply();
            }
        }

        /// <summary>The finger lifted: a flick glides on.</summary>
        public void Release()
        {
            _held = false;
            if (_velocity.magnitude < 1.5f)
            {
                _velocity = Vector2.zero;
            }
        }

        /// <param name="factor">&gt; 1 zooms in (fingers spreading), &lt; 1 zooms out.</param>
        public void Zoom(float factor, Vector2 anchor)
        {
            if (factor <= 0f)
            {
                return;
            }

            _flyTo = null;
            _flyDistance = null;
            var hasAnchor = TryScreenToLand(anchor, out var before);
            _distance = Mathf.Clamp(_distance / factor, MinDistance, MaxDistance);
            Apply();
            if (hasAnchor && TryScreenToLand(anchor, out var after))
            {
                _focus += before - after;
            }

            _velocity = Vector2.zero;
            Clamp();
            Apply();
        }

        public void Tick(float dt)
        {
            if (_flyTo != null)
            {
                var k = 1f - Mathf.Exp(-dt * 7f);
                _focus = Vector2.Lerp(_focus, _flyTo.Value, k);
                if (_flyDistance != null)
                {
                    _distance = Mathf.Lerp(_distance, _flyDistance.Value, k);
                }

                if (Vector2.Distance(_focus, _flyTo.Value) < 0.02f && (_flyDistance == null || Mathf.Abs(_distance - _flyDistance.Value) < 0.05f))
                {
                    _flyTo = null;
                    _flyDistance = null;
                }
            }
            else if (!_held && _velocity.sqrMagnitude > 0.0001f)
            {
                _focus += _velocity * dt;
                _velocity *= Mathf.Exp(-dt * 4.5f);
                if (_velocity.magnitude < 0.05f)
                {
                    _velocity = Vector2.zero;
                }
            }
            else
            {
                return;
            }

            Clamp();
            Apply();
        }

        private void Clamp()
        {
            if (_bound > 0f && _focus.magnitude > _bound)
            {
                _focus = _focus.normalized * _bound;
                _velocity = Vector2.zero;
            }
        }

        private void Apply()
        {
            var target = ToScene(_focus, 0f);
            _camera.transform.position = target + _direction * _distance;
            _camera.transform.rotation = Quaternion.LookRotation(-_direction, Vector3.up);
        }
    }
}
