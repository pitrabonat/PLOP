using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Plop.Core;
using Plop.Solver;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PLOP.Tests.PlayMode")]

namespace Plop.Presentation
{
    /// <summary>
    /// Gameplay controller: owns the <see cref="GameEngine"/>, routes input to it, and forwards
    /// engine events to the views. Saves campaign progress and runs bounded hints on request.
    /// Input can queue one command while the board finishes its current slide.
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
        private const string HapticsKey = "plop.haptics";
        private const string ReducedMotionKey = "plop.reducedMotion";
        private const int HintMaxStates = 300_000;
        private const int HintMaxMilliseconds = 200;
        private const int DeadlockMaxStates = 20_000;
        private const int DeadlockMaxMilliseconds = 80;
        private readonly GameEngine _engine = new GameEngine();
        private List<LevelDefinition> _levels = new List<LevelDefinition>();
        private int _index;
        private LevelProgress _progress;
        private WorldCatalog _worlds = WorldCatalog.Empty;
        private WorldTheme _world;
        private WorldBackdrop _backdrop;
        private readonly PlopHaptics _haptics = new PlopHaptics();
        private bool _reducedMotion;
        private bool _debugPreview;
        private CancellationTokenSource _hintWork;
        private bool _hintVisible;
        private DeadlockCatalog _deadlockCatalog;
        private DeadlockTable _deadlockTable;
        private CancellationTokenSource _deadlockWork;
        private Coroutine _deadlockReveal;
        private int _deadlockRevision;
        private bool _deadlockCheckQueued;
        private bool _deadlockPresented;
        private bool _applicationPaused;
        private bool _applicationFocused = true;
        private bool _suspended;
        private bool _recheckDeadlockOnResume;
        private Direction? _pendingMove;
        private BoardView _board;
        private SwipeInput _input;
        private GameHud _hud;
        private PlopAudio _audio;
        private Camera _camera;
        private float _lastActivity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PlopDiagnostics _diagnostics;
        internal bool IsSuspended => _suspended;
        internal string DiagnosticState => _hud.IsDeadlockOpen ? "Recovery"
            : _hud.IsSettingsOpen ? "Settings" : _hud.IsChoosingLevel ? "Level picker"
            : _engine.IsCompleted ? "Complete" : _board.IsTraveling ? "Traveling" : "Playing";
#endif

        /// <summary>When false, level progress is not read from or written to PlayerPrefs (tests, tools).</summary>
        public bool RememberProgress { get => rememberProgress; set => rememberProgress = value; }
        public GameEngine Engine => _engine;
        public BoardView Board => _board;
        public int LevelIndex => _index;
        public int LevelCount => _levels.Count;
        public FrameRateMode CurrentFrameRateMode => targetFrameRate;
        public WorldTheme CurrentWorld => _world;
        public bool HapticsEnabled => _haptics.Enabled;
        public bool ReducedMotionEnabled => _reducedMotion;
        public int CampaignResumeIndex => _progress?.FrontierIndex() ?? 0;
        internal Func<LevelDefinition, GameState, CancellationToken, Task<DeadlockStatus>> DeadlockFallback { get; set; }
            = SearchForDeadlockAsync;
        internal Direction? PendingMove => _pendingMove;
        private bool SavesProgress => rememberProgress && !_debugPreview;
        private bool GameplayBlocked => !isActiveAndEnabled || _suspended || _hud.BlocksGameplayInput;

        public bool IsLevelUnlocked(int index) => _progress?.IsUnlocked(index) == true;
        public bool IsLevelCompleted(int index) => _progress?.IsCompleted(index) == true;

