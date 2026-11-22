using Plop.Core;
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
        internal const float RestoreDuration = 0.12f;
        private const float LandingSqueeze = 0.095f;
        private const float SoftBumpDuration = 0.16f;
        private const float InterruptionBlend = 0.048f;

        public readonly Transform Root;
        private readonly Transform _pose, _body, _eyeL, _eyeR, _cheekL, _cheekR, _mouth, _shadow;
        private readonly SpriteRenderer _bodyR, _eyeLR, _eyeRR, _cheekLR, _cheekRR, _mouthR, _shadowR;
        private readonly Sprite _eyeOpen, _eyeClosed, _mouthAwake, _mouthSleep, _mouthBump;

        private Vector3 _from, _to, _restoreOffset;
        private Vector2 _moveDir, _bumpDir, _landDir, _look;
        private float _moveT0 = -1f, _bumpT0 = -1f, _landT0 = -1f, _sleepT0 = -1f, _hopT0 = -1f, _blinkT0 = -1f;
        private float _nextBlink, _lookUntil, _restoreT0 = -1f;
        private readonly float _phase;
        private bool _asleep, _sleepPending, _sleepPoseFrozen, _reducedMotion;
        private uint _rng;
        private bool _softBump;
        private float _blendT0 = -1f;
        private Vector3 _blendOffset, _displayedRoot, _displayedOffset;
        private Vector4 _displayedBody = new Vector4(1f, 1f, 0f, 0f);
        private Vector4 _blendBody;
        private float _displayedShadowWidth = 1f;
        private int _sortingBase = int.MinValue;

        private Vector2 _displayedLook;
        private float _displayedFaceSleep = -1f, _displayedEyeScale = 1f;
        private Sprite _displayedEye, _displayedMouth;

        internal bool IsSliding => _moveT0 >= 0f;
        public bool IsMoving => IsSliding || _bumpT0 >= 0f;
        public bool IsBusy(float now) => IsMoving || _landT0 >= 0f || _hopT0 >= 0f || _sleepPending || _restoreT0 >= 0f ||
                                         (_sleepT0 >= 0f && now - _sleepT0 < SleepDuration);
        public Vector3 Target => _to;
        public bool SleepJustStarted { get; private set; }

        public PlopView(Transform parent, ShapeAtlas atlas, int colorId, int seed, bool reducedMotion = false)
        {
            _reducedMotion = reducedMotion;
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
            _displayedEye = _eyeOpen;
            _displayedMouth = _mouthAwake;

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
            if (_sortingBase == order) return;
            _sortingBase = order;
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
            _moveT0 = _bumpT0 = _landT0 = _hopT0 = _sleepT0 = _blinkT0 = _restoreT0 = _blendT0 = -1f;
            _sleepPending = _sleepPoseFrozen = false;
            _asleep = asleep;
            _look = Vector2.zero;
            SleepJustStarted = false;
            _nextBlink = Time.unscaledTime + 1f + Rand() * 3f;
            if (_reducedMotion) ApplyStillPose();
            else Tick(Time.unscaledTime);
        }

        /// <summary>The view root restores immediately. A small Undo offset decays on its child pose.</summary>
        public void Restore(Vector3 position, bool asleep, bool ease)
        {
            Vector3 delta = _displayedRoot + _displayedOffset - position;
            Vector4 oldBody = _displayedBody;
            Snap(position, asleep);
            if (!ease || _reducedMotion || delta.sqrMagnitude < 0.0025f || delta.sqrMagnitude > 1.21f)
                return;

            float now = Time.unscaledTime;
            _restoreOffset = delta;
            _restoreT0 = _blendT0 = now;
            _blendOffset = delta;
            _blendBody = oldBody;
            DisplayPose(position, delta, oldBody);
            _sleepPoseFrozen = false;
        }

        public void SetReducedMotion(bool reduced)
        {
            if (_reducedMotion == reduced) return;
            _reducedMotion = reduced;
            Snap(_to, _asleep || _sleepPending);
        }

        private void ApplyStillPose()
        {
            var body = _asleep
                ? new Vector4(1.049f * BodyScale, 0.83f * BodyScale, -0.012f, 0f)
                : new Vector4(BodyScale, BodyScale, 0f, 0f);
            DisplayPose(_to, Vector3.zero, body);
            ApplyFace(Vector2.zero, 1f, _asleep ? 1f : 0f, 0f);
        }

        public void StartMove(Vector3 to, Vector2 dir, float now)
        {
            if (_reducedMotion)
            {
                Snap(to, false);
                return;
            }
            PreserveInterruptedPose(now);
            _from = _displayedRoot;
            _to = to;
            _moveDir = dir;
            _moveT0 = now;
            _bumpT0 = _landT0 = -1f;
            _look = dir * 0.033f;
            _lookUntil = now + 0.46f;
        }

        public void StartBump(Vector2 dir, float now, BlockReason reason = BlockReason.Wall)
        {
            if (_reducedMotion || _asleep || _sleepPending) return;
            PreserveInterruptedPose(now);
            _softBump = reason == BlockReason.Plop;
            _bumpDir = dir;
            _bumpT0 = now;
            _look = dir * 0.033f;
            _lookUntil = now + 0.33f;
        }

        private void PreserveInterruptedPose(float now)
        {
            if (!IsBusy(now)) return;
            _blendT0 = now;
            _blendOffset = _displayedOffset;
            _blendBody = _displayedBody;
        }

        public void QueueSleep(float now)
        {
            if (_reducedMotion)
            {
                Snap(_to, true);
                return;
            }
            _sleepPending = true;
            if (!IsSliding) _sleepT0 = now;
        }

        public void StartHop(float startTime)
        {
            if (_reducedMotion || _hopT0 >= 0f) return;
            _hopT0 = startTime;
            _sleepPoseFrozen = false;
        }

        public void Tick(float now)
        {
            SleepJustStarted = false;
            if (_reducedMotion || _sleepPoseFrozen) return;

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
            if (_sleepPending && !IsSliding)
            {
                _asleep = true;
                _sleepPending = false;
                if (_sleepT0 < 0f) _sleepT0 = now;
                SleepJustStarted = true;
            }

            float recoil = BumpAmount(now);
            float recoilReach = _softBump ? 0.046f : 0.083f;
            Vector3 offset = RestoreOffset(now) + (Vector3)(_bumpDir * (recoilReach * recoil));

            float asleep = !_asleep ? 0f : _sleepT0 < 0f ? 1f : Smooth((now - _sleepT0) / SleepDuration);
            float awake = 1f - asleep;
            float idle = IsMoving || _restoreT0 >= 0f ? awake * 0.24f : awake;
            float breath = Mathf.Sin(now * (2f * Mathf.PI / 3.4f) + _phase);
            float breathScale = 0.023f * breath * idle;
            float sx = 1f - breathScale * 0.65f + 0.049f * asleep;
            float sy = 1f + breathScale - 0.17f * asleep;
            float lift = -0.012f * asleep;
            float tilt = 0.85f * Mathf.Sin(now * 1.64f + _phase) * idle;

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
                    float release = 1f - Smooth(travel / 0.31f);
                    float landing = Smooth((travel - 0.71f) / 0.29f);
                    along = 1f - 0.15f * release + 0.20f * stretch - LandingSqueeze * landing;
                    tilt -= 4.2f * _moveDir.x * stretch;
                    lift += 0.029f * stretch;
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
                    float along = 1f - LandingSqueeze * Mathf.Cos(2.7f * Mathf.PI * u) * envelope;
                    ApplyAxis(_landDir, along, ref sx, ref sy);
                }
            }

            if (recoil != 0f)
            {
                float squash = _softBump ? 0.123f : 0.19f;
                ApplyAxis(_bumpDir, 1f - squash * recoil, ref sx, ref sy);
            }

            float celebration = Celebration(now);
            lift += celebration * 0.096f;
            sy += celebration * 0.07f;
            sx -= celebration * 0.05f;

            var body = new Vector4(sx * BodyScale, sy * BodyScale, lift, tilt);
            if (_blendT0 >= 0f)
            {
                float u = (now - _blendT0) / InterruptionBlend;
                if (u >= 1f) _blendT0 = -1f;
                else
                {
                    float blend = Smooth(u);
                    offset = Vector3.LerpUnclamped(_blendOffset, offset, blend);
                    body = Vector4.LerpUnclamped(_blendBody, body, blend);
                }
            }
            DisplayPose(position, offset, body);

            float eyeOpen = _asleep ? Mathf.Lerp(1f, 0.12f, Smooth(asleep / 0.38f)) : Blink(now);
            if (!_asleep && recoil > 0f) eyeOpen *= 1f - (_softBump ? 0.44f : 0.72f) * Smooth(recoil);
            float lookTime = (now - _lookUntil) / 0.29f;
            if (lookTime >= 1f) _look = Vector2.zero;
            Vector2 look = _look == Vector2.zero
                ? Vector2.zero
                : _look * ((1f - Smooth(lookTime)) * (1f - asleep));
            ApplyFace(look, eyeOpen, asleep, recoil);
            _sleepPoseFrozen = _asleep && asleep >= 1f && _hopT0 < 0f && _landT0 < 0f &&
                               _restoreT0 < 0f && _blendT0 < 0f && !IsMoving;
        }

        private void DisplayPose(Vector3 root, Vector3 offset, Vector4 body)
        {
            // Moving and resting views share pose emission. Awake idle still breathes, while
            // the stationary root, recoil offset, and fixed ground height make no repeated writes.
            if (!_displayedRoot.Equals(root))
            {
                Root.localPosition = root;
                _displayedRoot = root;
            }
            if (!_displayedOffset.Equals(offset))
            {
                _pose.localPosition = offset;
                _displayedOffset = offset;
            }
            if (_displayedBody.z != body.z) _body.localPosition = new Vector3(0f, GroundY + body.z, 0f);
            if (_displayedBody.w != body.w) _body.localRotation = Quaternion.AngleAxis(body.w, Vector3.forward);
            if (_displayedBody.x != body.x || _displayedBody.y != body.y)
                _body.localScale = new Vector3(body.x, body.y, 1f);
            _displayedBody = body;

            float shadowWidth = (body.x / BodyScale) * (1f - 1.2f * Mathf.Max(0f, body.z));
            if (_displayedShadowWidth == shadowWidth) return;
            _shadow.localScale = new Vector3(shadowWidth, shadowWidth, 1f);
            _displayedShadowWidth = shadowWidth;
        }

        private Vector3 RestoreOffset(float now)
        {
            if (_restoreT0 < 0f) return Vector3.zero;
            float progress = (now - _restoreT0) / RestoreDuration;
            if (progress < 1f) return _restoreOffset * (1f - Smooth(progress));
            _restoreT0 = -1f;
            return Vector3.zero;
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
            float duration = _softBump ? SoftBumpDuration : BumpDuration;
            float impact = _softBump ? 0.48f : BumpImpactTime;
            float u = (now - _bumpT0) / duration;
            if (u >= 1f)
            {
                _bumpT0 = -1f;
                return 0f;
            }
            if (u < impact) return Mathf.Sin(Mathf.PI * 0.5f * u / impact);
            float rebound = (u - impact) / (1f - impact);
            float envelope = (1f - rebound) * (1f - rebound);
            float ringing = _softBump ? 1.18f : 1.65f;
            return Mathf.Cos(Mathf.PI * ringing * rebound) * envelope;
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
            bool sleepingFace = _asleep && asleep >= 0.38f;
            bool closed = sleepingFace || (!_asleep && eyeOpen < 0.17f && recoil < 0.3f);
            Sprite eyeSprite = closed ? _eyeClosed : _eyeOpen;
            if (!ReferenceEquals(_displayedEye, eyeSprite))
            {
                _eyeLR.sprite = _eyeRR.sprite = eyeSprite;
                _displayedEye = eyeSprite;
            }
            Sprite mouthSprite = sleepingFace ? _mouthSleep : recoil > 0.58f && !_softBump ? _mouthBump : _mouthAwake;
            if (!ReferenceEquals(_displayedMouth, mouthSprite))
            {
                _mouthR.sprite = mouthSprite;
                _displayedMouth = mouthSprite;
            }

            float eyeScale = closed ? 1f : Mathf.Max(0.08f, eyeOpen);
            if (_displayedEyeScale != eyeScale)
            {
                _eyeL.localScale = _eyeR.localScale = new Vector3(1f, eyeScale, 1f);
                _displayedEyeScale = eyeScale;
            }
            if (_displayedLook.Equals(look) && _displayedFaceSleep == asleep) return;
            _displayedLook = look;
            _displayedFaceSleep = asleep;

            float faceOffset = -0.022f * asleep;
            _eyeL.localPosition = new Vector3(-0.138f + look.x, 0.407f + look.y + faceOffset, 0f);
            _eyeR.localPosition = new Vector3(0.122f + look.x, 0.403f + look.y + faceOffset, 0f);
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
            // A gently front-loaded curve with zero velocity at both endpoints.
            float remaining = 1f - t;
            return Smooth(t) + 2.15f * t * t * remaining * remaining;
        }
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
