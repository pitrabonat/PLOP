using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Animates the shared jelly toy and its face from board events.</summary>
    public sealed class PlopView
    {
        public const float MoveDuration = 0.155f;
        public const float BumpDuration = 0.185f;
        public const float LandDuration = 0.24f;
        public const float SleepDuration = 0.38f;
        public const float HopDuration = 0.30f;
        internal const float CelebrationDelay = MoveDuration + 0.07f;
        internal const float CelebrationStagger = 0.053f;

        private const float AnticipationDuration = 0.024f;
        private const float BlinkDuration = 0.14f;
        private const float BumpImpactTime = 0.42f;
        private const float BodyScale = 0.98f;
        private const float GroundY = -0.345f;

        public readonly Transform Root;
        private readonly Transform _pose, _body, _eyeL, _eyeR, _cheekL, _cheekR, _mouth, _shadow;
        private readonly SpriteRenderer _bodyR, _eyeLR, _eyeRR, _cheekLR, _cheekRR, _mouthR, _shadowR;
        private readonly Sprite _eyeOpen, _eyeClosed, _mouthAwake, _mouthSleep, _mouthBump;

        private Vector3 _from, _to;
        private Vector2 _moveDir, _bumpDir, _landDir, _look;
        private float _moveT0 = -1f, _bumpT0 = -1f, _landT0 = -1f, _sleepT0 = -1f, _hopT0 = -1f, _blinkT0 = -1f;
        private float _nextBlink, _lookUntil;
        private readonly float _phase;
        private bool _asleep, _sleepPending, _sleepPoseFrozen;
        private uint _rng;

        internal bool IsSliding => _moveT0 >= 0f;
        public bool IsMoving => IsSliding || _bumpT0 >= 0f;
        public bool IsBusy(float now) => IsMoving || _landT0 >= 0f || _hopT0 >= 0f || _sleepPending ||
                                         (_sleepT0 >= 0f && now - _sleepT0 < SleepDuration);
        public Vector3 Target => _to;
        public bool SleepJustStarted { get; private set; }

        public PlopView(Transform parent, ShapeAtlas atlas, int colorId, int seed)
        {
            Root = new GameObject("Plop").transform;
            Root.SetParent(parent, false);
            _pose = new GameObject("Pose").transform;
            _pose.SetParent(Root, false);
            _shadow = Child("Shadow", _pose, atlas.Get("plopShadow"), out _shadowR);
            _shadow.localPosition = new Vector3(0f, GroundY + 0.005f, 0f);
            _body = Child("Body", _pose, atlas.Get(ShapeAtlas.PlopKey(colorId)), out _bodyR);
            _body.localPosition = new Vector3(0f, GroundY, 0f);

            _cheekL = Child("CheekL", _body, atlas.Get("cheek"), out _cheekLR);
            _cheekR = Child("CheekR", _body, atlas.Get("cheek"), out _cheekRR);
            _eyeOpen = atlas.Get("eye");
            _eyeClosed = atlas.Get("eyeClosed");
            _eyeL = Child("EyeL", _body, _eyeOpen, out _eyeLR);
            _eyeR = Child("EyeR", _body, _eyeOpen, out _eyeRR);
            _mouthAwake = atlas.Get("mouth");
            _mouthSleep = atlas.Get("mouthSleep");
            _mouthBump = atlas.Get("mouthBump");
            _mouth = Child("Mouth", _body, _mouthAwake, out _mouthR);

            _rng = (uint)(seed * 747796405 + 2891336453);
            _phase = Rand() * Mathf.PI * 2f;
            _nextBlink = Time.unscaledTime + 1f + Rand() * 3f;
            ApplyFace(Vector2.zero, 1f, 0f, 0f);
        }

        private static Transform Child(string name, Transform parent, Sprite sprite, out SpriteRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return go.transform;
        }

        private float Rand()
        {
            _rng = _rng * 1664525u + 1013904223u;
            return (_rng >> 8) / 16777216f;
        }

        public void SetSortingBase(int order)
        {
            _shadowR.sortingOrder = order + 2;
            _bodyR.sortingOrder = order + 10;
            _cheekLR.sortingOrder = order + 11;
            _cheekRR.sortingOrder = order + 11;
            _mouthR.sortingOrder = order + 12;
            _eyeLR.sortingOrder = order + 12;
            _eyeRR.sortingOrder = order + 12;
        }

        /// <summary>Immediately restores a gameplay snapshot and clears transient animation.</summary>
        public void Snap(Vector3 position, bool asleep)
        {
            _from = _to = position;
            _moveT0 = _bumpT0 = _landT0 = _hopT0 = _sleepT0 = _blinkT0 = -1f;
            _sleepPending = _sleepPoseFrozen = false;
            _asleep = asleep;
            _look = Vector2.zero;
            _nextBlink = Time.unscaledTime + 1f + Rand() * 3f;
            Tick(Time.unscaledTime);
        }

        public void StartMove(Vector3 to, Vector2 dir, float now)
        {
            _from = Root.localPosition;
            _to = to;
            _moveDir = dir;
            _moveT0 = now;
            _bumpT0 = _landT0 = -1f;
            _look = dir * 0.033f;
            _lookUntil = now + 0.46f;
        }

        public void StartBump(Vector2 dir, float now)
        {
            if (_asleep || _sleepPending) return;
            _bumpDir = dir;
            _bumpT0 = now;
            _look = dir * 0.033f;
            _lookUntil = now + 0.33f;
        }

        public void QueueSleep(float now)
        {
            _sleepPending = true;
            if (!IsSliding) _sleepT0 = now;
        }

        public void StartHop(float startTime)
        {
            if (_hopT0 >= 0f) return;
            _hopT0 = startTime;
            _sleepPoseFrozen = false;
        }

        public void Tick(float now)
        {
            SleepJustStarted = false;
            if (_sleepPoseFrozen) return;

            // The root follows the board's supplied endpoints. Every other offset stays on a child.
            Vector3 position = _to;
            float moveTime = IsSliding ? now - _moveT0 : MoveDuration;
            if (IsSliding)
            {
                float travel = (moveTime - AnticipationDuration) / (MoveDuration - AnticipationDuration);
                if (travel >= 1f)
                {
                    _moveT0 = -1f;
                    _landT0 = now;
                    _landDir = _moveDir;
                }
                else position = Vector3.LerpUnclamped(_from, _to, Travel(Mathf.Clamp01(travel)));
            }
            Root.localPosition = position;

            if (_sleepPending && !IsSliding)
            {
                _asleep = true;
                _sleepPending = false;
                if (_sleepT0 < 0f) _sleepT0 = now;
                SleepJustStarted = true;
            }

            float recoil = BumpAmount(now);
            _pose.localPosition = (Vector3)(_bumpDir * (0.083f * recoil));

            float asleep = !_asleep ? 0f : _sleepT0 < 0f ? 1f : Smooth((now - _sleepT0) / SleepDuration);
            float awake = 1f - asleep;
            float breath = Mathf.Sin(now * (2f * Mathf.PI / 2.9f) + _phase);
            float breathScale = 0.028f * breath * awake;
            float sx = 1f - breathScale * 0.65f + 0.049f * asleep;
            float sy = 1f + breathScale - 0.17f * asleep;
            float lift = -0.012f * asleep;
            float tilt = 0.98f * Mathf.Sin(now * 1.79f + _phase) * awake;

            if (IsSliding)
            {
                float along;
                if (moveTime < AnticipationDuration)
                {
                    along = 1f - 0.15f * Smooth(moveTime / AnticipationDuration);
                }
                else
                {
                    float travel = Mathf.Clamp01((moveTime - AnticipationDuration) / (MoveDuration - AnticipationDuration));
                    float stretch = Mathf.Sin(Mathf.PI * travel);
                    along = 1f - 0.15f * Mathf.Max(0f, 1f - travel * 6f) + 0.20f * stretch;
                    tilt -= 5f * _moveDir.x * stretch;
                    lift += 0.028f * stretch;
                }
                ApplyAxis(_moveDir, along, ref sx, ref sy);
            }
            else if (_landT0 >= 0f)
            {
                float u = (now - _landT0) / LandDuration;
                if (u >= 1f) _landT0 = -1f;
                else
                {
                    float envelope = (1f - u) * (1f - u);
                    float along = 1f - 0.12f * Mathf.Cos(3f * Mathf.PI * u) * envelope;
                    ApplyAxis(_landDir, along, ref sx, ref sy);
                }
            }

            if (recoil != 0f)
                ApplyAxis(_bumpDir, 1f - 0.19f * recoil, ref sx, ref sy);

            float celebration = Celebration(now);
            lift += celebration * 0.096f;
            sy += celebration * 0.07f;
            sx -= celebration * 0.05f;

            _body.localPosition = new Vector3(0f, GroundY + lift, 0f);
            _body.localRotation = Quaternion.AngleAxis(tilt, Vector3.forward);
            _body.localScale = new Vector3(sx * BodyScale, sy * BodyScale, 1f);
            float shadowScale = sx * (1f - 1.2f * Mathf.Max(0f, lift));
            _shadow.localScale = new Vector3(shadowScale, shadowScale, 1f);

            float eyeOpen = _asleep ? Mathf.Lerp(1f, 0.12f, Smooth(asleep / 0.38f)) : Blink(now);
            if (!_asleep && recoil > 0f) eyeOpen *= 1f - 0.72f * Smooth(recoil);
            Vector2 look = Vector2.Lerp(_look, Vector2.zero, Smooth((now - _lookUntil) / 0.29f)) * (1f - asleep);
            ApplyFace(look, eyeOpen, asleep, recoil);
            _sleepPoseFrozen = _asleep && asleep >= 1f && _hopT0 < 0f && _landT0 < 0f && !IsMoving;
        }

        private float Blink(float now)
        {
            if (now >= _nextBlink)
            {
                _blinkT0 = now;
                _nextBlink = now + 2.6f + Rand() * 3.7f;
            }
            if (_blinkT0 < 0f) return 1f;
            float u = (now - _blinkT0) / BlinkDuration;
            if (u < 1f) return 1f - 0.91f * Mathf.Sin(Mathf.PI * u);
            _blinkT0 = -1f;
            return 1f;
        }

        private float BumpAmount(float now)
        {
            if (_bumpT0 < 0f) return 0f;
            float u = (now - _bumpT0) / BumpDuration;
            if (u >= 1f)
            {
                _bumpT0 = -1f;
                return 0f;
            }
            if (u < BumpImpactTime) return Mathf.Sin(Mathf.PI * 0.5f * u / BumpImpactTime);
            float rebound = (u - BumpImpactTime) / (1f - BumpImpactTime);
            float envelope = (1f - rebound) * (1f - rebound);
            return Mathf.Cos(Mathf.PI * 1.65f * rebound) * envelope;
        }

        private float Celebration(float now)
        {
            if (_hopT0 < 0f || now < _hopT0) return 0f;
            float u = (now - _hopT0) / HopDuration;
            if (u < 1f) return Mathf.Sin(Mathf.PI * u);
            _hopT0 = -1f;
            _landT0 = now;
            _landDir = Vector2.up;
            return 0f;
        }

        private void ApplyFace(Vector2 look, float eyeOpen, float asleep, float recoil)
        {
            bool closed = _asleep && asleep >= 0.38f;
            Sprite eyeSprite = closed ? _eyeClosed : _eyeOpen;
            if (_eyeLR.sprite != eyeSprite)
            {
                _eyeLR.sprite = eyeSprite;
                _eyeRR.sprite = eyeSprite;
            }
            Sprite mouthSprite = closed ? _mouthSleep : recoil > 0.58f ? _mouthBump : _mouthAwake;
            if (_mouthR.sprite != mouthSprite) _mouthR.sprite = mouthSprite;

            float faceOffset = -0.022f * asleep;
            _eyeL.localPosition = new Vector3(-0.138f + look.x, 0.407f + look.y + faceOffset, 0f);
            _eyeR.localPosition = new Vector3(0.122f + look.x, 0.403f + look.y + faceOffset, 0f);
            var eyeScale = new Vector3(1f, closed ? 1f : Mathf.Max(0.08f, eyeOpen), 1f);
            _eyeL.localScale = _eyeR.localScale = eyeScale;
            _mouth.localPosition = new Vector3(-0.005f + look.x * 0.75f, 0.300f + look.y * 0.65f + faceOffset, 0f);
            _cheekL.localPosition = new Vector3(-0.219f + look.x * 0.5f, 0.320f + look.y * 0.5f + faceOffset, 0f);
            _cheekR.localPosition = new Vector3(0.204f + look.x * 0.5f, 0.319f + look.y * 0.5f + faceOffset, 0f);
        }

        private static void ApplyAxis(Vector2 dir, float along, ref float sx, ref float sy)
        {
            float across = 1f / Mathf.Sqrt(Mathf.Max(0.3f, along));
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            {
                sx *= along;
                sy *= across;
            }
            else
            {
                sy *= along;
                sx *= across;
            }
        }

        private static float Travel(float t)
        {
            float settling = 1f - (1f - t) * (1f - t);
            return Mathf.Lerp(Smooth(t), settling, 0.45f);
        }
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