        private void Awake()
        {
            ApplyFrameRate();
            _camera = Camera.main;
            _board = new GameObject("Board").AddComponent<BoardView>();
            _board.transform.SetParent(transform, false);
            _backdrop = gameObject.AddComponent<WorldBackdrop>();
            _input = gameObject.AddComponent<SwipeInput>();
            _audio = gameObject.AddComponent<PlopAudio>();
            _hud = gameObject.AddComponent<GameHud>();

            _engine.EventRaised += OnEngineEvent;
            _input.DirectionRequested += RequestMove;
            _input.UndoRequested += Undo;
            _input.RestartRequested += Restart;
            _input.NextRequested += NextLevel;
            _input.HintRequested += OnHintRequested;
            _input.DismissRequested += DismissModal;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _input.DiagnosticsRequested += ToggleDiagnostics;
#endif
            _hud.UndoClicked += Undo;
            _hud.RestartClicked += Restart;
            _hud.NextClicked += NextLevel;
            _hud.HintClicked += OnHintRequested;
            _hud.SettingsClicked += ShowSettings;
            _hud.SettingsClosed += _audio.SavePreferences;
            _hud.ButtonActivated += _audio.PlayButton;
            _hud.MusicChanged += _audio.SetMusicVolume;
            _hud.SoundChanged += _audio.SetSoundVolume;
            _hud.MasterMuteChanged += _audio.SetMasterMuted;
            _hud.FrameRateChanged += SetFrameRateMode;
            _hud.HapticsChanged += SetHaptics;
            _hud.ReducedMotionChanged += SetReducedMotion;
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
            if (levelPackResource == LevelLibrary.DefaultPack)
            {
                _deadlockCatalog = LevelLibrary.LoadDeadlocks();
                _worlds = WorldCatalog.Load();
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Screenshot/demo settings must never grant durable progress.
            int debugLevel = rememberProgress ? PlayerPrefs.GetInt("plop.debug.level", 0) : 0;
            string debugMoves = rememberProgress ? PlayerPrefs.GetString("plop.debug.moves", "") : "";
            _debugPreview = debugLevel > 0 || !string.IsNullOrEmpty(debugMoves);
#endif
            _progress = new LevelProgress(_levels, SavesProgress ? levelPackResource : null);
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
            if (SavesProgress)
            {
                _haptics.Enabled = PlayerPrefs.GetInt(HapticsKey, 0) == 1;
                ApplyReducedMotion(PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1);
            }
            _hud.SetComfortOptions(HapticsEnabled, ReducedMotionEnabled);
            _audio.Initialize(SavesProgress, _worlds.ForLevelIndex(_index)?.Ambience ?? WorldAmbience.CozyNest);
            LoadCurrent();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PlayerPrefs.GetInt(PlopDiagnostics.Preference, 0) == 1) ToggleDiagnostics();
            if (!string.IsNullOrEmpty(debugMoves)) StartCoroutine(PlayDebugMoves(debugMoves));
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ToggleDiagnostics()
        {
            if (_diagnostics != null)
            {
                Destroy(_diagnostics);
                _diagnostics = null;
                return;
            }

            _diagnostics = gameObject.AddComponent<PlopDiagnostics>();
            _diagnostics.Inspect(this);
        }

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
            ClearPendingInput();
            InvalidateHint();
            CancelDeadlockCheck();
            _recheckDeadlockOnResume = false;
            if (_hud != null) _hud.CloseDeadlock();
            OnDemandRendering.renderFrameInterval = 1;
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            ApplyApplicationActivity();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ClearPendingInput();
            if (!Application.isMobilePlatform) return;
            _applicationFocused = focused;
            ApplyApplicationActivity();
        }

