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
    public class PlopAudioTests
    {
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

        private static GameEvent CueEvent(GameEventType type) =>
            new GameEvent(type, 0, default, default, Direction.Right, BlockReason.Wall, -1, 1);

        [UnityTest]
        public IEnumerator MusicAndEffectsKeepIndependentGainsWhileMuted()
        {
            var root = CreateAudioRoot();
            try
            {
                var audio = root.AddComponent<PlopAudio>();
                audio.SetMusicVolume(0f);
                audio.Initialize(loadPreferences: false);
                var beds = Beds(root);
                var effects = Effects(root);
                Assert.AreEqual(2, beds.Length);
                Assert.AreEqual(3, effects.Length);
                Assert.IsTrue(beds.All(source => source.clip == null));

                audio.SetSoundVolume(0.41f);
                audio.SetMusicVolume(0.28f);
                Assert.IsTrue(effects.All(source => Mathf.Abs(source.volume - 0.41f) < 0.001f));
                Assert.That(beds.Sum(source => source.volume), Is.EqualTo(0.28f).Within(0.001f));

                audio.SetMasterMuted(true);
                audio.PlayButton();
                Assert.IsTrue(root.GetComponents<AudioSource>().All(source => source.volume == 0f));
                Assert.IsTrue(effects.All(source => source.clip == null));
                Assert.AreEqual(0.28f, audio.MusicVolume);
                Assert.AreEqual(0.41f, audio.SoundVolume);

                audio.SetMasterMuted(false);
                Assert.That(beds.Sum(source => source.volume), Is.EqualTo(0.28f).Within(0.001f));
                Assert.IsTrue(effects.All(source => Mathf.Abs(source.volume - 0.41f) < 0.001f));
            }
            finally
            {
                Object.Destroy(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EachWorldHasAContinuousMonoBedAndSameWorldKeepsItsClip()
        {
            var root = CreateAudioRoot();
            try
            {
                var audio = root.AddComponent<PlopAudio>();
                audio.Initialize(loadPreferences: false);
                var bed = Beds(root)[0];
                var names = new HashSet<string>();
                foreach (WorldAmbience world in Enum.GetValues(typeof(WorldAmbience)))
                {
                    audio.SetMusicVolume(0f);
                    audio.SetWorldAmbience(world);
                    audio.SetMusicVolume(0.17f);
                    Assert.AreEqual(world, audio.CurrentAmbience);
                    Assert.NotNull(bed.clip, world.ToString());
                    Assert.AreEqual(1, bed.clip.channels);
                    Assert.IsTrue(names.Add(bed.clip.name));
                    var reused = bed.clip;
                    audio.SetWorldAmbience(world);
                    Assert.AreSame(reused, bed.clip);

                    const int window = 512;
                    var start = new float[window];
                    var end = new float[window];
                    Assert.IsTrue(bed.clip.GetData(start, 0));
                    Assert.IsTrue(bed.clip.GetData(end, bed.clip.samples - window));
                    Assert.IsTrue(Array.Exists(start, sample => Mathf.Abs(sample) > 0.002f));
                    Assert.IsTrue(Array.Exists(end, sample => Mathf.Abs(sample) > 0.002f));
                    Assert.That(Mathf.Abs(start[0] - end[window - 1]), Is.LessThan(0.006f),
                        "The imported PCM must not have a sharp wrap discontinuity.");
                }
                Assert.AreEqual(5, names.Count);
            }
            finally
            {
                Object.Destroy(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CrossfadeUsesTwoBedsAndCoalescesBeforePlayingTheLatestWorld()
        {
            var root = CreateAudioRoot();
            try
            {
                var audio = root.AddComponent<PlopAudio>();
                audio.Initialize(loadPreferences: false);
                var beds = Beds(root);
                audio.SetWorldAmbience(WorldAmbience.DewdropGarden);
                audio.SetWorldAmbience(WorldAmbience.RainyWindow);

                float deadline = Time.realtimeSinceStartup + 3f;
                while (beds.Count(source => source.clip != null) < 2 && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.IsTrue(beds.Any(source => source.clip != null && source.clip.name == "cozy-nest"));
                Assert.IsTrue(beds.Any(source => source.clip != null && source.clip.name == "rainy-window"));

                audio.SetWorldAmbience(WorldAmbience.TwilightAttic);
                audio.SetWorldAmbience(WorldAmbience.MoonlitClouds);
                deadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < deadline &&
                    !(beds.Count(source => source.clip != null) == 1 &&
                    beds.Any(source => source.clip != null && source.clip.name == "moonlit-clouds")))
                    yield return null;

                var active = beds.Single(source => source.clip != null);
                Assert.AreEqual("moonlit-clouds", active.clip.name);
                Assert.That(active.volume, Is.EqualTo(audio.MusicVolume).Within(0.001f));
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator FocusAndPauseBothGatePlayback()
        {
            var root = CreateAudioRoot();
            try
            {
                var audio = root.AddComponent<PlopAudio>();
                audio.Initialize(loadPreferences: false);
                var bed = Beds(root).Single(source => source.clip != null);
                var clip = bed.clip;
                audio.SendMessage("OnApplicationFocus", false);
                audio.SendMessage("OnApplicationPause", true);
                Assert.Zero(bed.volume);
                audio.SendMessage("OnApplicationFocus", true);
                Assert.Zero(bed.volume, "Focus alone must not resume a paused app.");
                audio.PlayButton();
                Assert.IsTrue(Effects(root).All(source => source.clip == null));

                audio.SendMessage("OnApplicationPause", false);
                Assert.AreSame(clip, bed.clip);
                Assert.That(bed.volume, Is.EqualTo(audio.MusicVolume).Within(0.001f));
            }
            finally
            {
                Object.Destroy(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WallAndSleepingPlopCollisionsUseDistinctQuietCues()
        {
            var root = CreateAudioRoot();
            try
            {
                var audio = root.AddComponent<PlopAudio>();
                audio.SetMusicVolume(0f);
                audio.Initialize(loadPreferences: false);
                var effects = Effects(root);
                var engine = new GameEngine();
                engine.LoadLevel(LevelText.Parse("wall", "R#r"));
                audio.PlayMove(engine.TryMove(Direction.Right).Events);
                Assert.IsTrue(effects.Any(source => source.clip != null && source.clip.name == "Pebble knock"));

                engine.LoadLevel(LevelText.Parse("body", "RBbr"));
                var settle = engine.TryMove(Direction.Right);
                Assert.IsTrue(settle.Events.Any(gameEvent => gameEvent.Type == GameEventType.PlopSettled));
                var blocked = engine.TryMove(Direction.Right);
                Assert.IsTrue(blocked.Events.Any(gameEvent => gameEvent.BlockReason == BlockReason.Plop));
                audio.PlayMove(blocked.Events);
                Assert.IsTrue(effects.Any(source => source.clip != null && source.clip.name == "Rubber bounce"));
                Assert.IsTrue(effects.All(source => source.volume <= audio.SoundVolume));
            }
            finally
            {
                Object.Destroy(root);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MusicSoundAndMasterMutePreferencesRoundTripSeparately()
        {
            const string musicKey = "plop.musicVolume";
            const string soundKey = "plop.soundVolume";
            const string muteKey = "plop.masterMuted";
            bool hadMusic = PlayerPrefs.HasKey(musicKey), hadSound = PlayerPrefs.HasKey(soundKey);
            bool hadMute = PlayerPrefs.HasKey(muteKey);
            float previousMusic = PlayerPrefs.GetFloat(musicKey), previousSound = PlayerPrefs.GetFloat(soundKey);
            int previousMute = PlayerPrefs.GetInt(muteKey);
            var root = CreateAudioRoot();
            var resumed = new GameObject("PLOP resumed settings test");
            try
            {
                PlayerPrefs.SetFloat(musicKey, 0f);
                PlayerPrefs.SetFloat(soundKey, 0.37f);
                PlayerPrefs.SetInt(muteKey, 0);
                var audio = root.AddComponent<PlopAudio>();
                audio.Initialize(loadPreferences: true);
                Assert.AreEqual(0f, audio.MusicVolume);
                Assert.That(audio.SoundVolume, Is.EqualTo(0.37f).Within(0.001f));

                audio.SetSoundVolume(0.92f);
                audio.SetMasterMuted(true);
                var loaded = resumed.AddComponent<PlopAudio>();
                loaded.Initialize(loadPreferences: true);
                Assert.IsTrue(loaded.MasterMuted);
                Assert.AreEqual(0f, loaded.MusicVolume);
                Assert.That(loaded.SoundVolume, Is.EqualTo(0.92f).Within(0.001f));
                Assert.IsTrue(Beds(resumed).All(source => source.clip == null));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(resumed);
                if (hadMusic) PlayerPrefs.SetFloat(musicKey, previousMusic);
                else PlayerPrefs.DeleteKey(musicKey);
                if (hadSound) PlayerPrefs.SetFloat(soundKey, previousSound);
                else PlayerPrefs.DeleteKey(soundKey);
                if (hadMute) PlayerPrefs.SetInt(muteKey, previousMute);
                else PlayerPrefs.DeleteKey(muteKey);
                PlayerPrefs.Save();
            }
            yield return null;
        }

        private static GameObject CreateAudioRoot()
        {
            var root = new GameObject("PLOP audio test");
            if (Object.FindAnyObjectByType<AudioListener>() == null) root.AddComponent<AudioListener>();
            return root;
        }

        private static AudioSource[] Beds(GameObject parent) =>
            Array.FindAll(parent.GetComponents<AudioSource>(), source => source.loop);

        private static AudioSource[] Effects(GameObject parent) =>
            Array.FindAll(parent.GetComponents<AudioSource>(), source => !source.loop);
    }
}
