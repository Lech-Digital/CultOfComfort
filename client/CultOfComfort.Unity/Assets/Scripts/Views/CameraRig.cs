using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Angled board camera: looks at a focus point on the ground from a fixed elevation at a variable distance.
    /// Pan keeps the ground point under the finger glued to it; zoom keeps the point under the pinch centre fixed.
    /// <see cref="Frame"/> picks the yaw (board long axis across the screen in landscape, up the screen in portrait)
    /// and the home distance that fits the whole board into the part of the screen the HUD leaves clear.
    /// </summary>
    public sealed class CameraRig
    {
        private const float MinDistance = 7f;
        private const float BoardHeightMargin = 0.8f;
        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        private readonly Camera _camera;
        private readonly Vector3 _offset;
        private readonly Vector2 _minFocus;
        private readonly Vector2 _maxFocus;
        private Vector3 _direction;
        private Vector3 _homeFocus;
        private float _homeDistance;
        private float _maxDistance;
        private Vector3 _focus;
        private float _distance;
        private Vector3 _shake;

        /// <param name="offset">Camera position relative to the focus at yaw 0 (camera south of the board).</param>
        /// <param name="boardMin">Lowest (x, z) of the board; the focus never leaves the board.</param>
        /// <param name="boardMax">Highest (x, z) of the board.</param>
        public CameraRig(Camera camera, Vector3 offset, Vector2 boardMin, Vector2 boardMax)
        {
            _camera = camera;
            _offset = offset;
            _minFocus = boardMin;
            _maxFocus = boardMax;
            _homeFocus = _focus = new Vector3((boardMin.x + boardMax.x) / 2f, 0f, (boardMin.y + boardMax.y) / 2f);
            _homeDistance = _distance = _maxDistance = offset.magnitude;
            _direction = offset.normalized;
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
            _distance = Mathf.Clamp(_distance / factor, Mathf.Min(MinDistance, _homeDistance), _maxDistance);
            Apply();
            if (hasAnchor && TryScreenToGround(anchorScreen, out var after))
            {
                _focus += before - after;
            }

            ClampAndApply();
        }

        /// <summary>
        /// Sets the yaw and the home view so the whole board sits inside <paramref name="clearViewport"/>
        /// (viewport coordinates, 0..1, bottom-left origin), then moves the camera there.
        /// </summary>
        /// <param name="yawDegrees">0 = board long axis left-to-right; -90 = long axis top-to-bottom (portrait).</param>
        public void Frame(float yawDegrees, Rect clearViewport)
        {
            _direction = (Quaternion.Euler(0f, yawDegrees, 0f) * _offset).normalized;
            _focus = new Vector3((_minFocus.x + _maxFocus.x) / 2f, 0f, (_minFocus.y + _maxFocus.y) / 2f);

            // Perspective makes size and position interact, so alternate "fit the size" and "centre it" a few times.
            for (var i = 0; i < 4; i++)
            {
                float near = 1f, far = 300f;
                for (var step = 0; step < 32; step++)
                {
                    _distance = (near + far) / 2f;
                    Apply();
                    var bounds = ProjectedBoardBounds();
                    if (bounds.width <= clearViewport.width && bounds.height <= clearViewport.height)
                    {
                        far = _distance;
                    }
                    else
                    {
                        near = _distance;
                    }
                }

                _distance = far;
                Apply();
                var centre = ProjectedBoardBounds().center;
                if (TryScreenToGround(_camera.ViewportToScreenPoint(centre), out var from)
                    && TryScreenToGround(_camera.ViewportToScreenPoint(clearViewport.center), out var to))
                {
                    _focus += from - to;
                    Apply();
                }
            }

            _homeFocus = _focus;
            _homeDistance = _distance;
            _maxDistance = _distance * 1.15f;
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

        /// <summary>Viewport-space bounding box of the board, including some height for towers on the far edge.</summary>
        private Rect ProjectedBoardBounds()
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (var corner = 0; corner < 8; corner++)
            {
                var world = new Vector3(
                    (corner & 1) == 0 ? _minFocus.x - 0.5f : _maxFocus.x + 0.5f,
                    (corner & 4) == 0 ? 0f : BoardHeightMargin,
                    (corner & 2) == 0 ? _minFocus.y - 0.5f : _maxFocus.y + 0.5f);
                var viewport = _camera.WorldToViewportPoint(world);
                xMin = Mathf.Min(xMin, viewport.x);
                xMax = Mathf.Max(xMax, viewport.x);
                yMin = Mathf.Min(yMin, viewport.y);
                yMax = Mathf.Max(yMax, viewport.y);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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