        private void ApplyApplicationActivity()
        {
            bool suspended = _applicationPaused || !_applicationFocused;
            if (_suspended == suspended) return;
            _suspended = suspended;
            if (suspended)
            {
                ClearPendingInput();
                InvalidateHint();
                _recheckDeadlockOnResume = _deadlockCheckQueued || _deadlockReveal != null ||
                    _deadlockWork?.IsCancellationRequested == false;
                CancelDeadlockCheck();
                return;
            }

            MarkActive();
            if (!_recheckDeadlockOnResume) return;
            _recheckDeadlockOnResume = false;
            CheckCurrentDeadlock();
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

        public void SetHaptics(bool value)
        {
            if (_haptics.Enabled == value) return;
            _haptics.Enabled = value;
            _hud.SetComfortOptions(HapticsEnabled, ReducedMotionEnabled);
            SaveComfortOption(HapticsKey, value);
            MarkActive();
        }

        public void SetReducedMotion(bool value)
        {
            if (_reducedMotion == value) return;
            ApplyReducedMotion(value);
            _hud.SetComfortOptions(HapticsEnabled, ReducedMotionEnabled);
            SaveComfortOption(ReducedMotionKey, value);
            MarkActive();
        }

        private void ApplyReducedMotion(bool value)
        {
            _reducedMotion = value;
            _board.SetReducedMotion(value);
            _backdrop.SetReducedMotion(value);
        }

        private void SaveComfortOption(string key, bool value)
        {
            if (!SavesProgress) return;
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Replaces the level list (used by tests and tools) and loads the first level.</summary>
        public void UseLevels(List<LevelDefinition> levels, int startIndex = 0)
        {
            ClearPendingInput();
            _levels = levels == null ? new List<LevelDefinition>() : new List<LevelDefinition>(levels);
            _worlds = WorldCatalog.Empty;
            CancelDeadlockCheck();
            _hud.CloseDeadlock();
            _progress = new LevelProgress(_levels);
            _index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, _levels.Count - 1));
            if (_levels.Count > 0) LoadCurrent();
        }

        internal void RequestMove(Direction direction)
        {
            if (GameplayBlocked || !_engine.HasLevel || _engine.IsCompleted) return;
            MarkActive();
            if (_board.IsTraveling || _pendingMove.HasValue)
            {
                _pendingMove ??= direction;
                return;
            }
            Move(direction);
        }

        private void ClearPendingInput()
        {
            _pendingMove = null;
            _input?.CancelGesture();
        }

        public void Move(Direction direction)
        {
            if (!_engine.HasLevel || GameplayBlocked) return;
            MarkActive();
            var result = _engine.TryMove(direction);
            if (result.Status == MoveStatus.Ignored) return;
            _board.Play(result.Events);
            _audio.PlayMove(result.Events);
            _haptics.PlayMove(result.Events);
            if (result.Changed && !result.CompletedLevel) CheckCurrentDeadlock();
        }

        public void Undo()
        {
            if (GameplayBlocked) return;
            ClearPendingInput();
            MarkActive();
            _engine.Undo();
        }

        public void Restart()
        {
            if (GameplayBlocked) return;
            ClearPendingInput();
            MarkActive();
            _engine.Restart();
        }

        public void NextLevel()
        {
            if (GameplayBlocked || !_engine.IsCompleted) return;
            TrySelectLevel(_index + 1);
        }

        public bool TrySelectLevel(int index)
        {
            if (!isActiveAndEnabled || _suspended || _hud.IsSettingsOpen || _hud.IsDeadlockOpen || !IsLevelUnlocked(index)) return false;
            ClearPendingInput();
            _hud.CloseLevelPicker();
            if (index == _index && !_engine.IsCompleted) return true;
            _index = index;
            _progress.Visit(index, SavesProgress);
            LoadCurrent();
            return true;
        }

        private void ShowLevelPicker()
        {
            if (_levels.Count == 0 || GameplayBlocked) return;
            ClearPendingInput();
            InvalidateHint();
            if (_worlds.Worlds.Count > 0 && _world != null)
                _hud.ShowWorlds(_worlds.Worlds, _levels, IsLevelUnlocked, IsLevelCompleted, _index,
                    resumeIndex: CampaignResumeIndex);
            else
                _hud.ShowLevels(_levels, IsLevelUnlocked, IsLevelCompleted, _index);
        }

