using System;
using System.Collections;
using NUnit.Framework;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Plop.Tests.PlayMode
{
    public class PlopAudioTests
    {
        [UnityTest]
        public IEnumerator OriginalMusicAndEffectsUseIndependentGains()
        {
            var root = new GameObject("PLOP audio test");
            try
            {
                if (Object.FindAnyObjectByType<AudioListener>() == null) root.AddComponent<AudioListener>();
                var audio = root.AddComponent<PlopAudio>();
                audio.SetMusicVolume(0f);
                audio.Initialize(loadPreferences: false);
                var sources = root.GetComponents<AudioSource>();
                Assert.AreEqual(2, sources.Length);
                var music = Array.Find(sources, source => source.loop);
                var sound = Array.Find(sources, source => !source.loop);
                Assert.NotNull(music);
                Assert.NotNull(sound);
                Assert.IsNull(music.clip, "A muted music source should not synthesize its loop at startup.");

                audio.SetSoundVolume(0.41f);
                audio.SetMusicVolume(0.28f);
                Assert.That(sound.volume, Is.EqualTo(0.41f).Within(0.001f));
                Assert.That(music.volume, Is.EqualTo(0.28f).Within(0.001f));
                Assert.NotNull(music.clip);
                Assert.AreEqual(1, music.clip.channels);
                var samples = new float[2400];
                Assert.IsTrue(music.clip.GetData(samples, 0));
                Assert.IsTrue(Array.Exists(samples, sample => Mathf.Abs(sample) > 0.002f), "The original loop should contain audible samples.");

                audio.SetMusicVolume(0f);
                Assert.IsFalse(music.isPlaying);
                Assert.That(sound.volume, Is.EqualTo(0.41f).Within(0.001f));
                audio.SetSoundVolume(0f);
                audio.PlayButton();
                Assert.IsFalse(sound.isPlaying);
            }
            finally
            {
                Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator MusicAndSoundPreferencesRoundTripSeparately()
        {
            const string musicKey = "plop.musicVolume";
            const string soundKey = "plop.soundVolume";
            bool hadMusic = PlayerPrefs.HasKey(musicKey), hadSound = PlayerPrefs.HasKey(soundKey);
            float previousMusic = PlayerPrefs.GetFloat(musicKey), previousSound = PlayerPrefs.GetFloat(soundKey);
            var root = new GameObject("PLOP audio preferences test");
            try
            {
                PlayerPrefs.SetFloat(musicKey, 0f);
                PlayerPrefs.SetFloat(soundKey, 0.37f);
                var audio = root.AddComponent<PlopAudio>();
                audio.Initialize(loadPreferences: true);
                Assert.AreEqual(0f, audio.MusicVolume);
                Assert.That(audio.SoundVolume, Is.EqualTo(0.37f).Within(0.001f));

                audio.SetSoundVolume(0.92f);
                audio.SavePreferences();
                Assert.AreEqual(0f, PlayerPrefs.GetFloat(musicKey));
                Assert.That(PlayerPrefs.GetFloat(soundKey), Is.EqualTo(0.92f).Within(0.001f));
            }
            finally
            {
                Object.Destroy(root);
                if (hadMusic) PlayerPrefs.SetFloat(musicKey, previousMusic);
                else PlayerPrefs.DeleteKey(musicKey);
                if (hadSound) PlayerPrefs.SetFloat(soundKey, previousSound);
                else PlayerPrefs.DeleteKey(soundKey);
                PlayerPrefs.Save();
            }

            yield return null;
        }
    }
}
