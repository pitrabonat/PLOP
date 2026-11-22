using System;
using System.Collections.Generic;
using Plop.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Plop.Presentation
{
    /// <summary>
    /// Event-driven input (Input System callbacks, no Update polling):
    ///  * swipe with touch or mouse drag (press -> release, dominant axis),
    ///  * arrow keys / WASD for moves, Z/Backspace undo, R restart, H hint, N/Enter next level, Esc close a menu.
    /// Taps and gestures that start over interactive UI never move the board.
    /// </summary>
    public sealed class SwipeInput : MonoBehaviour
    {
        private const float SwipeDistanceDp = 16f;
        private const float AxisDominance = 1.25f;

        public event Action<Direction> DirectionRequested;
        public event Action UndoRequested;
        public event Action RestartRequested;
        public event Action NextRequested;
        public event Action HintRequested;
        public event Action DismissRequested;

        private InputAction _press;
        private InputAction[] _actions;
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private PointerEventData _pointerEvent;
        private EventSystem _eventSystem;
        private Vector2 _pressStart;
        private ButtonControl _activePress;
        private bool _pressStartedOnBoard;

        private void Awake()
        {
            _press = new InputAction("Swipe press", InputActionType.PassThrough);
            _press.AddBinding("<Touchscreen>/touch*/press");
            _press.AddBinding("<Mouse>/leftButton");
            _press.performed += OnPressChanged;
            _actions = new[]
            {
                _press,
                Key("Up", () => DirectionRequested?.Invoke(Direction.Up), "<Keyboard>/upArrow", "<Keyboard>/w"),
                Key("Down", () => DirectionRequested?.Invoke(Direction.Down), "<Keyboard>/downArrow", "<Keyboard>/s"),
                Key("Left", () => DirectionRequested?.Invoke(Direction.Left), "<Keyboard>/leftArrow", "<Keyboard>/a"),
                Key("Right", () => DirectionRequested?.Invoke(Direction.Right), "<Keyboard>/rightArrow", "<Keyboard>/d"),
                Key("Undo", () => UndoRequested?.Invoke(), "<Keyboard>/z", "<Keyboard>/backspace"),
                Key("Restart", () => RestartRequested?.Invoke(), "<Keyboard>/r"),
                Key("Hint", () => HintRequested?.Invoke(), "<Keyboard>/h"),
                Key("Next", () => NextRequested?.Invoke(), "<Keyboard>/n", "<Keyboard>/enter"),
                Key("Dismiss", () => DismissRequested?.Invoke(), "<Keyboard>/escape")
            };
        }

        private static InputAction Key(string name, Action action, params string[] bindings)
        {
            var input = new InputAction(name, InputActionType.Button);
            foreach (var binding in bindings) input.AddBinding(binding);
            input.performed += _ => action();
            return input;
        }

        private void OnEnable()
        {
            foreach (var action in _actions) action.Enable();
        }

        private void OnDisable()
        {
            CancelGesture();
            foreach (var action in _actions) action.Disable();
        }

        private void OnDestroy()
        {
            foreach (var action in _actions) action.Dispose();
        }

        internal void CancelGesture() => _activePress = null;

        private void OnPressChanged(InputAction.CallbackContext context)
        {
            var press = (ButtonControl)context.control;
            var touch = press.parent as TouchControl;

            if (press.isPressed)
            {
                if (_activePress != null || (touch != null && touch.phase.ReadValue() != TouchPhase.Began))
                    return;
                _pressStart = Position(press, touch);
                _pressStartedOnBoard = !PressHitsUI(_pressStart);
                _activePress = press;
                return;
            }

            if (_activePress != press) return;
            CancelGesture();
            if (!_pressStartedOnBoard || (touch != null && touch.phase.ReadValue() != TouchPhase.Ended))
                return;

            Vector2 delta = Position(press, touch) - _pressStart;
            var direction = SwipeToDirection(delta, SwipeThreshold(Screen.width, Screen.height, Screen.dpi));
            if (direction.HasValue) DirectionRequested?.Invoke(direction.Value);
        }

        private static Vector2 Position(ButtonControl press, TouchControl touch)
            => touch != null ? touch.position.ReadValue() : ((Mouse)press.device).position.ReadValue();

        internal static float SwipeThreshold(int width, int height, float dpi)
        {
            float shortSide = Mathf.Min(width, height);
            float distance = dpi >= 72f && dpi <= 1000f ? SwipeDistanceDp * dpi / 160f : shortSide * 0.04f;
            return Mathf.Clamp(distance, 16f, Mathf.Max(16f, shortSide * 0.08f));
        }

        private bool PressHitsUI(Vector2 position)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            if (_eventSystem != eventSystem)
            {
                _eventSystem = eventSystem;
                _pointerEvent = new PointerEventData(eventSystem);
            }

            // The UI input module processes events later in the frame. Raycast the actual press position.
            _pointerEvent.Reset();
            _pointerEvent.position = position;
            eventSystem.RaycastAll(_pointerEvent, _uiHits);
            bool hasHits = _uiHits.Count > 0;
            _uiHits.Clear();
            return hasHits;
        }

        /// <summary>Maps a screen-space drag to a direction, or null for taps. Screen Y grows upwards like grid Y.</summary>
        public static Direction? SwipeToDirection(Vector2 delta, float minDistance)
        {
            float horizontal = Mathf.Abs(delta.x);
            float vertical = Mathf.Abs(delta.y);
            float major = Mathf.Max(horizontal, vertical);
            if (major <= 0f || major < minDistance || major < Mathf.Min(horizontal, vertical) * AxisDominance)
                return null;
            if (horizontal > vertical) return delta.x > 0f ? Direction.Right : Direction.Left;
            return delta.y > 0f ? Direction.Up : Direction.Down;
        }
    }
}
