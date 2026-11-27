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
                foreach (var slider in sliders)
                {
                    slider.value = 0f;
                    Canvas.ForceUpdateCanvases();
                    var handle = slider.handleRect.rect;
                    Assert.That(handle.width, Is.GreaterThan(0f));
                    Assert.That(handle.height, Is.EqualTo(handle.width).Within(0.1f),
                        "Slider anchors must not stretch the thumb into the volume label.");
                }
                sliders.Single(s => s.name == "Music volume").value = 0.2f;
                sliders.Single(s => s.name == "Effects volume").value = 0f;
                Assert.AreEqual(0.2f, audio.MusicVolume);
                Assert.AreEqual(0f, audio.SoundVolume);
                FindButton(root, "30 FPS").onClick.Invoke();
                Assert.AreEqual(30, Application.targetFrameRate);

                FindButton(root, "Done").onClick.Invoke();
                Assert.IsFalse(hud.BlocksGameplayInput);
                controller.Move(Direction.Right);
                Assert.IsTrue(controller.Engine.IsCompleted);
                var completionMessage = root.GetComponentsInChildren<Text>().Single(text => text.name == "Completion message");
                Assert.AreEqual("Sweet Dreams!", completionMessage.text);
                generator.Invalidate();
                generator.Populate(completionMessage.text,
                    completionMessage.GetGenerationSettings(completionMessage.rectTransform.rect.size));
                Assert.Greater(generator.vertexCount, 4, "The completion heading must generate visible glyphs.");
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
            .Single(button => button.name == label + " Button");
    }
}
