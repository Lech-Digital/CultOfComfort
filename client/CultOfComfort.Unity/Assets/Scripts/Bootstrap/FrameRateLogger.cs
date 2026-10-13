using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Development builds only: logs average and worst frame rate every 5 s, and every hitch (a frame over 50 ms) with what
    /// happened just before it (read with adb logcat).
    /// </summary>
    public sealed class FrameRateLogger : MonoBehaviour
    {
        private const float Window = 5f;
        private const float HitchSeconds = 0.05f;
        private static string _lastMark = "start";
        private static float _lastMarkAt;
        private float _elapsed;
        private int _frames;
        private float _worst = float.MaxValue;

        /// <summary>Labels the moment, so a hitch right after it says what caused it. Cheap enough to call on every event.</summary>
        public static void Mark(string what)
        {
            _lastMark = what;
            _lastMarkAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _frames++;
            _worst = Mathf.Min(_worst, 1f / Mathf.Max(dt, 0.0001f));
            if (dt > HitchSeconds && Time.frameCount > 10)
            {
                Debug.Log($"[CoC] hitch {dt * 1000f:0} ms, {Time.realtimeSinceStartup - _lastMarkAt:0.00} s after '{_lastMark}'");
            }

            if (_elapsed < Window)
            {
                return;
            }

            Debug.Log($"[CoC] fps {_frames / _elapsed:0.0} (min {_worst:0}) over {Window:0}s");
            _elapsed = 0f;
            _frames = 0;
            _worst = float.MaxValue;
        }
    }
}
