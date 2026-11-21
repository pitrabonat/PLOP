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

        private readonly List<GameObject> _static = new List<GameObject>();
        private readonly List<PlopView> _plops = new List<PlopView>();
        private readonly Dictionary<int, Transform> _nestByCell = new Dictionary<int, Transform>();
        private Transform[] _pulseNest = new Transform[8];
        private float[] _pulseT0 = new float[8];
        private LevelDefinition _level;
        private ShapeAtlas _atlas;

        /// <summary>True while any Plop is still travelling/bumping (logical positions not yet reached visually).</summary>
        public bool IsAnimating
        {
            get { foreach (var p in _plops) if (p.IsMoving) return true; return false; }
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
            _nestByCell.Clear();
            for (int i = 0; i < _pulseNest.Length; i++) _pulseNest[i] = null;
            _level = level;
            _atlas = atlas;

            // ---- rounded container + soft shadow (9-sliced so the corner radius stays constant) ----
            float pad = BoardLayout.PanelPadding;
            var panelSize = new Vector2(level.Width + 2 * pad, level.Height + 2 * pad);
            var shadow = Sliced("BoardShadow", atlas.Get("panelShadow"), panelSize + Vector2.one * (2 * ShapeAtlas.PanelShadowBlur), -1000);
            shadow.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            shadow.color = new Color(1f, 1f, 1f, 0.15f); // the baked mask is full strength; keep the lift gentle
            Sliced("BoardPanel", atlas.Get("panel"), panelSize, -990);

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
                    }
                    else
                    {
                        var tile = Simple("Tile " + x + "," + y, atlas.Get("tile"), -980);
                        tile.transform.localPosition = CellToLocal(pos);
                    }
                }
            }

            foreach (var nest in level.Nests)
            {
                var n = Simple("Nest " + nest.Color, atlas.Get(ShapeAtlas.NestKey((int)nest.Color)), -970);
                n.transform.localPosition = CellToLocal(nest.Position);
                _nestByCell[level.CellIndex(nest.Position)] = n.transform;
            }

            // ---- Plops ----
            for (int id = 0; id < level.Plops.Count; id++)
                _plops.Add(new PlopView(transform, atlas, (int)level.PlopColorOf(id), id + 1));
            Sync(state);
        }

        /// <summary>Snaps every Plop to the given state (after load, undo, restart). Cancels animations.</summary>
        public void Sync(GameState state)
        {
            for (int id = 0; id < _plops.Count; id++)
            {
                var pos = _level.PositionOf(state.CellOf(id));
                _plops[id].Snap(CellToLocal(pos), state.IsAsleep(id));
                _plops[id].SetSortingBase(RowOrder(pos.Y));
            }
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
                        view.SetSortingBase(RowOrder(Mathf.Min(e.From.Y, e.To.Y)));
                        break;
                    }
                    case GameEventType.PlopBlocked:
                        _plops[e.PlopId].StartBump(Delta(e.Direction), now);
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
            if (_level == null) return;
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
                float s = 1f + 0.07f * Mathf.Sin(Mathf.PI * u);
                _pulseNest[i].localScale = new Vector3(s, s, 1f);
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
