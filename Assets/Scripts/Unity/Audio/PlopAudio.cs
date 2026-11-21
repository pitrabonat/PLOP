using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Plays one set of tactile cues per swipe and owns the independent music and effects gains.</summary>
    public sealed class PlopAudio : MonoBehaviour
    {
        private const string MusicKey = "plop.musicVolume";
        private const string SoundKey = "plop.soundVolume";
        private const float DefaultMusic = 0.24f;
        private const float DefaultSound = 0.65f;

        private readonly AudioClip[] _cues = new AudioClip[(int)PlopCue.Count];
        private readonly double[] _cueReadyTimes = new double[(int)PlopCue.Count];
        private AudioSource _music, _effects;
        private AudioClip _musicClip;
        private bool _initialized, _storesPreferences, _preferencesChanged, _paused;

        public float MusicVolume { get; private set; } = DefaultMusic;
        public float SoundVolume { get; private set; } = DefaultSound;

        public void Initialize(bool loadPreferences)
        {
            if (_initialized) return;
            _storesPreferences = loadPreferences;
            if (loadPreferences)
            {
                MusicVolume = VolumeOrDefault(PlayerPrefs.GetFloat(MusicKey, DefaultMusic), DefaultMusic);
                SoundVolume = VolumeOrDefault(PlayerPrefs.GetFloat(SoundKey, DefaultSound), DefaultSound);
            }

            _effects = CreateSource(loop: false, priority: 100);
            _effects.volume = SoundVolume;
            _music = CreateSource(loop: true, priority: 180);
            _initialized = true;
            ApplyMusic();
        }

        public void SetMusicVolume(float volume)
        {
            volume = VolumeOrDefault(volume, MusicVolume);
            if (MusicVolume == volume) return;
            MusicVolume = volume;
            _preferencesChanged = true;
            ApplyMusic();
        }

        public void SetSoundVolume(float volume)
        {
            volume = VolumeOrDefault(volume, SoundVolume);
            if (SoundVolume == volume) return;
            SoundVolume = volume;
            _preferencesChanged = true;
            if (_effects == null) return;
            _effects.volume = volume;
            if (volume == 0f) _effects.Stop();
        }

        public void SavePreferences()
        {
            if (!_storesPreferences || !_preferencesChanged) return;
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.SetFloat(SoundKey, SoundVolume);
            PlayerPrefs.Save();
            _preferencesChanged = false;
        }

        public void PlayButton() => Play(PlopCue.Button, 0.68f, 0.055);

        public void PlayMove(GameEvent[] events)
        {
            if (!CanPlayEffects || events == null) return;

            bool moved = false, bumped = false, settled = false, completed = false;
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case GameEventType.PlopMoved: moved = true; break;
                    case GameEventType.PlopBlocked: bumped = true; break;
                    case GameEventType.PlopSettled: settled = true; break;
                    case GameEventType.LevelCompleted: completed = true; break;
                }
            }

            if (moved) Play(PlopCue.Move, settled ? 0.50f : 1f, 0.045);
            if (bumped) Play(PlopCue.Bump, moved ? 0.52f : 0.80f, 0.065);
            if (settled) Play(PlopCue.Settle, completed ? 0.50f : 0.88f, 0.080);
            if (completed) Play(PlopCue.Complete, 0.92f, 0.120);
        }

        private bool CanPlayEffects => _initialized && !_paused && isActiveAndEnabled && SoundVolume > 0f;

        private void Play(PlopCue cue, float gain, double repeatDelay)
        {
            if (!CanPlayEffects) return;
            int index = (int)cue;
            double now = Time.unscaledTimeAsDouble;
            if (now < _cueReadyTimes[index]) return;
            _cueReadyTimes[index] = now + repeatDelay;

            var clip = _cues[index];
            if (clip == null) _cues[index] = clip = PlopAudioClips.CreateCue(cue);
            _effects.PlayOneShot(clip, gain);
        }

        private AudioSource CreateSource(bool loop, int priority)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            return source;
        }

        private void ApplyMusic()
        {
            if (!_initialized) return;
            _music.volume = MusicVolume;
            if (MusicVolume == 0f || _paused || !isActiveAndEnabled)
            {
                _music.Stop();
                return;
            }

            if (_musicClip == null)
            {
                _musicClip = PlopAudioClips.CreateMusic();
                _music.clip = _musicClip;
            }
            if (!_music.isPlaying) _music.Play();
        }

        private void OnEnable() => ApplyMusic();

        private void OnDisable()
        {
            if (_effects != null) _effects.Stop();
            if (_music != null) _music.Stop();
            SavePreferences();
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (paused)
            {
                if (_effects != null) _effects.Stop();
                SavePreferences();
            }
            ApplyMusic();
        }

        private void OnDestroy()
        {
            foreach (var cue in _cues)
                if (cue != null) Destroy(cue);
            if (_musicClip != null) Destroy(_musicClip);
        }

        private static float VolumeOrDefault(float volume, float fallback) =>
            float.IsNaN(volume) ? fallback : Mathf.Clamp01(volume);
    }
}