        private void ShowSettings()
        {
            if (GameplayBlocked) return;
            ClearPendingInput();
            InvalidateHint();
            _hud.SetVolumes(_audio.MusicVolume, _audio.SoundVolume);
            _hud.SetMasterMuted(_audio.MasterMuted);
            _hud.SetComfortOptions(HapticsEnabled, ReducedMotionEnabled);
            _hud.ShowSettings();
        }

        private void DismissModal()
        {
            ClearPendingInput();
            _hud.CloseSettings();
            _hud.CloseLevelPicker();
            _hud.CloseDeadlock();
            _hud.CancelWorldAnnouncement();
        }

        private bool CanCheckDeadlock => isActiveAndEnabled && !_suspended &&
            _engine.HasLevel && !_engine.IsCompleted;

        private void CheckCurrentDeadlock()
        {
            if (!CanCheckDeadlock || _deadlockPresented) return;
            var known = _deadlockTable?.Classify(_engine.CurrentState) ?? DeadlockStatus.Unknown;
            if (known == DeadlockStatus.ProvenDeadlock)
            {
                RevealDeadlock();
            }
            else if (known == DeadlockStatus.Unknown)
            {
                _deadlockCheckQueued = true;
                TryStartDeadlockCheck();
            }
        }

        private void TryStartDeadlockCheck()
        {
            if (!_deadlockCheckQueued || !CanCheckDeadlock || _deadlockWork != null || _hintWork != null)
                return;
            _deadlockCheckQueued = false;
            _ = CheckUnknownDeadlockAsync(_engine.Level, _engine.GetState(), _deadlockRevision);
        }

        private async Task CheckUnknownDeadlockAsync(LevelDefinition level, GameState state, int revision)
        {
            var work = new CancellationTokenSource();
            _deadlockWork = work;
            try
            {
                // A short debounce coalesces gesture bursts and lets an explicit hint take priority.
                await Task.Delay(90, work.Token);
                var verdict = await DeadlockFallback(level, state, work.Token);
                if (!work.IsCancellationRequested && this != null && revision == _deadlockRevision &&
                    CanCheckDeadlock && verdict == DeadlockStatus.ProvenDeadlock)
                    RevealDeadlock();
            }
            catch (OperationCanceledException) when (work.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (!work.IsCancellationRequested && this != null)
                    Debug.LogWarning("[PLOP] Deadlock check unavailable: " + ex.GetType().Name);
            }
            finally
            {
                _deadlockWork = null;
                work.Dispose();
                if (this != null) TryStartDeadlockCheck();
            }
        }

        private static Task<DeadlockStatus> SearchForDeadlockAsync(LevelDefinition level, GameState state,
            CancellationToken token) => Task.Run(() =>
        {
            // A solver limit, cancellation, or invalid custom board provides no proof.
            var result = BfsSolver.Solve(level, state, new SolverOptions
            {
                MaxStates = DeadlockMaxStates,
                MaxMilliseconds = DeadlockMaxMilliseconds,
                Cancellation = token
            });
            return ClassifyDeadlockSearchResult(result.Status);
        }, token);

        internal static DeadlockStatus ClassifyDeadlockSearchResult(SolverStatus status)
        {
            switch (status)
            {
                case SolverStatus.ProvenUnsolvable: return DeadlockStatus.ProvenDeadlock;
                case SolverStatus.Solved: return DeadlockStatus.Solvable;
                default: return DeadlockStatus.Unknown;
            }
        }

        private void RevealDeadlock()
        {
            if (!_deadlockPresented && _deadlockReveal == null && !_hud.IsDeadlockOpen)
                _deadlockReveal = StartCoroutine(RevealDeadlockAfterMotion(_deadlockRevision));
        }

