using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Plop.Tests.PlayMode
{
    public sealed class ProductionHudTests
    {
        [Test]
        public void SettingsPauseBoardInputAndCompletionAllowsImmediateNext()
        {
            var root = new GameObject("Test game");
            int priorRate = Application.targetFrameRate;
            int priorVSync = QualitySettings.vSyncCount;
            try
            {
                var controller = root.AddComponent<PlopGameController>();
                controller.RememberProgress = false;
                controller.UseLevels(new List<LevelDefinition>
                {
                    LevelText.Parse("modal", "Rr"),
                    LevelText.Parse("next", "R.r")
                });
                var hud = root.GetComponent<GameHud>();
                var audio = root.GetComponent<PlopAudio>();

                var title = root.GetComponentsInChildren<Text>().Single(text => text.name == "Title");
                Assert.AreEqual("modal", title.text);
                var generator = new TextGenerator();
                generator.Populate(title.text, title.GetGenerationSettings(title.rectTransform.rect.size));
                Assert.Greater(generator.vertexCount, 4, "The level heading must fit and generate visible glyphs.");

                FindButton(root, "Settings").onClick.Invoke();
                Assert.IsTrue(hud.IsSettingsOpen);
                controller.Move(Direction.Right);
                Assert.AreEqual(0, controller.Engine.MoveCount);
                var hint = controller.RequestHintAsync();
                Assert.IsTrue(hint.IsCompleted, "A modal must reject the hint before solver work.");
                Assert.IsNull(hint.Result);

                var sliders = root.GetComponentsInChildren<Slider>();
                sliders.Single(s => s.name == "Music volume").value = 0.2f;
                sliders.Single(s => s.name == "Sound volume").value = 0f;
                Assert.AreEqual(0.2f, audio.MusicVolume);
                Assert.AreEqual(0f, audio.SoundVolume);
                FindButton(root, "30 FPS").onClick.Invoke();
                Assert.AreEqual(30, Application.targetFrameRate);

                FindButton(root, "Done").onClick.Invoke();
                Assert.IsFalse(hud.BlocksGameplayInput);
                controller.Move(Direction.Right);
                Assert.IsTrue(controller.Engine.IsCompleted);
                Assert.IsTrue(root.GetComponentsInChildren<Text>().Any(text => text.text == "Sweet Dreams!"));
                FindButton(root, "Next Level").onClick.Invoke();
                Assert.AreEqual(1, controller.LevelIndex);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Application.targetFrameRate = priorRate;
                QualitySettings.vSyncCount = priorVSync;
            }
        }

        private static Button FindButton(GameObject root, string label) => root.GetComponentsInChildren<Button>()
            .Single(button => button.GetComponentInChildren<Text>().text == label);
    }
}
