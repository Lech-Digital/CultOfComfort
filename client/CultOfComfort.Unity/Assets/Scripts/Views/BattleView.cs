using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>Raiders, the Prophet and his cultists stand this much bigger at Night than a tile would make them.</summary>
        public const float UnitScale = 1.6f;

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
        private readonly HashSet<EnemyType> _announced = new HashSet<EnemyType>();
        private Dictionary<int, Tower> _towers;

        private Camera _camera;

        public event Action<string> Announcement;

        /// <summary>A short note to float over a raider: world position, text (spec: Floating notes).</summary>
        public event Action<Vector3, string> Floater;

        /// <summary>A skill was actually used (never for a rejected order): drives the first-use tips.</summary>
        public event Action<PlayerActionType> SkillUsed;

        /// <summary>A village structure was hit: id, health fraction left, wrecked.</summary>
        public event Action<int, float, bool> StructureDamaged;

        // Unity objects must not be created in field initializers or constructors — Unity owns component construction.
        private void Awake() => _block = new MaterialPropertyBlock();

        // Fades every frame, not only while a Night renders: a Night that ends on a Hearth hit must not leave the camera shaking.
        private void Update() => _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * 0.8f);

        public float CameraShake => _shake;

        public void Begin(Battle battle)
        {
            Clear();
            _battle = battle;
            _camera = Camera.main;
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
                model.transform.localScale *= UnitScale;
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
            _torchGlow = CreateTorchGlow();

            _leaderCurrent = _leaderPrevious = Shapes.ToWorld(battle.Leader.Position);
            _leader.localPosition = _leaderCurrent;
            foreach (var defender in battle.Defenders)
            {
                _defenders[defender.Id] = CreateDefender(defender);
            }

            foreach (var tower in battle.Towers.Where(t => t.Manned))
            {
                CreateWarden(tower);
            }
        }

        /// <summary>A Guardhouse warden up on a manned tower (spec 2026-10-20 defence): it fires faster while they're there.</summary>
        private void CreateWarden(Tower tower)
        {
            var root = new GameObject($"Warden on tower #{tower.Id}").transform;
            root.SetParent(_root, false);
            root.localPosition = Shapes.ToWorld(tower.Position) + new Vector3(0.28f, 0.55f, -0.28f);
            var model = ArtLibrary.Instantiate("cultist", root);
            if (model != null)
            {
                model.transform.localScale *= UnitScale * 0.75f;
            }
            else
            {
                Shapes.Create(PrimitiveType.Capsule, Shapes.Hex("#46618c"), new Vector3(0f, 0.3f, 0f), new Vector3(0.22f, 0.3f, 0.22f), root, "Warden");
            }
        }

        // ---------------- the cultists out defending (spec 2026-10-15) ----------------

        private readonly Dictionary<int, DefenderVisual> _defenders = new Dictionary<int, DefenderVisual>();

        private sealed class DefenderVisual
        {
            public Transform Root;
            public PartAnimator Animator;
            public Transform HealthBar;
            public Vector3 Previous;
            public Vector3 Current;
            public float Health = 1f;
            public bool Down;
            public int TargetId = -1;
        }

        private DefenderVisual CreateDefender(Defender defender)
        {
            var root = new GameObject($"Cultist #{defender.Id}").transform;
            root.SetParent(_root, false);
            var at = Shapes.ToWorld(defender.Position);
            root.localPosition = at;
            var model = ArtLibrary.Instantiate("cultist", root);
            PartAnimator animator = null;
            if (model != null)
            {
                model.transform.localScale *= UnitScale;
                animator = model.AddComponent<PartAnimator>();
                animator.Bind();
            }
            else
            {
                Shapes.Create(PrimitiveType.Capsule, Shapes.Hex("#46618c"), new Vector3(0f, 0.4f, 0f), new Vector3(0.3f, 0.4f, 0.3f), root, "Cultist");
            }

            // A woad sash ring at their feet: ours, not theirs.
            Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#46618c"), new Vector3(0f, 0.01f, 0f), new Vector3(0.55f, 0.01f, 0.55f), root, "Ours");
            var bar = Shapes.Create(PrimitiveType.Cube, Palette.HealthGood, new Vector3(0f, 1.05f, 0f), new Vector3(0.5f, 0.05f, 0.05f), root, "Health");
            return new DefenderVisual { Root = root, Animator = animator, HealthBar = bar.transform, Previous = at, Current = at };
        }

        private void SyncDefenders()
        {
            foreach (var defender in _battle.Defenders)
            {
                if (!_defenders.TryGetValue(defender.Id, out var visual))
                {
                    continue;
                }

                visual.Current = Shapes.ToWorld(defender.Position);
                visual.Health = defender.MaxHealth > 0 ? defender.Health / (float)defender.MaxHealth : 0f;
                visual.Down = !defender.IsUp;
                visual.TargetId = defender.TargetId;
            }
        }

        private void RenderDefenders(float alpha)
        {
            foreach (var pair in _defenders)
            {
                var visual = pair.Value;
                var position = Vector3.Lerp(visual.Previous, visual.Current, alpha);
                visual.Root.localPosition = position;
                var moving = (visual.Current - visual.Previous).sqrMagnitude > 0.000001f;
                Vector3 heading = visual.Current - visual.Previous;
                if (!moving && visual.TargetId >= 0 && _enemies.TryGetValue(visual.TargetId, out var target))
                {
                    heading = target.Root.localPosition - position; // squaring up to the raider they're hitting
                }

                heading.y = 0f;
                if (heading.sqrMagnitude > 0.00001f)
                {
                    visual.Root.localRotation = Quaternion.Slerp(visual.Root.localRotation, Quaternion.LookRotation(heading.normalized, Vector3.up), 0.3f);
                }

                if (visual.Animator != null)
                {
                    visual.Animator.IsMoving = moving && !visual.Down;
                    visual.Animator.IsSwooning = visual.Down;
                }

                visual.HealthBar.gameObject.SetActive(!visual.Down && visual.Health < 0.999f);
                visual.HealthBar.localScale = new Vector3(0.5f * visual.Health, 0.05f, 0.05f);
                Shapes.Tint(visual.HealthBar.GetComponent<Renderer>(), visual.Health > 0.4f ? Palette.HealthGood : Palette.HealthBad, _block);
            }
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
        /// <summary>The pebble ring round the Prophet: how far an aimed skill reaches (tiles; default Hot Stones).</summary>
        public void ShowReach(bool visible, float reach = 0f)
        {
            if (_reachRing != null)
            {
                _reachRing.gameObject.SetActive(visible);
                var stones = (float)BalanceTable.Default.HotStones.Reach.ToDouble();
                _reachRing.localScale = Vector3.one * (reach > 0f ? reach / stones : 1f);
            }
        }

        public void Clear()
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }

            _enemies.Clear();
            _defenders.Clear();
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
            foreach (var visual in _defenders.Values)
            {
                visual.Previous = visual.Current;
            }
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
                visual.Slowed = enemy.IsSlowed(_battle.Tick);
                visual.Frightened = enemy.IsFrightened(_battle.Tick);
                visual.Blinded = enemy.IsBlinded(_battle.Tick);
                visual.Burning = enemy.IsBurning(_battle.Tick);
                visual.Fighting = enemy.IsFightingLeader;
            }

            _leaderCurrent = Shapes.ToWorld(_battle.Leader.Position);
            SyncDefenders();
        }

        private static readonly Color LaserRed = Shapes.Hex("#ff2340");
        private static readonly Color TorchLight = Shapes.Hex("#fff1b8");
        private GameObject _torchGlow;

        /// <summary>The dynamo torch's pool of light round the Prophet (shown while it is lit).</summary>
        private GameObject CreateTorchGlow()
        {
            var glow = new GameObject("Dynamo Torch");
            glow.transform.SetParent(_leader, false);
            var diameter = (float)BalanceTable.Default.Relics.TorchRadius.ToDouble() * 2f;
            var disc = Shapes.Create(PrimitiveType.Cylinder, TorchLight, new Vector3(0f, 0.02f, 0f), new Vector3(diameter, 0.005f, diameter), glow.transform, "Torch Pool");
            disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = new GameObject("Torch Light").AddComponent<Light>();
            light.transform.SetParent(glow.transform, false);
            light.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            light.type = LightType.Point;
            light.color = TorchLight;
            light.range = diameter + 1f;
            light.intensity = 6f;
            var torch = ArtLibrary.Instantiate("relic_dynamo_torch", glow.transform);
            if (torch != null)
            {
                torch.transform.localPosition = new Vector3(0.25f, 0.75f, 0.1f);
                torch.transform.localScale = Vector3.one * 1.4f;
            }

            glow.SetActive(false);
            return glow;
        }

        private void SpawnLaser(Vector3 from, Vector3 to)
        {
            var start = from + Vector3.up * 0.75f;
            var end = to + Vector3.up * 0.55f;
            var beam = Shapes.Create(PrimitiveType.Cube, LaserRed, (start + end) / 2f, new Vector3(0.025f, 0.025f, Vector3.Distance(start, end)), _root, "Laser");
            beam.transform.localRotation = Quaternion.LookRotation(end - start, Vector3.up);
            var dot = Shapes.Create(PrimitiveType.Sphere, LaserRed, end, Vector3.one * 0.12f, _root, "Red Dot");
            AddEffect(beam, 0.35f, (go, t) => go.SetActive(Mathf.Repeat(t * 7f, 1f) < 0.8f));
            AddEffect(dot, 1.2f, (go, t) => go.transform.localScale = Vector3.one * (0.12f + Mathf.Sin(t * 30f) * 0.03f) * (1f - t * 0.5f));
        }

        private void SpawnSunbeam(Vector3 at)
        {
            var radius = (float)BalanceTable.Default.Relics.SunFireRadius.ToDouble();
            var beam = Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#ffe27a"), at + Vector3.up * 3f, new Vector3(0.1f, 3f, 0.1f), _root, "Sunbeam");
            beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AddEffect(beam, 0.6f, (go, t) => go.transform.localScale = new Vector3(0.1f + t * radius, 3f * (1f - t), 0.1f + t * radius));
            var scorch = Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#2b2420"), at + Vector3.up * 0.02f, Vector3.zero, _root, "Scorch");
            AddEffect(scorch, 2f, (go, t) => go.transform.localScale = new Vector3(radius * 2f * Mathf.Min(1f, t * 4f), 0.005f, radius * 2f * Mathf.Min(1f, t * 4f)));
        }

        /// <summary>A flicker of fire on a burning raider (made on first need, kept for its life).</summary>
        private static void ShowFlame(EnemyVisual visual, bool burning)
        {
            if (visual.Flame == null)
            {
                if (!burning)
                {
                    return;
                }

                visual.Flame = Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#ff7a1f"), new Vector3(0f, 0.95f, 0f), Vector3.one * 0.3f, visual.Root, "Flame");
                visual.Flame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            visual.Flame.SetActive(burning);
            if (burning)
            {
                var flicker = 0.26f + Mathf.PerlinNoise(Time.time * 9f, visual.Phase) * 0.14f;
                visual.Flame.transform.localScale = new Vector3(flicker, flicker * 1.6f, flicker);
            }
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

                var tint = visual.Blinded ? Color.Lerp(visual.BaseColor, Color.white, 0.55f)
                    : visual.Frightened ? Palette.Fright : visual.Muddied ? Color.Lerp(visual.BaseColor, Palette.Mud, 0.6f) : visual.BaseColor;
                ShowFlame(visual, visual.Burning);
                // Only touch renderers when the tint changes, and drop the property block when it is neutral:
                // a renderer with a block falls out of URP's SRP Batcher, and a raider is ~8 renderers.
                if (tint != visual.LastTint)
                {
                    visual.LastTint = tint;
                    foreach (var renderer in visual.Renderers)
                    {
                        if (tint == visual.BaseColor)
                        {
                            renderer.SetPropertyBlock(null);
                        }
                        else
                        {
                            Shapes.Tint(renderer, tint, _block);
                        }
                    }
                }
                var icons = RaiderStatus.Select(visual.Muddied, visual.Slowed, visual.Frightened, visual.Fighting, visual.Icons);
                for (var k = 0; k < visual.Status.Length; k++)
                {
                    var sprite = k < icons ? IconSprites.Get(visual.Icons[k]) : null;
                    var status = visual.Status[k];
                    status.enabled = sprite != null;
                    if (sprite != null)
                    {
                        status.sprite = sprite;
                        if (_camera != null)
                        {
                            status.transform.rotation = _camera.transform.rotation; // face the camera
                        }
                    }
                }

                visual.HealthBar.localScale = new Vector3(0.6f * visual.Health, 0.06f, 0.06f);
                visual.HealthBar.gameObject.SetActive(visual.Health < 0.999f);
                Shapes.Tint(visual.HealthBarRenderer, visual.Health > 0.4f ? Palette.HealthGood : Palette.HealthBad, _block);
            }

            RenderDefenders(alpha);
            _leader.localPosition = Vector3.Lerp(_leaderPrevious, _leaderCurrent, alpha);
            var leaderHeading = _leaderCurrent - _leaderPrevious;
            if (leaderHeading.sqrMagnitude > 0.00001f)
            {
                _leader.localRotation = Quaternion.LookRotation(leaderHeading.normalized, Vector3.up);
            }

            var leader = _battle.Leader;
            var swooning = leader.IsSwooning(_battle.Tick);
            if (_torchGlow != null && _torchGlow.activeSelf != leader.IsTorchLit(_battle.Tick))
            {
                _torchGlow.SetActive(leader.IsTorchLit(_battle.Tick));
            }
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
        }

        private void HandleEvent(BattleEvent battleEvent)
        {
            if (battleEvent is not TowerFired && battleEvent is not LeaderAttacked)
            {
                FrameRateLogger.Mark(battleEvent is EnemySpawned spawned ? $"EnemySpawned {spawned.Type}" : battleEvent.GetType().Name);
            }

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
                    else if (tower.Type == TowerType.PressureSluice)
                    {
                        SpawnWaterJet(from, to);
                    }
                    else
                    {
                        SpawnRock(from, to, fired.Critical);
                    }

                    break;
                case DefenderAttacked hit:
                    if (_defenders.TryGetValue(hit.DefenderId, out var striker))
                    {
                        striker.Animator?.Attack();
                    }

                    break;
                case DefenderBack back:
                    if (_defenders.TryGetValue(back.DefenderId, out var patched))
                    {
                        SpawnPoof(patched.Current); // out of the hut again, bandaged
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
                case StructureHit hit:
                {
                    var structure = _battle.Structures.First(s => s.Id == hit.StructureId);
                    Floater?.Invoke(Shapes.ToWorld(structure.Position) + Vector3.up * 1.4f, "SMASH!");
                    StructureDamaged?.Invoke(structure.Id, structure.MaxHealth > 0 ? hit.HealthRemaining / (float)structure.MaxHealth : 0f, false);
                    break;
                }

                case StructureWrecked wrecked:
                {
                    var structure = _battle.Structures.First(s => s.Id == wrecked.StructureId);
                    var at = Shapes.ToWorld(structure.Position) + Vector3.up * 0.5f;
                    var poof = Fx("fx_poof", at) ?? Shapes.Create(PrimitiveType.Sphere, Palette.Linen, at, Vector3.zero, _root, "Wreck");
                    AddEffect(poof, 0.6f, (go, t) => go.transform.localScale = Vector3.one * (1f - t) * 3f * Mathf.Sqrt(t));
                    StructureDamaged?.Invoke(structure.Id, 0f, true);
                    break;
                }

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
                    var stones = BalanceTable.Default.HotStones.Radius;
                    var centre = thrown.Target.ToVector();
                    FloatOver(e => _battle.PositionOf(e).DistanceSquaredTo(centre) <= stones * stones, "ouch!");
                    Announcement?.Invoke(thrown.EnemiesAffected == 0
                        ? "Hot stones, thrown at nothing. The grass is now exfoliated."
                        : $"Hot Stone Therapy for {thrown.EnemiesAffected}. Unrequested, unrefundable.");
                    SkillUsed?.Invoke(PlayerActionType.HotStones); // after the announcement, so a first-use tip wins the toast
                    break;
                case EnemySpawned spawned when FirstSighting(spawned.Type) is string sighting && _announced.Add(spawned.Type):
                    Announcement?.Invoke(sighting);
                    break;
                case InterventionUsed { Type: PlayerActionType.OpenSluiceGate } used:
                    SpawnFlood();
                    FloatOver(_ => true, "washed back!");
                    Announcement?.Invoke($"Sluice gate opened. {used.EnemiesAffected} soldiers rediscover mud at speed.");
                    SkillUsed?.Invoke(PlayerActionType.OpenSluiceGate); // after the announcement, so a first-use tip wins the toast
                    break;
                case InterventionUsed { Type: PlayerActionType.PhantomVoice } used:
                    if (used.EnemiesAffected == 0)
                    {
                        _prophet?.Unimpressed();
                    }

                    SpawnShout();
                    FloatOver(e => e.IsFrightened(_battle.Tick), "?!");
                    Announcement?.Invoke(used.EnemiesAffected == 0
                        ? "He shouted into a tin can at nobody. The cultists exchanged looks."
                        : $"A disembodied voice told {used.EnemiesAffected} soldiers to go home. They did.");
                    SkillUsed?.Invoke(PlayerActionType.PhantomVoice); // after the announcement, so a first-use tip wins the toast
                    break;
                case RelicUsed { Type: PlayerActionType.LaserPointer } laser when _enemies.TryGetValue(laser.TargetId, out var dazzled):
                    SpawnLaser(_leaderCurrent, dazzled.Current);
                    FloatOver(e => e.Id == laser.TargetId, "my eyes!");
                    Announcement?.Invoke("The Finger of God. That raider now sees only a red dot, and God.");
                    SkillUsed?.Invoke(PlayerActionType.LaserPointer);
                    break;
                case RelicUsed { Type: PlayerActionType.DynamoTorch }:
                    Announcement?.Invoke("He cranks the torch. Light without fire. The raiders find this deeply rude.");
                    SkillUsed?.Invoke(PlayerActionType.DynamoTorch);
                    break;
                case RelicUsed { Type: PlayerActionType.SunFire } fire:
                    SpawnSunbeam(Shapes.ToWorld(fire.Target));
                    if (fire.EnemiesAffected == 0)
                    {
                        _prophet?.Unimpressed();
                    }

                    FloatOver(e => e.IsBurning(_battle.Tick), "hot! hot!");
                    Announcement?.Invoke(fire.EnemiesAffected == 0
                        ? "He set some grass on fire. On purpose, he says."
                        : $"Sun-fire! {fire.EnemiesAffected} raiders learn about lenses the hard way.");
                    SkillUsed?.Invoke(PlayerActionType.SunFire);
                    break;
                case InputRejected rejected:
                    _prophet?.Annoyed(); // he takes refusals personally
                    Announcement?.Invoke(rejected.Reason);
                    break;
                case WaveStarted wave:
                    Announcement?.Invoke($"Wave {wave.Wave} of {_battle.WaveCount}. {Ruler.Cap(Ruler.Full)} remains unconvinced.");
                    break;
                case HearthDamaged damaged:
                    _prophet?.Unimpressed();
                    _shake = 0.25f;
                    Announcement?.Invoke(damaged.Type switch
                    {
                        EnemyType.ManAtArms => $"A man-at-arms dented the Hearth. −{damaged.Damage}. Rude.",
                        EnemyType.Zealot => $"A zealot blessed the Hearth. Aggressively. −{damaged.Damage}.",
                        EnemyType.Goose => $"A goose pecked the Hearth. −{damaged.Damage}. It looks pleased with itself.",
                        EnemyType.Goat => $"A goat headbutted the Hearth. −{damaged.Damage}. Then ate a towel.",
                        EnemyType.Boar => $"A boar rammed the Hearth. −{damaged.Damage}. Snout first.",
                        EnemyType.Peasant => $"A peasant fiddled with the Hearth. −{damaged.Damage}. \"Just looking!\"",
                        EnemyType.Priest => $"A priest sprinkled cold water on the Hearth. −{damaged.Damage}. Sacrilege.",
                        EnemyType.Lord => $"{Ruler.Cap(Ruler.Full)} kicked the Hearth personally. −{damaged.Damage}. Very undignified.",
                        EnemyType.Bishop => $"The Bishop excommunicated the Hearth. −{damaged.Damage}. It took it personally.",
                        _ => $"A levy kicked the Hearth. −{damaged.Damage} warmth.",
                    });
                    break;
                case EnemyDefeated defeated when _enemies.TryGetValue(defeated.EnemyId, out var dead):
                    SpawnPoof(dead.Current);
                    break;
            }
        }

        /// <summary>Which model a raider wears. Peasants alternate between two villagers so a crowd doesn't look cloned.</summary>
        public static string EnemyModel(EnemyType type, int id) => type switch
        {
            EnemyType.ManAtArms => "manatarms",
            EnemyType.Zealot => "zealot",
            EnemyType.Goose => "goose",
            EnemyType.Goat => "goat",
            EnemyType.Boar => "pig",
            EnemyType.Peasant => id % 2 == 0 ? "villager_woman" : "herbwife",
            EnemyType.Priest => "friar_odo",
            EnemyType.Bishop => "bishop",
            EnemyType.Lord => "lord_wulfric",
            _ => "levy",
        };

        /// <summary>The toast the first time a raider type shows up in a Night; levies are too ordinary to mention.</summary>
        private static string FirstSighting(EnemyType type) => type switch
        {
            EnemyType.Zealot => "Zealots. Friar Odo's lot. They throw hymnals, and they aim for the heretic.",
            EnemyType.Goose => "A goose. It has chosen violence. Mind the Prophet's ankles.",
            EnemyType.Goat => "Goats. They butt fences and walls on principle.",
            EnemyType.Boar => "A wild boar. Tough, quick, and nobody invited it.",
            EnemyType.Peasant => "Nosy locals. They won't fight; they just poke your buildings to see what happens.",
            EnemyType.Priest => "Priests. Their blessing makes the soldiers around them harder to hurt. Get them first.",
            EnemyType.Lord => "LORD WULFRIC. In person. He is not amused.",
            EnemyType.Bishop => "A Bishop. Slow, sure, and blessing everyone near him. Point the laser at him: he'll take it for a miracle.",
            _ => null,
        };

        private EnemyVisual CreateEnemy(Enemy enemy)
        {
            var model = ArtLibrary.Instantiate(EnemyModel(enemy.Type, enemy.Id), null);
            if (model != null)
            {
                var modelRoot = new GameObject($"{enemy.Type} #{enemy.Id}").transform;
                modelRoot.SetParent(_root, false);
                model.transform.SetParent(modelRoot, false);
                var lord = enemy.Type == EnemyType.Lord;
                model.transform.localScale *= UnitScale; // chunkier than the village's people: they're the show tonight
                if (lord)
                {
                    model.transform.localScale *= 1.35f; // the boss should look like one
                }
                else if (enemy.Type == EnemyType.Bishop)
                {
                    model.transform.localScale *= 1.2f; // the mitre adds the rest
                }

                var animator = model.AddComponent<PartAnimator>();
                animator.Bind();
                var barHeight = (lord ? 1.5f : 1.1f) * UnitScale;
                var modelBar = Shapes.Create(PrimitiveType.Cube, Palette.HealthGood, new Vector3(0f, barHeight, 0f), new Vector3(0.6f, 0.06f, 0.06f), modelRoot, "Health");
                var start = Shapes.ToWorld(_battle.PositionOf(enemy));
                modelRoot.localPosition = start;
                return WithStatus(new EnemyVisual
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
                }, barHeight);
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

            return WithStatus(new EnemyVisual
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
            }, size * 2f + 0.45f);
        }

        private void SpawnRock(Vector3 from, Vector3 to, bool critical)
        {
            var rock = Fx("fx_rock", from) ?? Shapes.Create(PrimitiveType.Sphere, Palette.Stone, from, Vector3.one * 0.16f, _root, "Flung Rock");
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

        /// <summary>The Pressure Sluice: a straight, fast jet (no arc, unlike the Chute's lob) and a wet splash.</summary>
        private void SpawnWaterJet(Vector3 from, Vector3 to)
        {
            var start = from + Vector3.up * 0.4f;
            var jet = Shapes.Create(PrimitiveType.Cylinder, Palette.Water, start, new Vector3(0.08f, 0.01f, 0.08f), _root, "Jet");
            AddEffect(jet, 0.3f, (go, t) =>
            {
                var tip = Vector3.Lerp(start, to + Vector3.up * 0.3f, Mathf.Min(1f, t * 2f));
                var tail = Vector3.Lerp(start, tip, Mathf.Max(0f, t * 2f - 1f));
                go.transform.localPosition = (tip + tail) / 2f;
                go.transform.up = (tip - tail).sqrMagnitude > 0.0001f ? (tip - tail).normalized : Vector3.up;
                go.transform.localScale = new Vector3(0.08f, Mathf.Max(0.01f, (tip - tail).magnitude / 2f), 0.08f);
            });
            var splash = Shapes.Create(PrimitiveType.Sphere, Palette.Water, to + Vector3.up * 0.3f, Vector3.zero, _root, "Splash");
            AddEffect(splash, 0.5f, (go, t) =>
            {
                go.SetActive(t > 0.5f);
                go.transform.localScale = Vector3.one * (t - 0.5f) * 1.4f;
            });
        }

        private void SpawnMudSplat(Vector3 from, Vector3 to)
        {
            var glob = Fx("fx_mud_glob", from) ?? Shapes.Create(PrimitiveType.Sphere, Palette.Mud, from, Vector3.one * 0.25f, _root, "Slurry");
            var start = from + Vector3.up * 1f;
            AddEffect(glob, 0.25f, (go, t) => go.transform.localPosition = Vector3.Lerp(start, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.8f);
            var splatModel = Fx("fx_splat", to + Vector3.up * 0.01f);
            var splat = splatModel ?? Shapes.Create(PrimitiveType.Cylinder, Palette.Mud, to + Vector3.up * 0.02f, Vector3.zero, _root, "Splat");
            AddEffect(splat, 0.9f, (go, t) =>
            {
                var size = t < 0.3f ? t / 0.3f * 2f : 2f;
                go.transform.localScale = splatModel != null ? new Vector3(size * 0.55f, 1f, size * 0.55f) : new Vector3(size, 0.01f, size);
                go.SetActive(t > 0.25f);
            });
        }

        /// <summary>Two billboard icons above the health bar, filled per frame from <see cref="RaiderStatus"/>.</summary>
        private static EnemyVisual WithStatus(EnemyVisual visual, float barHeight)
        {
            for (var k = 0; k < visual.Status.Length; k++)
            {
                var icon = new GameObject($"Status{k}").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(visual.Root, false);
                icon.transform.localPosition = new Vector3(k == 0 ? -0.17f : 0.17f, barHeight + 0.3f, 0f);
                icon.sortingOrder = 10;
                icon.enabled = false;
                visual.Status[k] = icon;
            }

            return visual;
        }

        private void FloatOver(Func<Enemy, bool> affected, string text)
        {
            foreach (var enemy in _battle.Enemies)
            {
                if (enemy.IsActive && affected(enemy) && _enemies.TryGetValue(enemy.Id, out var visual))
                {
                    Floater?.Invoke(visual.Current + Vector3.up * 1.3f, text);
                }
            }
        }

        private const float FloodTravel = 1.3f; // seconds for the crest to run from the Hearth to the spawn
        private const float FloodLinger = 0.45f;
        private const float FloodDrain = 0.7f;
        private static readonly Color FloodWater = Shapes.Hex("#5f97b3");
        private static readonly Color FloodMurk = Shapes.Hex("#7a6a4a");
        private static readonly Color Foam = Shapes.Hex("#eef4f2");

        /// <summary>
        /// The flood: a foaming crest sweeps down the road from the Hearth end towards the spawn, the road floods behind it,
        /// spray flies off the sides, then the water drains away into shrinking puddles.
        /// </summary>
        private void SpawnFlood()
        {
            var path = _battle.Path;
            if (path.Count == 0)
            {
                return;
            }

            _shake = Mathf.Max(_shake, 0.15f);
            var points = path.Select(p => Shapes.ToWorld(p, 0f)).Reverse().ToList(); // Hearth end first
            SpawnCrest(points);
            for (var i = 0; i < points.Count; i++)
            {
                var arrives = points.Count == 1 ? 0f : i / (float)(points.Count - 1) * FloodTravel;
                SpawnFloodTile(points[i], arrives, i);
                if (i % 2 == 0)
                {
                    SpawnSpray(points[i], arrives, i);
                }
            }
        }

        private void SpawnFloodTile(Vector3 at, float arrives, int index)
        {
            var murky = index % 3 == 0;
            var water = Shapes.Create(PrimitiveType.Cube, murky ? FloodMurk : FloodWater, at + Vector3.up * 0.04f, Vector3.zero, _root, "Flood");
            water.SetActive(false);
            var lifetime = arrives + FloodLinger + FloodDrain + 0.25f;
            var wobble = index * 1.3f;
            AddEffect(water, lifetime, (go, t) =>
            {
                var time = t * lifetime - arrives;
                go.SetActive(time > 0f && t < 1f);
                if (time <= 0f)
                {
                    return;
                }

                // Surge up fast, slosh a little, then drain: lower and shrink into a puddle.
                var surge = Mathf.Clamp01(time / 0.18f);
                var drain = Mathf.Clamp01((time - FloodLinger) / FloodDrain);
                var height = Mathf.Lerp(0.02f, 0.16f + Mathf.Sin(time * 14f + wobble) * 0.03f, surge) * (1f - drain);
                var width = Mathf.Lerp(0.6f, 1.08f, surge) * Mathf.Lerp(1f, 0.35f, drain * drain);
                go.transform.localScale = new Vector3(width, Mathf.Max(0.01f, height), width);
                go.transform.localPosition = at + Vector3.up * (height * 0.5f);
            });
        }

        private void SpawnCrest(IReadOnlyList<Vector3> points)
        {
            var crest = new GameObject("Flood Crest");
            crest.transform.SetParent(_root, false);
            var swell = Shapes.Create(PrimitiveType.Capsule, FloodWater, new Vector3(0f, 0.12f, 0f), new Vector3(0.55f, 0.75f, 0.55f), crest.transform, "Swell");
            swell.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // lying across the road
            var foam = new Transform[6];
            for (var k = 0; k < foam.Length; k++)
            {
                foam[k] = Shapes.Create(PrimitiveType.Sphere, Foam, Vector3.zero, Vector3.one * 0.26f, crest.transform, "Foam").transform;
            }

            var lifetime = FloodTravel + 0.2f;
            AddEffect(crest, lifetime, (go, t) =>
            {
                var travel = Mathf.Clamp01(t * lifetime / FloodTravel);
                var (position, forward) = Along(points, travel);
                go.transform.localPosition = position;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    go.transform.localRotation = Quaternion.Slerp(go.transform.localRotation, Quaternion.LookRotation(forward), 0.35f);
                }

                // It rises out of the Hearth, rolls at full height, and collapses into the spawn.
                var size = Mathf.Clamp01(travel / 0.08f) * (1f - Mathf.Clamp01((t * lifetime - FloodTravel) / 0.2f));
                go.transform.localScale = Vector3.one * Mathf.Max(0.001f, size);
                var time = t * lifetime;
                for (var k = 0; k < foam.Length; k++)
                {
                    var across = (k - (foam.Length - 1) * 0.5f) * 0.2f;
                    var churn = Mathf.Sin(time * 18f + k * 2.1f);
                    foam[k].localPosition = new Vector3(across, 0.34f + churn * 0.07f, 0.1f + Mathf.Cos(time * 15f + k) * 0.06f);
                    foam[k].localScale = Vector3.one * (0.2f + (churn + 1f) * 0.05f);
                }
            });
        }

        private void SpawnSpray(Vector3 at, float arrives, int index)
        {
            for (var d = 0; d < 2; d++)
            {
                var drop = Shapes.Create(PrimitiveType.Sphere, d == 0 ? Foam : FloodWater, at, Vector3.one * 0.12f, _root, "Spray");
                drop.SetActive(false);
                var angle = (index * 73f + d * 180f + 40f) % 360f;
                var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (0.7f + (index % 3) * 0.15f);
                var flight = 0.5f;
                var lifetime = arrives + flight;
                AddEffect(drop, lifetime, (go, t) =>
                {
                    var k = (t * lifetime - arrives) / flight;
                    go.SetActive(k > 0f && k < 1f);
                    go.transform.localPosition = at + outward * k + Vector3.up * (0.2f + Mathf.Sin(k * Mathf.PI) * 0.75f);
                });
            }
        }

        /// <summary>A point a fraction <paramref name="u"/> of the way along a polyline (by tile count), and the direction there.</summary>
        private static (Vector3 Position, Vector3 Forward) Along(IReadOnlyList<Vector3> points, float u)
        {
            if (points.Count == 1)
            {
                return (points[0], Vector3.forward);
            }

            var x = Mathf.Clamp01(u) * (points.Count - 1);
            var i = Mathf.Min((int)x, points.Count - 2);
            return (Vector3.Lerp(points[i], points[i + 1], x - i), points[i + 1] - points[i]);
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
            var book = Fx("fx_hymnal", from) ?? Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#3b2a1c"), from, new Vector3(0.16f, 0.06f, 0.2f), _root, "Hymnal");
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
                var stone = Fx("fx_hot_stone", from) ?? Shapes.Create(PrimitiveType.Sphere, Palette.HotStone, from, Vector3.one * 0.2f, _root, "Hot Stone");
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
            var poofModel = Fx("fx_poof", at);
            var poof = poofModel ?? Shapes.Create(PrimitiveType.Sphere, Shapes.Hex("#c9bfae"), at + Vector3.up * 0.3f, Vector3.zero, _root, "Poof");
            var poofSize = poofModel != null ? 2.4f : 0.7f; // the model is ~0.35 m across, the primitive 1 m
            AddEffect(poof, 0.45f, (go, t) => go.transform.localScale = Vector3.one * (1f - t) * poofSize * Mathf.Sqrt(t));
        }

        /// <summary>A pipeline effect model at <paramref name="at"/>, or null so the caller falls back to a primitive.</summary>
        private GameObject Fx(string model, Vector3 at)
        {
            var go = ArtLibrary.Instantiate(model, _root);
            if (go != null)
            {
                go.transform.localPosition = at;
            }

            return go;
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
            public Color LastTint = new Color(-1f, -1f, -1f); // "never tinted": the first frame always applies
            public Vector3 Previous;
            public Vector3 Current;
            public float Health;
            public bool Muddied;
            public bool Slowed;
            public readonly SpriteRenderer[] Status = new SpriteRenderer[RaiderStatus.MaxIcons];
            public readonly string[] Icons = new string[RaiderStatus.MaxIcons];
            public bool Frightened;
            public bool Blinded;
            public bool Burning;
            public GameObject Flame;
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
