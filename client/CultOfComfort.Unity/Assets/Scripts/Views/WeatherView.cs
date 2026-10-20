using System.Collections.Generic;
using CultOfComfort.Simulation.Base;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The season in the air (better models and weather, 2026-10-20): snow in winter, falling leaves in autumn, rain on a
    /// spring Night, fireflies on a summer Night. A small pool of flakes kept round wherever the camera looks, so it costs
    /// next to nothing on the phone.
    /// </summary>
    public sealed class WeatherView : MonoBehaviour
    {
        private const int Count = 70;
        private const float Spread = 9f;
        private const float Height = 6f;

        private readonly List<Transform> _flakes = new List<Transform>();
        private readonly List<Vector3> _drift = new List<Vector3>();
        private Season _season;
        private bool _night;
        private bool _built;
        private string _kind;

        public void Set(Season season, bool night)
        {
            _season = season;
            _night = night;
            var kind = season switch
            {
                Season.Winter => "snow",
                Season.Autumn => "leaves",
                Season.Spring when night => "rain",
                Season.Summer when night => "fireflies",
                _ => null,
            };
            if (kind == _kind && _built)
            {
                return;
            }

            _kind = kind;
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (var flake in _flakes)
            {
                if (flake != null)
                {
                    Destroy(flake.gameObject);
                }
            }

            _flakes.Clear();
            _drift.Clear();
            _built = true;
            if (_kind == null)
            {
                return;
            }

            for (var i = 0; i < Count; i++)
            {
                var (colour, scale) = _kind switch
                {
                    "snow" => (Shapes.Hex("#f4f6f8"), new Vector3(0.07f, 0.07f, 0.07f)),
                    "leaves" => (i % 3 == 0 ? Shapes.Hex("#b5612b") : i % 3 == 1 ? Shapes.Hex("#d9a03a") : Shapes.Hex("#8a3f22"), new Vector3(0.12f, 0.015f, 0.08f)),
                    "rain" => (Shapes.Hex("#9fb8c8"), new Vector3(0.015f, 0.35f, 0.015f)),
                    _ => (Shapes.Hex("#f5e27a"), new Vector3(0.06f, 0.06f, 0.06f)),
                };
                var flake = Shapes.Create(_kind == "snow" || _kind == "fireflies" ? PrimitiveType.Sphere : PrimitiveType.Cube, colour, Vector3.zero, scale, transform, "Flake");
                _flakes.Add(flake.transform);
                _drift.Add(new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f)));
                Place(i, Focus() + new Vector3(Random.Range(-Spread, Spread), Random.Range(0f, Height), Random.Range(-Spread, Spread)));
            }
        }

        /// <summary>Where the camera looks on the ground.</summary>
        private static Vector3 Focus()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return Vector3.zero;
            }

            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return Mathf.Abs(ray.direction.y) < 0.01f ? camera.transform.position : ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
        }

        private void Place(int i, Vector3 at) => _flakes[i].position = at;

        private void Update()
        {
            if (_flakes.Count == 0)
            {
                return;
            }

            var focus = Focus();
            var dt = Time.deltaTime;
            var fall = _kind switch { "rain" => 9f, "leaves" => 0.9f, "snow" => 0.7f, _ => 0f };
            for (var i = 0; i < _flakes.Count; i++)
            {
                var flake = _flakes[i];
                var p = flake.position;
                if (_kind == "fireflies")
                {
                    p += new Vector3(Mathf.Sin(Time.time * 0.7f + i), Mathf.Sin(Time.time * 1.3f + i * 2f) * 0.5f, Mathf.Cos(Time.time * 0.6f + i)) * dt * 0.4f;
                    p.y = Mathf.Clamp(p.y, 0.3f, 2f);
                }
                else
                {
                    p += (Vector3.down * fall + _drift[i] + new Vector3(Mathf.Sin(Time.time + i) * 0.3f, 0f, 0f)) * dt;
                }

                if (_kind == "leaves")
                {
                    flake.Rotate(new Vector3(90f, 50f, 30f) * dt, Space.Self);
                }

                var away = new Vector2(p.x - focus.x, p.z - focus.z);
                if (p.y < 0f || away.sqrMagnitude > Spread * Spread * 2f)
                {
                    p = focus + new Vector3(Random.Range(-Spread, Spread), _kind == "fireflies" ? Random.Range(0.3f, 2f) : Height, Random.Range(-Spread, Spread));
                }

                flake.position = p;
            }
        }
    }
}
