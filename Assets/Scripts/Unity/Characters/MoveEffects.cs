using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Tiny cosmetic move effects (puff, sparkle, bubbles, stars) from a fixed pool of sprite parts: no
    /// allocation per move, at most <see cref="PoolSize"/> particles alive, nothing while idle, and none at all
    /// under Reduced Motion. Battery Saver halves the particle count. Purely visual.
    /// </summary>
    internal sealed class MoveEffects
    {
        public const int PoolSize = 12;

        private readonly PlopPart[] _parts = new PlopPart[PoolSize];
        private readonly float[] _t0 = new float[PoolSize];
        private readonly Vector3[] _origin = new Vector3[PoolSize];
        private readonly Vector2[] _drift = new Vector2[PoolSize];
        private readonly float[] _spin = new float[PoolSize];
        private readonly MoveEffectStyle _style;
        private int _next, _alive;
        private uint _rng = 0x9E3779B9u;

        public bool Enabled = true;
        public bool BatterySaver;
        public int Alive => _alive;
        public EffectKind Kind => _style?.Kind ?? EffectKind.None;

        public MoveEffects(Transform parent, ShapeAtlas atlas, bool ui, string keyPrefix = "")
        {
            _style = atlas.Look?.Effect;
            string key = (keyPrefix ?? "") + ShapeAtlas.EffectKey;
            if (_style == null || _style.Kind == EffectKind.None || !atlas.Has(key)) return;
            var sprite = atlas.Get(key);
            for (int i = 0; i < PoolSize; i++)
            {
                _parts[i] = new PlopPart("Effect " + i, parent, sprite, ui) { Visible = false };
                _t0[i] = -1f;
            }
        }

        private float Rand()
        {
            _rng = _rng * 1664525u + 1013904223u;
            return (_rng >> 8) / 16777216f;
        }

        /// <summary>Emits a short burst where a Plop starts moving. <paramref name="tint"/> colours puffs only.</summary>
        public void Spawn(Vector3 at, Vector2 direction, Color tint, float now, int order)
        {
            if (!Enabled || _parts[0] == null) return;
            int count = BatterySaver ? Mathf.Max(1, _style.Particles / 2) : _style.Particles;
            for (int n = 0; n < count; n++)
            {
                int i = _next;
                _next = (_next + 1) % PoolSize;
                if (_t0[i] < 0f) _alive++;
                _t0[i] = now + n * 0.03f;
                float side = Rand() * 2f - 1f;
                Vector2 across = new Vector2(-direction.y, direction.x);
                Vector3 origin = at;
                switch (_style.Kind)
                {
                    case EffectKind.Puff:
                        origin += (Vector3)(-direction * 0.22f + across * side * 0.16f) + new Vector3(0f, -0.27f, 0f);
                        _drift[i] = -direction * 0.12f + across * side * 0.08f;
                        break;
                    case EffectKind.Sparkle:
                        origin += (Vector3)(across * side * 0.30f) + new Vector3(0f, 0.05f + Rand() * 0.25f, 0f);
                        _drift[i] = Vector2.up * 0.06f;
                        break;
                    case EffectKind.Bubble:
                        origin += (Vector3)(-direction * 0.25f + across * side * 0.18f) + new Vector3(0f, -0.1f, 0f);
                        _drift[i] = new Vector2(side * 0.05f, 0.30f + Rand() * 0.12f);
                        break;
                    default:
                        origin += (Vector3)(-direction * (0.18f + 0.22f * n) + across * side * 0.10f);
                        _drift[i] = -direction * 0.10f;
                        break;
                }
                _origin[i] = origin;
                _spin[i] = (Rand() * 2f - 1f) * 160f;
                var part = _parts[i];
                part.Transform.localPosition = origin;
                part.Transform.localScale = Vector3.zero;
                part.Color = _style.Kind == EffectKind.Puff ? Color.Lerp(tint, Color.white, 0.55f) : Color.white;
                part.Order = order;
                part.Visible = true;
            }
        }

        public void Tick(float now)
        {
            if (_alive == 0) return;
            float life = _style.Life;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_t0[i] < 0f || now < _t0[i]) continue;
                float u = (now - _t0[i]) / life;
                var part = _parts[i];
                if (u >= 1f)
                {
                    _t0[i] = -1f;
                    _alive--;
                    part.Visible = false;
                    continue;
                }
                float scale, alpha = 1f - u * u;
                switch (_style.Kind)
                {
                    case EffectKind.Puff: scale = 0.7f + 0.8f * u; alpha = 0.85f * (1f - u); break;
                    case EffectKind.Sparkle: scale = Mathf.Sin(Mathf.PI * u) * 1.1f; break;
                    case EffectKind.Bubble: scale = 0.55f + 0.45f * Mathf.Sqrt(u); break;
                    default: scale = 1f - 0.65f * u; break;
                }
                var c = part.Color;
                c.a = alpha;
                part.Color = c;
                part.Transform.localPosition = _origin[i] + (Vector3)(_drift[i] * u);
                part.Transform.localScale = new Vector3(scale, scale, 1f);
                if (_style.Kind != EffectKind.Bubble && _style.Kind != EffectKind.Puff)
                    part.Transform.localRotation = Quaternion.AngleAxis(_spin[i] * u, Vector3.forward);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                _t0[i] = -1f;
                if (_parts[i] != null) _parts[i].Visible = false;
            }
            _alive = 0;
        }
    }
}
