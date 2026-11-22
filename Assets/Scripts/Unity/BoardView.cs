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
        private bool _reducedMotion;

        /// <summary>True while any Plop is still travelling/bumping (logical positions not yet reached visually).</summary>
        public bool IsAnimating
        {
            get { foreach (var p in _plops) if (p.IsMoving) return true; return false; }
        }

        /// <summary>True only while a Plop travels to its target cell, including anticipation.</summary>
        public bool IsTraveling
        {
            get { foreach (var p in _plops) if (p.IsSliding) return true; return false; }
        }

        /// <summary>True while any non-idle animation runs (landing wobble, sleep, hop, nest pulse).</summary>
        public bool IsBusy
        {
            get
            {
                float now = Time.unscaledTime;
                foreach (var p in _plops) if (p.IsBusy(now)) return true;
                for (int i = 0; i < _pulseNest.Length; i++) if (_pulseNest[i] != null) return true;
                return false;
            }
        }

        public LevelDefinition Level => _level;
        public ShapeAtlas Atlas => _atlas;
        public int PixelsPerUnitBucket => _atlas?.PixelsPerUnit ?? 0;
        public bool ReducedMotion => _reducedMotion;

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

            foreach (var nest in level.Nests)
            {
                var n = Simple("Nest " + nest.Color, atlas.Get(ShapeAtlas.NestKey((int)nest.Color)), -970);
                n.transform.localPosition = CellToLocal(nest.Position);
                var seal = Simple("Match seal " + nest.Color, atlas.Get(ShapeAtlas.NestSealKey((int)nest.Color)), NestSealOrder);
                seal.transform.SetParent(n.transform, false);
                seal.transform.localPosition = NestSealOffset;
                _nestByCell[level.CellIndex(nest.Position)] = n.transform;
            }

            // ---- Plops ----
            for (int id = 0; id < level.Plops.Count; id++)
                _plops.Add(new PlopView(transform, atlas, (int)level.PlopColorOf(id), id + 1, _reducedMotion));
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
        }

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
                        view.StartMove(CellToLocal(e.To), Delta(e.Direction), now);
                        view.SetSortingBase(RowOrder(_reducedMotion ? e.To.Y : Mathf.Min(e.From.Y, e.To.Y)));
                        break;
                    }
                    case GameEventType.PlopBlocked:
                        _plops[e.PlopId].StartBump(Delta(e.Direction), now, e.BlockReason);
                        break;
                    case GameEventType.PlopSettled:
                        _plops[e.PlopId].QueueSleep(now);
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
            if (_level == null || _reducedMotion) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < _plops.Count; i++)
            {
                var p = _plops[i];
                bool wasSliding = p.IsSliding;
                p.Tick(now);
                bool landed = wasSliding && !p.IsSliding;
                if (!landed && !p.SleepJustStarted) continue;

                int row = Mathf.RoundToInt(p.Target.y + (_level.Height - 1) * 0.5f);
                if (landed) p.SetSortingBase(RowOrder(row));
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
