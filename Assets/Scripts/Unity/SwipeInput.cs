using System;
using System.Collections.Generic;
using Plop.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Plop.Presentation
{
    /// <summary>
    /// Event-driven input (Input System callbacks, no Update polling):
    ///  * swipe with touch or mouse drag (press -> release, dominant axis),
    ///  * arrow keys / WASD for moves, Z/Backspace undo, R restart, H hint, N/Enter next level.
    /// Taps and gestures that start over interactive UI never move the board.
    /// </summary>
    public sealed class SwipeInput : MonoBehaviour
    {
        [Tooltip("Minimum swipe length as a fraction of the shorter screen side.")]
        [SerializeField] private float minSwipeFraction = 0.06f;

        public event Action<Direction> DirectionRequested;
        public event Action UndoRequested;
        public event Action RestartRequested;
        public event Action NextRequested;
        public event Action HintRequested;

        private InputAction _press;
        private InputAction[] _actions;
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private PointerEventData _pointerEvent;
        private EventSystem _eventSystem;
        private Vector2 _pressStart;
        private Pointer _activePointer;

        private void Awake()
        {
            _press = new InputAction("Press", InputActionType.Button, "<Pointer>/press");
            _press.started += OnPressStarted;
            _press.canceled += OnPressReleased;
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
                Key("Next", () => NextRequested?.Invoke(), "<Keyboard>/n", "<Keyboard>/enter")
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
            _activePointer = null;
            foreach (var action in _actions) action.Disable();
        }

        private void OnDestroy()
        {
            foreach (var action in _actions) action.Dispose();
        }

        private void OnPressStarted(InputAction.CallbackContext ctx)
        {
            var pointer = ctx.control.device as Pointer;
            if (pointer == null) return;
            _pressStart = pointer.position.ReadValue();
            if (!PressHitsUI(_pressStart)) _activePointer = pointer;
        }

        private void OnPressReleased(InputAction.CallbackContext ctx)
        {
            var pointer = _activePointer;
            _activePointer = null;
            if (pointer == null || pointer != ctx.control.device) return;
            Vector2 delta = pointer.position.ReadValue() - _pressStart;
            float min = Mathf.Min(Screen.width, Screen.height) * minSwipeFraction;
            var direction = SwipeToDirection(delta, min);
            if (direction.HasValue) DirectionRequested?.Invoke(direction.Value);
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
            if (delta.magnitude < minDistance) return null;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) return delta.x > 0 ? Direction.Right : Direction.Left;
            return delta.y > 0 ? Direction.Up : Direction.Down;
        }
    }
}
