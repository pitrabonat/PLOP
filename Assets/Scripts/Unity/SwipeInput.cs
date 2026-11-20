using System;
using Plop.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Plop.Presentation
{
    /// <summary>
    /// Event-driven input (Input System callbacks, no Update polling):
    ///  * swipe with touch or mouse drag (press -> release, dominant axis),
    ///  * arrow keys / WASD for moves, Z/Backspace undo, R restart, N/Enter next level.
    /// Taps (tiny drags) never move, so UI buttons can be pressed safely.
    /// </summary>
    public sealed class SwipeInput : MonoBehaviour
    {
        [Tooltip("Minimum swipe length as a fraction of the shorter screen side.")]
        [SerializeField] private float minSwipeFraction = 0.06f;

        public event Action<Direction> DirectionRequested;
        public event Action UndoRequested;
        public event Action RestartRequested;
        public event Action NextRequested;

        private InputAction _press, _up, _down, _left, _right, _undo, _restart, _next;
        private Vector2 _pressStart;
        private bool _pressing;

        private void Awake()
        {
            _press = new InputAction("Press", InputActionType.Button, "<Pointer>/press");
            _up = Key("Up", "<Keyboard>/upArrow", "<Keyboard>/w");
            _down = Key("Down", "<Keyboard>/downArrow", "<Keyboard>/s");
            _left = Key("Left", "<Keyboard>/leftArrow", "<Keyboard>/a");
            _right = Key("Right", "<Keyboard>/rightArrow", "<Keyboard>/d");
            _undo = Key("Undo", "<Keyboard>/z", "<Keyboard>/backspace");
            _restart = Key("Restart", "<Keyboard>/r");
            _next = Key("Next", "<Keyboard>/n", "<Keyboard>/enter");

            _press.started += OnPressStarted;
            _press.canceled += OnPressReleased;
            _up.performed += _ => DirectionRequested?.Invoke(Direction.Up);
            _down.performed += _ => DirectionRequested?.Invoke(Direction.Down);
            _left.performed += _ => DirectionRequested?.Invoke(Direction.Left);
            _right.performed += _ => DirectionRequested?.Invoke(Direction.Right);
            _undo.performed += _ => UndoRequested?.Invoke();
            _restart.performed += _ => RestartRequested?.Invoke();
            _next.performed += _ => NextRequested?.Invoke();
        }

        private static InputAction Key(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            return a;
        }

        private void OnEnable() { foreach (var a in All()) a.Enable(); }
        private void OnDisable() { foreach (var a in All()) a.Disable(); }

        private void OnDestroy()
        {
            foreach (var a in All()) a.Dispose();
        }

        private InputAction[] All() => new[] { _press, _up, _down, _left, _right, _undo, _restart, _next };

        private void OnPressStarted(InputAction.CallbackContext ctx)
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            _pressStart = pointer.position.ReadValue();
            _pressing = true;
        }

        private void OnPressReleased(InputAction.CallbackContext ctx)
        {
            if (!_pressing) return;
            _pressing = false;
            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 delta = pointer.position.ReadValue() - _pressStart;
            float min = Mathf.Min(Screen.width, Screen.height) * minSwipeFraction;
            var dir = SwipeToDirection(delta, min);
            if (dir.HasValue) DirectionRequested?.Invoke(dir.Value);
        }

        /// <summary>Maps a screen-space drag to a direction, or null for taps. Screen Y grows upwards like grid Y.</summary>
        public static Direction? SwipeToDirection(Vector2 delta, float minDistance)
        {
            if (delta.magnitude < minDistance) return null;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) return delta.x > 0 ? Direction.Right : Direction.Left;
            return delta.y > 0 ? Direction.Up : Direction.Down;
        }
    }
}
