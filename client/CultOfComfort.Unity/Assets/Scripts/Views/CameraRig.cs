using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Angled board camera: looks at a focus point on the ground from a fixed direction at a variable distance.
    /// Pan keeps the ground point under the finger glued to it; zoom keeps the point under the pinch centre fixed.
    /// </summary>
    public sealed class CameraRig
    {
        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        private readonly Camera _camera;
        private readonly Vector3 _direction;
        private readonly Vector3 _homeFocus;
        private readonly float _homeDistance;
        private readonly Vector2 _minFocus;
        private readonly Vector2 _maxFocus;
        private readonly float _minDistance;
        private readonly float _maxDistance;
        private Vector3 _focus;
        private float _distance;
        private Vector3 _shake;

        /// <param name="boardMin">Lowest (x, z) the focus may reach.</param>
        /// <param name="boardMax">Highest (x, z) the focus may reach.</param>
        public CameraRig(Camera camera, Vector3 focus, Vector3 offset, Vector2 boardMin, Vector2 boardMax)
        {
            _camera = camera;
            _direction = offset.normalized;
            _homeFocus = _focus = focus;
            _homeDistance = _distance = offset.magnitude;
            _minDistance = _homeDistance * 0.4f;
            _maxDistance = _homeDistance * 1.15f;
            _minFocus = boardMin;
            _maxFocus = boardMax;
            Apply();
        }

        public bool TryScreenToGround(Vector2 screen, out Vector3 point)
        {
            var ray = _camera.ScreenPointToRay(screen);
            point = default;
            if (!Ground.Raycast(ray, out var distance))
            {
                return false;
            }

            point = ray.GetPoint(distance);
            return true;
        }

        public void Pan(Vector2 fromScreen, Vector2 toScreen)
        {
            if (TryScreenToGround(fromScreen, out var from) && TryScreenToGround(toScreen, out var to))
            {
                _focus += from - to;
                ClampAndApply();
            }
        }

        /// <param name="factor">&gt; 1 zooms in (fingers spreading), &lt; 1 zooms out.</param>
        public void Zoom(float factor, Vector2 anchorScreen)
        {
            if (factor <= 0f)
            {
                return;
            }

            var hasAnchor = TryScreenToGround(anchorScreen, out var before);
            _distance = Mathf.Clamp(_distance / factor, _minDistance, _maxDistance);
            Apply();
            if (hasAnchor && TryScreenToGround(anchorScreen, out var after))
            {
                _focus += before - after;
            }

            ClampAndApply();
        }

        public void Reset()
        {
            _focus = _homeFocus;
            _distance = _homeDistance;
            Apply();
        }

        public void SetShake(Vector3 offset)
        {
            _shake = offset;
            Apply();
        }

        private void ClampAndApply()
        {
            _focus = new Vector3(
                Mathf.Clamp(_focus.x, _minFocus.x, _maxFocus.x),
                0f,
                Mathf.Clamp(_focus.z, _minFocus.y, _maxFocus.y));
            Apply();
        }

        private void Apply()
        {
            _camera.transform.position = _focus + _direction * _distance + _shake;
            _camera.transform.rotation = Quaternion.LookRotation(-_direction, Vector3.up);
        }
    }
}
