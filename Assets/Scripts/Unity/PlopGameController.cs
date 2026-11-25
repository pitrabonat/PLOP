using Plop.Meta;
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
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PLOP.CharacterTools")]

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
        // ---- Phase 8: menu, Perfect Moves, Dream Dust, daily features ----
        private MainMenu _menu;
        // ---- Phase 9: cosmetics and Character Studio ----
        private CharacterStudio _studio;
        private CosmeticCatalog _catalog;
        private CosmeticCollection _cosmetics;
        private CharacterLook _look = CharacterLook.Default;
        private readonly List<string> _newCosmetics = new List<string>();
        private DreamDustWallet _wallet;
        private LevelRecords _records;
        private List<LevelDefinition> _dailyPool = new List<LevelDefinition>();
        private bool _playingDaily;
        private DateTime _dailyDate;
        private bool _assisted;            // a hint was shown during this level visit (no Perfect)
        private int _attempts;             // restarts, failures and dead ends this visit (hint grace)
        // ---- Phase 10 ----
        private enum Recovery { None, Lava, OutOfMoves }
        private Recovery _pendingRecovery;
        /// <summary>Levels before this campaign index have free hints (onboarding), as do mechanic intro levels.</summary>
        internal const int FreeHintLevels = 6;
        private bool _settingsFromMenu;
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
        private bool GameplayBlocked => !isActiveAndEnabled || _suspended || _hud.BlocksGameplayInput || (_menu != null && _menu.IsOpen) || (_studio != null && _studio.IsOpen);

        /// <summary>Local calendar source for the daily features (tests inject a fixed clock).</summary>
        internal IClock Clock { get; set; } = new SystemClock();
        public DreamDustWallet Wallet => _wallet;
        public LevelRecords Records => _records;
        public MainMenu Menu => _menu;
        public CharacterStudio Studio => _studio;
        internal CosmeticCollection Cosmetics => _cosmetics;
        /// <summary>The equipped cosmetic look (visual only; never read by rules, solver or progress).</summary>
        public CharacterLook Look => _look;
        public bool PlayingDaily => _playingDaily;
        public bool HintUsedThisVisit => _assisted;

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
            _menu = gameObject.AddComponent<MainMenu>();
            _studio = gameObject.AddComponent<CharacterStudio>();
            InitMeta(persist: false); // tests and tools get an isolated in-memory wallet

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
            _hud.PerfectLookup = i => _records != null && i >= 0 && i < _levels.Count && _records.IsPerfect(_levels[i].Id);
            _hud.LayoutChanged += Relayout;
            _hud.HomeClicked += ShowMenu;
            _hud.SettingsClosed += OnSettingsClosed;
            _menu.PlayClicked += StartCampaign;
            _menu.DailyGiftClicked += () => ClaimDailyGift();
            _menu.DailyPuzzleClicked += () => StartDaily();
            _menu.LevelsClicked += OpenLevelsFromMenu;
            _menu.SettingsClicked += OpenSettingsFromMenu;
            _menu.StudioClicked += OpenStudio;
            _studio.Closed += OnStudioClosed;
            _studio.ButtonPressed += _audio.PlayButton;
            _board.SetBatterySaver(targetFrameRate == FrameRateMode.Fps30);
        }

        private void InitMeta(bool persist)
            => InitMeta(persist ? new PlayerPrefsStore() : (IKeyValueStore)new MemoryStore());

        /// <summary>Test hook: points the meta systems at a given store (simulates relaunching over the same save).</summary>
        internal void UseMetaStore(IKeyValueStore store)
        {
            InitMeta(store);
            UpdateCosmeticProgress();
            if (_engine.HasLevel) _board.Build(_engine.Level, _engine.CurrentState, ShapeAtlas.ForDensity(FitCamera(), _look));
        }

        private void InitMeta(IKeyValueStore store)
        {
            _wallet = new DreamDustWallet(store);
            _records = new LevelRecords(store);
            _catalog = CosmeticCatalogLoader.Load();
            _cosmetics = new CosmeticCollection(_catalog, _wallet, new AppearanceStore(store, _catalog));
            _look = CharacterArt.Resolve(_cosmetics.Equipped, _catalog);
            _menu?.SetLook(_look);
        }

        /// <summary>Progress counts for milestone cosmetics, derived from existing saves (never stored twice).</summary>
        internal ProgressSnapshot CosmeticProgress()
        {
            var snapshot = new ProgressSnapshot
            {
                PerfectLevels = _records?.PerfectCount ?? 0,
                DailyPuzzles = _wallet?.CountGrants("daily:") ?? 0
            };
            if (_progress == null) return snapshot;
            for (int i = 0; i < _levels.Count; i++) if (_progress.IsCompleted(i)) snapshot.LevelsCompleted++;
            foreach (var world in _worlds.Worlds)
            {
                if (!world.HasValidRange || world.LastLevel > _levels.Count) continue;
                bool all = true;
                for (int level = world.FirstLevel; level <= world.LastLevel && all; level++) all = _progress.IsCompleted(level - 1);
                if (all) snapshot.WorldsCompleted++;
            }
            return snapshot;
        }

        private void UpdateCosmeticProgress()
        {
            if (_cosmetics == null) return;
            foreach (var item in _cosmetics.UpdateProgress(CosmeticProgress()))
            {
                _newCosmetics.Add(item.Name);
                PlopLog.Info("Cosmetic unlocked by progress: " + item.Id);
            }
            if (_newCosmetics.Count > 0) _menu?.SetStudioNews(true);
        }

        /// <summary>Main menu → Character Studio.</summary>
        public void OpenStudio()
        {
            if (_studio == null || _studio.IsOpen) return;
            ClearPendingInput();
            InvalidateHint();
            _menu.Hide(instant: true);
            _menu.SetStudioNews(false);
            _studio.Open(_cosmetics, ReducedMotionEnabled, targetFrameRate == FrameRateMode.Fps30);
            MarkActive();
        }

        private void OnStudioClosed()
        {
            ApplyLook();
            ShowMenu();
        }

        /// <summary>Re-dresses the board and menu after the equipped look changed. Gameplay state is untouched.</summary>
        internal void ApplyLook()
        {
            var look = CharacterArt.Resolve(_cosmetics.Equipped, _catalog);
            bool boardChanged = look.Board != _look.Board;
            if (look.Key == _look.Key)
            {
                _look = look;
                if (boardChanged) ApplyBoardColors();
                return;
            }
            _look = look;
            ApplyBoardColors();
            if (_engine.HasLevel) _board.Build(_engine.Level, _engine.CurrentState, ShapeAtlas.ForDensity(FitCamera(), _look));
            _menu.SetLook(_look);
            ShapeAtlas.ReleaseUnused(_look);
            PlopLog.Info("Look " + _look.Key);
        }

        private void Start()
        {
            if (_progress != null) return; // catalog supplied by tests or tools
            // A scene saved before Phase 11 still names a retired pack; those are archived outside Resources.
            if (LevelLibrary.IsRetired(levelPackResource)) levelPackResource = LevelLibrary.DefaultPack;
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
            // Development previews show the player's real look and Dust but never write to the save.
            if (SavesProgress) InitMeta(true);
            else if (_debugPreview) InitMeta(new ReadOnlyStore(new PlayerPrefsStore()));
            else InitMeta(false);
            if (levelPackResource == LevelLibrary.DefaultPack) _dailyPool = LevelLibrary.Load(LevelLibrary.DailyPack);
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
            UpdateCosmeticProgress();
            LoadCurrent();
            if (!_debugPreview) ShowMenu(); // the game opens on the main menu
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
            if (paused) Playtest(PlaytestEvent.Left);
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
            Playtest(PlaytestEvent.Left);
            _engine.EventRaised -= OnEngineEvent;
            OnDemandRendering.renderFrameInterval = 1;
        }

        // ------------------------------------------------------------------ Phase 11 local playtest metrics

        /// <summary>Development builds: set to 1 to append one CSV row per level visit to persistentDataPath.</summary>
        internal const string PlaytestPreference = "plop.debug.playtest";
        internal const string PlaytestFile = "playtest_metrics.csv";
        private PlaytestLog _playtest;

        internal static string PlaytestPath => System.IO.Path.Combine(Application.persistentDataPath, PlaytestFile);

        /// <summary>Compiled out of release players. Local file only; nothing is sent anywhere.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void Playtest(PlaytestEvent e, int moves = -1)
        {
            if (_playtest == null)
            {
                if (e != PlaytestEvent.Start || !rememberProgress || PlayerPrefs.GetInt(PlaytestPreference, 0) != 1) return;
                _playtest = new PlaytestLog(DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
            }
            var level = _engine.HasLevel ? _engine.Level : null;
            string row = _playtest.Record(e, DateTime.UtcNow, _playingDaily ? 0 : _index + 1, level?.Id, level?.OptimalMoves ?? 0,
                moves >= 0 ? moves : _engine.MoveCount);
            if (row == null) return;
            try
            {
                if (!System.IO.File.Exists(PlaytestPath)) System.IO.File.WriteAllText(PlaytestPath, PlaytestLog.Header + "\n");
                System.IO.File.AppendAllText(PlaytestPath, row + "\n");
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                PlopLog.Info("Playtest log not written: " + ex.GetType().Name);
            }
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
            _board.SetBatterySaver(mode == FrameRateMode.Fps30);
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
            if (_menu != null) _menu.ReducedMotion = value;
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
            if (_engine.Undo()) Playtest(PlaytestEvent.Undo);
        }

        public void Restart()
        {
            if (GameplayBlocked) return;
            ClearPendingInput();
            MarkActive();
            if (_engine.HasLevel && _engine.MoveCount > 0) { _attempts++; Playtest(PlaytestEvent.Restart); }
            _engine.Restart();
        }

        public void NextLevel()
        {
            if (GameplayBlocked || !_engine.IsCompleted) return;
            if (_playingDaily) { ShowMenu(); return; }
            TrySelectLevel(_index + 1);
        }

        public bool TrySelectLevel(int index)
        {
            if (!isActiveAndEnabled || _suspended || _hud.IsSettingsOpen || _hud.IsDeadlockOpen || !IsLevelUnlocked(index)) return false;
            ClearPendingInput();
            _hud.CloseLevelPicker();
            _menu?.Hide(instant: true); // choosing a level always lands on the board
            if (index == _index && !_engine.IsCompleted && !_playingDaily) return true;
            _playingDaily = false;
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
            _engine.HasLevel && !_engine.IsCompleted && !_engine.IsFailed && !_engine.IsOutOfMoves;

        /// <summary>Hints cost Dream Dust except on the first levels and on levels that teach something.</summary>
        internal bool HintIsFree => _engine.HasLevel && !_playingDaily && (_index < FreeHintLevels || _engine.Level.Instruction != null);
        internal int HintCost => HintIsFree ? 0 : RewardRules.HintCost;
        internal int AttemptsThisVisit => _attempts;

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
            if (_deadlockPresented || _deadlockReveal != null || _hud.IsDeadlockOpen) return;
            _attempts++;
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
            if (_engine.IsFailed || _engine.IsOutOfMoves)
            {
                ShowHint("Undo or Restart first.");
                return null;
            }

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
                    // Pay only for a verified hint for this exact, still-current board; re-showing it is free.
                    string charged = "";
                    if (!HintIsFree)
                    {
                        var spend = HintPolicy.Charge(_wallet, level.Id, new StateCodec(level).Encode(state), _attempts);
                        if (spend == HintCharge.Insufficient)
                        {
                            int more = HintPolicy.GraceAttempts - _attempts;
                            ShowHint("A hint costs " + RewardRules.HintCost + " Dream Dust. You have " + _wallet.Balance + "." +
                                (more > 0 && !_wallet.HasSpent(HintPolicy.GraceKey(level.Id))
                                    ? "\nKeep trying: after " + more + " more " + (more == 1 ? "try" : "tries") + " the first hint here is free."
                                    : ""));
                            return null;
                        }
                        if (spend == HintCharge.Charged)
                            charged = "\n−" + RewardRules.HintCost + " Dream Dust · " + _wallet.Balance + " left";
                        else if (spend == HintCharge.Grace)
                            charged = "\nThis one is on the house.";
                    }
                    _assisted = true; // a revealed route means this visit can no longer earn Perfect
                    ShowHint("Try swiping " + direction.ToString().ToLowerInvariant() + "." + charged, direction: direction);
                    Playtest(PlaytestEvent.Hint);
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
            ApplyBoardColors();
        }

        /// <summary>World surface colours, softly blended toward the equipped Board decoration.</summary>
        private void ApplyBoardColors()
        {
            var decor = _look?.Board;
            if (_world == null)
            {
                _board.SetPalette(BoardDecorTint.Blend(Color.white, decor?.Tray ?? Color.white, decor?.Amount ?? 0f),
                    BoardDecorTint.Blend(Color.white, decor?.Tile ?? Color.white, decor?.Amount ?? 0f), Color.white);
                return;
            }
            Color key = Color.Lerp(Color.white, _world.KeyLight, _world.LightStrength * 0.18f);
            _board.SetPalette(BoardDecorTint.Blend(_world.TrayTint * key, decor?.Tray ?? Color.white, decor?.Amount ?? 0f),
                BoardDecorTint.Blend(_world.TileTint * key, decor?.Tile ?? Color.white, decor?.Amount ?? 0f), _world.PebbleTint);
        }

        private void CompleteCurrentLevel(int moveCount)
        {
            var level = _engine.Level;
            if (_playingDaily)
            {
                var daily = SafeReward(() => CompletionRewards.ForDaily(_wallet, _records, level.Id, _dailyDate, moveCount, level.OptimalMoves, _assisted));
                _hud.SetCompleted(true, true);
                _hud.SetCompletionResult(moveCount, level.OptimalMoves, daily.Attempt.Perfect, daily.DustEarned, returnsToMenu: true);
                UpdateCosmeticProgress();
                PlopLog.Info("Daily puzzle " + level.Id + " completed in " + moveCount);
                return;
            }
            int nextIndex = _index + 1;
            bool nextWasUnlocked = IsLevelUnlocked(nextIndex);
            _progress.Complete(_index, SavesProgress);
            bool nextUnlocked = IsLevelUnlocked(nextIndex);
            _hud.SetCompleted(true, nextUnlocked);
            var reward = SafeReward(() => CompletionRewards.ForLevel(_wallet, _records, level.Id, moveCount, level.OptimalMoves, _assisted));
            _hud.SetCompletionResult(moveCount, level.OptimalMoves, reward.Attempt.Perfect, reward.DustEarned, returnsToMenu: false);
            UpdateCosmeticProgress();

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
                    _board.Build(_engine.Level, _engine.CurrentState, ShapeAtlas.ForDensity(FitCamera(), _look));
                    InvalidateHint();
                    _assisted = false;
                    _attempts = 0;
                    Playtest(PlaytestEvent.Start);
                    _hud.SetPerfectTarget(_engine.Level.OptimalMoves, _records.IsPerfect(_engine.Level.Id));
                    _hud.SetLevel(_engine.Level.Name, _engine.Level.Instruction);
                    _hud.SetSubtitle(_playingDaily
                        ? _dailyDate.ToString("MMMM d", System.Globalization.CultureInfo.InvariantCulture)
                        : _world?.DisplayName);
                    _pendingRecovery = Recovery.None;
                    _hud.SetMoveLimit(_engine.Level.MoveLimit);
                    if (_engine.Level.MoveLimit > 0) _hud.SetSubtitle("Challenge · " + _engine.Level.MoveLimit + " moves");
                    _hud.SetHintCost(HintCost);
                    _hud.SetMoves(0);
                    _hud.SetCanUndo(false);
                    _hud.SetCompleted(false, false);
                    break;
                case GameEventType.MoveUndone:
                case GameEventType.LevelRestarted:
                    _pendingRecovery = Recovery.None;
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
                    Playtest(PlaytestEvent.Solved, e.MoveCount);
                    ClearPendingInput();
                    CompleteCurrentLevel(e.MoveCount);
                    break;
                case GameEventType.LevelFailed:
                    ClearPendingInput();
                    CancelDeadlockCheck();
                    _pendingRecovery = Recovery.Lava; // shown once the board has played the whole chain
                    _attempts++;
                    Playtest(PlaytestEvent.Failure);
                    PlopLog.Info("Attempt failed (Lava) on " + _engine.Level.Id);
                    break;
                case GameEventType.OutOfMoves:
                    ClearPendingInput();
                    CancelDeadlockCheck();
                    _pendingRecovery = Recovery.OutOfMoves;
                    _attempts++;
                    Playtest(PlaytestEvent.Failure);
                    break;
            }
        }

        /// <summary>Friendly failure card. No Dream Dust is taken and nothing restarts on its own.</summary>
        private void ShowPendingRecovery()
        {
            var sprite = _board.Atlas?.Get(ShapeAtlas.WorriedPlopKey);
            bool shown = _pendingRecovery == Recovery.Lava
                ? _hud.ShowRecovery("Oops! Too hot!", "Lava is no place for a nap. Undo or try again.", _engine.CanUndo, sprite)
                : _hud.ShowRecovery("Out of moves!", "This challenge allows " + _engine.Level.MoveLimit + " moves. Undo or try again.", _engine.CanUndo, sprite);
            if (shown || _hud.IsDeadlockOpen) _pendingRecovery = Recovery.None;
        }

        internal bool RecoveryPending => _pendingRecovery != Recovery.None;

        private static CompletionReward SafeReward(Func<CompletionReward> award)
        {
            try { return award(); }
            catch (InvalidOperationException exception)
            {
                // A move count below the verified optimum means corrupted data; never pay or record it.
                Debug.LogWarning("[PLOP] Result not recorded: " + exception.Message);
                return default;
            }
        }

        // ------------------------------------------------------------------ main menu & daily features

        public void ShowMenu()
        {
            if (_menu == null) return;
            ClearPendingInput();
            InvalidateHint();
            CancelDeadlockCheck();
            _hud.CloseDeadlock();
            _hud.CloseLevelPicker();
            if (_playingDaily)
            {
                _playingDaily = false;
                if (_levels.Count > 0) LoadCurrent();
            }
            RefreshMenu();
            _menu.SetLook(_look);
            _menu.Show();
            if (_newCosmetics.Count > 0)
            {
                _menu.ShowToast("New in Studio: " + string.Join(", ", _newCosmetics));
                _newCosmetics.Clear();
            }
        }

        private void RefreshMenu()
        {
            var now = Clock.LocalNow;
            _menu.Refresh(_wallet.Balance, CampaignResumeIndex + 1, DailyGift.IsAvailable(_wallet, now),
                _wallet.HasGrant(RewardRules.DailyKey(now)), _wallet.HasGrant(RewardRules.DailyPerfectKey(now)));
        }

        /// <summary>PLAY: resume the furthest unlocked campaign level (Level 1 without progress).</summary>
        public void StartCampaign()
        {
            if (_levels.Count == 0) return;
            _menu.Hide();
            _playingDaily = false;
            int frontier = CampaignResumeIndex;
            if (frontier != _index || _engine.IsCompleted || !_engine.HasLevel || _engine.Level != _levels[_index])
            {
                _index = frontier;
                _progress.Visit(frontier, SavesProgress);
                LoadCurrent();
            }
            MarkActive();
        }

        /// <summary>Starts today's prevalidated daily puzzle (offline, deterministic by local date).</summary>
        public bool StartDaily()
        {
            if (_dailyPool.Count == 0) { _menu.ShowToast("Daily puzzle unavailable"); return false; }
            var now = Clock.LocalNow;
            _dailyDate = now.Date;
            var puzzle = _dailyPool[DailyPickIndex(now)];
            _menu.Hide();
            ClearPendingInput();
            InvalidateHint();
            _playingDaily = true;
            _engine.LoadLevel(puzzle.WithMetadata(null, name: "Daily Puzzle"));
            MarkActive();
            return true;
        }

        private const string DailyPickKey = "plop.daily.pick.v2";

        /// <summary>
        /// Today's puzzle among those whose mechanics the player has met. The choice is remembered for the day, so
        /// finishing a World does not swap a puzzle that was already started.
        /// </summary>
        internal int DailyPickIndex(DateTime now)
        {
            string date = DailySchedule.DateKey(now);
            string saved = rememberProgress ? PlayerPrefs.GetString(DailyPickKey, "") : "";
            int bar = saved.IndexOf('|');
            if (bar > 0 && saved.Substring(0, bar) == date)
            {
                string id = saved.Substring(bar + 1);
                for (int i = 0; i < _dailyPool.Count; i++) if (_dailyPool[i].Id == id) return i;
            }
            int completed = 0;
            for (int i = 0; i < _levels.Count; i++) if (_progress != null && _progress.IsCompleted(i)) completed = i + 1;
            int index = Math.Max(0, DailySchedule.PickIndex(_dailyPool, now, completed));
            if (SavesProgress)
            {
                PlayerPrefs.SetString(DailyPickKey, date + "|" + _dailyPool[index].Id);
                PlayerPrefs.Save();
            }
            return index;
        }

        /// <summary>Supplies a daily pool directly (tests, tools).</summary>
        public void UseDailyPool(List<LevelDefinition> pool) => _dailyPool = pool ?? new List<LevelDefinition>();

        public int ClaimDailyGift()
        {
            int amount = DailyGift.Claim(_wallet, Clock.LocalNow);
            _menu.ShowToast(amount > 0 ? "+" + amount + " Dream Dust" : "Next gift tomorrow");
            if (amount > 0) _audio.PlayButton();
            RefreshMenu();
            return amount;
        }

        private void OpenLevelsFromMenu()
        {
            _menu.Hide(instant: true);
            ShowLevelPicker();
        }

        private void OpenSettingsFromMenu()
        {
            _menu.Hide(instant: true);
            _settingsFromMenu = true;
            ShowSettings();
        }

        private void OnSettingsClosed()
        {
            if (!_settingsFromMenu) return;
            _settingsFromMenu = false;
            RefreshMenu();
            _menu.Show();
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
            if (ShapeAtlas.Bucket(ppu) != _board.PixelsPerUnitBucket || _board.Look?.Key != _look.Key)
                _board.Build(_engine.Level, _engine.CurrentState, ShapeAtlas.ForDensity(ppu, _look));
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
            if (_pendingRecovery != Recovery.None && !_board.IsTraveling) ShowPendingRecovery();
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
