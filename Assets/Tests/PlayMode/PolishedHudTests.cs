using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Tests.PlayMode
{
    public sealed class PolishedHudTests
    {
        [Test]
        public void MasterMuteKeepsIndependentGainValuesAndFrameSelectionClear()
        {
            var root = new GameObject("Settings polish test");
            try
            {
                var hud = root.AddComponent<GameHud>();
                hud.SetVolumes(0.38f, 0.72f);
                hud.ShowSettings();

                var mute = FindButton(root, "Master mute");
                var music = FindSlider(root, "Music");
                var sound = FindSlider(root, "Effects");
                var ambience = FindSlider(root, "Ambience");
                bool? muteEvent = null;
                int muteEvents = 0;
                int gainEvents = 0;
                hud.MasterMuteChanged += value => { muteEvent = value; muteEvents++; };
                hud.MusicChanged += _ => gainEvents++;
                hud.SoundChanged += _ => gainEvents++;

                hud.SetMasterMuted(true);
                Assert.AreEqual(0, muteEvents, "Reading a saved preference must not emit a UI change.");
                Assert.AreEqual("On", ValueLabel(mute));
                mute.onClick.Invoke();
                Assert.AreEqual(false, muteEvent);
                Assert.AreEqual("Off", ValueLabel(mute));
                mute.onClick.Invoke();
                Assert.AreEqual(true, muteEvent);
                Assert.AreEqual("On", ValueLabel(mute));
                Assert.AreEqual(2, muteEvents);
                Assert.AreEqual(0, gainEvents);
                Assert.AreEqual(0.38f, music.value, 0.001f);
                Assert.AreEqual(0.72f, sound.value, 0.001f);
                Assert.IsTrue(music.IsInteractable());
                Assert.IsTrue(sound.IsInteractable());

                var rate30 = FindButton(root, "30 FPS");
                var rate60 = FindButton(root, "60 FPS");
                hud.SetFrameRate(PlopGameController.FrameRateMode.Fps30);
                Assert.IsTrue(rate30.transform.Find("Icon").gameObject.activeSelf);
                Assert.IsFalse(rate60.transform.Find("Icon").gameObject.activeSelf);
                hud.SetFrameRate(PlopGameController.FrameRateMode.Fps60);
                Assert.IsFalse(rate30.transform.Find("Icon").gameObject.activeSelf);
                Assert.IsTrue(rate60.transform.Find("Icon").gameObject.activeSelf);

                Canvas.ForceUpdateCanvases();
                var haptics = (RectTransform)FindButton(root, "Haptics").transform;
                var reducedMotion = (RectTransform)FindButton(root, "Reduced motion").transform;
                var card = (RectTransform)mute.transform.parent;
                Assert.GreaterOrEqual(card.rect.height, -reducedMotion.anchoredPosition.y + reducedMotion.rect.height * 0.5f);
                Assert.GreaterOrEqual(haptics.anchoredPosition.y - haptics.rect.height * 0.5f,
                    reducedMotion.anchoredPosition.y + reducedMotion.rect.height * 0.5f);

                // Sprint 02: ambience slider and Restore Defaults fit inside the card below everything else.
                var restore = (RectTransform)FindButton(root, "Restore Defaults").transform;
                Assert.AreSame(card, restore.parent);
                Assert.GreaterOrEqual(card.rect.height, -restore.anchoredPosition.y + restore.rect.height * 0.5f + 20f);
                Assert.GreaterOrEqual(reducedMotion.anchoredPosition.y - reducedMotion.rect.height * 0.5f,
                    restore.anchoredPosition.y + restore.rect.height * 0.5f);
                int restores = 0, ambienceEvents = 0;
                hud.RestoreDefaultsClicked += () => restores++;
                hud.AmbienceChanged += _ => ambienceEvents++;
                hud.SetVolumes(0.1f, 0.2f, 0.3f);
                Assert.AreEqual(0.3f, ambience.value, 0.001f);
                Assert.AreEqual(0, ambienceEvents, "SetVolumes reflects saved values silently");
                ambience.value = 0.6f;
                Assert.AreEqual(1, ambienceEvents);
                restore.GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(1, restores);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DirectionalCuePointsOutsideTheCellsAndClearsWithHintInvalidation()
        {
            var root = new GameObject("Directional hint test");
            try
            {
                var hud = root.AddComponent<GameHud>();
                hud.SetComfortOptions(false, true);
                var layout = BoardLayout.Compute(720, 1280, new Rect(0, 34, 720, 1202), 7, 8);
                hud.SetBoardLayout(layout);
                var cue = root.GetComponentsInChildren<RectTransform>(true).Single(rt => rt.name == "Directional hint");
                var arrow = cue.Find("Icon");
                var expected = new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

                for (int i = 0; i < expected.Length; i++)
                {
                    var direction = DirectionUtil.All[i];
                    var motion = expected[i];
                    hud.SetHint("Try a swipe.", false, direction);
                    Assert.IsTrue(cue.gameObject.activeSelf);
                    Assert.AreEqual(1f, cue.GetComponent<CanvasGroup>().alpha);
                    Assert.Greater(Vector2.Dot(motion, (Vector2)(arrow.localRotation * Vector3.right)), 0.99f);
                    Assert.IsTrue(cue.GetComponentsInChildren<Image>().All(image => !image.raycastTarget));
                    var cueCenter = cue.anchoredPosition * layout.CanvasScale + layout.SafePixels.min;
                    var rimCenter = layout.BoardRectPixels.center;
                    float radius = cue.sizeDelta.x * layout.CanvasScale * 0.5f;
                    float rimExtent = (Mathf.Abs(motion.x) * layout.BoardRectPixels.width +
                        Mathf.Abs(motion.y) * layout.BoardRectPixels.height) * 0.5f;
                    float inwardCoverage = rimExtent - Vector2.Dot(motion, cueCenter - rimCenter) + radius;
                    Assert.Less(inwardCoverage, BoardLayout.PanelPadding * layout.PixelsPerUnit);
                }

                hud.SetHint("Looking for a hint…", true);
                Assert.IsFalse(cue.gameObject.activeSelf);
                hud.SetHint("No hint available. Try Undo.", false);
                Assert.IsFalse(cue.gameObject.activeSelf);
                hud.SetHint("Try swiping right.", false, Direction.Right);
                hud.SetHint(null, false);
                Assert.IsFalse(cue.gameObject.activeSelf);

                hud.SetHint("Try swiping down.", false, Direction.Down);
                hud.ShowSettings();
                Assert.IsFalse(cue.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Button FindButton(GameObject root, string label) => root.GetComponentsInChildren<Button>()
            .Single(button => button.name == label + " Button");

        private static Slider FindSlider(GameObject root, string label) => root.GetComponentsInChildren<Slider>()
            .Single(slider => slider.name == label + " volume");

        private static string ValueLabel(Button button) => button.GetComponentsInChildren<Text>()
            .Single(text => text.name == "Value").text;
    }
}
