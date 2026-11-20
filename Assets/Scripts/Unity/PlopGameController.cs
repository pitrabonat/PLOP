using System.Collections;
using System.Collections.Generic;
using Plop.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plop.Presentation
{
    /// <summary>
    /// Prototype gameplay controller: owns the <see cref="GameEngine"/>, routes input to it, and forwards
    /// engine events to the views. Contains no game rules. Everything is event-driven; the only per-frame
    /// work is the short move interpolation coroutine and the idle render throttle check.
    /// </summary>
    public sealed class PlopGameController : MonoBehaviour
    {
        public enum FrameRateMode { Fps60 = 60, Fps30 = 30 }

        [Header("Performance")]
        [SerializeField] private FrameRateMode targetFrameRate = FrameRateMode.Fps60;
        [Tooltip("When idle, render only every Nth frame (input still runs every frame). 1 = off.")]
        [SerializeField, Range(1, 6)] private int idleRenderInterval = 3;
        [SerializeField] private float idleDelay = 0.75f;

        [Header("Levels")]
        [SerializeField] private string levelPackResource = LevelLibrary.DefaultPack;
        [SerializeField] private bool rememberProgress = true;

        private const string ProgressKey = "plop.levelIndex";
        private readonly GameEngine _engine = new GameEngine();
        private List<LevelDefinition> _levels = new List<LevelDefinition>();
        private int _index;
        private BoardView _board;
        private SwipeInput _input;
        private GameHud _hud;
        private Camera _camera;
        private float _lastActivity;

        /// <summary>When false, level progress is not read from or written to PlayerPrefs (tests, tools).</summary>
        public bool RememberProgress { get => rememberProgress; set => rememberProgress = value; }
        public GameEngine Engine => _engine;
        public BoardView Board => _board;
        public int LevelIndex => _index;
        public int LevelCount => _levels.Count;

        private void Awake()
        {
            ApplyFrameRate();
            _camera = Camera.main;
            _board = new GameObject("Board").AddComponent<BoardView>();
            _board.transform.SetParent(transform, false);
            _input = gameObject.AddComponent<SwipeInput>();
            _hud = gameObject.AddComponent<GameHud>();

            _engine.EventRaised += OnEngineEvent;
            _input.DirectionRequested += Move;
            _input.UndoRequested += Undo;
            _input.RestartRequested += Restart;
            _input.NextRequested += NextLevel;
            _hud.UndoClicked += Undo;
            _hud.RestartClicked += Restart;
            _hud.NextClicked += NextLevel;
            _hud.LayoutChanged += Relayout;
        }

        private void Start()
        {
            if (_levels.Count > 0) return; // already given levels (tests)
            _levels = LevelLibrary.Load(levelPackResource);
            if (_levels.Count == 0) return;
            _index = rememberProgress ? Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey, 0), 0, _levels.Count - 1) : 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Dev-only screenshot/demo hook: PlayerPrefs "plop.debug.level" (1-based) and "plop.debug.moves" ("RRUD").
            int debugLevel = PlayerPrefs.GetInt("plop.debug.level", 0);
            if (debugLevel > 0) _index = Mathf.Clamp(debugLevel - 1, 0, _levels.Count - 1);
#endif
            LoadCurrent();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string debugMoves = PlayerPrefs.GetString("plop.debug.moves", "");
            if (!string.IsNullOrEmpty(debugMoves)) StartCoroutine(PlayDebugMoves(debugMoves));
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private IEnumerator PlayDebugMoves(string moves)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (char c in moves)
            {
                if (DirectionUtil.TryParse(c, out var d)) Move(d);
                yield return new WaitForSecondsRealtime(0.45f);
            }
        }
