using System.Collections;
using System.Collections.Generic;
using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Visual representation of a board. Purely reactive: rebuilt from a level/state and animated from
    /// <see cref="GameEvent"/>s. It never changes gameplay state. One world unit = one grid cell.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private float moveDuration = 0.12f;
        [SerializeField] private float bumpDistance = 0.12f;

        private readonly List<SpriteRenderer> _plopViews = new List<SpriteRenderer>();
        private readonly List<GameObject> _tiles = new List<GameObject>();
        private readonly List<int> _movingIds = new List<int>(8);
        private readonly List<Vector3> _from = new List<Vector3>(8), _to = new List<Vector3>(8);
        private LevelDefinition _level;
        private Coroutine _anim;
        private Vector3[] _targets = new Vector3[0];

        public bool IsAnimating => _anim != null;
        public LevelDefinition Level => _level;

        public Vector3 CellToWorld(GridPosition p) =>
            transform.position + new Vector3(p.X - (_level.Width - 1) * 0.5f, p.Y - (_level.Height - 1) * 0.5f, 0f);

        public Vector3 PlopViewPosition(int plopId) => _plopViews[plopId].transform.position;

        public void Build(LevelDefinition level, GameState state)
        {
            StopAnimation();
            foreach (var t in _tiles) Destroy(t);
            foreach (var p in _plopViews) Destroy(p.gameObject);
            _tiles.Clear();
            _plopViews.Clear();
            _level = level;

            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    var pos = new GridPosition(x, y);
                    bool wall = level.IsWall(pos);
                    var tile = CreateSprite("Tile " + x + "," + y, PlaceholderArt.Square, wall ? PlaceholderArt.Wall : PlaceholderArt.Floor, 0);
                    tile.transform.position = CellToWorld(pos);
                    tile.transform.localScale = Vector3.one * (wall ? 0.98f : 0.94f);
                    _tiles.Add(tile.gameObject);
                }
            }

            foreach (var nest in level.Nests)
            {
                var n = CreateSprite("Nest " + nest.Color, PlaceholderArt.NestOutline, PlaceholderArt.ColorOf(nest.Color), 1);
                n.transform.position = CellToWorld(nest.Position);
                n.transform.localScale = Vector3.one * 0.82f;
                _tiles.Add(n.gameObject);
            }

            for (int id = 0; id < level.Plops.Count; id++)
            {
                var p = CreateSprite("Plop " + id, PlaceholderArt.Circle, PlaceholderArt.ColorOf(level.PlopColorOf(id)), 2);
                p.transform.localScale = Vector3.one * 0.7f;
                _plopViews.Add(p);
            }
            _targets = new Vector3[level.Plops.Count];
            Sync(state);
        }

        /// <summary>Snaps every Plop view to the given state (after load, undo, restart).</summary>
        public void Sync(GameState state)
        {
            StopAnimation();
            for (int id = 0; id < _plopViews.Count; id++)
            {
                var pos = _level.PositionOf(state.CellOf(id));
                _targets[id] = CellToWorld(pos);
                _plopViews[id].transform.position = _targets[id];
                ApplySleepLook(id, state.IsAsleep(id));
            }
        }

        /// <summary>Animates the events of one move. Any running animation is completed first.</summary>
        public void Play(GameEvent[] events)
        {
            StopAnimation();
            _movingIds.Clear(); _from.Clear(); _to.Clear();
            bool any = false;
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case GameEventType.PlopMoved:
                        _movingIds.Add(e.PlopId);
                        _from.Add(CellToWorld(e.From));
                        _to.Add(CellToWorld(e.To));
                        _targets[e.PlopId] = CellToWorld(e.To);
                        any = true;
                        break;
                    case GameEventType.PlopBlocked:
                        // small bump towards the blocked direction and back
                        var start = CellToWorld(e.From);
                        var dir = e.From.Offset(e.Direction);
                        var bump = Vector3.Lerp(start, CellToWorld(dir), bumpDistance);
                        _movingIds.Add(~e.PlopId); // negative marks a bump
                        _from.Add(start);
                        _to.Add(bump);
                        any = true;
                        break;
                    case GameEventType.PlopSettled:
                        ApplySleepLook(e.PlopId, true);
                        break;
                }
            }
            if (any) _anim = StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Mathf.Max(0.01f, moveDuration));
                float eased = 1f - (1f - t) * (1f - t);
                for (int i = 0; i < _movingIds.Count; i++)
                {
                    int id = _movingIds[i];
                    if (id >= 0) _plopViews[id].transform.position = Vector3.LerpUnclamped(_from[i], _to[i], eased);
                    else _plopViews[~id].transform.position = Vector3.Lerp(_from[i], _to[i], Mathf.Sin(eased * Mathf.PI));
                }
                yield return null;
            }
            _anim = null;
            SnapToTargets();
        }

        private void StopAnimation()
        {
            if (_anim == null) return;
            StopCoroutine(_anim);
            _anim = null;
            SnapToTargets();
        }

        private void SnapToTargets()
        {
            for (int id = 0; id < _plopViews.Count && id < _targets.Length; id++) _plopViews[id].transform.position = _targets[id];
        }

        private void ApplySleepLook(int id, bool asleep)
        {
            var r = _plopViews[id];
            var c = PlaceholderArt.ColorOf(_level.PlopColorOf(id));
            r.color = asleep ? Color.Lerp(c, Color.white, 0.45f) : c;
            r.transform.localScale = Vector3.one * (asleep ? 0.55f : 0.7f);
        }

        private SpriteRenderer CreateSprite(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sortingOrder = order;
            return r;
        }
    }
}
