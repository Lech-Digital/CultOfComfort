using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Plays the first-launch intro (<see cref="IntroScript"/>) on a stage far from the board, with the game's own
    /// models: the garage in 2024, the Marchlands in 947, the title card. Captions are paper notes; a tap moves on to
    /// the next shot, Skip ends it. Borrows the camera, the sun and the fog, and gives them back when it finishes.
    /// </summary>
    public sealed class IntroDirector : MonoBehaviour
    {
        /// <summary>Far from the board, so nothing of the village shows in a shot.</summary>
        public static readonly Vector3 StageOrigin = new Vector3(600f, 0f, 600f);

        private static readonly Vector3 MarchOffset = new Vector3(60f, 0f, 0f);
        private static readonly Quaternion FaceCamera = Quaternion.Euler(0f, 180f, 0f);
        private const float HearthScale = 1.6f;
        private const float RelicScale = 1.15f;
        private const float FadeOutSeconds = 0.7f;

        private Camera _camera;
        private Light _sun;
        private SavedScene _saved;
        private Transform _stage, _garage, _march;
        private GameObject _overlayGo;
        private VisualElement _overlay, _note, _flash, _title, _black;
        private Label _caption;
        private int _index = -1;
        private float _shotTime;
        private bool _finished;
        private float _fadeOut;
        private float _shake;
        private float _flashAlpha;

        // The cast.
        private GameObject _garageCar, _garageProphet, _scorch, _fallingCar, _hearth, _crashedCar, _prophet, _goose;
        private PartAnimator _garageAnimator, _prophetAnimator;
        private Light _bulb, _coilLight;
        private readonly List<GameObject> _locals = new List<GameObject>();
        private readonly List<Vector3> _localHides = new List<Vector3>();
        private readonly List<GameObject> _relics = new List<GameObject>();
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<Spark> _sparks = new List<Spark>();
        private float _nextSpark, _nextSwing;

        public event Action Finished;

        public bool IsPlaying => _index >= 0 && !_finished;

        public string CurrentShot => IsPlaying ? IntroScript.Shots[_index].Id : null;

        /// <summary>Seconds into the current shot.</summary>
        public float ShotTime => _shotTime;

        /// <summary>Builds the stage and starts the first shot.</summary>
        public void Play(Camera camera, PanelSettings panel)
        {
            _camera = camera;
            _sun = NightMood.FindSun();
            _saved = SavedScene.Capture(camera, _sun);
            BuildOverlay(panel);
            _stage = new GameObject("Intro Stage").transform;
            _stage.position = StageOrigin;
            BuildGarage();
            BuildMarchlands();
            Begin(0);
        }

        /// <summary>A tap: on to the next shot (the last one ends the intro).</summary>
        public void Next()
        {
            if (!IsPlaying)
            {
                return;
            }

            if (_index + 1 >= IntroScript.Shots.Count)
            {
                Finish();
            }
            else
            {
                Begin(_index + 1);
            }
        }

        public void Skip()
        {
            if (IsPlaying)
            {
                Finish();
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (_finished)
            {
                FadeOut(dt);
                return;
            }

            if (!IsPlaying)
            {
                return;
            }

            _shotTime += dt;
            var shot = IntroScript.Shots[_index];
            Tick(shot, _shotTime, dt);
            UpdateSparks(dt);
            UpdateFlights(dt);
            _flashAlpha = Mathf.Max(0f, _flashAlpha - dt * 1.4f);
            _flash.style.opacity = _flashAlpha;
            _shake = Mathf.Max(0f, _shake - dt * 1.6f);
            if (_shotTime >= shot.Seconds)
            {
                Next();
            }
        }

        // ---------------- shots ----------------

        private void Begin(int index)
        {
            _index = index;
            _shotTime = 0f;
            var shot = IntroScript.Shots[index];
            _caption.text = shot.Caption;
            _note.RemoveFromClassList("intro-note-in");
            _note.schedule.Execute(() => _note.AddToClassList("intro-note-in")).ExecuteLater(30);
            _title.EnableInClassList("hidden", shot.Set != IntroSet.Title);
            _garage.gameObject.SetActive(shot.Set == IntroSet.Garage);
            _march.gameObject.SetActive(shot.Set != IntroSet.Garage);
            SetLighting(shot.Set);
            switch (shot.Id)
            {
                case IntroScript.Garage:
                    Show(_garageCar, true);
                    Show(_garageProphet, true);
                    Show(_scorch, false);
                    _garageProphet.transform.localPosition = new Vector3(-1.5f, 0f, -0.1f);
                    break;
                case IntroScript.Machine:
                    Show(_garageCar, true);
                    Show(_garageProphet, true);
                    Show(_scorch, false);
                    break;
                case IntroScript.Jump:
                    Show(_garageProphet, false);
                    Show(_garageCar, false);
                    Show(_scorch, true);
                    _flashAlpha = 1f;
                    _shake = 0.5f;
                    _coilLight.intensity = 0f;
                    Poof(_garage, new Vector3(0.4f, 0.4f, 0.6f), 4f);
                    break;
                case IntroScript.Marchlands:
                    SetCrashed(false);
                    Show(_fallingCar, false);
                    Show(_prophet, false);
                    HideLocals();
                    HideRelics();
                    _goose.transform.localPosition = new Vector3(-3.5f, 0f, -1.4f);
                    break;
                case IntroScript.Landing:
                    SetCrashed(false);
                    Show(_fallingCar, true);
                    Show(_prophet, false);
                    HideLocals();
                    HideRelics();
                    break;
                case IntroScript.NoSockets:
                    SetCrashed(true);
                    Show(_fallingCar, false);
                    Show(_prophet, true);
                    HideLocals();
                    HideRelics();
                    break;
                case IntroScript.Boot:
                    SetCrashed(true);
                    Show(_prophet, true);
                    _prophet.transform.localPosition = ProphetSpot;
                    _prophet.transform.localRotation = FaceCamera;
                    PeekLocals(1f);
                    LaunchRelics();
                    break;
                case IntroScript.Cult:
                    SetCrashed(true);
                    Show(_prophet, true);
                    _prophet.transform.localPosition = ProphetSpot;
                    _prophet.transform.localRotation = FaceCamera;
                    LandRelics();
                    PeekLocals(1f);
                    break;
                case IntroScript.Title:
                    SetCrashed(true);
                    Show(_prophet, true);
                    LandRelics();
                    KneelLocals(1f, 0f);
                    break;
            }
        }

        private static readonly Vector3 ProphetSpot = new Vector3(0f, 0f, -1.7f);
        private static readonly Vector3 PoolPoint = new Vector3(0.25f, 0.35f, 0f);
        private static readonly Vector3 BootPoint = new Vector3(0.25f, 1.3f, 0.55f);

        private void Tick(IntroShot shot, float t, float dt)
        {
            var k = Mathf.Clamp01(t / shot.Seconds);
            switch (shot.Id)
            {
                case IntroScript.Garage:
                    Frame(_garage, new Vector3(-1.2f, 2.3f, -6.2f), new Vector3(-0.8f, 2.0f, -5.2f), new Vector3(-0.7f, 0.8f, 0.3f), k);
                    _bulb.intensity = 2.4f + Mathf.PerlinNoise(t * 7f, 0f) * 0.8f;
                    if (t >= _nextSwing)
                    {
                        _nextSwing = t + 0.75f;
                        _garageAnimator?.Attack();
                        Sparks(_garage, new Vector3(-0.85f, 0.55f, 0.1f), 6, 0.9f);
                    }

                    break;
                case IntroScript.Machine:
                    Frame(_garage, new Vector3(0.9f, 1.6f, -4.8f), new Vector3(0.6f, 1.4f, -4.0f), new Vector3(0.1f, 0.9f, 0.5f), k);
                    var charge = k * k;
                    _coilLight.intensity = (Mathf.Sin(t * (8f + 30f * charge)) * 0.5f + 0.5f) * (1f + 9f * charge);
                    _garageCar.transform.localPosition = GarageCarSpot + UnityEngine.Random.insideUnitSphere * 0.03f * charge;
                    if (t >= _nextSpark)
                    {
                        _nextSpark = t + Mathf.Lerp(0.5f, 0.06f, charge);
                        Sparks(_garage, GarageCarSpot + new Vector3(0f, 1.55f, 0f), 4 + (int)(8 * charge), 1.4f);
                    }

                    var walk = Mathf.InverseLerp(0.8f, 2.4f, t);
                    _garageProphet.transform.localPosition = Vector3.Lerp(new Vector3(-1.5f, 0f, -0.1f), new Vector3(-0.85f, 0f, -0.5f), walk);
                    _garageProphet.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    if (_garageAnimator != null)
                    {
                        _garageAnimator.IsMoving = walk > 0f && walk < 1f;
                    }

                    Show(_garageProphet, t < 2.6f); // gets in
                    break;
                case IntroScript.Jump:
                    Frame(_garage, new Vector3(0.6f, 2.2f, -4.8f), new Vector3(0.4f, 2.6f, -5.4f), new Vector3(0.3f, 0.3f, 0.6f), k);
                    _scorch.transform.Rotate(0f, dt * 15f, 0f);
                    break;
                case IntroScript.Marchlands:
                    Frame(_march, new Vector3(-0.5f, 6f, -11f), new Vector3(0f, 4.2f, -8f), new Vector3(0f, 0.6f, 0.2f), k);
                    Waddle(_goose, new Vector3(-3.5f, 0f, -1.4f), new Vector3(-1.2f, 0f, -1.8f), k, t);
                    break;
                case IntroScript.Landing:
                    Frame(_march, new Vector3(0f, 4.2f, -8f), new Vector3(0f, 4.2f, -8f), new Vector3(0f, 0.8f, 0.2f), k);
                    var fall = Mathf.Clamp01(t / 1.0f);
                    if (_fallingCar.activeSelf)
                    {
                        _fallingCar.transform.localPosition = Vector3.Lerp(new Vector3(4f, 14f, 5f), PoolPoint, fall * fall);
                        _fallingCar.transform.localRotation = Quaternion.Euler(fall * 400f, 30f, fall * 160f);
                    }

                    if (fall >= 1f && _fallingCar.activeSelf)
                    {
                        Show(_fallingCar, false);
                        SetCrashed(true);
                        _shake = 0.8f;
                        Poof(_march, PoolPoint + new Vector3(0f, 0.4f, -0.3f), 3f);
                        Poof(_march, PoolPoint + new Vector3(0.8f, 0.2f, -0.2f), 2f);
                        Poof(_march, PoolPoint + new Vector3(-0.7f, 0.3f, -0.1f), 2.2f);
                    }

                    // The goose has seen enough.
                    Waddle(_goose, new Vector3(-1.2f, 0f, -1.8f), new Vector3(-6f, 0f, -3f), Mathf.InverseLerp(1f, 2.4f, t), t * 3f);
                    break;
                case IntroScript.NoSockets:
                    Frame(_march, new Vector3(0.7f, 2.0f, -4.9f), new Vector3(0.5f, 1.7f, -4.3f), new Vector3(0f, 0.8f, -1.5f), k);
                    var hop = Mathf.Clamp01(t / 0.7f);
                    _prophet.transform.localPosition = Vector3.Lerp(new Vector3(0.25f, 0.6f, 0.1f), ProphetSpot, hop) + Vector3.up * Mathf.Sin(hop * Mathf.PI) * 0.9f;
                    _prophet.transform.localRotation = FaceCamera;
                    if (t > 1.3f && t - dt <= 1.3f)
                    {
                        _prophetAnimator?.Annoyed(); // the battery
                    }

                    PeekLocals(Mathf.Clamp01((t - 2.2f) / 0.6f) * (t < 4.2f ? 1f : 0.5f));
                    break;
                case IntroScript.Boot:
                    Frame(_march, new Vector3(0f, 3.4f, -6.8f), new Vector3(0.2f, 3.0f, -6.0f), new Vector3(0f, 0.5f, -1.9f), k);
                    if (t > 5.2f && t - dt <= 5.2f)
                    {
                        _prophetAnimator?.Unimpressed();
                    }

                    break;
                case IntroScript.Cult:
                    Frame(_march, new Vector3(0.3f, 3.3f, -6.6f), new Vector3(-0.2f, 4.2f, -8.0f), new Vector3(0f, 0.5f, -2.0f), k);
                    var approach = Mathf.Clamp01(t / 2.4f);
                    KneelLocals(approach, Mathf.InverseLerp(2.4f, 3.2f, t), t);
                    if (t > 3.8f && t - dt <= 3.8f)
                    {
                        _prophetAnimator?.Unimpressed(); // he decided not to correct them
                    }

                    break;
                case IntroScript.Title:
                    Frame(_march, new Vector3(-0.2f, 4.2f, -8.0f), new Vector3(0f, 7f, -11f), new Vector3(0f, 0.6f, -1.2f), k);
                    KneelLocals(1f, 1f, t + 6f);
                    break;
            }
        }

        /// <summary>
        /// Shots are composed for a 16:9 window; a tall phone screen backs the camera off and widens the lens so the
        /// same action stays in frame.
        /// </summary>
        private void Frame(Transform set, Vector3 from, Vector3 to, Vector3 look, float k)
        {
            var eased = k * k * (3f - 2f * k);
            var portrait = Mathf.Clamp01((1.2f - _camera.aspect) / 0.75f); // 0 landscape … 1 tall phone
            _camera.fieldOfView = Mathf.Lerp(40f, 52f, portrait);
            var back = Mathf.Lerp(1f, 1.45f, portrait);
            var shake = UnityEngine.Random.insideUnitSphere * _shake * 0.15f;
            var eye = look + (Vector3.Lerp(from, to, eased) - look) * back;
            _camera.transform.position = set.position + eye + shake;
            _camera.transform.LookAt(set.position + look);
        }

        // ---------------- the sets ----------------

        private static readonly Vector3 GarageCarSpot = new Vector3(0.4f, 0f, 0.6f);

        private void BuildGarage()
        {
            _garage = new GameObject("Garage 2024").transform;
            _garage.SetParent(_stage, false);
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#55585c"), new Vector3(0f, -0.1f, 0f), new Vector3(12f, 0.2f, 9f), _garage, "Concrete");
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#7d7468"), new Vector3(0f, 2f, 3.2f), new Vector3(12f, 4.2f, 0.2f), _garage, "Back Wall");
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#6e665b"), new Vector3(-4.6f, 2f, 0f), new Vector3(0.2f, 4.2f, 9f), _garage, "Side Wall");
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#a39a8a"), new Vector3(-2.7f, 1.9f, 3.08f), new Vector3(2.2f, 1.1f, 0.04f), _garage, "Pegboard");
            for (var i = 0; i < 5; i++)
            {
                Shapes.Create(PrimitiveType.Cube, Shapes.Hex(i % 2 == 0 ? "#c0392b" : "#2b2b2e"), new Vector3(-3.5f + i * 0.4f, 1.9f + (i % 3) * 0.15f, 3.04f),
                    new Vector3(0.06f, 0.5f, 0.04f), _garage, "Tool");
            }

            Shapes.Create(PrimitiveType.Cube, Palette.Wood, new Vector3(-2.7f, 0.45f, 2.6f), new Vector3(2.2f, 0.9f, 0.8f), _garage, "Workbench");
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#ff5a00"), new Vector3(-2.2f, 1.0f, 2.5f), new Vector3(0.35f, 0.2f, 0.25f), _garage, "Toolbox");
            Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#2fa39a"), new Vector3(-3.2f, 1.02f, 2.5f), new Vector3(0.12f, 0.12f, 0.12f), _garage, "Mug");
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#ece6d6"), new Vector3(1.8f, 2.3f, 3.08f), new Vector3(0.8f, 0.55f, 0.04f), _garage, "Blueprint");
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#1f6dff"), new Vector3(2.8f, 0.25f, 2.4f), new Vector3(1.0f, 0.5f, 0.6f), _garage, "Folded Tarp");
            Model("traffic_cone", _garage, new Vector3(-1.6f, 0f, 1.6f), 160f, 1.6f);
            Model("shopping_trolley", _garage, new Vector3(3.2f, 0f, 1.2f), 200f, 1.5f);
            _garageCar = Model("time_machine", _garage, GarageCarSpot, 90f, 2.2f);
            _garageProphet = Model("prophet", _garage, new Vector3(-1.5f, 0f, -0.1f), 90f, 1.3f);
            _garageAnimator = Animate(_garageProphet);
            _scorch = Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#2b2420"), GarageCarSpot + new Vector3(0f, 0.01f, 0f), new Vector3(2.6f, 0.005f, 1.4f), _garage, "Scorch");
            _bulb = PointLight(_garage, new Vector3(-0.2f, 3.2f, 0.4f), new Color(1f, 0.82f, 0.55f), 9f, 2.6f);
            Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#ffcf80"), new Vector3(-0.2f, 3.3f, 0.4f), Vector3.one * 0.18f, _garage, "Bulb");
            _coilLight = PointLight(_garage, GarageCarSpot + new Vector3(0f, 1.8f, 0f), new Color(0.6f, 0.85f, 1f), 6f, 0f);
        }

        private void BuildMarchlands()
        {
            _march = new GameObject("Marchlands 947").transform;
            _march.SetParent(_stage, false);
            _march.localPosition = MarchOffset;
            Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#56603a"), new Vector3(0f, -0.1f, 2f), new Vector3(40f, 0.2f, 30f), _march, "Grass");
            Shapes.Create(PrimitiveType.Cube, Palette.Road, new Vector3(0f, -0.09f, -6f), new Vector3(1.4f, 0.2f, 10f), _march, "Track");
            _hearth = Model("hearth_l1", _march, Vector3.zero, 180f, HearthScale);
            _crashedCar = _hearth != null ? ArtLibrary.FindPart(_hearth.transform, "TimeMachine")?.gameObject : null;
            _fallingCar = Model("time_machine", _march, new Vector3(4f, 14f, 5f), 0f, HearthScale * 0.68f);
            _prophet = Model("prophet", _march, ProphetSpot, 180f, 1.3f);
            _prophetAnimator = Animate(_prophet);
            _goose = Model("goose", _march, new Vector3(-3.5f, 0f, -1.4f), 90f, 1.2f);
            var bushes = new[] { new Vector3(-2.6f, 0f, 0.4f), new Vector3(2.9f, 0f, 0.1f), new Vector3(-1.9f, 0f, 2.4f), new Vector3(2.3f, 0f, 2.6f), new Vector3(-3.4f, 0f, -1.6f) };
            foreach (var spot in bushes)
            {
                Model("bush", _march, spot, spot.x * 40f, 1.6f);
            }

            foreach (var (model, spot, scale) in new[]
            {
                ("pine", new Vector3(-5f, 0f, 6f), 1.8f), ("pine", new Vector3(5.5f, 0f, 5f), 2f), ("oak", new Vector3(-6.5f, 0f, 2f), 1.6f),
                ("pine", new Vector3(7f, 0f, 1f), 1.7f), ("birch", new Vector3(3.6f, 0f, 6.5f), 1.5f), ("rock_a", new Vector3(-4f, 0f, -2.8f), 1.2f),
                ("dead_tree", new Vector3(6f, 0f, -2.5f), 1.4f), ("pine", new Vector3(-8f, 0f, 7f), 2.1f),
            })
            {
                Model(model, _march, spot, spot.x * 23f, scale);
            }

            for (var i = 0; i < IntroScript.Locals.Count; i++)
            {
                var hide = bushes[i] + new Vector3(0f, 0f, 0.35f);
                var local = Model(IntroScript.Locals[i], _march, hide, 180f, 1.15f);
                _locals.Add(local);
                _localHides.Add(hide);
                Animate(local);
            }

            foreach (var name in IntroScript.BootRelics)
            {
                _relics.Add(Model(name, _march, BootPoint, 0f, name == "shopping_trolley" ? RelicScale * 0.75f : RelicScale));
            }
        }

        private void SetLighting(IntroSet set)
        {
            if (set == IntroSet.Garage)
            {
                _camera.backgroundColor = Shapes.Hex("#1b1a1c");
                RenderSettings.fog = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.32f, 0.31f, 0.34f);
                if (_sun != null)
                {
                    _sun.intensity = 0.25f;
                }
            }
            else
            {
                _camera.backgroundColor = Shapes.Hex("#a9b6bf");
                RenderSettings.fog = true;
                RenderSettings.fogColor = Shapes.Hex("#a9b6bf");
                RenderSettings.fogStartDistance = 14f;
                RenderSettings.fogEndDistance = 40f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
                if (_sun != null)
                {
                    _sun.intensity = 1.25f;
                    _sun.color = new Color(1f, 0.9f, 0.74f);
                    _sun.transform.rotation = Quaternion.Euler(42f, -30f, 0f);
                }
            }

        }

        // ---------------- the cast ----------------

        private void SetCrashed(bool crashed)
        {
            if (_crashedCar != null)
            {
                _crashedCar.SetActive(crashed);
            }
        }

        private void HideLocals()
        {
            for (var i = 0; i < _locals.Count; i++)
            {
                Place(_locals[i], _localHides[i] + Vector3.down * 1.4f, FaceCamera);
            }
        }

        /// <summary>Heads up over the bushes (0: hidden, 1: peeking).</summary>
        private void PeekLocals(float amount)
        {
            for (var i = 0; i < _locals.Count; i++)
            {
                var stagger = Mathf.Clamp01(amount * 1.6f - i * 0.12f);
                Place(_locals[i], _localHides[i] + Vector3.down * Mathf.Lerp(1.4f, 0.45f, stagger), FaceCamera);
            }
        }

        /// <summary>The locals walk to a ring round the Prophet (<paramref name="approach"/>) and kneel (<paramref name="kneel"/>), bowing in time.</summary>
        private void KneelLocals(float approach, float kneel, float time = 0f)
        {
            for (var i = 0; i < _locals.Count; i++)
            {
                if (_locals[i] == null)
                {
                    continue;
                }

                var ring = ProphetSpot + KneelSpots[i % KneelSpots.Length];
                var from = _localHides[i] + Vector3.down * 0.45f;
                var walk = Mathf.Clamp01(approach * 1.15f - i * 0.04f);
                var at = Vector3.Lerp(from, ring, walk);
                at.y = Mathf.Lerp(from.y, 0f, Mathf.Clamp01(walk * 3f));
                var face = Quaternion.LookRotation(ProphetSpot - ring);
                var bow = kneel * (18f + Mathf.Max(0f, Mathf.Sin(time * 3f + i)) * 22f);
                Place(_locals[i], at + Vector3.down * kneel * 0.12f, walk < 1f ? Quaternion.LookRotation(ring - from) : face * Quaternion.Euler(bow, 0f, 0f));
                var animator = _locals[i].GetComponent<PartAnimator>();
                if (animator != null)
                {
                    animator.IsMoving = walk > 0f && walk < 1f;
                }
            }
        }

        /// <summary>Round the Prophet, outside the relics, where the camera sees faces.</summary>
        private static readonly Vector3[] KneelSpots =
        {
            new Vector3(-1.9f, 0f, 0.3f), new Vector3(1.9f, 0f, 0.3f), new Vector3(-1.7f, 0f, -0.9f), new Vector3(1.7f, 0f, -0.9f), new Vector3(0.1f, 0f, -1.8f),
        };

        private void HideRelics()
        {
            _flights.Clear();
            foreach (var relic in _relics)
            {
                Show(relic, false);
            }
        }

        private void LaunchRelics()
        {
            HideRelics();
            for (var i = 0; i < _relics.Count; i++)
            {
                _flights.Add(new Flight { Relic = _relics[i], Delay = 0.35f + i * 0.45f, Land = RelicSpot(i), Spin = 360f + i * 97f });
            }
        }

        private void LandRelics()
        {
            _flights.Clear();
            for (var i = 0; i < _relics.Count; i++)
            {
                Show(_relics[i], true);
                Place(_relics[i], RelicSpot(i), Quaternion.Euler(0f, 160f + i * 37f, 0f));
            }
        }

        /// <summary>An arc on the ground in front of the Prophet, where the camera can see every relic.</summary>
        private static Vector3 RelicSpot(int i)
        {
            var count = IntroScript.BootRelics.Count;
            var angle = Mathf.Lerp(200f, 340f, i / (float)(count - 1)) * Mathf.Deg2Rad;
            var radius = i % 2 == 0 ? 0.95f : 1.25f;
            return ProphetSpot + new Vector3(Mathf.Cos(angle) * radius * 1.35f, 0f, Mathf.Sin(angle) * radius * 0.75f - 0.15f);
        }

        private void UpdateFlights(float dt)
        {
            foreach (var flight in _flights)
            {
                flight.Time += dt;
                var t = (flight.Time - flight.Delay) / 0.75f;
                if (t < 0f || flight.Relic == null)
                {
                    continue;
                }

                Show(flight.Relic, true);
                var k = Mathf.Clamp01(t);
                var at = Vector3.Lerp(BootPoint, flight.Land, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.2f;
                if (k >= 1f)
                {
                    var bounce = Mathf.Max(0f, Mathf.Sin((t - 1f) * 9f)) * Mathf.Exp(-(t - 1f) * 6f) * 0.25f;
                    at = flight.Land + Vector3.up * bounce;
                }

                Place(flight.Relic, at, Quaternion.Euler(k < 1f ? k * flight.Spin : 0f, 160f + flight.Spin * 0.1f, 0f));
            }
        }

        private static void Waddle(GameObject bird, Vector3 from, Vector3 to, float k, float t)
        {
            if (bird == null)
            {
                return;
            }

            bird.transform.localPosition = Vector3.Lerp(from, to, Mathf.Clamp01(k)) + Vector3.up * Mathf.Abs(Mathf.Sin(t * 9f)) * 0.04f;
            bird.transform.localRotation = Quaternion.LookRotation(to - from) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 8f);
        }

        // ---------------- effects ----------------

        private void Sparks(Transform set, Vector3 at, int count, float speed)
        {
            for (var i = 0; i < count; i++)
            {
                var go = Shapes.Create(PrimitiveType.Sphere, i % 3 == 0 ? Shapes.Hex("#ffffff") : Shapes.Hex("#ffb04d"), at, Vector3.one * 0.05f, set, "Spark");
                var v = UnityEngine.Random.insideUnitSphere * speed;
                v.y = Mathf.Abs(v.y) + speed * 0.6f;
                _sparks.Add(new Spark { Go = go, Velocity = v, Life = 0.45f + UnityEngine.Random.value * 0.3f });
            }
        }

        private void UpdateSparks(float dt)
        {
            for (var i = _sparks.Count - 1; i >= 0; i--)
            {
                var spark = _sparks[i];
                spark.Life -= dt;
                if (spark.Life <= 0f || spark.Go == null)
                {
                    if (spark.Go != null)
                    {
                        Destroy(spark.Go);
                    }

                    _sparks.RemoveAt(i);
                    continue;
                }

                spark.Velocity += Vector3.down * 6f * dt;
                spark.Go.transform.localPosition += spark.Velocity * dt;
                spark.Go.transform.localScale = Vector3.one * 0.05f * Mathf.Clamp01(spark.Life * 3f);
            }
        }

        private void Poof(Transform set, Vector3 at, float scale)
        {
            var poof = Model("fx_poof", set, at, UnityEngine.Random.value * 360f, scale);
            if (poof != null)
            {
                Destroy(poof, 1.2f);
            }
        }

        // ---------------- helpers ----------------

        private static GameObject Model(string name, Transform parent, Vector3 at, float yaw, float scale)
        {
            var model = ArtLibrary.Instantiate(name, parent);
            if (model == null)
            {
                model = Shapes.Create(PrimitiveType.Cube, Palette.Wood, at, Vector3.one * 0.3f, parent, name);
                return model;
            }

            model.transform.localPosition = at;
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            model.transform.localScale = Vector3.one * scale;
            return model;
        }

        private static PartAnimator Animate(GameObject person)
        {
            if (person == null || ArtLibrary.FindPart(person.transform, "Body") == null)
            {
                return null;
            }

            var animator = person.AddComponent<PartAnimator>();
            animator.Bind();
            return animator;
        }

        private static Light PointLight(Transform parent, Vector3 at, Color color, float range, float intensity)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            return light;
        }

        private static void Show(GameObject go, bool shown)
        {
            if (go != null && go.activeSelf != shown)
            {
                go.SetActive(shown);
            }
        }

        private static void Place(GameObject go, Vector3 at, Quaternion rotation)
        {
            if (go != null)
            {
                go.transform.localPosition = at;
                go.transform.localRotation = rotation;
            }
        }

        // ---------------- overlay ----------------

        private void BuildOverlay(PanelSettings panel)
        {
            // A root object on purpose: a UIDocument under another UIDocument's object joins that document's tree,
            // and the HUD's tree is hidden while the intro plays.
            _overlayGo = new GameObject("Intro Overlay");
            var document = _overlayGo.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.sortingOrder = 50;
            var root = document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Hud"));
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Intro"));
            _overlay = new VisualElement { name = "intro" };
            _overlay.AddToClassList("intro");
            _overlay.RegisterCallback<PointerDownEvent>(_ => Next());
            root.Add(_overlay);

            var top = new VisualElement();
            top.AddToClassList("intro-bar");
            top.AddToClassList("intro-bar-top");
            var skip = new Button(Skip) { text = "Skip ›" };
            skip.AddToClassList("intro-skip");
            skip.RegisterCallback<PointerDownEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);
            top.Add(skip);
            _overlay.Add(top);

            _title = new VisualElement();
            _title.AddToClassList("intro-title");
            _title.AddToClassList("hidden");
            var name = new Label("Cult of Comfort");
            name.AddToClassList("intro-title-name");
            _title.Add(name);
            _overlay.Add(_title);

            var bottom = new VisualElement();
            bottom.AddToClassList("intro-bar");
            bottom.AddToClassList("intro-bar-bottom");
            _note = new VisualElement();
            _note.AddToClassList("intro-note");
            _note.AddToClassList("note-yellow");
            _caption = new Label();
            _caption.AddToClassList("intro-caption");
            var tap = new Label("tap ›");
            tap.AddToClassList("intro-tap");
            _note.Add(_caption);
            _note.Add(tap);
            bottom.Add(_note);
            _overlay.Add(bottom);

            _flash = new VisualElement { pickingMode = PickingMode.Ignore };
            _flash.AddToClassList("intro-flash");
            _flash.style.opacity = 0f;
            _overlay.Add(_flash);
            _black = new VisualElement { pickingMode = PickingMode.Ignore };
            _black.AddToClassList("intro-black");
            _black.style.opacity = 0f;
            root.Add(_black);
        }

        // ---------------- the end ----------------

        private void Finish()
        {
            _finished = true;
            foreach (var spark in _sparks)
            {
                if (spark.Go != null)
                {
                    Destroy(spark.Go);
                }
            }

            _sparks.Clear();
            if (_stage != null)
            {
                Destroy(_stage.gameObject);
            }

            _saved.Restore(_camera, _sun);
            _overlay.RemoveFromHierarchy();
            _black.style.opacity = 1f;
            _fadeOut = FadeOutSeconds;
            Finished?.Invoke();
        }

        private void FadeOut(float dt)
        {
            _fadeOut -= dt;
            if (_black != null)
            {
                _black.style.opacity = Mathf.Clamp01(_fadeOut / FadeOutSeconds);
            }

            if (_fadeOut <= 0f)
            {
                Destroy(_overlayGo);
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (_stage != null)
            {
                Destroy(_stage.gameObject);
            }

            if (_overlayGo != null)
            {
                Destroy(_overlayGo);
            }
        }

        private sealed class Flight
        {
            public GameObject Relic;
            public float Delay;
            public float Time;
            public Vector3 Land;
            public float Spin;
        }

        private sealed class Spark
        {
            public GameObject Go;
            public Vector3 Velocity;
            public float Life;
        }

        /// <summary>What the intro borrows from the game scene, to hand back unchanged.</summary>
        private readonly struct SavedScene
        {
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly float _fov;
            private readonly Color _background;
            private readonly bool _fog;
            private readonly Color _fogColor;
            private readonly float _fogStart, _fogEnd;
            private readonly UnityEngine.Rendering.AmbientMode _ambientMode;
            private readonly Color _ambient;
            private readonly Quaternion _sunRotation;
            private readonly Color _sunColor;
            private readonly float _sunIntensity;

            private SavedScene(Camera camera, Light sun)
            {
                _position = camera.transform.position;
                _rotation = camera.transform.rotation;
                _fov = camera.fieldOfView;
                _background = camera.backgroundColor;
                _fog = RenderSettings.fog;
                _fogColor = RenderSettings.fogColor;
                _fogStart = RenderSettings.fogStartDistance;
                _fogEnd = RenderSettings.fogEndDistance;
                _ambientMode = RenderSettings.ambientMode;
                _ambient = RenderSettings.ambientLight;
                _sunRotation = sun != null ? sun.transform.rotation : Quaternion.identity;
                _sunColor = sun != null ? sun.color : Color.white;
                _sunIntensity = sun != null ? sun.intensity : 1f;
            }

            public static SavedScene Capture(Camera camera, Light sun) => new SavedScene(camera, sun);

            public void Restore(Camera camera, Light sun)
            {
                camera.transform.SetPositionAndRotation(_position, _rotation);
                camera.fieldOfView = _fov;
                camera.backgroundColor = _background;
                RenderSettings.fog = _fog;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogStartDistance = _fogStart;
                RenderSettings.fogEndDistance = _fogEnd;
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientLight = _ambient;
                if (sun != null)
                {
                    sun.transform.rotation = _sunRotation;
                    sun.color = _sunColor;
                    sun.intensity = _sunIntensity;
                }
            }
        }
    }
}
