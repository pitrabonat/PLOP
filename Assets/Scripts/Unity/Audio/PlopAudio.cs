using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Owns local audio settings and mixes a small fixed pool of tactile voices.</summary>
    public sealed class PlopAudio : MonoBehaviour
    {
        private const string MusicKey = "plop.musicVolume";
        private const string SoundKey = "plop.soundVolume";
        private const string MuteKey = "plop.masterMuted";
        private const float DefaultMusic = 0.24f;
        private const float DefaultSound = 0.65f;
        private const int MaxEffectVoices = 3;

        private struct Voice
        {
            public AudioSource Source;
            public int Priority;
            public double StartedAt;
            public float Gain;
        }

        private readonly Voice[] _voices = new Voice[MaxEffectVoices];
        private readonly AudioClip[] _cues = new AudioClip[(int)PlopCue.Count];
        private readonly double[] _cueReadyTimes = new double[(int)PlopCue.Count];
        private PlopWorldAmbience _ambience;
        private uint _variation = 0xA981D37Fu;
        private bool _initialized, _storesPreferences, _preferencesChanged, _paused, _hasFocus = true;

        public float MusicVolume { get; private set; } = DefaultMusic;
        public float SoundVolume { get; private set; } = DefaultSound;
        public bool MasterMuted { get; private set; }
        public WorldAmbience CurrentAmbience { get; private set; } = WorldAmbience.CozyNest;

        public void Initialize(bool loadPreferences, WorldAmbience ambience = WorldAmbience.CozyNest)
        {
            if (_initialized) return;
            _storesPreferences = loadPreferences;
            CurrentAmbience = ambience;
            if (loadPreferences)
            {
                MusicVolume = VolumeOrDefault(PlayerPrefs.GetFloat(MusicKey, DefaultMusic), DefaultMusic);
                SoundVolume = VolumeOrDefault(PlayerPrefs.GetFloat(SoundKey, DefaultSound), DefaultSound);
                MasterMuted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
            }

            for (int index = 0; index < _voices.Length; index++)
                _voices[index].Source = CreateSource(gameObject, loop: false, priority: 100);
            _ambience = new PlopWorldAmbience(this);
            _initialized = true;
            ApplyPlayback();
        }

        public void SetWorldAmbience(WorldAmbience ambience)
        {
            if (CurrentAmbience == ambience) return;
            CurrentAmbience = ambience;
            ApplyPlayback();
        }

        public void SetMusicVolume(float volume)
        {
            volume = VolumeOrDefault(volume, MusicVolume);
            if (MusicVolume == volume) return;
            MusicVolume = volume;
            _preferencesChanged = true;
            ApplyPlayback();
        }

        public void SetSoundVolume(float volume)
        {
            volume = VolumeOrDefault(volume, SoundVolume);
            if (SoundVolume == volume) return;
            SoundVolume = volume;
            _preferencesChanged = true;
            ApplyPlayback();
        }

        public void SetMasterMuted(bool muted)
        {
            if (MasterMuted == muted) return;
            MasterMuted = muted;
            _preferencesChanged = true;
            ApplyPlayback();
            SavePreferences();
        }

        public void SavePreferences()
        {
            if (!_storesPreferences || !_preferencesChanged) return;
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.SetFloat(SoundKey, SoundVolume);
            PlayerPrefs.SetInt(MuteKey, MasterMuted ? 1 : 0);
            PlayerPrefs.Save();
            _preferencesChanged = false;
        }

        public void PlayButton() => Play(PlopCue.Button, 0.58f, 0.055, 0);
        public void PlayUndo()
        {
            StopEffects();
            Play(PlopCue.Undo, 0.56f, 0.075, 2);
        }

        public void PlayRestart()
        {
            StopEffects();
            Play(PlopCue.Restart, 0.56f, 0.12, 2);
        }

        public void PlayWorldUnlock() => Play(PlopCue.WorldUnlock, 0.60f, 0.30, 3);
        public void PlayDeadlock() => Play(PlopCue.Deadlock, 0.62f, 0.30, 4);

        public void PlayMove(GameEvent[] events)
        {
            if (!CanPlayEffects || events == null) return;

            bool moved = false, wall = false, body = false, settled = false, completed = false;
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case GameEventType.PlopMoved: moved = true; break;
                    case GameEventType.PlopBlocked:
                        if (e.BlockReason == BlockReason.Plop) body = true;
                        else wall = true;
                        break;
                    case GameEventType.PlopSettled: settled = true; break;
                    case GameEventType.LevelCompleted: completed = true; break;
                }
            }

            if (completed)
            {
                if (settled) Play(PlopCue.Settle, 0.32f, 0.080, 2);
                Play(PlopCue.Complete, 0.82f, 0.15, 4);
                return;
            }

            if (settled) Play(PlopCue.Settle, 0.78f, 0.080, 3);
            else if (moved) Play(PlopCue.Move, 0.84f, 0.045, 0);

            if (wall || body)
                Play(wall ? PlopCue.Wall : PlopCue.Plop, moved ? 0.50f : 0.74f, 0.065, 1);
        }

        private bool OutputAllowed => _initialized && !MasterMuted && !_paused && _hasFocus && isActiveAndEnabled;
        private bool CanPlayEffects => OutputAllowed && SoundVolume > 0f;

        private void Play(PlopCue cue, float gain, double repeatDelay, int priority)
        {
            if (!CanPlayEffects) return;
            int index = (int)cue;
            double now = Time.unscaledTimeAsDouble;
            if (now < _cueReadyTimes[index]) return;

            int slot = FindVoice(priority);
            if (slot < 0) return;
            _cueReadyTimes[index] = now + repeatDelay;
            ref Voice voice = ref _voices[slot];
            voice.Priority = priority;
            voice.StartedAt = now;
            bool tactile = cue == PlopCue.Move || cue == PlopCue.Wall || cue == PlopCue.Plop || cue == PlopCue.Button;
            voice.Gain = gain * (tactile ? 0.96f + 0.08f * Vary() : 1f);

            var clip = _cues[index];
            if (clip == null) _cues[index] = clip = PlopAudioClips.CreateCue(cue);
            voice.Source.Stop();
            voice.Source.clip = clip;
            voice.Source.pitch = tactile ? 0.965f + 0.07f * Vary() : 1f;
            voice.Source.volume = SoundVolume * voice.Gain;
            voice.Source.Play();
        }

        private int FindVoice(int priority)
        {
            int oldestQuiet = -1;
            for (int i = 0; i < _voices.Length; i++)
            {
                ref Voice candidate = ref _voices[i];
                if (!candidate.Source.isPlaying) return i;
                if (candidate.Priority > priority) continue;
                if (oldestQuiet == -1 ||
                    candidate.Priority < _voices[oldestQuiet].Priority ||
                    (candidate.Priority == _voices[oldestQuiet].Priority &&
                     candidate.StartedAt < _voices[oldestQuiet].StartedAt))
                    oldestQuiet = i;
            }
            return oldestQuiet;
        }

        private float Vary()
        {
            _variation ^= _variation << 13;
            _variation ^= _variation >> 17;
            _variation ^= _variation << 5;
            return (_variation & 0xffffu) / 65535f;
        }

        internal static AudioSource CreateSource(GameObject owner, bool loop, int priority)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            return source;
        }

        private void ApplyPlayback()
        {
            if (!_initialized) return;
            _ambience.Apply(CurrentAmbience, MusicVolume, OutputAllowed);
            if (!CanPlayEffects)
            {
                StopEffects();
                return;
            }

            foreach (var voice in _voices)
                voice.Source.volume = SoundVolume * (voice.Gain == 0f ? 1f : voice.Gain);
        }

        private void StopEffects()
        {
            if (!_initialized) return;
            for (int i = 0; i < _voices.Length; i++)
            {
                ref Voice voice = ref _voices[i];
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Source.volume = 0f;
                voice.Gain = 0f;
            }
        }

        private void OnEnable() => ApplyPlayback();

        private void OnDisable()
        {
            if (_initialized) ApplyPlayback();
            SavePreferences();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            ApplyPlayback();
            if (paused) SavePreferences();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (_hasFocus == focused) return;
            _hasFocus = focused;
            ApplyPlayback();
            if (!focused) SavePreferences();
        }

        private void OnDestroy()
        {
            foreach (var cue in _cues)
                if (cue != null) Destroy(cue);
        }

        private static float VolumeOrDefault(float volume, float fallback) =>
            float.IsNaN(volume) ? fallback : Mathf.Clamp01(volume);
    }
}