#endif

        private void OnDestroy()
        {
            _engine.EventRaised -= OnEngineEvent;
            OnDemandRendering.renderFrameInterval = 1;
        }

        public void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = 0; // targetFrameRate is ignored on mobile when vSync is on
            Application.targetFrameRate = (int)targetFrameRate;
        }

        public void SetFrameRateMode(FrameRateMode mode)
        {
            targetFrameRate = mode;
            ApplyFrameRate();
        }

        /// <summary>Replaces the level list (used by tests and tools) and loads the first level.</summary>
        public void UseLevels(List<LevelDefinition> levels, int startIndex = 0)
        {
            _levels = levels ?? new List<LevelDefinition>();
            _index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, _levels.Count - 1));
            if (_levels.Count > 0) LoadCurrent();
        }

        public void Move(Direction direction)
        {
            if (!_engine.HasLevel) return;
            MarkActive();
            var result = _engine.TryMove(direction);
            if (result.Status != MoveStatus.Ignored) _board.Play(result.Events);
        }

        public void Undo() { MarkActive(); _engine.Undo(); }
        public void Restart() { MarkActive(); _engine.Restart(); }

        public void NextLevel()
        {
            if (_levels.Count == 0) return;
            _index = (_index + 1) % _levels.Count;
            if (rememberProgress) { PlayerPrefs.SetInt(ProgressKey, _index); PlayerPrefs.Save(); }
            LoadCurrent();
        }

        private void LoadCurrent()
        {
            MarkActive();
            _engine.LoadLevel(_levels[_index]);
            PlopLog.Info("Loaded " + _levels[_index].Id);
        }

        private void OnEngineEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.LevelLoaded:
                    _board.Build(_engine.Level, _engine.CurrentState, ShapeAtlas.ForDensity(FitCamera()));
                    _hud.SetLevel(_engine.Level.Name);
                    _hud.SetMoves(0);
                    _hud.SetCanUndo(false);
                    _hud.SetCompleted(false, 0);
                    break;
                case GameEventType.MoveUndone:
                case GameEventType.LevelRestarted:
                    _board.Sync(_engine.CurrentState);
                    _hud.SetMoves(_engine.MoveCount);
                    _hud.SetCanUndo(_engine.CanUndo);
                    _hud.SetCompleted(false, 0);
                    break;
                case GameEventType.PlopMoved:
                    _hud.SetMoves(e.MoveCount);
                    _hud.SetCanUndo(true);
                    break;
                case GameEventType.LevelCompleted:
                    _hud.SetCompleted(true, e.MoveCount);
                    PlopLog.Info("Completed " + _engine.Level.Id + " in " + e.MoveCount);
                    break;
            }
        }

        /// <summary>Fits the camera to the board inside the safe area (see BoardLayout). Returns pixels per cell.</summary>
        private float FitCamera()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || !_engine.HasLevel) return 128f;
            var layout = BoardLayout.Compute(Screen.width, Screen.height, Screen.safeArea, _engine.Level.Width, _engine.Level.Height);
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = PlopPalette.Milk;
            _camera.orthographicSize = layout.OrthographicSize;
            _camera.transform.position = new Vector3(layout.CameraPosition.x, layout.CameraPosition.y, -10f);
            return layout.PixelsPerUnit;
        }

        /// <summary>Screen or safe area changed: refit; rebuild sprites only if the density bucket changed.</summary>
        private void Relayout()
        {
            if (!_engine.HasLevel) return;
            float ppu = FitCamera();
            if (ShapeAtlas.Bucket(ppu) != _board.PixelsPerUnitBucket)
                _board.Build(_engine.Level, _engine.CurrentState, ShapeAtlas.ForDensity(ppu));
            MarkActive();
        }

        private void MarkActive()
        {
            _lastActivity = Time.unscaledTime;
            OnDemandRendering.renderFrameInterval = 1;
        }

        private void Update()
        {
            // Battery: drop the render rate while nothing is moving (input/UI still processed every frame).
            if (idleRenderInterval > 1 && OnDemandRendering.renderFrameInterval == 1 &&
                !_board.IsBusy && Time.unscaledTime - _lastActivity > idleDelay)
            {
                OnDemandRendering.renderFrameInterval = idleRenderInterval;
            }
        }
    }
}
