using UnityEngine;
using UnityEngine.Rendering;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Night falls on the valley: the sun dims to a cool moonlight, the golden haze turns to dusk blue and the ambient light drops,
    /// so the Hearth's glow carries the scene. <see cref="Apply"/> takes 0 (day, exactly as captured) to 1 (full night).
    /// </summary>
    public sealed class NightMood
    {
        private static readonly Color Moonlight = Shapes.Hex("#8fa6d6");
        private static readonly Color NightHaze = Shapes.Hex("#26304a");
        private const float NightSunIntensity = 0.75f; // bright enough to read the board on a phone (owner 2026-10-15: "does not look great")
        private const float NightAmbient = 0.62f;

        private readonly Camera _camera;
        private readonly Light _sun;
        private readonly Color _dayHaze;
        private readonly Color _daySunColor;
        private readonly float _daySunIntensity;
        private readonly SphericalHarmonicsL2 _dayAmbient;

        private NightMood(Camera camera, Light sun)
        {
            _camera = camera;
            _sun = sun;
            _dayHaze = camera.backgroundColor;
            _dayAmbient = RenderSettings.ambientProbe;
            if (sun != null)
            {
                _daySunColor = sun.color;
                _daySunIntensity = sun.intensity;
            }
        }

        public static NightMood Capture(Camera camera, Light sun) => new NightMood(camera, sun);

        /// <summary>The scene's directional light, or null if it has none.</summary>
        public static Light FindSun()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }

        public void Apply(float darkness)
        {
            var k = Mathf.Clamp01(darkness);
            var haze = Color.Lerp(_dayHaze, NightHaze, k);
            _camera.backgroundColor = haze;
            RenderSettings.fogColor = haze;
            RenderSettings.ambientProbe = _dayAmbient * Mathf.Lerp(1f, NightAmbient, k);
            if (_sun != null)
            {
                _sun.color = Color.Lerp(_daySunColor, Moonlight, k);
                _sun.intensity = Mathf.Lerp(_daySunIntensity, NightSunIntensity, k);
            }
        }
    }
}
