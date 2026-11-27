using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// One looping bus (music or ambience): two sources share an equal-power crossfade, rapid requests collapse to the
    /// latest loop, and a paused bus resumes where it stopped. Driven by <see cref="Step"/> (unscaled time) so it keeps
    /// no coroutines and can be stepped deterministically in tests.
    /// </summary>
    internal sealed class PlopLoopPlayer
    {
        private readonly string _folder;
        private readonly float _crossfadeSeconds, _fadeInSeconds, _fadeOutSeconds;
        private readonly AudioSource[] _sources;
        private string _requested, _playing, _incoming;
        private int _active;
        private float _level, _fade, _mix;
        private bool _wanted, _paused, _settleFrame;

        public PlopLoopPlayer(GameObject owner, string folder, float crossfadeSeconds, float fadeInSeconds, float fadeOutSeconds, int priority)
        {
            _folder = folder;
            _crossfadeSeconds = crossfadeSeconds;
            _fadeInSeconds = fadeInSeconds;
            _fadeOutSeconds = fadeOutSeconds;
            _sources = new[]
            {
                PlopAudio.CreateSource(owner, loop: true, priority: priority),
                PlopAudio.CreateSource(owner, loop: true, priority: priority)
            };
        }

        public string Playing => _playing;
        public string Requested => _requested;
        public bool IsCrossfading => _incoming != null;
        public AudioSource ActiveSource => _sources[_active];
        internal float Fade => _fade;

        /// <summary>
        /// Sets what should play. <paramref name="level"/> is the final linear gain (bus × category). With
        /// <paramref name="immediate"/> the change is applied now (mute, app pause, volume sliders); otherwise the bus
        /// fades in or out over its fade time (context changes such as menu ↔ board).
        /// </summary>
        public void Request(string loop, float level, bool enabled, bool immediate)
        {
            if (_requested != loop) _settleFrame = true;
            _requested = loop;
            _level = level;
            _wanted = enabled && level > 0f && !string.IsNullOrEmpty(loop);
            if (immediate) _fade = _wanted ? (_playing == null || _playing == loop ? 1f : _fade) : 0f;
            Step(0f);
        }

        public void Step(float dt)
        {
            float target = _wanted ? 1f : 0f;
            if (_fade < target) _fade = Mathf.Min(target, _fade + dt / Mathf.Max(0.01f, _fadeInSeconds));
            else if (_fade > target) _fade = Mathf.Max(target, _fade - dt / Mathf.Max(0.01f, _fadeOutSeconds));

            if (!_wanted && _fade <= 0f)
            {
                Suspend();
                SetGains();
                return;
            }

            if (_wanted)
            {
                if (_paused) Resume();
                if (_playing == null)
                {
                    if (Load(_sources[_active], _requested))
                    {
                        _playing = _requested;
                        _sources[_active].Play();
                    }
                    _settleFrame = false;
                }
                else if (_incoming == null && _playing != _requested)
                {
                    // Wait one step so same-frame navigation settles on its final destination before loading.
                    if (_settleFrame) _settleFrame = false;
                    else if (Load(_sources[1 - _active], _requested))
                    {
                        _incoming = _requested;
                        _mix = 0f;
                        _sources[1 - _active].Play();
                    }
                }
            }

            if (_incoming != null)
            {
                _mix = Mathf.Min(1f, _mix + dt / Mathf.Max(0.01f, _crossfadeSeconds));
                if (_mix >= 1f)
                {
                    Release(_sources[_active]);
                    _active = 1 - _active;
                    _playing = _incoming;
                    _incoming = null;
                    _mix = 0f;
                }
            }
            SetGains();
        }

        private void Suspend()
        {
            if (_incoming != null)
            {
                // A crossfade interrupted by mute/pause resolves to the newer loop.
                Release(_sources[_active]);
                _active = 1 - _active;
                _playing = _incoming;
                _incoming = null;
                _mix = 0f;
            }
            if (_playing != null && _playing != _requested)
            {
                Release(_sources[_active]);
                _playing = null;
                _paused = false;
                return;
            }
            if (_playing != null && !_paused)
            {
                _sources[_active].Pause();
                _paused = true;
            }
        }

        private void Resume()
        {
            _paused = false;
            var source = _sources[_active];
            source.UnPause();
            if (!source.isPlaying && source.clip != null) source.Play();
        }

        private void SetGains()
        {
            float v = _level * Smooth(_fade);
            _sources[_active].volume = v * Mathf.Sqrt(1f - _mix);
            _sources[1 - _active].volume = _incoming != null ? v * Mathf.Sqrt(_mix) : 0f;
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);

        private bool Load(AudioSource source, string loop)
        {
            if (string.IsNullOrEmpty(loop)) return false;
            source.clip = Resources.Load<AudioClip>(_folder + "/" + loop);
            if (source.clip == null) PlopLog.Error("Missing audio loop " + _folder + "/" + loop);
            return source.clip != null;
        }

        private static void Release(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }
    }
}
