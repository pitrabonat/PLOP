using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Small pooled particle system for board feedback (frost trail, wind puffs, portal sparks, lava steam and
    /// embers). A fixed pool of sprite renderers, no allocation after construction, ticked by the board's
    /// single Update. Disabled under Reduced Motion; Battery Saver spawns every other particle.
    /// </summary>
    internal sealed class BoardFx
    {
        public enum Kind : byte { Dot, Ring, Spark, Flake, Puff }
        public const int Capacity = 40;

        private struct Particle
        {
            public SpriteRenderer Renderer;
            public Transform Transform;
            public Vector3 Position, Velocity;
            public float Born, Life, Size0, Size1, Spin, Drag, Gravity, Angle;
            public Color Color;
            public bool Live;
        }

        private readonly Particle[] _pool = new Particle[Capacity];
        private readonly Sprite[] _sprites = new Sprite[5];
        private int _cursor, _saverParity, _live;

        public bool Enabled { get; set; } = true;
        public bool BatterySaver { get; set; }
        public int LiveCount => _live;

        public BoardFx(Transform parent, ShapeAtlas atlas)
        {
            _sprites[(int)Kind.Dot] = atlas.Get(ShapeAtlas.FxDotKey);
            _sprites[(int)Kind.Ring] = atlas.Get(ShapeAtlas.FxRingKey);
            _sprites[(int)Kind.Spark] = atlas.Get(ShapeAtlas.FxSparkKey);
            _sprites[(int)Kind.Flake] = atlas.Get(ShapeAtlas.FxFlakeKey);
            _sprites[(int)Kind.Puff] = atlas.Get(ShapeAtlas.FxPuffKey);
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("Fx " + i);
                go.transform.SetParent(parent, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.enabled = false;
                _pool[i].Renderer = r;
                _pool[i].Transform = go.transform;
            }
        }

        public struct Emit
        {
            public Kind Kind;
            public Vector3 Position, Velocity;
            public Color Color;
            public float Life, Size0, Size1, Spin, Drag, Gravity;
            public int Order;
        }

        public void Spawn(in Emit e, float now)
        {
            if (!Enabled) return;
            if (BatterySaver && (++_saverParity & 1) == 0) return;
            int slot = -1;
            for (int k = 0; k < Capacity; k++)
            {
                int i = (_cursor + k) % Capacity;
                if (!_pool[i].Live) { slot = i; break; }
            }
            if (slot < 0) slot = _cursor; // full: recycle the oldest-ish slot
            _cursor = (slot + 1) % Capacity;

            ref var p = ref _pool[slot];
            if (!p.Live) _live++;
            p.Live = true;
            p.Position = e.Position;
            p.Velocity = e.Velocity;
            p.Born = now;
            p.Life = Mathf.Max(0.05f, e.Life);
            p.Size0 = e.Size0;
            p.Size1 = e.Size1;
            p.Spin = e.Spin;
            p.Drag = e.Drag;
            p.Gravity = e.Gravity;
            p.Angle = 0f;
            p.Color = e.Color;
            p.Renderer.sprite = _sprites[(int)e.Kind];
            p.Renderer.sortingOrder = e.Order;
            p.Renderer.color = e.Color;
            p.Renderer.enabled = true;
            p.Transform.localPosition = e.Position;
            p.Transform.localScale = Vector3.one * e.Size0;
            p.Transform.localRotation = Quaternion.identity;
        }

        public void Tick(float now, float dt)
        {
            if (_live == 0) return;
            dt = Mathf.Min(dt, 0.05f);
            for (int i = 0; i < Capacity; i++)
            {
                ref var p = ref _pool[i];
                if (!p.Live) continue;
                float u = (now - p.Born) / p.Life;
                if (u >= 1f)
                {
                    Kill(ref p);
                    continue;
                }
                p.Velocity *= 1f / (1f + p.Drag * dt);
                p.Velocity.y += p.Gravity * dt;
                p.Position += p.Velocity * dt;
                p.Angle += p.Spin * dt;
                float fadeIn = Mathf.Clamp01(u / 0.12f);
                float fadeOut = 1f - u * u;
                var c = p.Color;
                c.a *= fadeIn * fadeOut;
                p.Renderer.color = c;
                p.Transform.localPosition = p.Position;
                p.Transform.localScale = Vector3.one * Mathf.Lerp(p.Size0, p.Size1, 1f - (1f - u) * (1f - u));
                if (p.Spin != 0f) p.Transform.localRotation = Quaternion.AngleAxis(p.Angle, Vector3.forward);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) if (_pool[i].Live) Kill(ref _pool[i]);
            _live = 0;
        }

        private void Kill(ref Particle p)
        {
            p.Live = false;
            p.Renderer.enabled = false;
            _live = Mathf.Max(0, _live - 1);
        }
    }
}
