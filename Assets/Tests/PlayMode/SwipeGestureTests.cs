using System.Collections.Generic;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Plop.Tests.PlayMode
{
    public sealed class SwipeGestureTests
    {
        private InputSettings.BackgroundBehavior _previousBackground;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorInput;
#endif

        [SetUp]
        public void RouteSyntheticEventsToTheGame()
        {
            _previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            _previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        }

        [TearDown]
        public void RestoreInputSettings()
        {
            InputSystem.settings.backgroundBehavior = _previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorInput;
#endif
        }

        [Test]
        public void ThresholdFitsDensityAndRejectsAmbiguousDiagonals()
        {
            float portrait = SwipeInput.SwipeThreshold(1080, 1920, 420f);
            float compact = SwipeInput.SwipeThreshold(720, 1280, 320f);
            Assert.AreEqual(42f, portrait);
            Assert.AreEqual(32f, compact);
            Assert.AreEqual(43.2f, SwipeInput.SwipeThreshold(1080, 1920, 0f), 0.01f);
            Assert.IsNull(SwipeInput.SwipeToDirection(new Vector2(180, 175), portrait));
            Assert.IsNull(SwipeInput.SwipeToDirection(new Vector2(30, 0), compact));
            Assert.AreEqual(Direction.Right, SwipeInput.SwipeToDirection(new Vector2(48, 0), portrait));
            Assert.AreEqual(Direction.Down, SwipeInput.SwipeToDirection(new Vector2(20, -60), compact));
        }

        [Test]
        public void OnlyTheFirstContactOwnsTheGestureAndCancellationDiscardsIt()
        {
            var root = new GameObject("Swipe input test");
            var screen = InputSystem.AddDevice<Touchscreen>();
            var directions = new List<Direction>();
            try
            {
                var input = root.AddComponent<SwipeInput>();
                input.DirectionRequested += directions.Add;

                Touch(screen, 1, TouchPhase.Began, 100, 100);
                Touch(screen, 2, TouchPhase.Began, 200, 100);
                Touch(screen, 2, TouchPhase.Ended, 350, 100);
                Assert.IsEmpty(directions, "A second contact cannot commit the first contact's gesture.");
                Touch(screen, 1, TouchPhase.Ended, 100, 300);
                CollectionAssert.AreEqual(new[] { Direction.Up }, directions);

                Touch(screen, 3, TouchPhase.Began, 100, 100);
                Touch(screen, 3, TouchPhase.Canceled, 280, 100);
                CollectionAssert.AreEqual(new[] { Direction.Up }, directions);
                Touch(screen, 4, TouchPhase.Began, 100, 100);
                input.CancelGesture();
                Touch(screen, 4, TouchPhase.Ended, 300, 100);
                CollectionAssert.AreEqual(new[] { Direction.Up }, directions);

                Touch(screen, 5, TouchPhase.Began, 300, 100);
                Touch(screen, 5, TouchPhase.Ended, 100, 100);
                CollectionAssert.AreEqual(new[] { Direction.Up, Direction.Left }, directions);
            }
            finally
            {
                Object.DestroyImmediate(root);
                InputSystem.RemoveDevice(screen);
            }
        }

        [Test]
        public void MouseDragStillDispatchesExactlyOnce()
        {
            var root = new GameObject("Mouse input test");
            var mouse = InputSystem.AddDevice<Mouse>();
            var directions = new List<Direction>();
            try
            {
                root.AddComponent<SwipeInput>().DirectionRequested += directions.Add;
                SendMouse(mouse, 100f, true);
                SendMouse(mouse, 200f, true);
                Assert.IsEmpty(directions);
                SendMouse(mouse, 400f, false);
                SendMouse(mouse, 600f, false);
                CollectionAssert.AreEqual(new[] { Direction.Right }, directions);
            }
            finally
            {
                Object.DestroyImmediate(root);
                InputSystem.RemoveDevice(mouse);
            }
        }

        private static void Touch(Touchscreen screen, int id, TouchPhase phase, float x, float y)
        {
            InputSystem.QueueStateEvent(screen, new TouchState
            {
                touchId = id,
                phase = phase,
                position = new Vector2(x, y)
            });
            InputSystem.Update();
        }

        private static void SendMouse(Mouse mouse, float x, bool down)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                buttons = down ? (ushort)1 : (ushort)0,
                position = new Vector2(x, 100f)
            });
            InputSystem.Update();
        }
    }
}
