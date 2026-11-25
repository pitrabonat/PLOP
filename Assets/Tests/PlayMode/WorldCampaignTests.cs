using System.Collections;
using System.Linq;
using NUnit.Framework;
using Plop.Presentation;
using Plop.Solver;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace Plop.Tests.PlayMode
{
    public sealed class WorldCampaignTests
    {
        [UnityTest]
        public IEnumerator CampaignBoundariesUnlockOneWorldAndResumeReturnsFromReplay()
        {
            var root = new GameObject("World campaign test");
            int frameRate = Application.targetFrameRate;
            int vSync = QualitySettings.vSyncCount;
            try
            {
                var controller = root.AddComponent<PlopGameController>();
                controller.RememberProgress = false;
                yield return null;
                Assert.IsTrue(controller.Menu.IsOpen, "the shipped game opens on the main menu");
                controller.StartCampaign(); // PLAY

                var catalog = WorldCatalog.Load();
                var audio = root.GetComponent<PlopAudio>();
                var hud = root.GetComponent<GameHud>();
                Assert.AreEqual(160, controller.LevelCount);
                Assert.AreEqual("cozy-nest", controller.CurrentWorld.Id);
                controller.SetHaptics(true);
                controller.SetReducedMotion(true);
                Assert.IsTrue(controller.HapticsEnabled);
                Assert.IsTrue(controller.ReducedMotionEnabled);
                audio.SetMusicVolume(0f);

                for (int i = 0; i < 80; i++)
                {
                    Assert.AreEqual(i, controller.LevelIndex);
                    var world = catalog.ForLevelIndex(i);
                    Assert.AreSame(world, controller.CurrentWorld);
                    Assert.AreEqual(world.Ambience, audio.CurrentAmbience);
                    Assert.IsFalse(controller.IsLevelUnlocked(i + 1));
                    var solution = BfsSolver.Solve(controller.Engine.Level);
                    Assert.AreEqual(SolverStatus.Solved, solution.Status);
                    foreach (var move in solution.Moves) controller.Move(move);
                    Assert.IsTrue(controller.Engine.IsCompleted);
                    Assert.IsTrue(controller.IsLevelUnlocked(i + 1));
                    Assert.IsFalse(hud.BlocksGameplayInput, "The unlock announcement must not block Next Level.");
                    controller.NextLevel();
                    // Let Unity dispose the old board objects between small batches.
                    if (i % 8 == 7) yield return null;
                }

                Assert.AreEqual(80, controller.LevelIndex);
                Assert.AreEqual("dewdrop-garden", controller.CurrentWorld.Id);
                Assert.AreEqual(80, controller.CampaignResumeIndex);
                Assert.IsTrue(controller.TrySelectLevel(0));
                Assert.AreEqual("cozy-nest", controller.CurrentWorld.Id);
                Assert.AreEqual(80, controller.CampaignResumeIndex);
                Assert.IsTrue(controller.TrySelectLevel(controller.CampaignResumeIndex));
                Assert.AreEqual("dewdrop-garden", controller.CurrentWorld.Id);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Application.targetFrameRate = frameRate;
                QualitySettings.vSyncCount = vSync;
            }
        }

        [UnityTest]
        public IEnumerator PortraitWorldFramesExposeUnityRenderCounters()
        {
            var root = new GameObject("World render profiling");
            var cameraObject = new GameObject("World render camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            int width = 720, height = 1280;
            var target = new RenderTexture(width, height, 16);
            var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 8);
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 8);
            try
            {
                camera.targetTexture = target;
                camera.orthographic = true;
                // Fit a real portrait camera target even when batch PlayMode has a desktop Game View.
                camera.aspect = (float)width / height;
                camera.orthographicSize = 6f;
                camera.transform.position = new Vector3(0, 0, -10);
                var levels = LevelLibrary.Load();
                var board = root.AddComponent<BoardView>();
                var backdrop = root.AddComponent<WorldBackdrop>();
                var world = WorldCatalog.Load().ForLevelIndex(80);
                var level = levels[99];
                var layout = BoardLayout.Compute(width, height, new Rect(0, 0, width, height), level.Width, level.Height);
                camera.orthographicSize = layout.OrthographicSize;
                camera.transform.position = new Vector3(layout.CameraPosition.x, layout.CameraPosition.y, -10);
                backdrop.Show(world, camera, layout);
                board.SetPalette(world.TrayTint, world.TileTint, world.PebbleTint);
                board.Build(level, level.CreateInitialState(), ShapeAtlas.ForDensity(layout.PixelsPerUnit));
                for (int i = 0; i < 10; i++) yield return null;

                Debug.Log($"[PLOP] Unity render sample at {width}x{height}, Level 100, board and backdrop: " +
                    $"draw calls {Measurement(drawCalls)}, batches {Measurement(batches)}, " +
                    $"{root.GetComponentsInChildren<SpriteRenderer>().Count(sprite => sprite.enabled)} active sprites.");
            }
            finally
            {
                drawCalls.Dispose();
                batches.Dispose();
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
            }
        }

        private static string Measurement(ProfilerRecorder recorder) =>
            recorder.Valid && recorder.Count > 0 && recorder.LastValue > 0 ? recorder.LastValue.ToString() : "unavailable";
    }
}
