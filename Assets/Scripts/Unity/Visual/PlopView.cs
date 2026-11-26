using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Animates one jelly Plop from board events (or Studio/menu calls). The same rig renders with
    /// SpriteRenderers on the board and with UI Images in menus, so there is a single animation implementation.
    /// Cosmetics (style, finish, face, accessory, personality) come from the atlas's <see cref="CharacterLook"/>;
    /// none of it changes where the Plop is: the root follows the endpoints the board supplies.
    /// </summary>
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
        private const float WobbleDuration = 0.8f;

        public readonly Transform Root;
        private readonly Transform _pose;
        private readonly PlopPart _body, _shadow, _eyeL, _eyeR, _cheekL, _cheekR, _mouth, _browL, _browR, _accessory;
        private readonly ShapeAtlas _atlas;
        private readonly Sprite[] _eyes, _mouths; // cached per rig: no per-frame string keys
        private readonly string _prefix;
        private readonly BodyStyle _style;
        private readonly Personality _personality;
        private readonly bool _ui;

        private Vector3 _from, _to, _restoreOffset;
        private Vector2 _moveDir, _bumpDir, _landDir, _look;
        private float _moveT0 = -1f, _bumpT0 = -1f, _landT0 = -1f, _sleepT0 = -1f, _hopT0 = -1f, _blinkT0 = -1f, _wobbleT0 = -1f;
        private float _nextBlink, _lookUntil, _restoreT0 = -1f;
        private float _moveDuration = MoveDuration, _anticipation = AnticipationDuration;
        private readonly float _phase;
        private bool _asleep, _sleepPending, _sleepPoseFrozen, _reducedMotion;
        private uint _rng;
        private bool _softBump;
        private float _blendT0 = -1f;
        private Vector3 _blendOffset, _displayedRoot, _displayedOffset;
        private Vector4 _displayedBody = new Vector4(1f, 1f, 0f, 0f);
        private Vector4 _blendBody;
        private float _displayedShadowWidth = 1f;
        // Phase 11.5 Loop teleport: shrink into the entry portal, reappear and grow at the exit.
        private Vector3 _warpFrom;
        private float _warpT0 = -1f, _warpDuration = 0.3f, _displayedWarp = 1f;
        internal const float WarpEntryShare = 0.45f;
        private int _sortingBase = int.MinValue;

        private Expression _override = Expression.Neutral;
        private float _overrideUntil = -1f;
        private bool _overrideForever;
        private float _sleepyDroop;
        private Vector2 _displayedLook;
        private float _displayedFaceSleep = -1f, _displayedEyeScale = -1f;
        private Expression _displayedExpression = (Expression)(-1);

        internal bool IsSliding => _moveT0 >= 0f || _warpT0 >= 0f;
        internal bool IsWarping => _warpT0 >= 0f;
        public bool IsMoving => IsSliding || _bumpT0 >= 0f;
        public bool IsBusy(float now) => IsMoving || _landT0 >= 0f || _hopT0 >= 0f || _sleepPending || _restoreT0 >= 0f ||
                                         (_sleepT0 >= 0f && now - _sleepT0 < SleepDuration);
        public Vector3 Target => _to;
        public bool SleepJustStarted { get; private set; }
        public bool IsAsleep => _asleep;
        public CharacterLook Look => _atlas.Look;
        /// <summary>The expression currently shown (after overrides, sleep, recoil and celebration).</summary>
        public Expression CurrentExpression => _displayedExpression;
        public Transform AccessoryTransform => _accessory?.Transform;

        public PlopView(Transform parent, ShapeAtlas atlas, int colorId, int seed, bool reducedMotion = false)
            : this(parent, atlas, colorId, seed, reducedMotion, false) { }

        /// <param name="ui">Build with UI Images under a RectTransform parent (menus, Studio). The caller scales
        /// <see cref="Root"/> to its pixels per cell.</param>
        /// <param name="keyPrefix">Atlas key prefix for multi-variant atlases (Studio thumbnails).</param>
        public PlopView(Transform parent, ShapeAtlas atlas, int colorId, int seed, bool reducedMotion, bool ui, string keyPrefix = "")
        {
            _atlas = atlas;
            _prefix = keyPrefix ?? "";
            _ui = ui;
            _reducedMotion = reducedMotion;
            var look = atlas.Look ?? CharacterLook.Default;
            _style = look.Style;
            _personality = _style.Personality;

            Root = ui ? new GameObject("Plop", typeof(RectTransform)).transform : new GameObject("Plop").transform;
            Root.SetParent(parent, false);
            _pose = ui ? new GameObject("Pose", typeof(RectTransform)).transform : new GameObject("Pose").transform;
            _pose.SetParent(Root, false);
            if (ui) ((RectTransform)_pose).sizeDelta = ((RectTransform)Root).sizeDelta = Vector2.zero;

            _shadow = new PlopPart("Shadow", _pose, Get(ShapeAtlas.ShadowKey), ui);
            _shadow.Transform.localPosition = new Vector3(0f, GroundY + 0.005f, 0f);
            _body = new PlopPart("Body", _pose, Get(ShapeAtlas.PlopKey(colorId)), ui);
            _body.Transform.localPosition = new Vector3(0f, GroundY, 0f);

            _eyes = new Sprite[System.Enum.GetValues(typeof(EyeShape)).Length];
            for (int i = 0; i < _eyes.Length; i++) _eyes[i] = Get(ShapeAtlas.EyeKey((EyeShape)i));
            _mouths = new Sprite[System.Enum.GetValues(typeof(MouthShape)).Length];
            for (int i = 0; i < _mouths.Length; i++) _mouths[i] = Get(ShapeAtlas.MouthKey((MouthShape)i));

            _cheekL = new PlopPart("CheekL", _body.Transform, Get(ShapeAtlas.CheekKey), ui);
            _cheekR = new PlopPart("CheekR", _body.Transform, Get(ShapeAtlas.CheekKey), ui);
            _mouth = new PlopPart("Mouth", _body.Transform, Get(ShapeAtlas.MouthKey(MouthShape.Neutral)), ui);
            _eyeL = new PlopPart("EyeL", _body.Transform, Get(ShapeAtlas.EyeKey(EyeShape.Open)), ui);
            _eyeR = new PlopPart("EyeR", _body.Transform, Get(ShapeAtlas.EyeKey(EyeShape.Open)), ui);
            _browL = new PlopPart("BrowL", _body.Transform, Get(ShapeAtlas.BrowKey), ui) { Visible = false };
            _browR = new PlopPart("BrowR", _body.Transform, Get(ShapeAtlas.BrowKey), ui) { Visible = false };

            var accessory = look.Accessory;
            if (accessory != null && accessory.Anchor != AccessoryAnchor.None && atlas.Has(_prefix + ShapeAtlas.AccessoryKey))
            {
                _accessory = new PlopPart("Accessory", _body.Transform, Get(ShapeAtlas.AccessoryKey), ui);
                Vector2 anchor = accessory.Anchor == AccessoryAnchor.Top ? _style.TopAnchor : _style.SideAnchor;
                _accessory.Transform.localPosition = anchor + accessory.Offset;
                _accessory.Transform.localRotation = Quaternion.AngleAxis(accessory.Rotation, Vector3.forward);
            }

            _rng = (uint)(seed * 747796405 + 2891336453);
            _phase = Rand() * Mathf.PI * 2f;
            _nextBlink = Time.unscaledTime + 1f + Rand() * 3f;
            ApplyFace(Vector2.zero, 1f, 0f, 0f, Time.unscaledTime);
        }

        private Sprite Get(string key) => _atlas.Get(_prefix + key);

        private float Rand()
        {
            _rng = _rng * 1664525u + 1013904223u;
            return (_rng >> 8) / 16777216f;
        }

        public void SetSortingBase(int order)
        {
            if (_sortingBase == order) return;
            _sortingBase = order;
            _shadow.Order = order + 2;
            _body.Order = order + 10;
            _cheekL.Order = _cheekR.Order = order + 11;
            _mouth.Order = _eyeL.Order = _eyeR.Order = order + 12;
            _browL.Order = _browR.Order = order + 12;
            if (_accessory != null) _accessory.Order = order + 13;
        }

        // ------------------------------------------------------------------ expressions & reactions

        /// <summary>Shows an expression for <paramref name="seconds"/> (infinite keeps it until cleared). Visual only.</summary>
        public void SetExpression(Expression expression, float seconds = float.PositiveInfinity)
        {
            _override = expression;
            _overrideForever = float.IsPositiveInfinity(seconds);
            _overrideUntil = _overrideForever ? float.MaxValue : Time.unscaledTime + seconds;
            _sleepPoseFrozen = false;
            if (_reducedMotion) ApplyStillPose();
        }

        public void ClearExpression()
        {
            _overrideUntil = -1f;
            _overrideForever = false;
            _sleepPoseFrozen = false;
            if (_reducedMotion) ApplyStillPose();
        }

        /// <summary>A short playful response (Studio taps, menu). Reduced Motion keeps only the face change.</summary>
        public void React(Reaction reaction, float now)
        {
            if (_asleep) return;
            switch (reaction)
            {
                case Reaction.Bounce:
                    SetOverride(Expression.Happy, now, 0.62f);
                    if (!_reducedMotion) { _hopT0 = -1f; StartHop(now); }
                    break;
                case Reaction.Surprised:
                    SetOverride(Expression.Surprised, now, 0.75f);
                    if (!_reducedMotion) { _softBump = true; _bumpDir = Vector2.down; _bumpT0 = now; }
                    break;
                case Reaction.Wobble:
                    SetOverride(Expression.Curious, now, 0.9f);
                    if (!_reducedMotion) _wobbleT0 = now;
                    break;
                case Reaction.HappyBlink:
                    SetOverride(Expression.Happy, now, 0.5f);
                    break;
                case Reaction.Squash:
                    SetOverride(Expression.Excited, now, 0.55f);
                    if (!_reducedMotion) { _landT0 = now; _landDir = Vector2.down; }
                    break;
                case Reaction.Sleepy:
                    SetOverride(Expression.Sleepy, now, 1.4f);
                    break;
            }
            _sleepPoseFrozen = false;
            if (_reducedMotion) ApplyStillPose(now);
        }

        private void SetOverride(Expression e, float now, float seconds)
        {
            _override = e;
            _overrideForever = false;
            _overrideUntil = now + seconds;
        }

        // ------------------------------------------------------------------ board API

        /// <summary>Immediately restores a gameplay snapshot and clears transient animation.</summary>
        public void Snap(Vector3 position, bool asleep)
        {
            _from = _to = position;
            _moveT0 = _bumpT0 = _landT0 = _hopT0 = _sleepT0 = _blinkT0 = _restoreT0 = _blendT0 = _wobbleT0 = _warpT0 = -1f;
            _sleepPending = _sleepPoseFrozen = false;
            SetWarpScale(1f);
            _asleep = asleep;
            _look = Vector2.zero;
            if (!_overrideForever) _overrideUntil = -1f;
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

        private void ApplyStillPose() => ApplyStillPose(Time.unscaledTime);

        private void ApplyStillPose(float now)
        {
            var body = _asleep
                ? new Vector4(1.049f * BodyScale, 0.83f * BodyScale, -0.012f, 0f)
                : new Vector4(BodyScale, BodyScale, 0f, 0f);
            DisplayPose(_to, Vector3.zero, body);
            ApplyFace(Vector2.zero, 1f, _asleep ? 1f : 0f, 0f, now);
        }

        public void StartMove(Vector3 to, Vector2 dir, float now) => StartMove(to, dir, now, MoveDuration, true);

        /// <summary>
        /// Travel to <paramref name="to"/> over <paramref name="duration"/> seconds. Forced movement (slides, fan pushes)
        /// passes a longer duration for several cells and skips the anticipation squash.
        /// </summary>
        public void StartMove(Vector3 to, Vector2 dir, float now, float duration, bool anticipate)
        {
            _moveDuration = Mathf.Max(0.05f, duration);
            _anticipation = anticipate ? AnticipationDuration : 0f;
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

        /// <summary>
        /// Loop teleport: the Plop shrinks into the portal at its current position, then reappears at
        /// <paramref name="to"/> and grows back with a small overshoot. Counts as travelling (IsSliding).
        /// </summary>
        public void StartWarp(Vector3 to, float now, float duration)
        {
            if (_reducedMotion)
            {
                Snap(to, false);
                return;
            }
            PreserveInterruptedPose(now);
            _warpFrom = _displayedRoot;
            _from = _to = to;
            _warpT0 = now;
            _warpDuration = Mathf.Max(0.08f, duration);
            _moveT0 = _bumpT0 = _landT0 = -1f;
            _sleepPoseFrozen = false;
        }

        private void SetWarpScale(float scale)
        {
            if (_displayedWarp == scale) return;
            _displayedWarp = scale;
            _pose.localScale = new Vector3(scale, scale, 1f);
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

        // ------------------------------------------------------------------ per-frame pose

        public void Tick(float now)
        {
            SleepJustStarted = false;
            if (_reducedMotion)
            {
                // Still pose; only a changed expression (started or expired) needs a redraw.
                if (_overrideUntil >= 0f && !_overrideForever && now >= _overrideUntil) _overrideUntil = -1f;
                if (ResolveExpression(_asleep ? 1f : 0f, 0f, now) != _displayedExpression) ApplyStillPose(now);
                return;
            }
            if (_sleepPoseFrozen) return;

            Vector3 position = _to;
            bool moving = _moveT0 >= 0f;
            float moveTime = moving ? now - _moveT0 : _moveDuration;
            float warpScale = 1f, warpSpin = 0f;
            if (_warpT0 >= 0f)
            {
                float u = (now - _warpT0) / _warpDuration;
                if (u >= 1f)
                {
                    _warpT0 = -1f;
                    _landT0 = now;
                    _landDir = Vector2.down;
                }
                else if (u < WarpEntryShare)
                {
                    float k = Smooth(u / WarpEntryShare);
                    position = _warpFrom;
                    warpScale = 1f - 0.90f * k;
                    warpSpin = 140f * k;
                }
                else
                {
                    float k = (u - WarpEntryShare) / (1f - WarpEntryShare);
                    float back = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
                    warpScale = 0.10f + 0.90f * back;
                    warpSpin = -110f * (1f - k);
                }
            }
            if (moving)
            {
                float travel = (moveTime - _anticipation) / (_moveDuration - _anticipation);
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

            var p = _personality;
            float recoil = BumpAmount(now);
            float recoilReach = (_softBump ? 0.046f : 0.083f) * p.Reaction;
            Vector3 offset = RestoreOffset(now) + (Vector3)(_bumpDir * (recoilReach * recoil));

            float asleep = !_asleep ? 0f : _sleepT0 < 0f ? 1f : Smooth((now - _sleepT0) / SleepDuration);
            float awake = 1f - asleep;
            float idle = IsMoving || _restoreT0 >= 0f ? awake * 0.24f : awake;
            float breath = Mathf.Sin(now * (2f * Mathf.PI / p.BreathPeriod) + _phase);
            float breathScale = p.BreathAmount * breath * idle;
            float sx = 1f - breathScale * 0.65f + 0.049f * asleep;
            float sy = 1f + breathScale - 0.17f * asleep;
            float lift = -0.012f * asleep;
            float tilt = p.Wobble * Mathf.Sin(now * 1.64f + _phase) * idle;

            bool sleepy = Overriding(now) && _override == Expression.Sleepy;
            _sleepyDroop = Mathf.MoveTowards(_sleepyDroop, sleepy ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            sy -= 0.045f * _sleepyDroop;
            sx += 0.025f * _sleepyDroop;

            if (_wobbleT0 >= 0f)
            {
                float u = (now - _wobbleT0) / WobbleDuration;
                if (u >= 1f) _wobbleT0 = -1f;
                else tilt += 7f * Mathf.Sin(2f * Mathf.PI * 2.5f * u) * (1f - u) * (1f - u);
            }

            tilt += warpSpin;
            if (moving)
            {
                float along;
                if (moveTime < _anticipation)
                {
                    along = 1f - 0.15f * p.Reaction * Smooth(moveTime / _anticipation);
                }
                else
                {
                    float travel = Mathf.Clamp01((moveTime - _anticipation) / (_moveDuration - _anticipation));
                    float stretch = Mathf.Sin(Mathf.PI * travel);
                    float release = 1f - Smooth(travel / 0.31f);
                    float landing = Smooth((travel - 0.71f) / 0.29f);
                    along = 1f - 0.15f * release + 0.20f * p.Reaction * stretch - LandingSqueeze * landing;
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
                    float along = 1f - LandingSqueeze * p.Reaction * Mathf.Cos(2.7f * Mathf.PI * u) * envelope;
                    ApplyAxis(_landDir, along, ref sx, ref sy);
                }
            }

            if (recoil != 0f)
            {
                float squash = (_softBump ? 0.123f : 0.19f) * p.Reaction;
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
            SetWarpScale(warpScale);

            float eyeOpen = _asleep ? Mathf.Lerp(1f, 0.12f, Smooth(asleep / 0.38f)) : Blink(now);
            if (!_asleep && recoil > 0f) eyeOpen *= 1f - (_softBump ? 0.44f : 0.72f) * Smooth(recoil);
            float lookTime = (now - _lookUntil) / 0.29f;
            if (lookTime >= 1f) _look = Vector2.zero;
            Vector2 look = _look == Vector2.zero
                ? Vector2.zero
                : _look * ((1f - Smooth(lookTime)) * (1f - asleep));
            ApplyFace(look, eyeOpen, asleep, recoil, now);
            _sleepPoseFrozen = _asleep && asleep >= 1f && _hopT0 < 0f && _landT0 < 0f && _wobbleT0 < 0f &&
                               _restoreT0 < 0f && _blendT0 < 0f && !IsMoving && !Overriding(now) && _sleepyDroop <= 0f;
        }

        private bool Overriding(float now) => _overrideUntil >= 0f && (_overrideForever || now < _overrideUntil);

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
            var bodyT = _body.Transform;
            if (_displayedBody.z != body.z) bodyT.localPosition = new Vector3(0f, GroundY + body.z, 0f);
            if (_displayedBody.w != body.w) bodyT.localRotation = Quaternion.AngleAxis(body.w, Vector3.forward);
            if (_displayedBody.x != body.x || _displayedBody.y != body.y)
                bodyT.localScale = new Vector3(body.x, body.y, 1f);
            _displayedBody = body;

            float shadowWidth = (body.x / BodyScale) * (1f - 1.2f * Mathf.Max(0f, body.z));
            if (_displayedShadowWidth == shadowWidth) return;
            _shadow.Transform.localScale = new Vector3(shadowWidth, shadowWidth, 1f);
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
                _nextBlink = now + _personality.BlinkMin + Rand() * _personality.BlinkRange;
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

        /// <summary>Chooses the expression for this frame: override, sleep, recoil, celebration, then neutral.</summary>
        private Expression ResolveExpression(float asleep, float recoil, float now)
        {
            if (Overriding(now)) return _override;
            if (_asleep && asleep >= 0.38f) return Expression.Sleeping;
            if (!_asleep && recoil > 0.58f && !_softBump) return Expression.Collision;
            if (_hopT0 >= 0f && now >= _hopT0) return Expression.Happy;
            return _personality.Neutral;
        }

        private void ApplyFace(Vector2 look, float eyeOpen, float asleep, float recoil, float now)
        {
            var expression = ResolveExpression(asleep, recoil, now);
            var f = FaceFeatures.For(expression);
            bool blinkClosed = f.Blinks && !_asleep && eyeOpen < 0.17f && recoil < 0.3f;
            var left = blinkClosed || (_asleep && expression == Expression.Sleeping) ? EyeShape.Closed : f.Left;
            var right = blinkClosed || (_asleep && expression == Expression.Sleeping) ? EyeShape.Closed : f.Right;
            _eyeL.Sprite = _eyes[(int)left];
            _eyeR.Sprite = _eyes[(int)right];
            _mouth.Sprite = _mouths[(int)f.Mouth];

            float baseOpen = f.Left == EyeShape.Open && expression == _personality.Neutral ? _personality.NeutralEyeOpen : 1f;
            float eyeScale = left == EyeShape.Closed || !f.Blinks ? 1f : Mathf.Max(0.08f, eyeOpen) * baseOpen;
            if (_displayedEyeScale != eyeScale)
            {
                _eyeL.Transform.localScale = _eyeR.Transform.localScale = new Vector3(1f, eyeScale, 1f);
                _displayedEyeScale = eyeScale;
            }
            if (_displayedExpression != expression)
            {
                _displayedExpression = expression;
                float cheek = Mathf.Clamp01(f.Cheeks);
                _cheekL.Color = _cheekR.Color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.72f + 0.28f * cheek));
                float cs = Mathf.Max(1f, f.Cheeks);
                _cheekL.Transform.localScale = _cheekR.Transform.localScale = new Vector3(cs, cs, 1f);
                ApplyBrows(f.Brows);
                _displayedLook = new Vector2(float.NaN, float.NaN); // force a reposition
            }
            if (_displayedLook.Equals(look) && _displayedFaceSleep == asleep) return;
            _displayedLook = look;
            _displayedFaceSleep = asleep;

            var st = _style;
            float faceOffset = -0.022f * asleep;
            Vector2 eyeL = st.EyeL, eyeR = st.EyeR, mouth = st.Mouth, cheekL = st.CheekL, cheekR = st.CheekR;
            _eyeL.Transform.localPosition = new Vector3(eyeL.x + look.x, eyeL.y + look.y + faceOffset, 0f);
            _eyeR.Transform.localPosition = new Vector3(eyeR.x + look.x, eyeR.y + look.y + faceOffset, 0f);
            _mouth.Transform.localPosition = new Vector3(mouth.x + look.x * 0.75f, mouth.y + look.y * 0.65f + faceOffset, 0f);
            _cheekL.Transform.localPosition = new Vector3(cheekL.x + look.x * 0.5f, cheekL.y + look.y * 0.5f + faceOffset, 0f);
            _cheekR.Transform.localPosition = new Vector3(cheekR.x + look.x * 0.5f, cheekR.y + look.y * 0.5f + faceOffset, 0f);
            Vector2 browL = st.BrowL + look, browR = st.BrowR + look;
            float lift = BrowLift(_browL, _browR);
            _browL.Transform.localPosition = new Vector3(browL.x, browL.y + lift + faceOffset, 0f);
            _browR.Transform.localPosition = new Vector3(browR.x, browR.y + lift + faceOffset, 0f);
        }

        private BrowPose _brows;

        private void ApplyBrows(BrowPose pose)
        {
            _brows = pose;
            _browL.Visible = pose == BrowPose.Raised || pose == BrowPose.Worried || pose == BrowPose.LeftUp;
            _browR.Visible = pose == BrowPose.Raised || pose == BrowPose.Worried || pose == BrowPose.RightUp;
            float l = 0f, r = 0f;
            switch (pose)
            {
                case BrowPose.Raised: l = -7f; r = 7f; break;     // arched: outer ends up
                case BrowPose.Worried: l = 16f; r = -16f; break;  // inner ends up, never cross
                case BrowPose.LeftUp: l = 12f; break;
                case BrowPose.RightUp: r = -12f; break;
            }
            _browL.Transform.localRotation = Quaternion.AngleAxis(l, Vector3.forward);
            _browR.Transform.localRotation = Quaternion.AngleAxis(r, Vector3.forward);
        }

        private float BrowLift(PlopPart left, PlopPart right)
            => _brows == BrowPose.Raised ? 0.020f : _brows == BrowPose.LeftUp || _brows == BrowPose.RightUp ? 0.012f : 0f;

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
