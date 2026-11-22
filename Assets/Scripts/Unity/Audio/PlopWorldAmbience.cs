using System.Collections;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Two looping sources share one crossfade. Rapid requests collapse to the latest world.</summary>
    internal sealed class PlopWorldAmbience
    {
        private const float CrossfadeSeconds = 0.56f;
        private static readonly string[] AssetNames =
        {
            "cozy-nest", "dewdrop-garden", "rainy-window", "twilight-attic", "moonlit-clouds"
        };

        private readonly MonoBehaviour _host;
        private readonly AudioSource[] _sources;
        private Coroutine _crossfade;
        private WorldAmbience _requested;
        private WorldAmbience? _playing;
        private int _active;
        private float _volume, _mix;
        private bool _enabled, _paused;

        public PlopWorldAmbience(MonoBehaviour host)
        {
            _host = host;
            _sources = new[]
            {
                PlopAudio.CreateSource(host.gameObject, loop: true, priority: 180),
                PlopAudio.CreateSource(host.gameObject, loop: true, priority: 180)
            };
        }

        public void Apply(WorldAmbience requested, float volume, bool canPlay)
        {
            _requested = requested;
            _volume = volume;
            _enabled = canPlay && volume > 0f;
            if (!_enabled)
            {
                Suspend();
                return;
            }

            if (_paused)
            {
                _paused = false;
                if (_playing == requested)
                {
                    var source = _sources[_active];
                    source.UnPause();
                    if (!source.isPlaying) source.Play();
                }
                else RemoveActive();
            }

            if (_playing == null)
            {
                var source = _sources[_active];
                if (Load(source, requested))
                {
                    _playing = requested;
                    source.volume = volume;
                    source.Play();
                }
            }

            SetGains();
            if (_playing.HasValue && _playing != requested && _crossfade == null)
                _crossfade = _host.StartCoroutine(ChangeBed());
        }

        private void Suspend()
        {
            if (_crossfade != null)
            {
                _host.StopCoroutine(_crossfade);
                _crossfade = null;
                Release(_sources[1 - _active]);
                _mix = 0f;
            }

            if (_playing.HasValue && _playing != _requested)
                RemoveActive();

            if (_playing.HasValue && !_paused)
            {
                _sources[_active].Pause();
                _paused = true;
            }
            SetGains();
        }

        private IEnumerator ChangeBed()
        {
            // Wait one frame so rapid same-frame navigation requests choose their final destination.
            yield return null;

            while (_enabled && _playing != _requested)
            {
                var requested = _requested;
                var incoming = _sources[1 - _active];
                if (!Load(incoming, requested)) break;
                incoming.volume = 0f;
                incoming.Play();

                float elapsed = 0f;
                while (elapsed < CrossfadeSeconds)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    _mix = Mathf.SmoothStep(0f, 1f, elapsed / CrossfadeSeconds);
                    SetGains();
                }

                Release(_sources[_active]);
                _playing = requested;
                _active = 1 - _active;
                _mix = 0f;
                SetGains();
            }
            _crossfade = null;
        }

        private void RemoveActive()
        {
            Release(_sources[_active]);
            _paused = false;
            _playing = null;
        }

        private void SetGains()
        {
            float volume = _enabled ? _volume : 0f;
            _sources[_active].volume = volume * Mathf.Sqrt(1f - _mix);
            _sources[1 - _active].volume = volume * Mathf.Sqrt(_mix);
        }

        private static bool Load(AudioSource source, WorldAmbience world)
        {
            int index = (int)world;
            if (index < 0 || index >= AssetNames.Length) index = 0;
            source.clip = Resources.Load<AudioClip>("Ambience/" + AssetNames[index]);
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
