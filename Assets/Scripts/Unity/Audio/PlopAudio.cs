using System.Collections.Generic;
using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>What the music bus follows: the menu theme (menu, Studio, World select and Settings opened from the
    /// menu) or the current World's arrangement while the board is in play.</summary>
    public enum MusicContext { Menu, Gameplay }

    /// <summary>Mixer groups. Implemented as code-side buses (no AudioMixer asset): final gain = master × category × cue.</summary>
    public enum AudioBus { Music, Sfx, Ambience, Ui }

    internal enum PlopCue
    {
        Button, Back, Toggle, Move, Wall, Plop, Settle, Sleep, Undo, Restart, Complete, Perfect, WorldUnlock, Deadlock,
        IceSlide, IceStop, FanWhoosh, PortalIn, PortalOut, PortalBlocked, LavaHiss, LavaBubble, Dust, CosmeticUnlock,
        SonicLogo, Count
    }

    /// <summary>
    /// The one audio manager: owns the audio settings, the Music and Ambience loop buses and a small pool of effect
    /// voices. Every sound is an original asset rendered by <c>Tools/AudioSynth</c> into <c>Resources/{Music,Ambience,Sfx}</c>.
    /// Effects are triggered from authoritative game events (engine results and the board's movement trace) and are
    /// cancelled on undo, restart, level change, mute and app pause.
    /// </summary>
    public sealed class PlopAudio : MonoBehaviour
    {
        // Sprint 02 keys. The Phase 7–13 keys (plop.musicVolume / plop.soundVolume) are migrated once.
        internal const string MusicKey = "plop.audio.music";
        internal const string SoundKey = "plop.audio.sfx";
        internal const string AmbienceKey = "plop.audio.ambience";
        internal const string MuteKey = "plop.masterMuted";
        internal const string LegacyMusicKey = "plop.musicVolume";
        internal const string LegacySoundKey = "plop.soundVolume";
        private const float LegacyMusicDefault = 0.24f, LegacySoundDefault = 0.65f;

        public const float DefaultMusic = 0.45f;
        public const float DefaultSound = 0.80f;
        public const float DefaultAmbience = 0.30f;
        internal const int EffectVoices = 6;
        internal const float MusicCrossfade = 1.2f;
        private const float UiTrim = 0.85f;
        private const float DuckGain = 0.55f;

        internal static readonly string[] WorldLoops =
        {
            "cozy-nest", "dewdrop-garden", "rainy-window", "twilight-attic", "moonlit-clouds",
            "frosted-pond", "breezy-meadow", "ember-hollow", "prism-observatory"
        };
        internal const string MenuLoop = "menu";

        private readonly struct CueDef
        {
            public readonly string[] Clips;
            public readonly float Gain;
            public readonly double Repeat;
            public readonly int Priority;
            public readonly AudioBus Bus;
            public readonly bool Vary, Hero;

            public CueDef(string[] clips, float gain, double repeat, int priority, AudioBus bus = AudioBus.Sfx, bool vary = false, bool hero = false)
            {
                Clips = clips; Gain = gain; Repeat = repeat; Priority = priority; Bus = bus; Vary = vary; Hero = hero;
            }
        }

        private static CueDef[] BuildCues()
        {
            var c = new CueDef[(int)PlopCue.Count];
            c[(int)PlopCue.Button] = new CueDef(new[] { "ui_tap" }, 1f, 0.05, 0, AudioBus.Ui, vary: true);
            c[(int)PlopCue.Back] = new CueDef(new[] { "ui_back" }, 1f, 0.05, 0, AudioBus.Ui);
            c[(int)PlopCue.Toggle] = new CueDef(new[] { "ui_toggle" }, 1f, 0.09, 0, AudioBus.Ui);
            c[(int)PlopCue.Move] = new CueDef(new[] { "move_1", "move_2", "move_3", "move_4" }, 1f, 0.045, 1, vary: true);
            c[(int)PlopCue.Wall] = new CueDef(new[] { "blocked_1", "blocked_2" }, 0.9f, 0.065, 1, vary: true);
            c[(int)PlopCue.Plop] = new CueDef(new[] { "collision_1", "collision_2" }, 0.9f, 0.065, 1, vary: true);
            c[(int)PlopCue.Settle] = new CueDef(new[] { "settle" }, 1f, 0.08, 2);
            c[(int)PlopCue.Sleep] = new CueDef(new[] { "sleep" }, 1f, 0.12, 3);
            c[(int)PlopCue.Undo] = new CueDef(new[] { "undo" }, 1f, 0.075, 2);
            c[(int)PlopCue.Restart] = new CueDef(new[] { "restart" }, 1f, 0.12, 2);
            c[(int)PlopCue.Complete] = new CueDef(new[] { "complete" }, 1f, 0.3, 5, hero: true);
            c[(int)PlopCue.Perfect] = new CueDef(new[] { "perfect" }, 1f, 0.3, 5, hero: true);
            c[(int)PlopCue.WorldUnlock] = new CueDef(new[] { "world_unlock" }, 1f, 0.5, 5, hero: true);
            c[(int)PlopCue.Deadlock] = new CueDef(new[] { "deadlock" }, 1f, 0.5, 4);
            c[(int)PlopCue.IceSlide] = new CueDef(new[] { "ice_slide" }, 0.9f, 0.08, 1);
            c[(int)PlopCue.IceStop] = new CueDef(new[] { "ice_stop" }, 0.9f, 0.08, 1);
            c[(int)PlopCue.FanWhoosh] = new CueDef(new[] { "fan" }, 1f, 0.10, 1);
            c[(int)PlopCue.PortalIn] = new CueDef(new[] { "portal_in" }, 1f, 0.06, 2);
            c[(int)PlopCue.PortalOut] = new CueDef(new[] { "portal_out" }, 1f, 0.06, 2);
            c[(int)PlopCue.PortalBlocked] = new CueDef(new[] { "portal_blocked" }, 1f, 0.10, 1);
            c[(int)PlopCue.LavaHiss] = new CueDef(new[] { "lava_sizzle" }, 1f, 0.15, 3);
            c[(int)PlopCue.LavaBubble] = new CueDef(new[] { "lava_bubble" }, 0.35f, 2.0, 0);
            c[(int)PlopCue.Dust] = new CueDef(new[] { "dust" }, 0.8f, 0.2, 2);
            c[(int)PlopCue.CosmeticUnlock] = new CueDef(new[] { "cosmetic_unlock" }, 1f, 0.3, 4, hero: true);
            c[(int)PlopCue.SonicLogo] = new CueDef(new[] { "sonic_logo" }, 1f, 1.0, 6, hero: true);
            return c;
        }

        private struct Voice
        {
            public AudioSource Source;
            public int Priority;
            public double StartedAt;
            public float Gain;
            public AudioBus Bus;
            public bool Hero;
        }

        private struct Pending
        {
            public double At;
            public PlopCue Cue;
        }

        private static readonly CueDef[] Cues = BuildCues();

        /// <summary>Every effect asset the cue table can play (tests check each one is present).</summary>
        internal static IEnumerable<string> AllEffectClips()
        {
            foreach (var def in Cues)
                if (def.Clips != null)
                    foreach (var name in def.Clips) yield return name;
        }

        /// <summary>True when an effect voice holds a clip whose name starts with <paramref name="prefix"/> (tests).
        /// Stopping a voice clears its clip, so a cancelled cue no longer counts.</summary>
        internal bool HasVoiceClip(string prefix)
        {
            if (!_initialized) return false;
            foreach (var voice in _voices)
                if (voice.Source.clip != null && voice.Source.clip.name.StartsWith(prefix)) return true;
            return false;
        }

        internal int PendingCount => _pending.Count;
        private readonly Voice[] _voices = new Voice[EffectVoices];
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly double[] _cueReadyTimes = new double[(int)PlopCue.Count];
        private readonly int[] _lastVariant = new int[(int)PlopCue.Count];
        private readonly List<Pending> _pending = new List<Pending>();
        private PlopLoopPlayer _music, _ambience;
        private uint _variation = 0xA981D37Fu;
        private double _clock, _duckUntil;
        private float _duck = 1f;
        private bool _initialized, _storesPreferences, _preferencesChanged, _paused, _hasFocus = true;

        public float MusicVolume { get; private set; } = DefaultMusic;
        public float SoundVolume { get; private set; } = DefaultSound;
        public float AmbienceVolume { get; private set; } = DefaultAmbience;
        public bool MasterMuted { get; private set; }
        public WorldAmbience CurrentAmbience { get; private set; } = WorldAmbience.CozyNest;
        public MusicContext Context { get; private set; } = MusicContext.Menu;
        internal PlopLoopPlayer Music => _music;
        internal PlopLoopPlayer AmbienceBus => _ambience;
        internal bool OutputAllowed => _initialized && !MasterMuted && !_paused && _hasFocus && !_adSuspended && isActiveAndEnabled;
        private bool _adSuspended;

        /// <summary>A full-screen ad is on screen (Sprint 04).</summary>
        public bool SuspendedForAd => _adSuspended;

        /// <summary>
        /// Before a full-screen ad: silences music, ambience and effects the same way an app pause does (loops keep
        /// their position, scheduled cues are dropped). Volumes, mute and saved preferences are not touched.
        /// </summary>
        public void SuspendForFullscreenAd()
        {
            if (_adSuspended) return;
            _adSuspended = true;
            _pending.Clear();
            OutputChanged();
        }

        /// <summary>After the ad closes or fails: the same loops resume where they paused (never a second copy).</summary>
        public void ResumeAfterFullscreenAd()
        {
            if (!_adSuspended) return;
            _adSuspended = false;
            OutputChanged();
        }
        private bool CanPlayEffects => OutputAllowed && SoundVolume > 0f;

        public void Initialize(bool loadPreferences, WorldAmbience ambience = WorldAmbience.CozyNest,
            MusicContext context = MusicContext.Menu)
        {
            if (_initialized) return;
            _storesPreferences = loadPreferences;
            CurrentAmbience = ambience;
            Context = context;
            if (loadPreferences) LoadPreferences();

            _music = new PlopLoopPlayer(gameObject, "Music", MusicCrossfade, 0.9f, 0.6f, priority: 32);
            _ambience = new PlopLoopPlayer(gameObject, "Ambience", 0.9f, 1.2f, 0.5f, priority: 160);
            for (int index = 0; index < _voices.Length; index++)
                _voices[index].Source = CreateSource(gameObject, loop: false, priority: 64);
            _initialized = true;
            ApplyLoops(immediate: true);
        }

        private void LoadPreferences()
        {
            MasterMuted = PlayerPrefs.GetInt(MuteKey, 0) != 0;
            float legacyMusic = PlayerPrefs.GetFloat(LegacyMusicKey, LegacyMusicDefault);
            MusicVolume = PlayerPrefs.HasKey(MusicKey)
                ? VolumeOrDefault(PlayerPrefs.GetFloat(MusicKey, DefaultMusic), DefaultMusic)
                : Migrate(LegacyMusicKey, LegacyMusicDefault, DefaultMusic);
            SoundVolume = PlayerPrefs.HasKey(SoundKey)
                ? VolumeOrDefault(PlayerPrefs.GetFloat(SoundKey, DefaultSound), DefaultSound)
                : Migrate(LegacySoundKey, LegacySoundDefault, DefaultSound);
            // Ambience used to ride on the music slider: a player who silenced music also silenced ambience.
            AmbienceVolume = PlayerPrefs.HasKey(AmbienceKey)
                ? VolumeOrDefault(PlayerPrefs.GetFloat(AmbienceKey, DefaultAmbience), DefaultAmbience)
                : PlayerPrefs.HasKey(LegacyMusicKey) && legacyMusic <= 0.001f ? 0f : DefaultAmbience;
        }

        /// <summary>A legacy value the player chose is kept; an untouched legacy default becomes the new default.</summary>
        private static float Migrate(string legacyKey, float legacyDefault, float newDefault)
        {
            if (!PlayerPrefs.HasKey(legacyKey)) return newDefault;
            float v = PlayerPrefs.GetFloat(legacyKey, legacyDefault);
            if (float.IsNaN(v)) return newDefault;
            return Mathf.Abs(v - legacyDefault) < 0.005f ? newDefault : Mathf.Clamp01(v);
        }

        // ------------------------------------------------------------------ settings

        public void SetWorldAmbience(WorldAmbience ambience)
        {
            if (CurrentAmbience == ambience) return;
            CurrentAmbience = ambience;
            ApplyLoops(immediate: false);
        }

        public void SetMusicContext(MusicContext context)
        {
            if (Context == context) return;
            Context = context;
            ApplyLoops(immediate: false);
        }

        public void SetMusicVolume(float volume)
        {
            volume = VolumeOrDefault(volume, MusicVolume);
            if (MusicVolume == volume) return;
            MusicVolume = volume;
            VolumesChanged();
        }

        public void SetSoundVolume(float volume)
        {
            volume = VolumeOrDefault(volume, SoundVolume);
            if (SoundVolume == volume) return;
            SoundVolume = volume;
            VolumesChanged();
        }

        public void SetAmbienceVolume(float volume)
        {
            volume = VolumeOrDefault(volume, AmbienceVolume);
            if (AmbienceVolume == volume) return;
            AmbienceVolume = volume;
            VolumesChanged();
        }

        private void VolumesChanged()
        {
            _preferencesChanged = true;
            ApplyLoops(immediate: true);
            if (!CanPlayEffects) StopEffects();
            else RefreshEffectGains();
        }

        public void SetMasterMuted(bool muted)
        {
            if (MasterMuted == muted) return;
            MasterMuted = muted;
            _preferencesChanged = true;
            ApplyLoops(immediate: true);
            if (muted) StopEffects();
            SavePreferences();
        }

        /// <summary>Music 45 %, effects 80 %, ambience 30 %, sound on. Saved at once.</summary>
        public void RestoreDefaults()
        {
            MusicVolume = DefaultMusic;
            SoundVolume = DefaultSound;
            AmbienceVolume = DefaultAmbience;
            MasterMuted = false;
            _preferencesChanged = true;
            ApplyLoops(immediate: true);
            RefreshEffectGains();
            SavePreferences();
        }

        public void SavePreferences()
        {
            if (!_storesPreferences || !_preferencesChanged) return;
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.SetFloat(SoundKey, SoundVolume);
            PlayerPrefs.SetFloat(AmbienceKey, AmbienceVolume);
            PlayerPrefs.SetInt(MuteKey, MasterMuted ? 1 : 0);
            PlayerPrefs.Save();
            _preferencesChanged = false;
        }

        // ------------------------------------------------------------------ cues

        public void PlayButton() => Play(PlopCue.Button);
        public void PlayBack() => Play(PlopCue.Back);
        /// <summary>Slider/toggle confirmation at the new level (silent while muted or when effects are at zero).</summary>
        public void PlayToggle() => Play(PlopCue.Toggle);
        public void PlayDust() => Play(PlopCue.Dust);
        public void PlayCosmeticUnlock() => Play(PlopCue.CosmeticUnlock);
        public void PlayWorldUnlock() => Schedule(PlopCue.WorldUnlock, 0.9);
        public void PlayDeadlock() => Play(PlopCue.Deadlock);
        /// <summary>Splash sonic logo. Respects the saved mute and effects volume (prefs are loaded before the splash).</summary>
        public void PlaySonicLogo() => Play(PlopCue.SonicLogo);

        public void PlayUndo()
        {
            StopEffects();
            Play(PlopCue.Undo);
        }

        public void PlayRestart()
        {
            StopEffects();
            Play(PlopCue.Restart);
        }

        /// <summary>
        /// Called when the engine reports the level complete (before the move's own cues). The jingle waits for the
        /// last Plop's hop and sleep chime; Dream Dust sparkles after it. Undo/restart/level change cancel both.
        /// </summary>
        public void PlayCompletion(bool perfect, int dustEarned)
        {
            Schedule(perfect ? PlopCue.Perfect : PlopCue.Complete, 0.32);
            if (dustEarned > 0) Schedule(PlopCue.Dust, perfect ? 1.45 : 1.15);
        }

        public void PlayMove(GameEvent[] events)
        {
            if (!CanPlayEffects || events == null) return;

            bool moved = false, wall = false, body = false, settled = false;
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
                }
            }

            if (moved) Play(PlopCue.Move);
            if (settled) Schedule(PlopCue.Sleep, 0.12); // lands on its Nest, then the lullaby fall
            if (body) Play(PlopCue.Plop);
            else if (wall && !moved) Play(PlopCue.Wall); // nothing moved: a soft "bonk"
        }

        /// <summary>Mechanic cues raised as the board shows each step of the movement trace (already in sync with the art).</summary>
        internal void PlayTrace(BoardEnvironment.Cue cue)
        {
            switch (cue)
            {
                case BoardEnvironment.Cue.IceSlide: Play(PlopCue.IceSlide); break;
                case BoardEnvironment.Cue.IceStop: Play(PlopCue.IceStop); break;
                case BoardEnvironment.Cue.FanPush: Play(PlopCue.FanWhoosh); break;
                case BoardEnvironment.Cue.PortalIn: Play(PlopCue.PortalIn); break;
                case BoardEnvironment.Cue.PortalOut: Play(PlopCue.PortalOut); break;
                case BoardEnvironment.Cue.PortalBlocked: Play(PlopCue.PortalBlocked); break;
                case BoardEnvironment.Cue.LavaHiss: Play(PlopCue.LavaHiss); break;
                case BoardEnvironment.Cue.LavaBubble: Play(PlopCue.LavaBubble); break;
            }
        }

        /// <summary>Stops every effect voice and drops scheduled cues (undo, restart, level change, mute, pause).</summary>
        public void StopEffects()
        {
            _pending.Clear();
            if (!_initialized) return;
            for (int i = 0; i < _voices.Length; i++)
            {
                ref Voice voice = ref _voices[i];
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Source.volume = 0f;
                voice.Gain = 0f;
            }
            _duckUntil = 0;
        }

        /// <summary>
        /// Level change: drops scheduled cues and stops gameplay voices, but lets a button tap or a reward jingle that
        /// is already sounding finish naturally (the tap that pressed "Next Level" is not cut off).
        /// </summary>
        public void CancelPending()
        {
            _pending.Clear();
            if (!_initialized) return;
            for (int i = 0; i < _voices.Length; i++)
            {
                ref Voice voice = ref _voices[i];
                if (voice.Bus == AudioBus.Ui || voice.Hero || !voice.Source.isPlaying) continue;
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Gain = 0f;
            }
        }

        private void Schedule(PlopCue cue, double delay)
        {
            if (!CanPlayEffects) return;
            _pending.Add(new Pending { At = _clock + delay, Cue = cue });
        }

        private void Play(PlopCue cue)
        {
            if (!CanPlayEffects) return;
            int index = (int)cue;
            var def = Cues[index];
            if (_clock < _cueReadyTimes[index]) return;

            int slot = FindVoice(def.Priority);
            if (slot < 0) return;
            var clip = Clip(def.Clips, index, def.Vary);
            if (clip == null) return;
            _cueReadyTimes[index] = _clock + def.Repeat;

            ref Voice voice = ref _voices[slot];
            voice.Priority = def.Priority;
            voice.StartedAt = _clock;
            voice.Bus = def.Bus;
            voice.Hero = def.Hero;
            voice.Gain = def.Gain * (def.Vary ? 0.94f + 0.08f * Vary() : 1f);
            voice.Source.Stop();
            voice.Source.clip = clip;
            voice.Source.pitch = def.Vary ? 0.97f + 0.06f * Vary() : 1f;
            voice.Source.volume = BusGain(def.Bus) * voice.Gain;
            voice.Source.Play();
            if (def.Hero) _duckUntil = System.Math.Max(_duckUntil, _clock + clip.length * 0.8);
        }

        private AudioClip Clip(string[] names, int cueIndex, bool vary)
        {
            int pick = 0;
            if (names.Length > 1)
            {
                // a different variant from last time, so repeated moves never sound identical
                pick = (int)(Vary() * (names.Length - 1) + 0.5f) % (names.Length - 1);
                if (pick >= _lastVariant[cueIndex]) pick++;
                _lastVariant[cueIndex] = pick;
            }
            string name = names[pick];
            if (!_clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Sfx/" + name);
                if (clip == null) PlopLog.Error("Missing audio cue Sfx/" + name);
                _clips[name] = clip;
            }
            return clip;
        }

        private int FindVoice(int priority)
        {
            int steal = -1;
            for (int i = 0; i < _voices.Length; i++)
            {
                ref Voice candidate = ref _voices[i];
                if (!candidate.Source.isPlaying) return i;
                if (candidate.Priority > priority) continue;
                if (steal == -1 ||
                    candidate.Priority < _voices[steal].Priority ||
                    (candidate.Priority == _voices[steal].Priority && candidate.StartedAt < _voices[steal].StartedAt))
                    steal = i;
            }
            return steal;
        }

        private float BusGain(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Music: return MusicVolume * _duck;
                case AudioBus.Ambience: return AmbienceVolume;
                case AudioBus.Ui: return SoundVolume * UiTrim;
                default: return SoundVolume;
            }
        }

        private void RefreshEffectGains()
        {
            if (!_initialized) return;
            foreach (var voice in _voices)
                if (voice.Source.clip != null) voice.Source.volume = BusGain(voice.Bus) * voice.Gain;
        }

        private float Vary()
        {
            _variation ^= _variation << 13;
            _variation ^= _variation >> 17;
            _variation ^= _variation << 5;
            return (_variation & 0xffffu) / 65535f;
        }

        // ------------------------------------------------------------------ loops

        private void ApplyLoops(bool immediate)
        {
            if (!_initialized) return;
            bool allowed = OutputAllowed;
            int world = Mathf.Clamp((int)CurrentAmbience, 0, WorldLoops.Length - 1);
            _music.Request(Context == MusicContext.Menu ? MenuLoop : WorldLoops[world], BusGain(AudioBus.Music), allowed, immediate);
            // Ambience belongs to the board: it fades out under the opaque menu and Studio.
            _ambience.Request(WorldLoops[world], BusGain(AudioBus.Ambience), allowed && Context == MusicContext.Gameplay, immediate);
        }

        /// <summary>Advances fades, crossfades, ducking and scheduled cues. Called every frame with unscaled time.</summary>
        internal void Tick(float dt)
        {
            if (!_initialized) return;
            _clock += dt;
            float duckTarget = _clock < _duckUntil ? DuckGain : 1f;
            _duck = Mathf.MoveTowards(_duck, duckTarget, dt / (duckTarget < _duck ? 0.15f : 0.6f) * (1f - DuckGain));

            for (int i = 0; i < _pending.Count;)
            {
                if (_pending[i].At > _clock) { i++; continue; }
                var cue = _pending[i].Cue;
                _pending.RemoveAt(i);
                Play(cue);
            }

            ApplyLoops(immediate: false);
            _music.Step(dt);
            _ambience.Step(dt);
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

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

        private void OutputChanged()
        {
            if (!_initialized) return;
            if (!OutputAllowed) StopEffects();
            ApplyLoops(immediate: true);
        }

        private void OnEnable() => OutputChanged();

        private void OnDisable()
        {
            OutputChanged();
            SavePreferences();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            OutputChanged();
            if (paused) SavePreferences();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (_hasFocus == focused) return;
            _hasFocus = focused;
            OutputChanged();
            if (!focused) SavePreferences();
        }

        private static float VolumeOrDefault(float volume, float fallback) =>
            float.IsNaN(volume) ? fallback : Mathf.Clamp01(volume);
    }
}
