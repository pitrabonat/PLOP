using System;
using System.Collections.Generic;
using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Visual representation of a board in "Soft Toy Minimalism": a rounded container with a soft shadow,
    /// rounded floor tiles, pebble walls, pillow Nests and jelly Plops — all sprites from one
    /// <see cref="ShapeAtlas"/>. Purely reactive: rebuilt from a level/state and animated from
    /// <see cref="GameEvent"/>s; it never changes gameplay state. One world unit = one grid cell.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float NestPulseDuration = 0.35f;
        private const int NestSealOrder = 1000;
        private static readonly Vector3 NestSealOffset = new Vector3(0.325f, 0.315f, 0f);
        private static readonly Color WarmIvoryTint = new Color(0.990f, 0.979f, 0.966f);

        private readonly List<GameObject> _static = new List<GameObject>();
        private readonly List<PlopView> _plops = new List<PlopView>();
        private readonly List<(SpriteRenderer Renderer, Color Shade)> _tiles = new List<(SpriteRenderer, Color)>();
        private readonly List<SpriteRenderer> _pebbles = new List<SpriteRenderer>();
        private readonly Dictionary<int, Transform> _nestByCell = new Dictionary<int, Transform>();
        private Transform[] _pulseNest = new Transform[8];
        private float[] _pulseT0 = new float[8];
        private LevelDefinition _level;
        private ShapeAtlas _atlas;
        private SpriteRenderer _tray;
        private Color _trayTint = Color.white, _tileTint = Color.white, _pebbleTint = Color.white;
        private bool _reducedMotion, _batterySaver;
        private MoveEffects _effects;

        // ---- Phase 10: forced-movement playback and tile feedback ----
        private enum StepKind : byte { Slide, Push, Teleport, Settle, Burn, PortalBlocked, None }
        private readonly struct Step
        {
            public readonly StepKind Kind;
            public readonly GridPosition From, To;
            public readonly Direction Direction;
            public Step(StepKind kind, GridPosition from, GridPosition to, Direction direction) { Kind = kind; From = from; To = to; Direction = direction; }
        }
        private readonly List<Queue<Step>> _queues = new List<Queue<Step>>();
        private readonly List<StepKind> _lastStep = new List<StepKind>();
        private readonly List<Direction> _lastStepDirection = new List<Direction>();
        public const float BurnRevealDelay = 0.18f;

        // ---- Phase 11.5: procedural mechanic surfaces, fans, portals and trace feedback ----
        private BoardEnvironment _env;
        private bool _visible = true;
        internal BoardEnvironment Environment => _env;
        /// <summary>Mechanic sound cues, raised when each step of the trace is shown.</summary>
        internal event Action<BoardEnvironment.Cue> TraceSound;

        /// <summary>True while any Plop still has forced-movement steps to show.</summary>
        public bool HasQueuedSteps
        {
            get { foreach (var q in _queues) if (q.Count > 0) return true; return false; }
        }

        /// <summary>True while any Plop is still travelling/bumping (logical positions not yet reached visually).</summary>
        public bool IsAnimating
        {
            get { foreach (var p in _plops) if (p.IsMoving) return true; return false; }
        }

        /// <summary>True only while a Plop travels to its target cell, including anticipation.</summary>
        public bool IsTraveling
        {
            get
            {
                foreach (var p in _plops) if (p.IsSliding) return true;
                return HasQueuedSteps;
            }
        }

        /// <summary>True while any non-idle animation runs (landing wobble, sleep, hop, nest pulse).</summary>
        public bool IsBusy
        {
            get
            {
                float now = Time.unscaledTime;
                foreach (var p in _plops) if (p.IsBusy(now)) return true;
                for (int i = 0; i < _pulseNest.Length; i++) if (_pulseNest[i] != null) return true;
                if (HasQueuedSteps || (_env != null && _env.HasActiveFeedback)) return true;
                return false;
            }
        }

        public LevelDefinition Level => _level;
        public ShapeAtlas Atlas => _atlas;
        public int PixelsPerUnitBucket => _atlas?.PixelsPerUnit ?? 0;
        public bool ReducedMotion => _reducedMotion;
        public CharacterLook Look => _atlas?.Look;
        internal MoveEffects Effects => _effects;
        internal PlopView PlopViewAt(int plopId) => _plops[plopId];
        public int PlopCount => _plops.Count;

        /// <summary>Battery Saver (30 FPS) halves cosmetic move effects.</summary>
        public void SetBatterySaver(bool saver)
        {
            _batterySaver = saver;
            if (_effects != null) _effects.BatterySaver = saver;
            _env?.SetBatterySaver(saver);
        }

        /// <summary>False while the board is covered (main menu, Studio) or the app is in the background:
        /// ambient fan, lava and portal motion pause; gameplay feedback is unaffected.</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            _env?.SetVisible(visible);
        }

        public bool Visible => _visible;

        private WorldTheme _world;
        private BoardMaterialProfile _materials;
        private ShapeAtlas _surfaces;
        private bool _surfacesPending;

        /// <summary>The World whose board materials dress the tray, floor and pebbles (see BoardMaterials).</summary>
        public void SetWorld(WorldTheme world)
        {
            _world = world;
            var profile = BoardMaterials.For(world);
            PlopShaders.SetWindTint(profile != null ? profile.Wind.WithAlpha(PlopShaders.DefaultWindTint.a) : PlopShaders.DefaultWindTint);
            if (profile == _materials) return;
            _materials = profile;
            // On a live board the new World's surfaces bake in the background and swap in when ready.
            RequestSurfaces(synchronous: !Application.isPlaying);
        }

        /// <summary>The World board-material profile in use, or null for the default Cozy materials.</summary>
        public BoardMaterialProfile Materials => _materials;
        /// <summary>True when the tray, tiles and pebbles show the current World's own materials.</summary>
        public bool ShowsWorldMaterials => SurfacesMatch(_surfaces);
        public bool SurfacesPending => _surfacesPending;

        private bool SurfacesMatch(ShapeAtlas s) => s != null && _materials != null && _atlas != null
            && s.WorldId == _materials.Id && s.PixelsPerUnit == _atlas.PixelsPerUnit;

        private void RequestSurfaces(bool synchronous)
        {
            _surfacesPending = false;
            if (_materials != null && _atlas != null && !SurfacesMatch(_surfaces))
            {
                var baked = ShapeAtlas.WorldSurfaces(_materials, _atlas.PixelsPerUnit, synchronous);
                if (baked == null) { _surfacesPending = true; return; }
                _surfaces = baked;
            }
            ApplySurfaces();
        }

        /// <summary>Points the tray, tile and pebble renderers at the World atlas (or back at the shared one).</summary>
        private void ApplySurfaces()
        {
            if (_atlas == null) return;
            var src = SurfacesMatch(_surfaces) ? _surfaces : _atlas;
            if (_tray != null) _tray.sprite = src.Get("panel");
            foreach (var tile in _tiles) tile.Renderer.sprite = src.Get("tile");
            foreach (var pebble in _pebbles) pebble.sprite = src.Get("pebble");
        }

        /// <summary>Applies the current World's surface colors on the shared board atlas.</summary>
        public void SetPalette(Color trayTint, Color tileTint, Color pebbleTint)
        {
            _trayTint = trayTint.WithAlpha(1f);
            _tileTint = tileTint.WithAlpha(1f);
            _pebbleTint = pebbleTint.WithAlpha(1f);
            if (_tray != null) _tray.color = _trayTint;
            foreach (var tile in _tiles) tile.Renderer.color = tile.Shade * _tileTint;
            foreach (var pebble in _pebbles) pebble.color = _pebbleTint;
        }

        public void SetReducedMotion(bool reduced)
        {
            if (_reducedMotion == reduced) return;
            _reducedMotion = reduced;
            foreach (var plop in _plops)
            {
                plop.SetReducedMotion(reduced);
                if (!reduced) continue;
                int row = Mathf.RoundToInt(plop.Target.y + (_level.Height - 1) * 0.5f);
                plop.SetSortingBase(RowOrder(row));
            }
            if (reduced) ClearNestPulses();
            _env?.SetReducedMotion(reduced);
            if (_effects != null)
            {
                _effects.Enabled = !reduced;
                if (reduced) _effects.Clear();
            }
        }

        public Vector3 CellToLocal(GridPosition p) =>
            new Vector3(p.X - (_level.Width - 1) * 0.5f, p.Y - (_level.Height - 1) * 0.5f, 0f);

        public Vector3 CellToWorld(GridPosition p) => transform.TransformPoint(CellToLocal(p));

        public Vector3 PlopViewPosition(int plopId) => _plops[plopId].Root.position;

        private int RowOrder(int row) => (_level.Height - 1 - row) * 20;

        public void Build(LevelDefinition level, GameState state, ShapeAtlas atlas)
        {
            foreach (var go in _static) Destroy(go);
            foreach (var p in _plops) Destroy(p.Root.gameObject);
            _static.Clear();
            _plops.Clear();
            _queues.Clear();
            _lastStep.Clear();
            _lastStepDirection.Clear();
            _tiles.Clear();
            _pebbles.Clear();
            _nestByCell.Clear();
            for (int i = 0; i < _pulseNest.Length; i++) _pulseNest[i] = null;
            _level = level;
            _atlas = atlas;

            // ---- rounded container + soft shadow (9-sliced so the corner radius stays constant) ----
            float pad = BoardLayout.PanelPadding;
            var panelSize = new Vector2(level.Width + 2 * pad, level.Height + 2 * pad);
            var shadow = Sliced("BoardShadow", atlas.Get("panelShadow"), panelSize + Vector2.one * (2 * ShapeAtlas.PanelShadowBlur), -1000);
            shadow.transform.localPosition = new Vector3(0.045f, -0.10f, 0f);
            shadow.color = new Color(1f, 1f, 1f, 0.25f);
            _tray = Sliced("BoardPanel", atlas.Get("panel"), panelSize, -990);
            _tray.color = _trayTint;

            // ---- floor tiles, pebbles, nests ----
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    var pos = new GridPosition(x, y);
                    if (level.IsWall(pos))
                    {
                        var pebble = Simple("Pebble " + x + "," + y, atlas.Get("pebble"), RowOrder(y) + 5);
                        pebble.transform.localPosition = CellToLocal(pos);
                        pebble.color = _pebbleTint;
                        _pebbles.Add(pebble);
                    }
                    else
                    {
                        var tile = Simple("Tile " + x + "," + y, atlas.Get("tile"), -980);
                        tile.transform.localPosition = CellToLocal(pos);
                        float variation = ((x * 13 + y * 17 + x * y * 3) % 7) / 6f;
                        Color shade = Color.Lerp(WarmIvoryTint, Color.white, variation);
                        tile.color = shade * _tileTint;
                        _tiles.Add((tile, shade));
                    }
                }
            }

            // ---- mechanic tiles (above floor tiles, below Nests and Plops): see BoardEnvironment ----
            if (_env == null)
            {
                _env = new BoardEnvironment(transform, CellToLocal, PlopLocal, RowOrder);
                _env.Sound += cue => TraceSound?.Invoke(cue);
            }
            _env.Build(level, atlas, Simple, _reducedMotion, _batterySaver);
            _env.SetBatterySaver(_batterySaver);
            _env.SetVisible(_visible);
            // World board materials: wait for a bake already in flight (level load is a hitch point anyway).
            RequestSurfaces(synchronous: true);

            foreach (var nest in level.Nests)
            {
                var bed = Simple("Nest bed " + nest.Color, atlas.Get(ShapeAtlas.NestBedKey), -972);
                bed.transform.localPosition = CellToLocal(nest.Position);
                var n = Simple("Nest " + nest.Color, atlas.Get(ShapeAtlas.NestKey((int)nest.Color)), -970);
                n.transform.localPosition = CellToLocal(nest.Position);
                var seal = Simple("Match seal " + nest.Color, atlas.Get(ShapeAtlas.NestSealKey((int)nest.Color)), NestSealOrder);
                seal.transform.SetParent(n.transform, false);
                seal.transform.localPosition = NestSealOffset;
                _nestByCell[level.CellIndex(nest.Position)] = n.transform;
            }

            // ---- Plops (cosmetic look from the atlas; logical colour decides body colour and match mark) ----
            for (int id = 0; id < level.Plops.Count; id++)
            {
                _plops.Add(new PlopView(transform, atlas, (int)level.PlopColorOf(id), id + 1, _reducedMotion));
                _queues.Add(new Queue<Step>());
                _lastStep.Add(StepKind.None);
                _lastStepDirection.Add(Direction.Up);
            }

            var effects = new GameObject("Move effects");
            effects.transform.SetParent(transform, false);
            _static.Add(effects);
            _effects = new MoveEffects(effects.transform, atlas, false) { Enabled = !_reducedMotion, BatterySaver = _batterySaver };
            Sync(state);
        }

        /// <summary>Restores the exact snapshot, with optional child-only easing for single-move Undo.</summary>
        public void Sync(GameState state, bool easeRestore = false)
        {
            for (int id = 0; id < _plops.Count; id++)
            {
                var pos = _level.PositionOf(state.CellOf(id));
                _plops[id].Restore(CellToLocal(pos), state.IsAsleep(id), easeRestore);
                _plops[id].SetSortingBase(RowOrder(pos.Y));
            }
            ClearNestPulses();
            _effects?.Clear();
            _env?.Clear();
            foreach (var q in _queues) q.Clear();
            for (int i = 0; i < _lastStep.Count; i++) _lastStep[i] = StepKind.None;
        }

        private Vector3 PlopLocal(int plopId) => transform.InverseTransformPoint(_plops[plopId].Root.position);

        private void ClearNestPulses()
        {
            for (int i = 0; i < _pulseNest.Length; i++)
            {
                if (_pulseNest[i] != null) _pulseNest[i].localScale = Vector3.one;
                _pulseNest[i] = null;
            }
        }

        /// <summary>Animates the events of one command. Starts on the same frame as the input (no delay).</summary>
        public void Play(GameEvent[] events)
        {
            float now = Time.unscaledTime;
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case GameEventType.PlopMoved:
                    {
                        var view = _plops[e.PlopId];
                        _lastStep[e.PlopId] = StepKind.None;
                        view.StartMove(CellToLocal(e.To), Delta(e.Direction), now);
                        if (_effects != null && _effects.Enabled)
                        {
                            var tint = _atlas.Look?.Palette.Toy((int)_level.PlopColorOf(e.PlopId)) ?? Color.white;
                            int order = RowOrder(e.From.Y) + (_effects.Kind == EffectKind.Sparkle ? 15 : 1);
                            _effects.Spawn(CellToLocal(e.From), Delta(e.Direction), tint, now, order);
                        }
                        view.SetSortingBase(RowOrder(_reducedMotion ? e.To.Y : Mathf.Min(e.From.Y, e.To.Y)));
                        break;
                    }
                    case GameEventType.PlopBlocked:
                        if (e.BlockReason == BlockReason.PortalExit)
                        {
                            Enqueue(e.PlopId, new Step(StepKind.PortalBlocked, e.To, e.To, e.Direction), now);
                            break;
                        }
                        if (_queues.Count > e.PlopId && _queues[e.PlopId].Count > 0) break; // the chain ended against it
                        _plops[e.PlopId].StartBump(Delta(e.Direction), now, e.BlockReason);
                        break;
                    case GameEventType.PlopSettled:
                        if (_queues[e.PlopId].Count > 0) _queues[e.PlopId].Enqueue(new Step(StepKind.Settle, e.To, e.To, e.Direction));
                        else _plops[e.PlopId].QueueSleep(now);
                        break;
                    case GameEventType.PlopSlid:
                        Enqueue(e.PlopId, new Step(StepKind.Slide, e.From, e.To, e.Direction), now);
                        break;
                    case GameEventType.PlopPushed:
                        Enqueue(e.PlopId, new Step(StepKind.Push, e.From, e.To, e.Direction), now);
                        break;
                    case GameEventType.PlopTeleported:
                        Enqueue(e.PlopId, new Step(StepKind.Teleport, e.From, e.To, e.Direction), now);
                        break;
                    case GameEventType.PlopBurned:
                        Enqueue(e.PlopId, new Step(StepKind.Burn, e.To, e.To, e.Direction), now);
                        break;
                    case GameEventType.LevelCompleted:
                        for (int i = 0; i < _plops.Count; i++)
                            _plops[i].StartHop(now + PlopView.CelebrationDelay + i * PlopView.CelebrationStagger);
                        break;
                }
            }
        }

        private void Update()
        {
            if (_level == null) return;
            float now = Time.unscaledTime;
            _env?.Tick(now, Time.unscaledDeltaTime);
            if (_surfacesPending) RequestSurfaces(synchronous: false);
            if (_reducedMotion) return;
            _effects?.Tick(now);
            for (int i = 0; i < _queues.Count; i++)
                if (_queues[i].Count > 0 && !_plops[i].IsSliding) StartStep(i, _queues[i].Dequeue(), now);
            for (int i = 0; i < _plops.Count; i++)
            {
                var p = _plops[i];
                bool wasSliding = p.IsSliding;
                p.Tick(now);
                bool landed = wasSliding && !p.IsSliding;
                if (!landed && !p.SleepJustStarted) continue;

                int row = Mathf.RoundToInt(p.Target.y + (_level.Height - 1) * 0.5f);
                if (landed) p.SetSortingBase(RowOrder(row));
                if (landed && _lastStep[i] == StepKind.Slide && _queues[i].Count == 0)
                {
                    _env?.OnSlideStopped(i, _lastStepDirection[i], now);
                    _lastStep[i] = StepKind.None;
                }
                if (p.SleepJustStarted)
                {
                    int column = Mathf.RoundToInt(p.Target.x + (_level.Width - 1) * 0.5f);
                    if (_nestByCell.TryGetValue(_level.CellIndex(column, row), out var nest)) StartPulse(nest, now);
                }
            }
            for (int i = 0; i < _pulseNest.Length; i++)
            {
                if (_pulseNest[i] == null) continue;
                float u = (now - _pulseT0[i]) / NestPulseDuration;
                if (u >= 1f) { _pulseNest[i].localScale = Vector3.one; _pulseNest[i] = null; continue; }
                float pulse = Mathf.Sin(Mathf.PI * u);
                _pulseNest[i].localScale = new Vector3(1f + 0.049f * pulse, 1f + 0.023f * pulse, 1f);
            }
        }

        private void Enqueue(int plopId, Step step, float now)
        {
            if (_reducedMotion)
            {
                StartStep(plopId, step, now); // Reduced Motion: every step lands at once
                return;
            }
            _queues[plopId].Enqueue(step);
        }

        /// <summary>Plays one forced-movement step. Logic already happened; this only shows it.</summary>
        private void StartStep(int plopId, Step step, float now)
        {
            var view = _plops[plopId];
            int cells = Mathf.Abs(step.To.X - step.From.X) + Mathf.Abs(step.To.Y - step.From.Y);
            int behind = _queues[plopId].Count;
            _lastStep[plopId] = step.Kind;
            _lastStepDirection[plopId] = step.Direction;
            switch (step.Kind)
            {
                case StepKind.Slide:
                case StepKind.Push:
                {
                    bool slide = step.Kind == StepKind.Slide;
                    float duration = slide ? TraceTiming.Slide(cells, behind) : TraceTiming.Push(cells, behind);
                    view.StartMove(CellToLocal(step.To), Delta(step.Direction), now, duration, false);
                    view.SetSortingBase(RowOrder(Mathf.Min(step.From.Y, step.To.Y)));
                    if (slide) _env?.OnSlide(plopId, step.From, step.To, step.Direction, duration, now);
                    else _env?.OnPush(plopId, step.From, step.Direction, duration, now);
                    break;
                }
                case StepKind.Teleport:
                {
                    float duration = TraceTiming.Teleport(behind);
                    view.StartWarp(CellToLocal(step.To), now, duration);
                    view.SetSortingBase(RowOrder(step.To.Y));
                    view.React(Reaction.Surprised, now);
                    _env?.OnTeleport(plopId, step.From, step.To, duration, now);
                    break;
                }
                case StepKind.PortalBlocked:
                    view.React(Reaction.Wobble, now);
                    _env?.OnPortalBlocked(step.To, now);
                    break;
                case StepKind.Settle:
                    view.QueueSleep(now);
                    break;
                case StepKind.Burn:
                    view.SetExpression(Expression.Surprised);
                    view.StartBump(Vector2.up, now, BlockReason.Wall);
                    _env?.OnBurn(plopId, step.To, now);
                    break;
            }
        }

        private void StartPulse(Transform nest, float now)
        {
            for (int i = 0; i < _pulseNest.Length; i++)
            {
                if (_pulseNest[i] != null && _pulseNest[i] != nest) continue;
                _pulseNest[i] = nest;
                _pulseT0[i] = now;
                return;
            }
        }

        private static Vector2 Delta(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return Vector2.up;
                case Direction.Down: return Vector2.down;
                case Direction.Left: return Vector2.left;
                default: return Vector2.right;
            }
        }

        private SpriteRenderer Simple(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            _static.Add(go);
            return r;
        }

        private SpriteRenderer Sliced(string name, Sprite sprite, Vector2 size, int order)
        {
            var r = Simple(name, sprite, order);
            r.drawMode = SpriteDrawMode.Sliced;
            r.size = size;
            return r;
        }
    }
}