        private IEnumerator RevealDeadlockAfterMotion(int revision)
        {
            // Wait for the triggering move to be visible before dimming the board.
            yield return null;
            while (CanCheckDeadlock && _deadlockRevision == revision)
            {
                if (!_board.IsBusy && !_hud.BlocksGameplayInput &&
                    _hud.ShowDeadlock(_engine.CanUndo, _board.Atlas.Get(ShapeAtlas.WorriedPlopKey)))
                {
                    _deadlockPresented = true;
                    ClearPendingInput();
                    InvalidateHint();
                    _audio.PlayDeadlock();
                    MarkActive();
                    PlopLog.Info("Deadlock confirmed on " + _engine.Level.Id + " after " + _engine.MoveCount + " moves");
                    break;
                }
                yield return null;
            }
            _deadlockReveal = null;
        }

        private void CancelDeadlockCheck()
        {
            ++_deadlockRevision;
            _deadlockCheckQueued = false;
            _deadlockWork?.Cancel();
            if (_deadlockReveal != null)
            {
                StopCoroutine(_deadlockReveal);
                _deadlockReveal = null;
            }
        }

        private void OnHintRequested() => _ = RequestHintAsync();

        /// <summary>Finds and verifies a direction for the current board without applying it.</summary>
        public async Task<Direction?> RequestHintAsync()
        {
            if (!isActiveAndEnabled || !_engine.HasLevel || _engine.IsCompleted ||
                GameplayBlocked || _hintWork != null)
                return null;

            var level = _engine.Level;
            var state = _engine.GetState();
            var work = new CancellationTokenSource();
            _hintWork = work;
            // Let the requested hint take over the worker budget for this state.
            if (_deadlockWork?.IsCancellationRequested == false)
            {
                _deadlockCheckQueued = true;
                _deadlockWork.Cancel();
            }
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
                if (_deadlockCheckQueued)
                {
                    // The requested hint can answer the same pending proof without a second search.
                    var verdict = ClassifyDeadlockSearchResult(result.Status);
                    if (verdict != DeadlockStatus.Unknown) _deadlockCheckQueued = false;
                    if (verdict == DeadlockStatus.ProvenDeadlock) RevealDeadlock();
                }
                if (result.Status == SolverStatus.Solved && result.Moves.Length > 0)
                {
                    Direction direction = result.Moves[0];
                    ShowHint("Try swiping " + direction.ToString().ToLowerInvariant() + ".", direction: direction);
                    return direction;
                }

                ShowHint(result.Status == SolverStatus.ProvenUnsolvable
                    ? "No route from here. Try Undo."
                    : "No hint available. Try Undo.");
                return null;
            }
            catch (Exception exception)
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
                if (this != null) TryStartDeadlockCheck();
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

        private void ShowHint(string hint, bool searching = false, Direction? direction = null)
        {
            _hintVisible = hint != null;
            _hud.SetHint(hint, searching, direction);
        }

        private void InvalidateHint()
        {
            _hintWork?.Cancel();
            if (_hintVisible) ShowHint(null);
        }

        private void LoadCurrent()
        {
            MarkActive();
            _hud.CancelWorldAnnouncement();
            var previousWorld = _world;
            SetWorld(_worlds.ForLevelIndex(_index));
            _engine.LoadLevel(_levels[_index]);
            if (previousWorld != null && previousWorld != _world && _world != null)
                _backdrop.PlayReveal();
            PlopLog.Info("Loaded " + _levels[_index].Id);
        }

        private void SetWorld(WorldTheme world)
        {
            if (_world == world) return;
            _world = world;
            _hud.SetCurrentWorld(world);
            _audio.SetWorldAmbience(world?.Ambience ?? WorldAmbience.CozyNest);
            if (world == null)
            {
                _board.SetPalette(Color.white, Color.white, Color.white);
                return;
            }

            Color key = Color.Lerp(Color.white, world.KeyLight, world.LightStrength * 0.18f);
            _board.SetPalette(world.TrayTint * key, world.TileTint * key, world.PebbleTint);
        }

