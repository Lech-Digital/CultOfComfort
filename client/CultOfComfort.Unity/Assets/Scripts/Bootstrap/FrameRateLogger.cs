using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>Development builds only: logs average and worst frame rate every 5 s (read with adb logcat).</summary>
    public sealed class FrameRateLogger : MonoBehaviour
    {
        private const float Window = 5f;
        private float _elapsed;
        private int _frames;
        private float _worst = float.MaxValue;

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _frames++;
            _worst = Mathf.Min(_worst, 1f / Mathf.Max(dt, 0.0001f));
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
