using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Visual-only jelly Plop: shadow, body (gumdrop sprite), cheeks and eyes. Animation is pure math on
    /// transforms driven by <see cref="BoardView"/> — no physics, no per-frame allocation. The root position
    /// always ends exactly on the logical cell supplied by gameplay events; squash, hops and breathing only
    /// touch the body's local transform.
    /// </summary>
    public sealed class PlopView
    {
        public const float MoveDuration = 0.13f;
        public const float BumpDuration = 0.17f;
        public const float LandDuration = 0.24f;
        public const float SleepDuration = 0.45f;
        public const float HopDuration = 0.40f;
        private const float BodyScale = 0.95f;
        private const float GroundY = -0.30f;   // body pivot (ground contact) relative to cell center

        public readonly Transform Root;
        private readonly Transform _body, _eyeL, _eyeR, _cheekL, _cheekR, _shadow;
        private readonly SpriteRenderer _bodyR, _eyeLR, _eyeRR, _cheekLR, _cheekRR, _shadowR;
        private readonly Sprite _eyeOpen, _eyeClosed;

        private Vector3 _from, _to;
        private Vector2 _moveDir, _bumpDir, _landDir, _look;
        private float _moveT0 = -1f, _bumpT0 = -1f, _landT0 = -1f, _sleepT0 = -1f, _hopT0 = -1f, _blinkT0 = -1f;
        private float _nextBlink, _lookUntil;
        private readonly float _phase;
        private bool _asleep, _sleepPending;
        private uint _rng;

        public bool IsMoving => _moveT0 >= 0f || _bumpT0 >= 0f;
        public bool IsBusy(float now) => IsMoving || _landT0 >= 0f || _hopT0 >= 0f || _sleepPending ||
                                          (_sleepT0 >= 0f && now - _sleepT0 < SleepDuration);
        public Vector3 Target => _to;
        /// <summary>Set by BoardView when a pending sleep starts (to pulse the nest).</summary>
        public bool SleepJustStarted { get; private set; }

        public PlopView(Transform parent, ShapeAtlas atlas, int colorId, int seed)
        {
            Root = new GameObject("Plop").transform;
            Root.SetParent(parent, false);
            _shadow = Child("Shadow", Root, atlas.Get("plopShadow"), out _shadowR);
            _shadow.localPosition = new Vector3(0f, GroundY + 0.005f, 0f);
            _body = Child("Body", Root, atlas.Get(ShapeAtlas.PlopKey(colorId)), out _bodyR);
            _body.localPosition = new Vector3(0f, GroundY, 0f);
            _cheekL = Child("CheekL", _body, atlas.Get("cheek"), out _cheekLR);
            _cheekR = Child("CheekR", _body, atlas.Get("cheek"), out _cheekRR);
            _eyeOpen = atlas.Get("eye");
            _eyeClosed = atlas.Get("eyeClosed");
            _eyeL = Child("EyeL", _body, _eyeOpen, out _eyeLR);
            _eyeR = Child("EyeR", _body, _eyeOpen, out _eyeRR);
            _rng = (uint)(seed * 747796405 + 2891336453);
            _phase = Rand() * Mathf.PI * 2f;
            _nextBlink = 1.0f + Rand() * 3f;
            ApplyFace(Vector2.zero, 1f);
        }

        private static Transform Child(string name, Transform parent, Sprite sprite, out SpriteRenderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            return go.transform;
        }

        private float Rand()
        {
            _rng = _rng * 1664525u + 1013904223u;
            return (_rng >> 8) / 16777216f;
        }

        /// <summary>Depth order: rows nearer the bottom of the screen draw in front.</summary>
        public void SetSortingBase(int order)
        {
            _shadowR.sortingOrder = order + 2;
            _bodyR.sortingOrder = order + 10;
            _cheekLR.sortingOrder = order + 11;
            _cheekRR.sortingOrder = order + 11;
            _eyeLR.sortingOrder = order + 12;
            _eyeRR.sortingOrder = order + 12;
        }

        // ------------------------------------------------------------------ commands

        public void Snap(Vector3 position, bool asleep)
        {
            _from = _to = position;
            Root.localPosition = position;
            _moveT0 = _bumpT0 = _landT0 = _hopT0 = -1f;
            _sleepPending = false;
            _asleep = asleep;
            _sleepT0 = asleep ? -1000f : -1f;
            _look = Vector2.zero;
        }

        public void StartMove(Vector3 to, Vector2 dir, float now)
        {
            _from = Root.localPosition - BumpOffset(now); // continue smoothly from where it is drawn
            _to = to;
            _moveDir = dir;
            _moveT0 = now;
            _bumpT0 = -1f;
            _landT0 = -1f;
            _look = dir * 0.032f;
            _lookUntil = now + 0.7f;
        }

        public void StartBump(Vector2 dir, float now)
        {
            if (_asleep) return;
            _bumpDir = dir;
            _bumpT0 = now;
            _look = dir * 0.032f;
            _lookUntil = now + 0.6f;
        }

        /// <summary>Falls asleep when the current move lands (or now, if not moving).</summary>
        public void QueueSleep(float now)
        {
            if (_moveT0 >= 0f) _sleepPending = true;
            else BeginSleep(now);
        }

        public void StartHop(float startTime) { if (_hopT0 < 0f) _hopT0 = startTime; }

        private void BeginSleep(float now)
        {
            _asleep = true;
            _sleepPending = false;
            _sleepT0 = now;
            SleepJustStarted = true;
        }

        // ------------------------------------------------------------------ per-frame

        public void Tick(float now)
        {
            SleepJustStarted = false;

            // ---- position (authoritative target comes from gameplay events) ----
            Vector3 pos = _to;
            if (_moveT0 >= 0f)
            {
                float u = (now - _moveT0) / MoveDuration;
                if (u >= 1f)
                {
                    _moveT0 = -1f;
                    _landT0 = now;
                    _landDir = _moveDir;
                    if (_sleepPending) BeginSleep(now);
                }
                else pos = Vector3.LerpUnclamped(_from, _to, EaseOutCubic(u));
            }
            Root.localPosition = pos + BumpOffset(now);

            // ---- body deformation ----
            float sx = 1f, sy = 1f, lift = 0f;
            float sleepAmount = !_asleep ? 0f : _sleepT0 < 0f ? 1f : Smooth((now - _sleepT0) / SleepDuration);

            float breathPeriod = _asleep ? 3.4f : 2.6f;
            float b = Mathf.Sin(now * (2f * Mathf.PI / breathPeriod) + _phase);
            float amp = Mathf.Lerp(0.018f, 0.030f, sleepAmount);
            sy *= 1f + amp * b;
            sx *= 1f - amp * 0.65f * b;
            sy *= 1f - 0.12f * sleepAmount;   // sleeping Plops relax and flatten a little
            sx *= 1f + 0.07f * sleepAmount;

            if (_moveT0 >= 0f)
            {
                float u = Mathf.Clamp01((now - _moveT0) / MoveDuration);
                float along = 1f - 0.10f * Mathf.Max(0f, 1f - u / 0.2f)                    // tiny squash at start
                              + 0.15f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((u - 0.08f) / 0.92f)); // stretch in flight
                ApplyAxis(_moveDir, along, ref sx, ref sy);
            }
            else if (_landT0 >= 0f)
            {
                float u = (now - _landT0) / LandDuration;
                if (u >= 1f) _landT0 = -1f;
                else
                {
                    float along = 1f - 0.11f * Mathf.Exp(-3f * u) * Mathf.Cos(3f * Mathf.PI * u) * (1f - u); // settle wobble
                    ApplyAxis(_landDir, along, ref sx, ref sy);
                }
            }

            if (_bumpT0 >= 0f)
            {
                float u = Mathf.Clamp01((now - _bumpT0) / BumpDuration);
                ApplyAxis(_bumpDir, 1f - 0.16f * Mathf.Sin(Mathf.PI * u), ref sx, ref sy);
            }

            if (_hopT0 >= 0f && now >= _hopT0)
            {
                float u = (now - _hopT0) / HopDuration;
                if (u >= 1f)
                {
                    _hopT0 = -1f;
                    _landT0 = now;
                    _landDir = Vector2.up;
                }
                else
                {
                    lift = 0.20f * Mathf.Sin(Mathf.PI * u);
                    sy *= 1f + 0.08f * Mathf.Sin(Mathf.PI * u);
                    sx *= 1f - 0.05f * Mathf.Sin(Mathf.PI * u);
                }
            }

            _body.localPosition = new Vector3(0f, GroundY + lift, 0f);
            _body.localScale = new Vector3(sx * BodyScale, sy * BodyScale, 1f);
            float shadowScale = sx * (1f - 1.2f * lift);
            _shadow.localScale = new Vector3(shadowScale, shadowScale, 1f);

            // ---- face: blink, look direction, closed eyes when asleep ----
            float eyeOpen = 1f;
            if (_asleep) eyeOpen = 1f;
            else
            {
                if (now >= _nextBlink) { _blinkT0 = now; _nextBlink = now + 2.4f + Rand() * 3.2f; }
                if (_blinkT0 >= 0f)
                {
                    float u = (now - _blinkT0) / 0.14f;
                    if (u >= 1f) _blinkT0 = -1f;
                    else eyeOpen = 1f - 0.9f * Mathf.Sin(Mathf.PI * u);
                }
            }
            if (now > _lookUntil) _look = Vector2.Lerp(_look, Vector2.zero, 0.15f);
            var eyeSprite = _asleep ? _eyeClosed : _eyeOpen;
            if (_eyeLR.sprite != eyeSprite) { _eyeLR.sprite = eyeSprite; _eyeRR.sprite = eyeSprite; }
            ApplyFace(_asleep ? Vector2.zero : _look, eyeOpen);
        }

        private Vector3 BumpOffset(float now)
        {
            if (_bumpT0 < 0f) return Vector3.zero;
            float u = (now - _bumpT0) / BumpDuration;
            if (u >= 1f) { _bumpT0 = -1f; return Vector3.zero; }
            return (Vector3)(_bumpDir * (0.09f * Mathf.Sin(Mathf.PI * u)));
        }

        private void ApplyFace(Vector2 look, float eyeOpen)
        {
            _eyeL.localPosition = new Vector3(-0.105f + look.x, 0.40f + look.y, 0f);
            _eyeR.localPosition = new Vector3(0.105f + look.x, 0.40f + look.y, 0f);
            var eyeScale = new Vector3(1f, Mathf.Max(0.08f, eyeOpen), 1f);
            _eyeL.localScale = eyeScale;
            _eyeR.localScale = eyeScale;
            _cheekL.localPosition = new Vector3(-0.205f + look.x * 0.5f, 0.30f + look.y * 0.5f, 0f);
            _cheekR.localPosition = new Vector3(0.205f + look.x * 0.5f, 0.30f + look.y * 0.5f, 0f);
        }

        /// <summary>Stretches along the movement axis and conserves area across it.</summary>
        private static void ApplyAxis(Vector2 dir, float along, ref float sx, ref float sy)
        {
            float across = 1f / Mathf.Sqrt(Mathf.Max(0.3f, along));
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) { sx *= along; sy *= across; }
            else { sy *= along; sx *= across; }
        }

        private static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