        private void CompleteCurrentLevel(int moveCount)
        {
            int nextIndex = _index + 1;
            bool nextWasUnlocked = IsLevelUnlocked(nextIndex);
            _progress.Complete(_index, SavesProgress);
            bool nextUnlocked = IsLevelUnlocked(nextIndex);
            _hud.SetCompleted(true, nextUnlocked);

            var nextWorld = _worlds.ForLevelIndex(nextIndex);
            if (!nextWasUnlocked && nextUnlocked && nextWorld != null && nextWorld != _world)
            {
                _hud.ShowWorldUnlocked(nextWorld.DisplayName, nextWorld.Preview);
                _audio.PlayWorldUnlock();
                PlopLog.Info("World unlocked " + nextWorld.Id);
            }
            PlopLog.Info("Completed " + _engine.Level.Id + " in " + moveCount);
        }

        private void OnEngineEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.LevelLoaded:
                    _deadlockPresented = false;
                    ClearPendingInput();
                    CancelDeadlockCheck();
                    _hud.CloseDeadlock();
                    _deadlockTable = _deadlockCatalog?.ForLevel(_engine.Level);
                    _board.Build(_engine.Level, _engine.CurrentState, ShapeAtlas.ForDensity(FitCamera()));
                    InvalidateHint();
                    _hud.SetLevel(_engine.Level.Name, _engine.Level.Instruction);
                    _hud.SetMoves(0);
                    _hud.SetCanUndo(false);
                    _hud.SetCompleted(false, false);
                    break;
                case GameEventType.MoveUndone:
                case GameEventType.LevelRestarted:
                    _deadlockPresented = false;
                    ClearPendingInput();
                    CancelDeadlockCheck();
                    _hud.CloseDeadlock();
                    InvalidateHint();
                    _hud.CancelWorldAnnouncement();
                    _board.Sync(_engine.CurrentState, easeRestore: e.Type == GameEventType.MoveUndone);
                    _hud.SetMoves(_engine.MoveCount);
                    _hud.SetCanUndo(_engine.CanUndo);
                    _hud.SetCompleted(false, false);
                    if (e.Type == GameEventType.MoveUndone)
                    {
                        _audio.PlayUndo();
                        if (_engine.CanUndo) CheckCurrentDeadlock();
                    }
                    else _audio.PlayRestart();
                    break;
                case GameEventType.PlopMoved:
                    _deadlockPresented = false;
                    CancelDeadlockCheck();
                    _hud.CloseDeadlock();
                    InvalidateHint();
                    _hud.SetMoves(e.MoveCount);
                    _hud.SetCanUndo(true);
                    break;
                case GameEventType.LevelCompleted:
                    ClearPendingInput();
                    CompleteCurrentLevel(e.MoveCount);
                    break;
            }
        }

        /// <summary>Fits the camera to the board inside the safe area (see BoardLayout). Returns pixels per cell.</summary>
        private float FitCamera()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || !_engine.HasLevel) return 128f;
            var layout = BoardLayout.Compute(Screen.width, Screen.height, Screen.safeArea, _engine.Level.Width, _engine.Level.Height);
            _hud.SetBoardLayout(layout);
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _world?.BackgroundColor ?? PlopPalette.PlayBackground;
            _camera.orthographicSize = layout.OrthographicSize;
            _camera.transform.position = new Vector3(layout.CameraPosition.x, layout.CameraPosition.y, -10f);
            _backdrop.Show(_world, _camera, layout);
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
            if (_suspended) return;
            if (_pendingMove.HasValue && !_board.IsTraveling)
            {
                var direction = _pendingMove.Value;
                _pendingMove = null;
                Move(direction);
            }

            // Battery: drop the render rate while nothing is moving (input/UI still processed every frame).
            if (idleRenderInterval > 1 && OnDemandRendering.renderFrameInterval == 1 &&
                Time.unscaledTime - _lastActivity > idleDelay && !_board.IsBusy)
            {
                OnDemandRendering.renderFrameInterval = idleRenderInterval;
            }
        }
    }
}
