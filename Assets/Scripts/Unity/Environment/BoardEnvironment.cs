using System;
using System.Collections.Generic;
using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Mechanic presentation on the board: connected Lava pools and Frost paths (Phase 11.6: shaders built from
    /// the supplied art tiles), fans (supplied housing + blades spinning on their hub, strength chevrons,
    /// directional airflow), Loop portals (supplied frame + a separately rotating vortex), and the event-driven
    /// feedback that follows the Core's movement trace (frost trail, wind gust, teleport flashes, lava steam).
    /// Owned and ticked by <see cref="BoardView"/> (one Update for the whole board). Purely visual: it never
    /// reads or changes gameplay state beyond the level layout.
    /// </summary>
    internal sealed class BoardEnvironment
    {
        public enum Cue : byte { IceSlide, FanPush, PortalIn, PortalOut, PortalBlocked, LavaHiss, LavaBubble, IceStop }

        public const int BackOrder = -960;    // behind every Plop, above Nests
        public const int FrontOrder = 950;    // above Plops, below Nest seals
        private const float IdleBladeSpeed = 75f;   // degrees per second
        private const float IdleVortexSpeed = 42f;
        private const float WarpVortexBoost = 520f;
        private const float PushBladeBoost = 980f;
        private const float WindIdle = 0.42f;
        private const float SurfaceEffectSeconds = 0.65f;
        private const float BurnHold = 0.38f;

        private struct Surface
        {
            public SpriteRenderer Renderer;
            public byte Mask, Seed;
            public TileKind Kind;
            public float T0, Peak, Hold;
        }

        private struct PortalView
        {
            public SpriteRenderer Renderer;     // static frame (carries the pair pips)
            public SpriteRenderer Vortex;       // rotates independently under the frame
            public int Pair, Cell;
            public float FlashT0, BlockT0, Angle, Boost, Seed, NextSpark;
        }

        private struct FanView
        {
            public Transform Blades;
            public SpriteRenderer[] Wind;
            public int Run;
            public float Angle, Boost, GustT0, Seed, Shown;
        }

        private struct Emitter
        {
            public int PlopId;
            public BoardFx.Kind Kind;
            public float Until, Next, Interval;
            public Vector2 Dir;
        }

        private struct Pending
        {
            public float At;
            public int Kind;            // 0 = exit flash + burst
            public GridPosition Cell;
        }

        private readonly Transform _root;
        private readonly Func<int, Vector3> _plopLocal;
        private readonly Func<GridPosition, Vector3> _cellLocal;
        private readonly Func<int, int> _rowOrder;
        private readonly List<Surface> _surfaces = new List<Surface>();
        private readonly Dictionary<int, int> _surfaceByCell = new Dictionary<int, int>();
        private readonly List<PortalView> _portals = new List<PortalView>();
        private readonly Dictionary<int, int> _portalByCell = new Dictionary<int, int>();
        private readonly List<FanView> _fans = new List<FanView>();
        private readonly Dictionary<int, int> _fanByCell = new Dictionary<int, int>();
        private readonly List<Emitter> _emitters = new List<Emitter>();
        private readonly List<Pending> _pending = new List<Pending>();
        private LevelDefinition _level;
        private BoardFx _fx;
        private bool _shaders, _reduced, _saver, _visible = true, _hasLava;
        private float _nextBubble;
        private uint _rng = 0x9E3779B9u;

        public event Action<Cue> Sound;

        public bool UsesShaders => _shaders;
        public int SurfaceCount => _surfaces.Count;
        public int PortalCount => _portals.Count;
        public int FanCount => _fans.Count;
        internal BoardFx Fx => _fx;
        public bool Visible => _visible;

        /// <summary>Transient feedback still playing (does not include continuous ambient motion).</summary>
        public bool HasActiveFeedback => _pending.Count > 0 || _emitters.Count > 0 || (_fx != null && _fx.LiveCount > 0);

        public BoardEnvironment(Transform root, Func<GridPosition, Vector3> cellLocal, Func<int, Vector3> plopLocal, Func<int, int> rowOrder)
        {
            _root = root;
            _cellLocal = cellLocal;
            _plopLocal = plopLocal;
            _rowOrder = rowOrder;
        }

        // ------------------------------------------------------------------ construction

        public delegate SpriteRenderer Factory(string name, Sprite sprite, int order);

        public void Build(LevelDefinition level, ShapeAtlas atlas, Factory make, bool reduced, bool saver)
        {
            _level = level;
            _reduced = reduced;
            _saver = saver;
            _surfaces.Clear();
            _surfaceByCell.Clear();
            _portals.Clear();
            _portalByCell.Clear();
            _fans.Clear();
            _fanByCell.Clear();
            _emitters.Clear();
            _pending.Clear();
            _hasLava = false;
            _shaders = PlopShaders.Available;
            PlopShaders.SetFrozen(reduced);

            foreach (var t in level.Tiles)
            {
                var at = _cellLocal(t.Position);
                int cell = level.CellIndex(t.Position);
                switch (t.Kind)
                {
                    case TileKind.Frost:
                    case TileKind.Lava:
                    {
                        bool lava = t.Kind == TileKind.Lava;
                        _hasLava |= lava;
                        byte mask = SurfaceConnectivity.Mask(level, t.Position.X, t.Position.Y, t.Kind);
                        byte seed = SurfaceConnectivity.Seed(t.Position.X, t.Position.Y);
                        SpriteRenderer r;
                        if (_shaders)
                        {
                            r = make((lava ? "Lava " : "Frost ") + t.Position + " " + SurfaceConnectivity.Classify(mask), PlopShaders.Quad, -976);
                            r.sharedMaterial = lava ? PlopShaders.Lava : PlopShaders.Frost;
                            r.color = PlopShaders.Pack(SurfaceConnectivity.EncodeSurface(mask, seed, 0f));
                        }
                        else r = make((lava ? "Lava " : "Frost ") + t.Position, atlas.Get(lava ? ShapeAtlas.LavaKey : ShapeAtlas.FrostKey), -976);
                        r.transform.localPosition = at;
                        _surfaceByCell[cell] = _surfaces.Count;
                        _surfaces.Add(new Surface { Renderer = r, Mask = mask, Seed = seed, Kind = t.Kind, T0 = -1f });
                        break;
                    }
                    case TileKind.Portal:
                    {
                        int slot = SurfaceConnectivity.PortalSlot(t.Pair);
                        SpriteRenderer vortex = null;
                        var vortexSprite = atlas.Get(ShapeAtlas.PortalVortexKey(slot));
                        if (vortexSprite != null)
                        {
                            vortex = make("Vortex " + t.Pair + " " + t.Position, vortexSprite, -977);
                            vortex.transform.localPosition = at;
                        }
                        var r = make("Loop " + t.Pair + " " + t.Position, atlas.Get(ShapeAtlas.PortalKey(slot)), -976);
                        r.transform.localPosition = at;
                        float pseed = (SurfaceConnectivity.Seed(t.Position.X, t.Position.Y) % 97) / 97f;
                        _portalByCell[cell] = _portals.Count;
                        _portals.Add(new PortalView
                        {
                            Renderer = r, Vortex = vortex, Pair = t.Pair, Cell = cell, FlashT0 = -1f, BlockT0 = -1f,
                            Angle = pseed * 360f, Seed = pseed, NextSpark = Time.unscaledTime + 0.5f + 1.5f * pseed
                        });
                        if (vortex != null) vortex.transform.localRotation = Quaternion.AngleAxis(pseed * 360f, Vector3.forward);
                        break;
                    }
                    case TileKind.Fan:
                        BuildFan(t, at, cell, atlas, make);
                        break;
                }
            }

            // Created through the factory so a rebuild destroys the pool with the rest of the board.
            var fxRoot = make("Trace effects", null, 0).transform;
            _fx = new BoardFx(fxRoot, atlas) { Enabled = !reduced, BatterySaver = saver };
            _nextBubble = Time.unscaledTime + 4f + 4f * Rand();
            SyncBoardSpace();
        }

        private void BuildFan(MechanicTile t, Vector3 at, int cell, ShapeAtlas atlas, Factory make)
        {
            make("Breeze " + t.Position, atlas.Get(ShapeAtlas.FanKey), -976).transform.localPosition = at;
            var blades = make("Breeze blades " + t.Position, atlas.Get(ShapeAtlas.FanBladesKey), -975);
            blades.transform.localPosition = at;
            var dir = Delta(t.Direction);
            var spin = Quaternion.FromToRotation(Vector3.up, dir);
            var chevron = atlas.Get(ShapeAtlas.FanChevronKey);
            for (int k = 0; k < t.Power; k++)
            {
                // Strength chevrons on the downwind rim: one per cell of push, pointing where the wind blows.
                // They are static, so strength and direction read the same with animations off.
                var c = make("Breeze power " + t.Position, chevron, -973);
                c.transform.localPosition = at + (Vector3)(dir * (0.285f + 0.068f * k - 0.034f * (t.Power - 1)) + dir * 0.06f);
                c.transform.localRotation = spin;
            }

            int run = SurfaceConnectivity.WindRun(_level, t.Position, t.Direction, t.Power);
            float seed = (SurfaceConnectivity.Seed(t.Position.X, t.Position.Y) % 64) / 64f;
            SpriteRenderer[] wind = Array.Empty<SpriteRenderer>();
            if (_shaders && run > 1)
            {
                wind = new SpriteRenderer[run];
                float angle = SurfaceConnectivity.DirectionAngle(t.Direction);
                for (int k = 0; k < run; k++)
                {
                    var w = make("Breeze wind " + t.Position + " " + k, PlopShaders.Quad, -978);
                    w.sharedMaterial = PlopShaders.Wind;
                    w.transform.localPosition = at + (Vector3)(dir * k);
                    w.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                    w.color = PlopShaders.Pack(SurfaceConnectivity.EncodeWind(k, run, WindIdle, seed));
                    wind[k] = w;
                }
            }
            _fanByCell[cell] = _fans.Count;
            _fans.Add(new FanView
            {
                Blades = blades.transform, Wind = wind, Run = run, Seed = seed, GustT0 = -1f, Shown = WindIdle,
                Angle = (SurfaceConnectivity.Seed(t.Position.X, t.Position.Y) % 90)
            });
            blades.transform.localRotation = Quaternion.AngleAxis(_fans[_fans.Count - 1].Angle, Vector3.forward);
        }

        // ------------------------------------------------------------------ settings

        public void SetReducedMotion(bool reduced)
        {
            _reduced = reduced;
            PlopShaders.SetFrozen(reduced);
            if (_fx != null)
            {
                _fx.Enabled = !reduced;
                if (reduced) Clear();
            }
        }

        public void SetBatterySaver(bool saver)
        {
            _saver = saver;
            PlopShaders.Speed = saver ? 0.75f : 1f;
            if (_fx != null) _fx.BatterySaver = saver;
        }

        /// <summary>Pauses ambient motion when the board is covered (menu, Studio) or the app is backgrounded.</summary>
        public void SetVisible(bool visible) => _visible = visible;

        /// <summary>Undo / Restart / rebuild: drop every transient effect and return tiles to rest.</summary>
        public void Clear()
        {
            _fx?.Clear();
            _emitters.Clear();
            _pending.Clear();
            for (int i = 0; i < _surfaces.Count; i++)
            {
                var s = _surfaces[i];
                s.T0 = -1f;
                s.Hold = 0f;
                _surfaces[i] = s;
                SetSurfaceEffect(i, 0f);
            }
            for (int i = 0; i < _portals.Count; i++)
            {
                var p = _portals[i];
                p.FlashT0 = p.BlockT0 = -1f;
                p.Boost = 0f;
                _portals[i] = p;
                SetPortal(i, 0f, 0f, PlopShaders.Time);
            }
            for (int i = 0; i < _fans.Count; i++)
            {
                var f = _fans[i];
                f.Boost = 0f;
                f.GustT0 = -1f;
                _fans[i] = f;
                SetWind(i, WindIdle);
            }
        }

        // ------------------------------------------------------------------ trace feedback

        public void OnSlide(int plopId, GridPosition from, GridPosition to, Direction dir, float duration, float now)
        {
            Sound?.Invoke(Cue.IceSlide);
            // The ice shimmers along the path the Plop actually took.
            var d = Delta(dir);
            var p = from;
            int guard = 0;
            while (guard++ < 32)
            {
                if (_surfaceByCell.TryGetValue(_level.CellIndex(p), out int s) && _surfaces[s].Kind == TileKind.Frost)
                    StartSurfaceEffect(s, now, 1f, 0f);
                if (p.Equals(to)) break;
                p = p.Offset(dir);
                if (!_level.InBounds(p)) break;
            }
            if (_reduced) return;
            _emitters.Add(new Emitter { PlopId = plopId, Kind = BoardFx.Kind.Flake, Until = now + duration, Next = now, Interval = 0.03f, Dir = d });
        }

        /// <summary>A slide ended: a small spray of frost in front of the Plop.</summary>
        public void OnSlideStopped(int plopId, Direction dir, float now)
        {
            Sound?.Invoke(Cue.IceStop); // sound is not motion: it plays with Reduced Motion too
            if (_reduced) return;
            var d = (Vector3)Delta(dir);
            var at = _plopLocal(plopId) + d * 0.30f + Vector3.down * 0.25f;
            for (int k = 0; k < 4; k++)
            {
                var side = new Vector3(-d.y, d.x, 0f) * ((k - 1.5f) * 0.35f);
                Emit(BoardFx.Kind.Flake, at, (d * 0.55f + side) * (0.8f + 0.4f * Rand()), new Color(0.93f, 0.97f, 1f, 0.95f),
                    0.45f, 0.75f, 0.35f, FrontOrder, spin: 220f, drag: 4f, now: now);
            }
            Emit(BoardFx.Kind.Puff, at, d * 0.2f, new Color(0.92f, 0.96f, 1f, 0.55f), 0.40f, 0.45f, 0.95f, FrontOrder, now: now);
        }

        public void OnPush(int plopId, GridPosition fanCell, Direction dir, float duration, float now)
        {
            Sound?.Invoke(Cue.FanPush);
            if (_level.InBounds(fanCell) && _fanByCell.TryGetValue(_level.CellIndex(fanCell), out int f))
            {
                var fan = _fans[f];
                fan.Boost = _reduced ? 0f : PushBladeBoost;
                fan.GustT0 = now;
                _fans[f] = fan;
                SetWind(f, 1f);
            }
            if (_reduced) return;
            _emitters.Add(new Emitter { PlopId = plopId, Kind = BoardFx.Kind.Dot, Until = now + duration, Next = now, Interval = 0.045f, Dir = Delta(dir) });
        }

        public void OnTeleport(int plopId, GridPosition from, GridPosition to, float duration, float now)
        {
            Sound?.Invoke(Cue.PortalIn);
            Flash(from, now);
            float exitAt = now + duration * PlopView.WarpEntryShare;
            _pending.Add(new Pending { At = exitAt, Kind = 0, Cell = to });
            if (_reduced) return;
            var c = _cellLocal(from);
            var tint = PortalTint(from);
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f + Rand();
                var off = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.36f;
                Emit(BoardFx.Kind.Spark, c + off, -off * 2.6f, Color.Lerp(tint, Color.white, 0.5f), 0.22f, 0.9f, 0.4f, FrontOrder, now: now);
            }
        }

        public void OnPortalBlocked(GridPosition entry, float now)
        {
            Sound?.Invoke(Cue.PortalBlocked);
            if (!_level.InBounds(entry) || !_portalByCell.TryGetValue(_level.CellIndex(entry), out int i)) return;
            var p = _portals[i];
            p.BlockT0 = now;
            _portals[i] = p;
            if (_reduced) SetPortal(i, 0f, 0.6f, now);
        }

        public void OnBurn(int plopId, GridPosition cell, float now)
        {
            Sound?.Invoke(Cue.LavaHiss);
            if (_level.InBounds(cell) && _surfaceByCell.TryGetValue(_level.CellIndex(cell), out int s))
                StartSurfaceEffect(s, now, 1f, BurnHold);
            if (_reduced) return;
            var c = _cellLocal(cell);
            Emit(BoardFx.Kind.Ring, c + Vector3.down * 0.12f, Vector3.zero, new Color(1f, 0.72f, 0.38f, 0.85f), 0.55f, 0.55f, 1.55f, BackOrder, now: now);
            for (int k = 0; k < 3; k++)
                Emit(BoardFx.Kind.Puff, c + new Vector3((k - 1) * 0.18f, 0.15f, 0f), new Vector3((k - 1) * 0.12f, 0.75f + 0.2f * Rand(), 0f),
                    new Color(0.97f, 0.95f, 0.93f, 0.70f), 0.75f + 0.15f * k, 0.55f, 1.35f, FrontOrder, drag: 1.5f, now: now);
            for (int k = 0; k < 6; k++)
                Emit(BoardFx.Kind.Dot, c + new Vector3((Rand() - 0.5f) * 0.5f, -0.1f, 0f), new Vector3((Rand() - 0.5f) * 0.6f, 1.0f + Rand() * 0.8f, 0f),
                    new Color(1f, 0.62f + 0.25f * Rand(), 0.25f, 1f), 0.55f + 0.3f * Rand(), 0.55f, 0.15f, FrontOrder, drag: 0.8f, gravity: -0.6f, now: now);
        }

        // ------------------------------------------------------------------ per frame

        public void Tick(float now, float dt)
        {
            if (_level == null) return;
            SyncBoardSpace();
            if (_visible) PlopShaders.Advance(dt);

            for (int i = 0; i < _fans.Count; i++)
            {
                var f = _fans[i];
                if (!_reduced && _visible)
                {
                    f.Angle -= (IdleBladeSpeed * (_saver ? 0.6f : 1f) + f.Boost) * dt;
                    if (f.Angle < -360f) f.Angle += 360f;
                    f.Boost *= Mathf.Exp(-dt * 3.0f);
                    if (f.Boost < 1f) f.Boost = 0f;
                    f.Blades.localRotation = Quaternion.AngleAxis(f.Angle, Vector3.forward);
                }
                if (f.GustT0 >= 0f)
                {
                    float u = (now - f.GustT0) / 0.75f;
                    float k = u >= 1f ? WindIdle : Mathf.Lerp(1f, WindIdle, u * u);
                    if (u >= 1f) f.GustT0 = -1f;
                    _fans[i] = f;
                    SetWind(i, k);
                }
                else _fans[i] = f;
            }

            for (int i = 0; i < _surfaces.Count; i++)
            {
                var s = _surfaces[i];
                if (s.T0 < 0f) continue;
                float u = (now - s.T0) / SurfaceEffectSeconds;
                float v = u >= 1f ? s.Hold : Mathf.Lerp(s.Peak, s.Hold, u);
                if (u >= 1f) { s.T0 = -1f; _surfaces[i] = s; }
                SetSurfaceEffect(i, v);
            }

            bool ambient = !_reduced && _visible;
            for (int i = 0; i < _portals.Count; i++)
            {
                var p = _portals[i];
                if (ambient)
                {
                    p.Angle += (IdleVortexSpeed * (_saver ? 0.6f : 1f) + p.Boost) * dt;
                    if (p.Angle > 360f) p.Angle -= 360f;
                    p.Boost *= Mathf.Exp(-dt * 2.6f);
                    if (p.Boost < 1f) p.Boost = 0f;
                    if (p.Vortex != null) p.Vortex.transform.localRotation = Quaternion.AngleAxis(p.Angle, Vector3.forward);
                    if (now >= p.NextSpark)
                    {
                        // A small mote drifts into the vortex now and then (half as often in Battery Saver).
                        p.NextSpark = now + (_saver ? 2.6f : 1.3f) * (0.7f + 0.6f * Rand());
                        float a = Rand() * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                        var c = _cellLocal(_level.PositionOf(p.Cell));
                        var tint = PlopShaders.DefaultPortalTints[SurfaceConnectivity.PortalSlot(p.Pair)];
                        Emit(BoardFx.Kind.Spark, c + dir * 0.30f, -dir * 0.32f, Color.Lerp(tint, Color.white, 0.6f).WithAlpha(0.85f),
                            0.8f, 0.30f, 0.08f, BackOrder + 30, drag: 0.4f, now: now, ambient: true);
                    }
                }
                if (p.FlashT0 < 0f && p.BlockT0 < 0f)
                {
                    _portals[i] = p;
                    if (ambient) SetPortal(i, 0f, 0f, now);
                    continue;
                }
                float flash = 0f, block = 0f;
                if (p.FlashT0 >= 0f)
                {
                    float u = (now - p.FlashT0) / 0.45f;
                    if (u >= 1f) p.FlashT0 = -1f;
                    else flash = Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.6f)) * (1f - u * 0.5f);
                }
                if (p.BlockT0 >= 0f)
                {
                    float u = (now - p.BlockT0) / 0.55f;
                    if (u >= 1f) p.BlockT0 = -1f;
                    else block = Mathf.Sin(Mathf.PI * u);
                }
                _portals[i] = p;
                SetPortal(i, _reduced ? 0f : flash, block, now);
            }

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (now < _pending[i].At) continue;
                var pending = _pending[i];
                _pending.RemoveAt(i);
                ExitBurst(pending.Cell, now);
            }

            for (int i = _emitters.Count - 1; i >= 0; i--)
            {
                var e = _emitters[i];
                if (now > e.Until) { _emitters.RemoveAt(i); continue; }
                if (now < e.Next) continue;
                e.Next = now + e.Interval;
                _emitters[i] = e;
                var at = _plopLocal(e.PlopId);
                var back = -(Vector3)e.Dir;
                var side = new Vector3(-e.Dir.y, e.Dir.x, 0f) * (Rand() - 0.5f);
                if (e.Kind == BoardFx.Kind.Flake)
                    Emit(BoardFx.Kind.Flake, at + back * 0.28f + Vector3.down * 0.22f + side * 0.4f, back * 0.25f + side * 0.5f,
                        new Color(0.90f, 0.96f, 1f, 0.9f), 0.40f, 0.65f, 0.25f, BackOrder, spin: 180f, drag: 2.5f, now: now);
                else
                    Emit(BoardFx.Kind.Dot, at + back * 0.38f + side * 0.55f, (Vector3)e.Dir * 0.9f + side * 0.3f,
                        new Color(0.80f, 0.95f, 0.91f, 0.85f), 0.35f, 0.75f, 0.35f, BackOrder, drag: 2f, now: now);
            }

            if (_hasLava && _visible && now >= _nextBubble)
            {
                _nextBubble = now + 6.5f + 5f * Rand();
                Sound?.Invoke(Cue.LavaBubble);
            }

            _fx?.Tick(now, dt);
        }

        // ------------------------------------------------------------------ helpers

        private void ExitBurst(GridPosition cell, float now)
        {
            Sound?.Invoke(Cue.PortalOut);
            Flash(cell, now);
            if (_reduced) return;
            var c = _cellLocal(cell);
            var tint = PortalTint(cell);
            Emit(BoardFx.Kind.Ring, c, Vector3.zero, Color.Lerp(tint, Color.white, 0.45f), 0.40f, 0.9f, 1.9f, FrontOrder, now: now);
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f + Rand();
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Emit(BoardFx.Kind.Spark, c + dir * 0.12f, dir * 1.3f, Color.Lerp(tint, Color.white, 0.55f), 0.38f, 0.8f, 0.3f, FrontOrder, drag: 3f, now: now);
            }
        }

        private void Flash(GridPosition cell, float now)
        {
            if (!_level.InBounds(cell) || !_portalByCell.TryGetValue(_level.CellIndex(cell), out int i)) return;
            var p = _portals[i];
            p.FlashT0 = now;
            if (!_reduced) p.Boost = WarpVortexBoost;
            _portals[i] = p;
        }

        private Color PortalTint(GridPosition cell)
        {
            if (_level.InBounds(cell) && _portalByCell.TryGetValue(_level.CellIndex(cell), out int i))
                return PlopShaders.DefaultPortalTints[SurfaceConnectivity.PortalSlot(_portals[i].Pair)];
            return Color.white;
        }

        private void StartSurfaceEffect(int i, float now, float peak, float hold)
        {
            var s = _surfaces[i];
            s.T0 = now;
            s.Peak = peak;
            s.Hold = _reduced ? Mathf.Max(hold, peak * 0.5f) : hold;
            _surfaces[i] = s;
            if (_reduced) { s.T0 = -1f; _surfaces[i] = s; SetSurfaceEffect(i, s.Hold); }
            else SetSurfaceEffect(i, peak);
        }

        private void SetSurfaceEffect(int i, float amount)
        {
            if (!_shaders) return;
            var s = _surfaces[i];
            if (s.Renderer == null) return;
            s.Renderer.color = PlopShaders.Pack(SurfaceConnectivity.EncodeSurface(s.Mask, s.Seed, amount));
        }

        /// <summary>
        /// Portal pose: the vortex breathes slightly and pops on a teleport flash; a blocked exit dims it and
        /// wobbles the frame. Reduced Motion keeps only the static dim (no scale or wobble).
        /// </summary>
        private void SetPortal(int i, float flash, float blocked, float now)
        {
            var p = _portals[i];
            if (p.Renderer == null) return;
            bool still = _reduced || !_visible;
            float breathe = still ? 0f : 0.025f * Mathf.Sin(now * 1.35f + p.Seed * 6.2831f);
            if (p.Vortex != null)
            {
                p.Vortex.transform.localScale = Vector3.one * (1f + breathe + 0.20f * flash);
                float dim = 1f - 0.38f * blocked;
                p.Vortex.color = new Color(dim, dim, dim, 1f);
            }
            float wobble = still ? 0f : 9f * Mathf.Sin(blocked * Mathf.PI * 3f) * blocked;
            p.Renderer.transform.localRotation = Quaternion.AngleAxis(wobble, Vector3.forward);
        }

        private void SetWind(int i, float intensity)
        {
            var f = _fans[i];
            if (Mathf.Abs(f.Shown - intensity) < 0.004f && intensity != WindIdle) return;
            f.Shown = intensity;
            _fans[i] = f;
            for (int k = 0; k < f.Wind.Length; k++)
                if (f.Wind[k] != null) f.Wind[k].color = PlopShaders.Pack(SurfaceConnectivity.EncodeWind(k, f.Run, intensity, f.Seed));
        }

        private void SyncBoardSpace()
        {
            if (_root == null) return;
            var origin = _root.TransformPoint(_cellLocal(new GridPosition(0, 0)));
            float scale = Mathf.Abs(_root.lossyScale.x);
            PlopShaders.SetBoard(origin, scale > 1e-5f ? 1f / scale : 1f);
            if (_level != null)
            {
                const float margin = BoardLayout.PanelPadding + 0.15f;
                PlopShaders.SetClear(new Vector4(-0.5f - margin, -0.5f - margin, _level.Width - 0.5f + margin, _level.Height - 0.5f + margin));
            }
        }

        private void Emit(BoardFx.Kind kind, Vector3 at, Vector3 velocity, Color color, float life, float size0, float size1, int order,
            float spin = 0f, float drag = 0f, float gravity = 0f, float now = 0f, bool ambient = false)
        {
            _fx?.Spawn(new BoardFx.Emit
            {
                Kind = kind, Position = at, Velocity = velocity, Color = color, Life = life, Size0 = size0, Size1 = size1,
                Order = order, Spin = spin, Drag = drag, Gravity = gravity, Ambient = ambient
            }, now);
        }

        private float Rand()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng & 0xffffu) / 65535f;
        }

        internal static Vector2 Delta(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return Vector2.up;
                case Direction.Down: return Vector2.down;
                case Direction.Left: return Vector2.left;
                default: return Vector2.right;
            }
        }

        // ---- test hooks ----
        internal byte SurfaceMaskAt(GridPosition p) => _surfaceByCell.TryGetValue(_level.CellIndex(p), out int i) ? _surfaces[i].Mask : (byte)0;
        internal SpriteRenderer SurfaceRendererAt(GridPosition p) => _surfaceByCell.TryGetValue(_level.CellIndex(p), out int i) ? _surfaces[i].Renderer : null;
        internal SpriteRenderer PortalRendererAt(GridPosition p) => _portalByCell.TryGetValue(_level.CellIndex(p), out int i) ? _portals[i].Renderer : null;
        internal SpriteRenderer VortexAt(GridPosition p) => _portalByCell.TryGetValue(_level.CellIndex(p), out int i) ? _portals[i].Vortex : null;
        internal int WindRunAt(GridPosition p) => _fanByCell.TryGetValue(_level.CellIndex(p), out int i) ? _fans[i].Run : 0;
        internal SpriteRenderer[] WindAt(GridPosition p) => _fanByCell.TryGetValue(_level.CellIndex(p), out int i) ? _fans[i].Wind : null;
        internal Transform BladesAt(GridPosition p) => _fanByCell.TryGetValue(_level.CellIndex(p), out int i) ? _fans[i].Blades : null;
    }
}
