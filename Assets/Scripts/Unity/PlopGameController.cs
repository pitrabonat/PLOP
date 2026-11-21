using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Plop.Core;
using Plop.Solver;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plop.Presentation
{
    /// <summary>
    /// Gameplay controller: owns the <see cref="GameEngine"/>, routes input to it, and forwards
    /// engine events to the views. Saves campaign progress and runs bounded hints on request.
    /// The controller's per-frame work is limited to the idle render throttle check.
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

        private const string FrameRateKey = "plop.frameRate";
        private const int HintMaxStates = 300_000;
        private const int HintMaxMilliseconds = 200;
        private readonly GameEngine _engine = new GameEngine();
        private List<LevelDefinition> _levels = new List<LevelDefinition>();
        private int _index;
        private LevelProgress _progress;
        private bool _debugPreview;
        private CancellationTokenSource _hintWork;
        private bool _hintVisible;
        private BoardView _board;
        private SwipeInput _input;
        private GameHud _hud;
        private PlopAudio _audio;
        private Camera _camera;
        private float _lastActivity;

        /// <summary>When false, level progress is not read from or written to PlayerPrefs (tests, tools).</summary>
        public bool RememberProgress { get => rememberProgress; set => rememberProgress = value; }
        public GameEngine Engine => _engine;
        public BoardView Board => _board;
        public int LevelIndex => _index;
        public int LevelCount => _levels.Count;
        public FrameRateMode CurrentFrameRateMode => targetFrameRate;
        private bool SavesProgress => rememberProgress && !_debugPreview;

        public bool IsLevelUnlocked(int index) => _progress?.IsUnlocked(index) == true;
        public bool IsLevelCompleted(int index) => _progress?.IsCompleted(index) == true;

        private void Awake()
        {
            ApplyFrameRate();
            _camera = Camera.main;
            _board = new GameObject("Board").AddComponent<BoardView>();
            _board.transform.SetParent(transform, false);
            _input = gameObject.AddComponent<SwipeInput>();
            _audio = gameObject.AddComponent<PlopAudio>();
            _hud = gameObject.AddComponent<GameHud>();

            _engine.EventRaised += OnEngineEvent;
            _input.DirectionRequested += Move;
            _input.UndoRequested += Undo;
            _input.RestartRequested += Restart;
            _input.NextRequested += NextLevel;
            _input.HintRequested += OnHintRequested;
            _input.DismissRequested += DismissModal;
            _hud.UndoClicked += Undo;
            _hud.RestartClicked += Restart;
            _hud.NextClicked += NextLevel;
            _hud.HintClicked += OnHintRequested;
            _hud.SettingsClicked += ShowSettings;
            _hud.SettingsClosed += _audio.SavePreferences;
            _hud.ButtonActivated += _audio.PlayButton;
            _hud.MusicChanged += _audio.SetMusicVolume;
            _hud.SoundChanged += _audio.SetSoundVolume;
            _hud.FrameRateChanged += SetFrameRateMode;
            _hud.LevelsClicked += ShowLevelPicker;
            _hud.LevelChosen += index => TrySelectLevel(index);
            _hud.ModalInteracted += MarkActive;
            _hud.SetFrameRate(targetFrameRate);
            _hud.LayoutChanged += Relayout;
        }

        private void Start()
        {
            if (_progress != null) return; // catalog supplied by tests or tools
            _levels = LevelLibrary.Load(levelPackResource);
            if (_levels.Count == 0) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Screenshot/demo settings must never grant durable progress.
            int debugLevel = rememberProgress ? PlayerPrefs.GetInt("plop.debug.level", 0) : 0;
            string debugMoves = rememberProgress ? PlayerPrefs.GetString("plop.debug.moves", "") : "";
            _debugPreview = debugLevel > 0 || !string.IsNullOrEmpty(debugMoves);
#endif
            _progress = new LevelProgress(_levels, SavesProgress ? levelPackResource : null);
            _audio.Initialize(SavesProgress);
            _index = _progress.ResumeIndex();
            if (rememberProgress)
            {
                int savedRate = PlayerPrefs.GetInt(FrameRateKey, (int)targetFrameRate);
                if (savedRate == 30 || savedRate == 60)
                {
                    targetFrameRate = (FrameRateMode)savedRate;
                    ApplyFrameRate();
                    _hud.SetFrameRate(targetFrameRate);
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugLevel > 0) _index = Mathf.Clamp(debugLevel - 1, 0, _levels.Count - 1);
#endif
            LoadCurrent();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

        private void OnDisable()
        {
            InvalidateHint();
            OnDemandRendering.renderFrameInterval = 1;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) InvalidateHint();
        }

        private void OnDestroy()
        {
            _engine.EventRaised -= OnEngineEvent;
            OnDemandRendering.renderFrameInterval = 1;
        }

        public void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = 0; // Use the same cap in the Editor and on mobile.
            Application.targetFrameRate = (int)targetFrameRate;
        }

        public void SetFrameRateMode(FrameRateMode mode)
        {
            if (mode != FrameRateMode.Fps30 && mode != FrameRateMode.Fps60) return;
            targetFrameRate = mode;
            ApplyFrameRate();
            _hud.SetFrameRate(mode);
            if (SavesProgress)
            {
                PlayerPrefs.SetInt(FrameRateKey, (int)mode);
                PlayerPrefs.Save();
            }
            MarkActive();
        }

        /// <summary>Replaces the level list (used by tests and tools) and loads the first level.</summary>
        public void UseLevels(List<LevelDefinition> levels, int startIndex = 0)
        {
            _levels = levels == null ? new List<LevelDefinition>() : new List<LevelDefinition>(levels);
            _progress = new LevelProgress(_levels);
            _index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, _levels.Count - 1));
            if (_levels.Count > 0) LoadCurrent();
        }

        public void Move(Direction direction)
        {
            if (!_engine.HasLevel || _hud.BlocksGameplayInput) return;
            MarkActive();
            var result = _engine.TryMove(direction);
            if (result.Status == MoveStatus.Ignored) return;
            _board.Play(result.Events);
            _audio.PlayMove(result.Events);
        }

        public void Undo()
        {
            if (_hud.BlocksGameplayInput) return;
            MarkActive();
            _engine.Undo();
        }

        public void Restart()
        {
            if (_hud.BlocksGameplayInput) return;
            MarkActive();
            _engine.Restart();
        }

        public void NextLevel()
        {
            if (_hud.BlocksGameplayInput || !_engine.IsCompleted) return;
            TrySelectLevel(_index + 1);
        }

        public bool TrySelectLevel(int index)
        {
            if (_hud.IsSettingsOpen || !IsLevelUnlocked(index)) return false;
            _hud.CloseLevelPicker();
            if (index == _index && !_engine.IsCompleted) return true;
            _index = index;
            _progress.Visit(index, SavesProgress);
            LoadCurrent();
            return true;
        }

        private void ShowLevelPicker()
        {
            if (_levels.Count == 0 || _hud.BlocksGameplayInput) return;
            InvalidateHint();
            _hud.ShowLevels(_levels, IsLevelUnlocked, IsLevelCompleted, _index);
        }

        private void ShowSettings()
        {
            if (_hud.BlocksGameplayInput) return;
            InvalidateHint();
            _hud.SetVolumes(_audio.MusicVolume, _audio.SoundVolume);
            _hud.ShowSettings();
        }

        private void DismissModal()
        {
            _hud.CloseSettings();
            _hud.CloseLevelPicker();
        }

        private void OnHintRequested() => _ = RequestHintAsync();

        /// <summary>Finds and verifies a direction for the current board without applying it.</summary>
        public async Task<Direction?> RequestHintAsync()
        {
            if (!isActiveAndEnabled || !_engine.HasLevel || _engine.IsCompleted ||
                _hud.BlocksGameplayInput || _hintWork != null)
                return null;

            var level = _engine.Level;
            var state = _engine.GetState();
            var work = new CancellationTokenSource();
            _hintWork = work;
            ShowHint("Looking for a hint…", true);
            MarkActive();
            try
            {
                var token = work.Token;
                var result = await Task.Run(() => FindHint(level, state, token));
                if (work.IsCancellationRequested || this == null || !isActiveAndEnabled ||
                    _engine.Level != level || !_engine.CurrentState.Equals(state))
                    return null;

                MarkActive();
                if (result.Status == SolverStatus.Solved && result.Moves.Length > 0)
                {
                    Direction direction = result.Moves[0];
                    ShowHint("Try swiping " + direction.ToString().ToLowerInvariant() + ".");
                    return direction;
                }

                ShowHint(result.Status == SolverStatus.ProvenUnsolvable
                    ? "No route from here. Try Undo."
                    : "No hint available. Try Undo.");
                return null;
            }
            catch (System.Exception exception)
            {
                if (!work.IsCancellationRequested && this != null && isActiveAndEnabled)
                {
                    Debug.LogWarning("[PLOP] Hint failed: " + exception.GetType().Name);
                    ShowHint("No hint available. Try Undo.");
                }
                return null;
            }
            finally
            {
                _hintWork = null;
                work.Dispose();
            }
        }

        private static SolverResult FindHint(LevelDefinition level, GameState state, CancellationToken cancellation)
        {
            var result = BfsSolver.Solve(level, state, new SolverOptions
            {
                MaxStates = HintMaxStates,
                MaxMilliseconds = HintMaxMilliseconds,
                Cancellation = cancellation
            });
            if (!cancellation.IsCancellationRequested && result.Status == SolverStatus.Solved &&
                !BfsSolver.VerifySolution(level, result.Moves, state))
                return new SolverResult { Status = SolverStatus.InvalidLevel };
            return result;
        }

        private void ShowHint(string hint, bool searching = false)
        {
            _hintVisible = hint != null;
            _hud.SetHint(hint, searching);
        }

        private void InvalidateHint()
        {
            _hintWork?.Cancel();
            if (_hintVisible) ShowHint(null);
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
                    InvalidateHint();
                    _hud.SetLevel(_engine.Level.Name, _engine.Level.Instruction);
                    _hud.SetMoves(0);
                    _hud.SetCanUndo(false);
                    _hud.SetCompleted(false, false);
                    break;
                case GameEventType.MoveUndone:
                case GameEventType.LevelRestarted:
                    InvalidateHint();
                    _board.Sync(_engine.CurrentState);
                    _hud.SetMoves(_engine.MoveCount);
                    _hud.SetCanUndo(_engine.CanUndo);
                    _hud.SetCompleted(false, false);
                    break;
                case GameEventType.PlopMoved:
                    InvalidateHint();
                    _hud.SetMoves(e.MoveCount);
                    _hud.SetCanUndo(true);
                    break;
                case GameEventType.LevelCompleted:
                    _progress.Complete(_index, SavesProgress);
                    _hud.SetCompleted(true, IsLevelUnlocked(_index + 1));
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
