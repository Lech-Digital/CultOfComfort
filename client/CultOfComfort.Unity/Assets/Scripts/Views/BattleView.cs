using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Combat;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Read-only mirror of a live <see cref="Battle"/>. The simulation ticks at 20 Hz; this view
    /// interpolates between the last two ticks so movement looks smooth at any frame rate.
    /// </summary>
    public sealed class BattleView : MonoBehaviour
    {
        private readonly Dictionary<int, EnemyVisual> _enemies = new Dictionary<int, EnemyVisual>();
        private readonly List<Effect> _effects = new List<Effect>();
        private MaterialPropertyBlock _block;
        private Battle _battle;
        private Transform _root;
        private Transform _leader;
        private Transform _umbrella;
        private Transform _leaderBody;
        private PartAnimator _prophet;
        private Transform _leaderHealth;
        private Renderer _leaderHealthRenderer;
        private Transform _reachRing;
        private float _leaderFlash;
        private float _leaderTilt;
        private Vector3 _leaderPrevious;
        private Vector3 _leaderCurrent;
        private float _umbrellaSwing;
        private float _shake;
        private bool _zealotAnnounced;
        private Dictionary<int, Tower> _towers;

        public event Action<string> Announcement;

        // Unity objects must not be created in field initializers or constructors — Unity owns component construction.
        private void Awake() => _block = new MaterialPropertyBlock();

        public float CameraShake => _shake;

        public void Begin(Battle battle)
        {
            Clear();
            _battle = battle;
            _towers = new Dictionary<int, Tower>();
            foreach (var tower in battle.Towers)
            {
                _towers[tower.Id] = tower;
            }

            _root = new GameObject("Battle").transform;
            _root.SetParent(transform, false);

            _leader = new GameObject("The Prophet").transform;
            _leader.SetParent(_root, false);
            Shapes.Create(PrimitiveType.Cylinder, Palette.Highlight, new Vector3(0f, 0.01f, 0f), new Vector3(0.75f, 0.01f, 0.75f), _leader, "Halo (ironic)");

            var model = ArtLibrary.Instantiate("prophet", _leader);
            if (model != null)
            {
                _prophet = model.AddComponent<PartAnimator>();
                _prophet.Bind();
            }
            else
            {
                BuildPlaceholderProphet();
            }

            var bar = Shapes.Create(PrimitiveType.Cube, Palette.HealthGood, new Vector3(0f, 1.3f, 0f), new Vector3(0.7f, 0.07f, 0.07f), _leader, "Prophet Health");
            _leaderHealth = bar.transform;
            _leaderHealthRenderer = bar.GetComponent<Renderer>();
            _reachRing = CreateReachRing((float)BalanceTable.Default.HotStones.Reach.ToDouble());
            _reachRing.gameObject.SetActive(false);

            _leaderCurrent = _leaderPrevious = Shapes.ToWorld(battle.Leader.Position);
            _leader.localPosition = _leaderCurrent;
        }

        /// <summary>Primitive stand-in used until art/build.sh has produced the prophet model.</summary>
        private void BuildPlaceholderProphet()
        {
            // Body pivots at the feet so swooning tips him over instead of sinking him into the ground.
            _leaderBody = new GameObject("Body").transform;
            _leaderBody.SetParent(_leader, false);
            Shapes.Create(PrimitiveType.Capsule, Palette.Linen, new Vector3(0f, 0.45f, 0f), new Vector3(0.38f, 0.45f, 0.38f), _leaderBody, "Linen Shirt");
            Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#e0b48f"), new Vector3(0f, 1.0f, 0f), Vector3.one * 0.28f, _leaderBody, "Tired Face");
            var umbrellaPivot = new GameObject("Umbrella Pivot").transform;
            umbrellaPivot.SetParent(_leaderBody, false);
            umbrellaPivot.localPosition = new Vector3(0.25f, 0.6f, 0f);
            Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#1d1d24"), new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.3f, 0.05f), umbrellaPivot, "Umbrella (2024)");
            _umbrella = umbrellaPivot;
        }

        /// <summary>While aiming Hot Stones: a ring of pebbles around the Prophet marking how far he can throw.</summary>
        public void ShowReach(bool visible)
        {
            if (_reachRing != null)
            {
                _reachRing.gameObject.SetActive(visible);
            }
        }

        public void Clear()
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }

            _enemies.Clear();
            _effects.Clear();
            _battle = null;
            _prophet = null;
            _leaderBody = null;
            _umbrella = null;
        }

        /// <summary>Call immediately before <see cref="Battle.Step"/>.</summary>
        public void BeforeStep()
        {
            foreach (var visual in _enemies.Values)
            {
                visual.Previous = visual.Current;
            }

            _leaderPrevious = _leaderCurrent;
        }

        /// <summary>Call immediately after <see cref="Battle.Step"/>: syncs visuals and spawns effects for this tick's events.</summary>
        public void AfterStep()
        {
            foreach (var enemy in _battle.Enemies)
            {
                if (enemy.IsActive && !_enemies.ContainsKey(enemy.Id))
                {
                    _enemies[enemy.Id] = CreateEnemy(enemy);
                }
            }

            foreach (var battleEvent in _battle.LastTickEvents)
            {
                HandleEvent(battleEvent);
            }

            foreach (var enemy in _battle.Enemies)
            {
                if (!_enemies.TryGetValue(enemy.Id, out var visual))
                {
                    continue;
                }

                if (!enemy.IsActive)
                {
                    Destroy(visual.Root.gameObject);
                    _enemies.Remove(enemy.Id);
                    continue;
                }

                visual.Current = Shapes.ToWorld(_battle.PositionOf(enemy));
                visual.Health = (float)(enemy.Health.ToDouble() / enemy.MaxHealth.ToDouble());
                visual.Muddied = enemy.IsMuddied(_battle.Tick);
                visual.Frightened = enemy.IsFrightened(_battle.Tick);
                visual.Fighting = enemy.IsFightingLeader;
            }

            _leaderCurrent = Shapes.ToWorld(_battle.Leader.Position);
        }

        /// <param name="alpha">Fraction of the way from the previous tick to the current one.</param>
        public void Render(float alpha)
        {
            if (_battle == null)
            {
                return;
            }

            foreach (var visual in _enemies.Values)
            {
                var position = Vector3.Lerp(visual.Previous, visual.Current, alpha);
                var heading = visual.Fighting ? _leader.localPosition - position : visual.Current - visual.Previous;
                heading.y = 0f;
                var animated = visual.Animator != null;
                var lunge = visual.Fighting && !animated ? Mathf.Max(0f, Mathf.Sin(Time.time * 9f + visual.Phase)) * 0.18f : 0f;
                var bob = animated ? 0f : Mathf.Abs(Mathf.Sin(Time.time * 12f + visual.Phase)) * 0.06f;
                if (animated)
                {
                    visual.Animator.IsMoving = !visual.Fighting && (visual.Current - visual.Previous).sqrMagnitude > 0.000001f;
                }

                visual.Root.localPosition = position + Vector3.up * bob + (heading.sqrMagnitude > 0.0001f ? heading.normalized * lunge : Vector3.zero);
                if (heading.sqrMagnitude > 0.00001f)
                {
                    visual.Root.localRotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
                }

                var tint = visual.Frightened ? Palette.Fright : visual.Muddied ? Color.Lerp(visual.BaseColor, Palette.Mud, 0.6f) : visual.BaseColor;
                foreach (var renderer in visual.Renderers)
                {
                    Shapes.Tint(renderer, tint, _block);
                }
                visual.HealthBar.localScale = new Vector3(0.6f * visual.Health, 0.06f, 0.06f);
                visual.HealthBar.gameObject.SetActive(visual.Health < 0.999f);
                Shapes.Tint(visual.HealthBarRenderer, visual.Health > 0.4f ? Palette.HealthGood : Palette.HealthBad, _block);
            }

            _leader.localPosition = Vector3.Lerp(_leaderPrevious, _leaderCurrent, alpha);
            var leaderHeading = _leaderCurrent - _leaderPrevious;
            if (leaderHeading.sqrMagnitude > 0.00001f)
            {
                _leader.localRotation = Quaternion.LookRotation(leaderHeading.normalized, Vector3.up);
            }

            var leader = _battle.Leader;
            var swooning = leader.IsSwooning(_battle.Tick);
            if (_prophet != null)
            {
                _prophet.IsSwooning = swooning;
                _prophet.IsMoving = leaderHeading.sqrMagnitude > 0.00001f;
            }
            else
            {
                _leaderTilt = Mathf.MoveTowards(_leaderTilt, swooning ? 1f : 0f, Time.deltaTime * (swooning ? 5f : 2f));
                _leaderBody.localRotation = Quaternion.Euler(_leaderTilt * 88f, 0f, 0f);
            }
            var health = leader.Health / (float)leader.MaxHealth;
            _leaderHealth.localScale = new Vector3(0.7f * health, 0.07f, 0.07f);
            _leaderHealth.gameObject.SetActive(!swooning && health < 0.999f);
            _leaderFlash = Mathf.MoveTowards(_leaderFlash, 0f, Time.deltaTime * 5f);
            Shapes.Tint(_leaderHealthRenderer, Color.Lerp(health > 0.4f ? Palette.HealthGood : Palette.HealthBad, Color.white, _leaderFlash), _block);

            if (_umbrella != null)
            {
                _umbrellaSwing = Mathf.MoveTowards(_umbrellaSwing, 0f, Time.deltaTime * 4f);
                _umbrella.localRotation = Quaternion.Euler(_umbrellaSwing * 110f, 0f, 0f);
            }

            for (var i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];
                effect.Age += Time.deltaTime;
                var t = Mathf.Clamp01(effect.Age / effect.Lifetime);
                effect.Update(effect.Target, t);
                if (t >= 1f)
                {
                    Destroy(effect.Target);
                    _effects.RemoveAt(i);
                }
            }

            _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 0.8f);
        }

        private void HandleEvent(BattleEvent battleEvent)
        {
            switch (battleEvent)
            {
                case TowerFired fired when _enemies.TryGetValue(fired.TargetId, out var target):
                    target.Animator?.Hit();
                    var tower = _towers[fired.TowerId];
                    var from = Shapes.ToWorld(tower.Position);
                    var to = target.Current;
                    if (tower.Type == TowerType.SlurryChute)
                    {
                        SpawnMudSplat(from, to);
                    }
                    else
                    {
                        SpawnRock(from, to, fired.Critical);
                    }

                    break;
                case LeaderAttacked _:
                    if (_prophet != null)
                    {
                        _prophet.Attack();
                    }
                    else
                    {
                        _umbrellaSwing = 1f;
                    }

                    break;
                case LeaderHit hit:
                    _prophet?.Hit();
                    if (_enemies.TryGetValue(hit.EnemyId, out var attacker))
                    {
                        attacker.Animator?.Attack();
                    }

                    _leaderFlash = 1f;
                    if (hit.Type == EnemyType.Zealot && _enemies.TryGetValue(hit.EnemyId, out var thrower))
                    {
                        SpawnHymnal(thrower.Current, _leaderCurrent);
                    }

                    break;
                case LeaderSwooned _:
                    _shake = 0.15f;
                    Announcement?.Invoke("The Prophet has fainted. Dramatically, into the mud. Back in 10 seconds.");
                    break;
                case LeaderRevived _:
                    Announcement?.Invoke("He's up. Muddy, offended, and back in charge.");
                    break;
                case HotStonesThrown thrown:
                    if (thrown.EnemiesAffected == 0)
                    {
                        _prophet?.Unimpressed();
                    }

                    SpawnHotStones(_leaderCurrent, Shapes.ToWorld(thrown.Target));
                    Announcement?.Invoke(thrown.EnemiesAffected == 0
                        ? "Hot stones, thrown at nothing. The grass is now exfoliated."
                        : $"Hot Stone Therapy for {thrown.EnemiesAffected}. Unrequested, unrefundable.");
                    break;
                case EnemySpawned { Type: EnemyType.Zealot } when !_zealotAnnounced:
                    _zealotAnnounced = true;
                    Announcement?.Invoke("Zealots. Friar Odo's lot. They throw hymnals, and they aim for the heretic.");
                    break;
                case InterventionUsed { Type: PlayerActionType.OpenSluiceGate } used:
                    SpawnFlood();
                    Announcement?.Invoke($"Sluice gate opened. {used.EnemiesAffected} soldiers rediscover mud at speed.");
                    break;
                case InterventionUsed { Type: PlayerActionType.PhantomVoice } used:
                    if (used.EnemiesAffected == 0)
                    {
                        _prophet?.Unimpressed();
                    }

                    SpawnShout();
                    Announcement?.Invoke(used.EnemiesAffected == 0
                        ? "He shouted into a tin can at nobody. The cultists exchanged looks."
                        : $"A disembodied voice told {used.EnemiesAffected} soldiers to go home. They did.");
                    break;
                case InputRejected rejected:
                    _prophet?.Annoyed(); // he takes refusals personally
                    Announcement?.Invoke(rejected.Reason);
                    break;
                case WaveStarted wave:
                    Announcement?.Invoke($"Wave {wave.Wave} of {_battle.WaveCount}. Lord Wulfric remains unconvinced.");
                    break;
                case HearthDamaged damaged:
                    _prophet?.Unimpressed();
                    _shake = 0.25f;
                    Announcement?.Invoke(damaged.Type switch
                    {
                        EnemyType.ManAtArms => $"A man-at-arms dented the Hearth. −{damaged.Damage}. Rude.",
                        EnemyType.Zealot => $"A zealot blessed the Hearth. Aggressively. −{damaged.Damage}.",
                        _ => $"A levy kicked the Hearth. −{damaged.Damage} warmth.",
                    });
                    break;
                case EnemyDefeated defeated when _enemies.TryGetValue(defeated.EnemyId, out var dead):
                    SpawnPoof(dead.Current);
                    break;
            }
        }

        private static string ModelName(EnemyType type) => type switch
        {
            EnemyType.ManAtArms => "manatarms",
            EnemyType.Zealot => "zealot",
            _ => "levy",
        };

        private EnemyVisual CreateEnemy(Enemy enemy)
        {
            var model = ArtLibrary.Instantiate(ModelName(enemy.Type), null);
            if (model != null)
            {
                var modelRoot = new GameObject($"{enemy.Type} #{enemy.Id}").transform;
                modelRoot.SetParent(_root, false);
                model.transform.SetParent(modelRoot, false);
                var animator = model.AddComponent<PartAnimator>();
                animator.Bind();
                var modelBar = Shapes.Create(PrimitiveType.Cube, Palette.HealthGood, new Vector3(0f, 1.1f, 0f), new Vector3(0.6f, 0.06f, 0.06f), modelRoot, "Health");
                var start = Shapes.ToWorld(_battle.PositionOf(enemy));
                modelRoot.localPosition = start;
                return new EnemyVisual
                {
                    Root = modelRoot,
                    Renderers = model.GetComponentsInChildren<Renderer>(),
                    Animator = animator,
                    HealthBar = modelBar.transform,
                    HealthBarRenderer = modelBar.GetComponent<Renderer>(),
                    BaseColor = Color.white, // the palette texture carries the colour; tints multiply it
                    Previous = start,
                    Current = start,
                    Health = 1f,
                    Phase = enemy.Id * 1.7f,
                };
            }

            var heavy = enemy.Type == EnemyType.ManAtArms;
            var zealot = enemy.Type == EnemyType.Zealot;
            var color = heavy ? Palette.ManAtArms : zealot ? Palette.Zealot : Palette.Levy;
            var root = new GameObject($"{enemy.Type} #{enemy.Id}").transform;
            root.SetParent(_root, false);
            var size = heavy ? 0.42f : zealot ? 0.28f : 0.3f;
            var body = Shapes.Create(PrimitiveType.Capsule, color, new Vector3(0f, size, 0f), new Vector3(size, size, size), root, "Body");
            if (heavy)
            {
                Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#6c747b"), new Vector3(0f, size * 2.1f, 0f), Vector3.one * size * 0.75f, root, "Helmet");
            }
            else if (zealot)
            {
                Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#e0b48f"), new Vector3(0f, size * 2.1f, 0f), Vector3.one * size * 0.7f, root, "Tonsure");
                Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#3b2a1c"), new Vector3(0.16f, size * 1.2f, 0.12f), new Vector3(0.14f, 0.18f, 0.06f), root, "Hymnal");
            }
            else
            {
                var fork = Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, new Vector3(0.18f, 0.5f, 0.1f), new Vector3(0.03f, 0.45f, 0.03f), root, "Pitchfork");
                fork.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            }

            var bar = Shapes.Create(PrimitiveType.Cube, Palette.HealthGood, new Vector3(0f, size * 2f + 0.45f, 0f), new Vector3(0.6f, 0.06f, 0.06f), root, "Health");
            var position = Shapes.ToWorld(_battle.PositionOf(enemy));
            root.localPosition = position;

            return new EnemyVisual
            {
                Root = root,
                Renderers = new[] { body.GetComponent<Renderer>() },
                HealthBar = bar.transform,
                HealthBarRenderer = bar.GetComponent<Renderer>(),
                BaseColor = color,
                Previous = position,
                Current = position,
                Health = 1f,
                Phase = enemy.Id * 1.7f,
            };
        }

        private void SpawnRock(Vector3 from, Vector3 to, bool critical)
        {
            var rock = Shapes.Create(PrimitiveType.Sphere, Palette.Stone, from, Vector3.one * 0.16f, _root, "Flung Rock");
            var start = from + Vector3.up * 1f;
            AddEffect(rock, 0.3f, (go, t) =>
            {
                go.transform.localPosition = Vector3.Lerp(start, to + Vector3.up * 0.4f, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.2f;
            });

            if (critical)
            {
                var flash = Shapes.Create(PrimitiveType.Sphere, Palette.Highlight, to + Vector3.up * 0.5f, Vector3.zero, _root, "THWACK");
                AddEffect(flash, 0.35f, (go, t) => go.transform.localScale = Vector3.one * Mathf.Sin(t * Mathf.PI) * 0.6f);
            }
        }

        private void SpawnMudSplat(Vector3 from, Vector3 to)
        {
            var glob = Shapes.Create(PrimitiveType.Sphere, Palette.Mud, from, Vector3.one * 0.25f, _root, "Slurry");
            var start = from + Vector3.up * 1f;
            AddEffect(glob, 0.25f, (go, t) => go.transform.localPosition = Vector3.Lerp(start, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.8f);
            var splat = Shapes.Create(PrimitiveType.Cylinder, Palette.Mud, to + Vector3.up * 0.02f, Vector3.zero, _root, "Splat");
            AddEffect(splat, 0.9f, (go, t) =>
            {
                var size = t < 0.3f ? t / 0.3f * 2f : 2f;
                go.transform.localScale = new Vector3(size, 0.01f, size);
                go.SetActive(t > 0.25f);
            });
        }

        private void SpawnFlood()
        {
            foreach (var tile in _battle.Path)
            {
                var water = Shapes.Create(PrimitiveType.Cube, Palette.Water, Shapes.ToWorld(tile, 0.05f), new Vector3(1f, 0.08f, 1f), _root, "Flood");
                var delay = tile.X * 0.01f;
                AddEffect(water, 1.4f, (go, t) =>
                {
                    var height = Mathf.Sin(Mathf.Clamp01(t - delay) * Mathf.PI) * 0.25f;
                    go.transform.localScale = new Vector3(1f, Mathf.Max(0.01f, height), 1f);
                });
            }
        }

        private void SpawnShout()
        {
            var center = _leaderCurrent + Vector3.up * 0.1f;
            var ring = Shapes.Create(PrimitiveType.Cylinder, Palette.Fright, center, Vector3.zero, _root, "Phantom Voice");
            var diameter = (float)BalanceTable.Default.Leader.VoiceRadius.ToDouble() * 2f;
            AddEffect(ring, 0.6f, (go, t) => go.transform.localScale = new Vector3(diameter * t, 0.01f, diameter * t));
        }

        private void SpawnHymnal(Vector3 from, Vector3 to)
        {
            var book = Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#3b2a1c"), from, new Vector3(0.16f, 0.06f, 0.2f), _root, "Hymnal");
            var start = from + Vector3.up * 0.6f;
            var end = to + Vector3.up * 0.8f;
            AddEffect(book, 0.45f, (go, t) =>
            {
                go.transform.localPosition = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.9f;
                go.transform.localRotation = Quaternion.Euler(t * 720f, t * 200f, 0f);
            });
        }

        private void SpawnHotStones(Vector3 from, Vector3 to)
        {
            var radius = (float)BalanceTable.Default.HotStones.Radius.ToDouble();
            for (var i = 0; i < 4; i++)
            {
                var offset = Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward * (i == 0 ? 0f : 0.45f);
                var stone = Shapes.Create(PrimitiveType.Sphere, Palette.HotStone, from, Vector3.one * 0.2f, _root, "Hot Stone");
                var start = from + Vector3.up * 1.1f;
                var end = to + offset + Vector3.up * 0.1f;
                var delay = i * 0.06f;
                AddEffect(stone, 0.55f + delay, (go, t) =>
                {
                    var k = Mathf.Clamp01((t * (0.55f + delay) - delay) / 0.55f);
                    go.transform.localPosition = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.8f;
                });
            }

            var steam = Shapes.Create(PrimitiveType.Sphere, Palette.Steam, to + Vector3.up * 0.2f, Vector3.zero, _root, "Steam");
            AddEffect(steam, 1.1f, (go, t) =>
            {
                var k = Mathf.Clamp01((t - 0.45f) / 0.55f);
                go.SetActive(k > 0f);
                go.transform.localScale = new Vector3(radius * 2f * Mathf.Sqrt(k), 0.6f * (1f - k), radius * 2f * Mathf.Sqrt(k));
            });
        }

        private Transform CreateReachRing(float radius)
        {
            var ring = new GameObject("Hot Stones Reach").transform;
            ring.SetParent(_leader, false);
            const int pebbles = 40;
            for (var i = 0; i < pebbles; i++)
            {
                var angle = i * Mathf.PI * 2f / pebbles;
                Shapes.Create(PrimitiveType.Sphere, Palette.HotStone, new Vector3(Mathf.Cos(angle) * radius, 0.06f, Mathf.Sin(angle) * radius),
                    Vector3.one * 0.12f, ring, "Pebble");
            }

            return ring;
        }

        private void SpawnPoof(Vector3 at)
        {
            var poof = Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#c9bfae"), at + Vector3.up * 0.3f, Vector3.zero, _root, "Poof");
            AddEffect(poof, 0.4f, (go, t) => go.transform.localScale = Vector3.one * (1f - t) * 0.7f * Mathf.Sqrt(t));
        }

        private void AddEffect(GameObject target, float lifetime, Action<GameObject, float> update) =>
            _effects.Add(new Effect { Target = target, Lifetime = lifetime, Update = update });

        private sealed class EnemyVisual
        {
            public Transform Root;
            public Renderer[] Renderers;
            public PartAnimator Animator;
            public Transform HealthBar;
            public Renderer HealthBarRenderer;
            public Color BaseColor;
            public Vector3 Previous;
            public Vector3 Current;
            public float Health;
            public bool Muddied;
            public bool Frightened;
            public bool Fighting;
            public float Phase;
        }

        private sealed class Effect
        {
            public GameObject Target;
            public float Age;
            public float Lifetime;
            public Action<GameObject, float> Update;
        }
    }
}
