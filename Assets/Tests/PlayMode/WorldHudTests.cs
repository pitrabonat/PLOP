using System.Linq;
using NUnit.Framework;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Tests.PlayMode
{
    public sealed class WorldHudTests
    {
        [Test]
        public void WorldsShowProgressAndRouteToOnlyTheirAvailableLevels()
        {
            var root = new GameObject("Worlds test");
            try
            {
                var hud = root.AddComponent<GameHud>();
                var campaign = LevelLibrary.Load();
                var worlds = WorldCatalog.Load().Worlds;
                Assert.AreEqual(9, worlds.Count);
                int chosen = -1;
                hud.LevelChosen += index => chosen = index;

                hud.SetCurrentWorld(worlds[0]);
                Assert.AreEqual("Cozy Nest", FindText(root, "Current world").text);
                hud.ShowWorlds(worlds, campaign, index => index <= 24, index => index < 22,
                    currentIndex: 4, resumeIndex: 24);

                Assert.IsTrue(hud.BlocksGameplayInput);
                var ranges = root.GetComponentsInChildren<Text>()
                    .Where(text => text.name == "World levels and progress")
                    .Select(text => text.text).ToArray();
                CollectionAssert.Contains(ranges, "Levels 1–5 · 5/5");
                CollectionAssert.Contains(ranges, "Levels 6–20 · 15/15");
                CollectionAssert.Contains(ranges, "Levels 21–34 · 2/14");
                Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(text => text.name == "World status" && text.text == "Current"));
                var rainyWindow = FindButton(root, "Rainy Window");
                Assert.IsFalse(rainyWindow.IsInteractable());
                rainyWindow.onClick.Invoke();
                Assert.IsFalse(FindButton(root, "Dewdrop Garden").IsInteractable());
                Assert.IsTrue(FindButton(root, "Breezy Meadow").IsInteractable());

                FindButton(root, "Breezy Meadow").onClick.Invoke();
                Assert.AreEqual("Breezy Meadow", FindText(root, "Picker title").text);
                Assert.AreEqual("Levels 21–30", FindText(root, "Level range").text);
                var available = root.GetComponentsInChildren<Button>().Single(button =>
                    button.name == "Level Button" && button.GetComponentInChildren<Text>().text == "Level 23");
                available.onClick.Invoke();
                Assert.AreEqual(22, chosen);
                var unavailable = root.GetComponentsInChildren<Button>().Single(button =>
                    button.name == "Level Button" && button.GetComponentInChildren<Text>().text == "Level 26 · Locked");
                Assert.IsFalse(unavailable.IsInteractable());
                unavailable.onClick.Invoke();
                Assert.AreEqual(22, chosen);

                var more = FindButton(root, "More");
                more.onClick.Invoke();
                Assert.AreEqual("Levels 31–34", FindText(root, "Level range").text);
                Assert.IsFalse(more.IsInteractable());
                FindButton(root, "Close").onClick.Invoke();
                var resume = FindButton(root, "Resume");
                Assert.AreEqual("Resume Level 25", resume.GetComponentInChildren<Text>().text);
                resume.onClick.Invoke();
                Assert.AreEqual(24, chosen);

                hud.CloseLevelPicker();
                Assert.IsFalse(hud.BlocksGameplayInput);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ComfortSettingsAreExplicitAndWorldAnnouncementIsNonmodal()
        {
            var root = new GameObject("Comfort test");
            try
            {
                var hud = root.AddComponent<GameHud>();
                int hapticEvents = 0;
                bool? motionEvent = null;
                hud.HapticsChanged += value => hapticEvents += value ? 1 : -1;
                hud.ReducedMotionChanged += value => motionEvent = value;

                hud.SetComfortOptions(false, false);
                hud.ShowSettings();
                var haptics = FindButton(root, "Haptics");
                var motion = FindButton(root, "Reduced motion");
                Assert.AreEqual("Off", FindText(haptics.gameObject, "Value").text);
                haptics.onClick.Invoke();
                Assert.AreEqual(1, hapticEvents);
                Assert.AreEqual("On", FindText(haptics.gameObject, "Value").text);
                motion.onClick.Invoke();
                Assert.AreEqual(true, motionEvent);
                Assert.AreEqual("On", FindText(motion.gameObject, "Value").text);
                Assert.IsFalse(motion.GetComponent<PressScale>().enabled);

                var scrim = (RectTransform)root
                    .GetComponentsInChildren<Image>().Single(image => image.name == "Settings").transform;
                Assert.AreEqual("HUD Canvas", scrim.parent.name);
                Assert.AreEqual(Vector2.zero, scrim.anchorMin);
                Assert.AreEqual(Vector2.one, scrim.anchorMax);
                hud.CloseSettings();

                hud.ShowWorldUnlocked("Dewdrop Garden");
                Assert.AreEqual("Dewdrop Garden", FindText(root, "Unlocked world").text);
                Assert.IsFalse(hud.BlocksGameplayInput);
                hud.CancelWorldAnnouncement();
                Assert.IsFalse(root.GetComponentsInChildren<Text>().Any(text => text.name == "Unlocked world"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Button FindButton(GameObject root, string name) => root.GetComponentsInChildren<Button>()
            .Single(button => button.name == name + " Button");

        private static Text FindText(GameObject root, string name) => root.GetComponentsInChildren<Text>()
            .Single(text => text.name == name);
    }
}
