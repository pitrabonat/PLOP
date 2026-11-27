using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Plop.Tests.PlayMode
{
    /// <summary>
    /// Sprint 02 audio: one manager, Music / Ambience loop buses and an effect pool fed by the AudioSynth assets;
    /// defaults 45/80/30, migration from the Phase 7–13 keys, menu ↔ World music with crossfades, scheduled reward
    /// cues that undo/restart/level change cancel, and mute/pause gating.
    /// </summary>
    public class PlopAudioTests
    {
        private static readonly string[] Keys =
        {
            PlopAudio.MusicKey, PlopAudio.SoundKey, PlopAudio.AmbienceKey, PlopAudio.MuteKey,
            PlopAudio.LegacyMusicKey, PlopAudio.LegacySoundKey
        };

        private Dictionary<string, (bool has, float f, int i)> _saved;
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SavePrefs() => _saved = Keys.ToDictionary(k => k, k => (PlayerPrefs.HasKey(k), PlayerPrefs.GetFloat(k), PlayerPrefs.GetInt(k)));

        [TearDown]
        public void RestorePrefs()
        {
            foreach (var go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            foreach (var pair in _saved)
            {
                PlayerPrefs.DeleteKey(pair.Key);
                if (!pair.Value.has) continue;
                if (pair.Key == PlopAudio.MuteKey) PlayerPrefs.SetInt(pair.Key, pair.Value.i);
                else PlayerPrefs.SetFloat(pair.Key, pair.Value.f);
            }
            PlayerPrefs.Save();
        }

        private PlopAudio Audio(bool prefs = false, MusicContext context = MusicContext.Menu,
            WorldAmbience world = WorldAmbience.CozyNest)
        {
            var root = new GameObject("PLOP audio test");
            _made.Add(root);
            if (Object.FindAnyObjectByType<AudioListener>() == null) root.AddComponent<AudioListener>();
            var audio = root.AddComponent<PlopAudio>();
            audio.Initialize(prefs, world, context);
            return audio;
        }

        private static void Run(PlopAudio audio, float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 60f) audio.Tick(1f / 60f);
        }

        private static GameEvent CueEvent(GameEventType type, BlockReason reason = BlockReason.Wall) =>
            new GameEvent(type, 0, default, default, Direction.Right, reason, -1, 1);

        // ------------------------------------------------------------------ assets

        [Test]
        public void EveryCueMusicAndAmbienceAssetIsPresent()
        {
            foreach (var name in PlopAudio.AllEffectClips().Distinct())
                Assert.NotNull(Resources.Load<AudioClip>("Sfx/" + name), "Sfx/" + name);
            Assert.NotNull(Resources.Load<AudioClip>("Music/" + PlopAudio.MenuLoop));
            foreach (var world in PlopAudio.WorldLoops)
            {
                var music = Resources.Load<AudioClip>("Music/" + world);
                Assert.NotNull(music, "Music/" + world);
                Assert.AreEqual(2, music.channels);
                Assert.Greater(music.length, 40f, "a 16-bar loop");
                var bed = Resources.Load<AudioClip>("Ambience/" + world);
                Assert.NotNull(bed, "Ambience/" + world);
                Assert.AreEqual(30f, bed.length, 0.05f);
            }
            var sonic = Resources.Load<AudioClip>("Sfx/sonic_logo");
            Assert.That(sonic.length, Is.InRange(0.6f, 1.2f), "sonic logo 0.6–1.2 s");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Reads the rendered 16-bit WAVs (the imported clips are compressed, so GetData cannot read them back): the
        /// step from the last frame to the first is no bigger than the largest step inside the first few frames.
        /// </summary>
        [Test]
        public void MusicAndAmbienceLoopsWrapWithoutAClick()
        {
            foreach (var folder in new[] { "Music", "Ambience" })
                foreach (var name in PlopAudio.WorldLoops.Concat(folder == "Music" ? new[] { PlopAudio.MenuLoop } : new string[0]))
                {
                    var path = System.IO.Path.Combine(Application.dataPath, "Audio/Resources", folder, name + ".wav");
                    var bytes = System.IO.File.ReadAllBytes(path);
                    Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4), path);
                    int channels = BitConverter.ToInt16(bytes, 22), bits = BitConverter.ToInt16(bytes, 34);
                    Assert.AreEqual(16, bits);
                    int data = 36;
                    while (System.Text.Encoding.ASCII.GetString(bytes, data, 4) != "data") data += 8 + BitConverter.ToInt32(bytes, data + 4);
                    int size = BitConverter.ToInt32(bytes, data + 4), first = data + 8, frames = size / (2 * channels);
                    float Sample(int frame) => BitConverter.ToInt16(bytes, first + frame * 2 * channels) / 32768f;
                    float typical = 0f, peak = 0f;
                    for (int i = 1; i < 2048; i++)
                    {
                        typical = Mathf.Max(typical, Mathf.Abs(Sample(i) - Sample(i - 1)));
                        peak = Mathf.Max(peak, Mathf.Abs(Sample(i)));
                    }
                    Assert.Greater(peak, 0.001f, path + " is not silent at the loop start");
                    Assert.LessOrEqual(Mathf.Abs(Sample(0) - Sample(frames - 1)), typical + 0.002f, path + ": wrap step");
                }
        }
#endif

        // ------------------------------------------------------------------ buses and defaults

        [Test]
        public void DefaultsAre45_80_30AndHapticsOn()
        {
            Assert.AreEqual(0.45f, PlopAudio.DefaultMusic);
            Assert.AreEqual(0.80f, PlopAudio.DefaultSound);
            Assert.AreEqual(0.30f, PlopAudio.DefaultAmbience);
            var audio = Audio();
            Assert.AreEqual(0.45f, audio.MusicVolume);
            Assert.AreEqual(0.80f, audio.SoundVolume);
            Assert.AreEqual(0.30f, audio.AmbienceVolume);
            Assert.IsFalse(audio.MasterMuted);
        }

        [Test]
        public void MenuPlaysTheThemeAndAmbienceWaitsForTheBoard()
        {
            var audio = Audio();
            Run(audio, 1.5f);
            Assert.AreEqual("menu", audio.Music.Playing);
            Assert.AreEqual("menu", audio.Music.ActiveSource.clip.name);
            Assert.AreEqual(audio.MusicVolume, audio.Music.ActiveSource.volume, 0.001f);
            Assert.IsNull(audio.AmbienceBus.Playing, "no ambience under the opaque menu");

            audio.SetMusicContext(MusicContext.Gameplay);
            Run(audio, 0.1f);
            Assert.IsTrue(audio.Music.IsCrossfading, "menu → World crossfades instead of cutting");
            Run(audio, PlopAudio.MusicCrossfade + 0.3f);
            Assert.AreEqual("cozy-nest", audio.Music.Playing);
            Assert.AreEqual("cozy-nest", audio.AmbienceBus.Playing);
            Assert.AreEqual(audio.AmbienceVolume, audio.AmbienceBus.ActiveSource.volume, 0.001f);
            Assert.AreEqual(2, audio.GetComponents<AudioSource>().Count(s => s.loop && s.clip != null));
        }

        [Test]
        public void LevelChangesInsideAWorldNeverRestartTheMusic()
        {
            var audio = Audio(context: MusicContext.Gameplay);
            Run(audio, 1f);
            var source = audio.Music.ActiveSource;
            var clip = source.clip;
            audio.CancelPending();
            audio.SetWorldAmbience(WorldAmbience.CozyNest);
            Run(audio, 0.5f);
            Assert.AreSame(source, audio.Music.ActiveSource);
            Assert.AreSame(clip, source.clip);
        }

        [Test]
        public void RapidWorldChangesCoalesceToTheLatestWorld()
        {
            var audio = Audio(context: MusicContext.Gameplay);
            Run(audio, 1f);
            audio.SetWorldAmbience(WorldAmbience.DewdropGarden);
            audio.SetWorldAmbience(WorldAmbience.RainyWindow);
            audio.SetWorldAmbience(WorldAmbience.MoonlitClouds);
            Run(audio, 4f);
            Assert.AreEqual("moonlit-clouds", audio.Music.Playing);
            Assert.AreEqual("moonlit-clouds", audio.AmbienceBus.Playing);
            var loops = audio.GetComponents<AudioSource>().Where(s => s.loop && s.clip != null).Select(s => s.clip.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "moonlit-clouds", "moonlit-clouds" }, loops, "old loops released");
        }

        [Test]
        public void MuteSilencesEverythingImmediatelyAndKeepsTheLevels()
        {
            var audio = Audio(context: MusicContext.Gameplay);
            Run(audio, 1.5f);
            audio.SetMusicVolume(0.28f);
            audio.SetSoundVolume(0.41f);
            audio.SetAmbienceVolume(0.12f);
            Assert.AreEqual(0.28f, audio.Music.ActiveSource.volume, 0.001f);
            Assert.AreEqual(0.12f, audio.AmbienceBus.ActiveSource.volume, 0.001f);

            audio.SetMasterMuted(true);
            Assert.IsTrue(audio.GetComponents<AudioSource>().All(s => s.volume == 0f));
            audio.PlayButton();
            audio.PlayToggle();
            Assert.IsFalse(audio.HasVoiceClip("ui_"), "no confirmation sound while muted");
            Assert.AreEqual(0.28f, audio.MusicVolume);
            Assert.AreEqual(0.41f, audio.SoundVolume);

            audio.SetMasterMuted(false);
            Assert.AreEqual(0.28f, audio.Music.ActiveSource.volume, 0.001f);
            audio.PlayButton();
            Assert.IsTrue(audio.HasVoiceClip("ui_tap"));
            var tap = audio.GetComponents<AudioSource>().First(s => !s.loop && s.clip != null);
            Assert.LessOrEqual(tap.volume, 0.41f, "UI rides the effects slider");
        }

        [Test]
        public void FocusAndPauseGatePlaybackAndResumeTheSameLoop()
        {
            var audio = Audio(context: MusicContext.Gameplay);
            Run(audio, 1.5f);
            var clip = audio.Music.ActiveSource.clip;
            audio.SendMessage("OnApplicationFocus", false);
            audio.SendMessage("OnApplicationPause", true);
            Assert.Zero(audio.Music.ActiveSource.volume);
            audio.SendMessage("OnApplicationFocus", true);
            Assert.Zero(audio.Music.ActiveSource.volume, "focus alone must not resume a paused app");
            audio.PlayButton();
            Assert.IsFalse(audio.HasVoiceClip("ui_"));
            audio.SendMessage("OnApplicationPause", false);
            Assert.AreSame(clip, audio.Music.ActiveSource.clip, "resumes, not restarts");
            Assert.AreEqual(audio.MusicVolume, audio.Music.ActiveSource.volume, 0.001f);
        }

        // ------------------------------------------------------------------ gameplay cues

        [Test]
        public void MovesUseVariantsWallsAndBodiesUseTheirOwnCues()
        {
            var audio = Audio(context: MusicContext.Gameplay);
            string last = null;
            int repeats = 0;
            for (int i = 0; i < 12; i++)
            {
                audio.StopEffects();
                Run(audio, 0.1f);
                audio.PlayMove(new[] { CueEvent(GameEventType.PlopMoved) });
                var clip = audio.GetComponents<AudioSource>().Single(s => !s.loop && s.clip != null).clip.name;
                StringAssert.StartsWith("move_", clip);
                if (clip == last) repeats++;
                last = clip;
            }
            Assert.Zero(repeats, "never the same move variant twice in a row");

            audio.StopEffects();
            audio.PlayMove(new[] { CueEvent(GameEventType.PlopBlocked) });
            Assert.IsTrue(audio.HasVoiceClip("blocked_"), "nothing moved: a soft bonk");
            audio.StopEffects();
            Run(audio, 0.2f);
            audio.PlayMove(new[] { CueEvent(GameEventType.PlopMoved), CueEvent(GameEventType.PlopBlocked, BlockReason.Plop) });
            Assert.IsTrue(audio.HasVoiceClip("collision_"));
            Assert.IsTrue(audio.HasVoiceClip("move_"));
        }

        [Test]
        public void EngineMovesDriveTheCues()
        {
            var audio = Audio(context: MusicContext.Gameplay);
            var engine = new GameEngine();
            engine.LoadLevel(LevelText.Parse("wall", "R#r"));
            audio.PlayMove(engine.TryMove(Direction.Right).Events);
            Assert.IsTrue(audio.HasVoiceClip("blocked_"));

            audio.StopEffects();
            Run(audio, 0.2f);
            engine.LoadLevel(LevelText.Parse("sleep", "R.r."));
            var result = engine.TryMove(Direction.Right);
            result = engine.TryMove(Direction.Right);
            Assert.IsTrue(result.Events.Any(e => e.Type == GameEventType.PlopSettled));
            audio.PlayMove(result.Events);
            Run(audio, 0.2f);
            Assert.IsTrue(audio.HasVoiceClip("sleep"), "reaching the Nest plays the lullaby fall");
        }

        [Test]
        public void CompletionJingleWaitsAndUndoRestartOrLevelChangeCancelIt()
        {
            var audio = Audio(context: MusicContext.Gameplay);
            audio.PlayCompletion(perfect: true, dustEarned: 12);
            Assert.AreEqual(2, audio.PendingCount);
            Run(audio, 0.5f);
            Assert.IsTrue(audio.HasVoiceClip("perfect"));
            Run(audio, 1.2f);
            Assert.IsTrue(audio.HasVoiceClip("dust"));

            audio.StopEffects();
            audio.PlayCompletion(perfect: false, dustEarned: 0);
            audio.PlayUndo();
            Run(audio, 0.6f);
            Assert.IsFalse(audio.HasVoiceClip("complete"), "undo cancels the pending jingle");

            audio.PlayCompletion(perfect: false, dustEarned: 8);
            audio.CancelPending();
            Run(audio, 1.6f);
            Assert.IsFalse(audio.HasVoiceClip("complete"));
            Assert.IsFalse(audio.HasVoiceClip("dust"));
        }

        [Test]
        public void HeroCuesDuckTheMusicAndItRecovers()
        {
            var audio = Audio(context: MusicContext.Gameplay);
            Run(audio, 1.5f);
            audio.PlayWorldUnlock();
            Run(audio, 1.2f);
            Assert.Less(audio.Music.ActiveSource.volume, audio.MusicVolume * 0.75f, "music steps back under the fanfare");
            Run(audio, 4f);
            Assert.AreEqual(audio.MusicVolume, audio.Music.ActiveSource.volume, 0.01f);
        }

        [Test]
        public void HapticsUseOneCueWithCompletionTakingPriority()
        {
            var blocked = CueEvent(GameEventType.PlopBlocked);
            var move = CueEvent(GameEventType.PlopMoved);
            var settle = CueEvent(GameEventType.PlopSettled);
            var complete = CueEvent(GameEventType.LevelCompleted);
            Assert.AreEqual(PlopHaptics.Cue.None, PlopHaptics.SelectCue(null));
            Assert.AreEqual(PlopHaptics.Cue.Blocked, PlopHaptics.SelectCue(new[] { blocked }));
            Assert.AreEqual(PlopHaptics.Cue.Move, PlopHaptics.SelectCue(new[] { blocked, move }));
            Assert.AreEqual(PlopHaptics.Cue.Settle, PlopHaptics.SelectCue(new[] { settle, move, blocked }));
            Assert.AreEqual(PlopHaptics.Cue.Complete, PlopHaptics.SelectCue(new[] { complete, blocked, settle }));
        }

        // ------------------------------------------------------------------ preferences

        [Test]
        public void UntouchedLegacyDefaultsMigrateToTheNewDefaults()
        {
            foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.SetFloat(PlopAudio.LegacyMusicKey, 0.24f);
            PlayerPrefs.SetFloat(PlopAudio.LegacySoundKey, 0.65f);
            var audio = Audio(prefs: true);
            Assert.AreEqual(0.45f, audio.MusicVolume);
            Assert.AreEqual(0.80f, audio.SoundVolume);
            Assert.AreEqual(0.30f, audio.AmbienceVolume);
        }

        [Test]
        public void ChosenLegacyLevelsAreKept_AndSilencedMusicKeepsAmbienceSilent()
        {
            foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.SetFloat(PlopAudio.LegacyMusicKey, 0f);
            PlayerPrefs.SetFloat(PlopAudio.LegacySoundKey, 0.37f);
            PlayerPrefs.SetInt(PlopAudio.MuteKey, 1);
            var audio = Audio(prefs: true);
            Assert.AreEqual(0f, audio.MusicVolume);
            Assert.AreEqual(0.37f, audio.SoundVolume, 0.001f);
            Assert.AreEqual(0f, audio.AmbienceVolume, "ambience used to follow the music slider");
            Assert.IsTrue(audio.MasterMuted);
        }

        [Test]
        public void NewKeysRoundTripAndRestoreDefaultsSaves()
        {
            foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
            var audio = Audio(prefs: true);
            audio.SetMusicVolume(0.2f);
            audio.SetSoundVolume(0.9f);
            audio.SetAmbienceVolume(0.05f);
            audio.SetMasterMuted(true); // saves
            var loaded = Audio(prefs: true);
            Assert.AreEqual(0.2f, loaded.MusicVolume, 0.001f);
            Assert.AreEqual(0.9f, loaded.SoundVolume, 0.001f);
            Assert.AreEqual(0.05f, loaded.AmbienceVolume, 0.001f);
            Assert.IsTrue(loaded.MasterMuted);
            Assert.IsNull(loaded.Music.Playing, "a muted start plays nothing (sonic logo included)");
            loaded.PlaySonicLogo();
            Assert.IsFalse(loaded.HasVoiceClip("sonic_logo"));

            loaded.RestoreDefaults();
            var fresh = Audio(prefs: true);
            Assert.AreEqual(0.45f, fresh.MusicVolume);
            Assert.AreEqual(0.80f, fresh.SoundVolume);
            Assert.AreEqual(0.30f, fresh.AmbienceVolume);
            Assert.IsFalse(fresh.MasterMuted);
        }

        [UnityTest]
        public IEnumerator ControllerKeepsTheMenuThemeAcrossMenuScreensAndSwitchesForTheBoard()
        {
            var root = new GameObject("PLOP audio flow test");
            _made.Add(root);
            var controller = root.AddComponent<PlopGameController>();
            controller.RememberProgress = false;
            controller.UseLevels(new List<LevelDefinition> { LevelText.Parse("a", "R..r"), LevelText.Parse("b", "R.r") });
            yield return null;
            var audio = root.GetComponent<PlopAudio>();
            controller.ShowMenu();
            yield return null;
            Assert.AreEqual(MusicContext.Menu, audio.Context);
            controller.OpenStudio();
            yield return null;
            Assert.AreEqual(MusicContext.Menu, audio.Context, "Studio keeps the menu theme");
            controller.HandleBack();
            yield return null;
            Assert.AreEqual(MusicContext.Menu, audio.Context);
            controller.StartCampaign();
            yield return null;
            Assert.AreEqual(MusicContext.Gameplay, audio.Context);
            controller.ShowMenu();
            yield return null;
            Assert.AreEqual(MusicContext.Menu, audio.Context);
        }

        [UnityTest]
        public IEnumerator SonicLogoPlaysOnAnUnmutedStart()
        {
            var audio = Audio();
            audio.PlaySonicLogo();
            Assert.IsTrue(audio.HasVoiceClip("sonic_logo"));
            yield return null;
        }
    }
}
