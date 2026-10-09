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
        private Vector3 _leaderPrevious;
        private Vector3 _leaderCurrent;
        private float _umbrellaSwing;
        private float _shake;
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
            Shapes.Create(PrimitiveType.Capsule, Palette.Linen, new Vector3(0f, 0.45f, 0f), new Vector3(0.38f, 0.45f, 0.38f), _leader, "Linen Shirt");
            Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#e0b48f"), new Vector3(0f, 1.0f, 0f), Vector3.one * 0.28f, _leader, "Tired Face");
            Shapes.Create(PrimitiveType.Cylinder, Palette.Highlight, new Vector3(0f, 0.01f, 0f), new Vector3(0.75f, 0.01f, 0.75f), _leader, "Halo (ironic)");
            var umbrellaPivot = new GameObject("Umbrella Pivot").transform;
            umbrellaPivot.SetParent(_leader, false);
            umbrellaPivot.localPosition = new Vector3(0.25f, 0.6f, 0f);
            Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#1d1d24"), new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.3f, 0.05f), umbrellaPivot, "Umbrella (2024)");
            _umbrella = umbrellaPivot;

            _leaderCurrent = _leaderPrevious = Shapes.ToWorld(battle.Leader.Position);
            _leader.localPosition = _leaderCurrent;
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
                var heading = visual.Current - visual.Previous;
                visual.Root.localPosition = position + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.time * 12f + visual.Phase)) * 0.06f);
                if (heading.sqrMagnitude > 0.00001f)
                {
                    visual.Root.localRotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
                }

                var tint = visual.Frightened ? Palette.Fright : visual.Muddied ? Color.Lerp(visual.BaseColor, Palette.Mud, 0.6f) : visual.BaseColor;
                Shapes.Tint(visual.Body, tint, _block);
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

            _umbrellaSwing = Mathf.MoveTowards(_umbrellaSwing, 0f, Time.deltaTime * 4f);
            _umbrella.localRotation = Quaternion.Euler(_umbrellaSwing * 110f, 0f, 0f);

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
                    _umbrellaSwing = 1f;
                    break;
                case InterventionUsed { Type: PlayerActionType.OpenSluiceGate } used:
                    SpawnFlood();
                    Announcement?.Invoke($"Sluice gate opened. {used.EnemiesAffected} soldiers rediscover mud at speed.");
                    break;
                case InterventionUsed { Type: PlayerActionType.PhantomVoice } used:
                    SpawnShout();
                    Announcement?.Invoke(used.EnemiesAffected == 0
                        ? "He shouted into a tin can at nobody. The cultists exchanged looks."
                        : $"A disembodied voice told {used.EnemiesAffected} soldiers to go home. They did.");
                    break;
                case InputRejected rejected:
                    Announcement?.Invoke(rejected.Reason);
                    break;
                case WaveStarted wave:
                    Announcement?.Invoke($"Wave {wave.Wave} of {_battle.WaveCount}. Lord Wulfric remains unconvinced.");
                    break;
                case HearthDamaged damaged:
                    _shake = 0.25f;
                    Announcement?.Invoke(damaged.Type == EnemyType.ManAtArms
                        ? $"A man-at-arms dented the Hearth. −{damaged.Damage}. Rude."
                        : $"A levy kicked the Hearth. −{damaged.Damage} warmth.");
                    break;
                case EnemyDefeated defeated when _enemies.TryGetValue(defeated.EnemyId, out var dead):
                    SpawnPoof(dead.Current);
                    break;
            }
        }

        private EnemyVisual CreateEnemy(Enemy enemy)
        {
            var heavy = enemy.Type == EnemyType.ManAtArms;
            var color = heavy ? Palette.ManAtArms : Palette.Levy;
            var root = new GameObject($"{enemy.Type} #{enemy.Id}").transform;
            root.SetParent(_root, false);
            var size = heavy ? 0.42f : 0.3f;
            var body = Shapes.Create(PrimitiveType.Capsule, color, new Vector3(0f, size, 0f), new Vector3(size, size, size), root, "Body");
            if (heavy)
            {
                Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#6c747b"), new Vector3(0f, size * 2.1f, 0f), Vector3.one * size * 0.75f, root, "Helmet");
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
                Body = body.GetComponent<Renderer>(),
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
            public Renderer Body;
            public Transform HealthBar;
            public Renderer HealthBarRenderer;
            public Color BaseColor;
            public Vector3 Previous;
            public Vector3 Current;
            public float Health;
            public bool Muddied;
            public bool Frightened;
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
