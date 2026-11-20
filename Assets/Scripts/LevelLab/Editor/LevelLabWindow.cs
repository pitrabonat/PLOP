using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Plop.Core;
using Plop.Generation;
using Plop.Solver;
using UnityEditor;
using UnityEngine;

namespace Plop.LevelLab
{
    /// <summary>
    /// PLOP Level Lab — developer-only level editor/generator/solver (Editor assembly, never in player builds).
    /// Menu: PLOP → Level Lab.
    /// </summary>
    public sealed class LevelLabWindow : EditorWindow
    {
        private enum Tool { Wall, Floor, Plop, Nest, Erase }

        private const string DefaultPackPath = "Assets/Levels/Resources/Levels/PLOP_Pack_001.json";
        private static readonly Color[] PlopColors =
        {
            new Color(0.92f, 0.30f, 0.30f), new Color(0.30f, 0.52f, 0.95f), new Color(0.96f, 0.82f, 0.25f),
            new Color(0.35f, 0.80f, 0.40f), new Color(0.65f, 0.40f, 0.85f), new Color(0.98f, 0.58f, 0.22f)
        };

        // ---- editable level ----
        private string _levelId = "custom-001";
        private string _levelName = "Custom 1";
        private int _width = 5, _height = 5;
        private bool[] _walls = new bool[25];
        private readonly List<(PlopColor color, GridPosition pos)> _plops = new List<(PlopColor, GridPosition)>();
        private readonly List<(PlopColor color, GridPosition pos)> _nests = new List<(PlopColor, GridPosition)>();
        private LevelMetadata _metadata;

        // ---- UI state ----
        private Tool _tool = Tool.Wall;
        private PlopColor _color = PlopColor.Red;
        private Vector2 _scroll, _packScroll;
        private string _status = "Ready.";
        private MessageType _statusType = MessageType.Info;

        // ---- generation ----
        private string _seedText = "1";
        private GeneratorSettings _gen = new GeneratorSettings();
        private int _targetIndex; // 0 = any, 1.. = category + 1
        private int _batchCount = 20;

        // ---- solving / replay ----
        private Direction[] _solution;
        private StateSpaceAnalysis _analysis;
        private DifficultyInfo _difficulty;
        private int _replayStep;
        private GameEngine _replay;

        // ---- pack ----
        private string _packPath = DefaultPackPath;
        private List<LevelDefinition> _pack = new List<LevelDefinition>();
        private int _packSelection = -1;

        [MenuItem("PLOP/Level Lab", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<LevelLabWindow>("PLOP Level Lab");
            w.minSize = new Vector2(760, 560);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(330));
            DrawLevelSection();
            DrawGeneratorSection();
            DrawSolveSection();
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            DrawBoard();
            DrawReplayControls();
            DrawMetrics();
            DrawPackSection();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(_status, _statusType);
            EditorGUILayout.EndScrollView();
        }

        // ================= level editing =================

        private void DrawLevelSection()
        {
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
            _levelId = EditorGUILayout.TextField("Id", _levelId);
            _levelName = EditorGUILayout.TextField("Name", _levelName);
            EditorGUILayout.BeginHorizontal();
            int w = EditorGUILayout.IntField("Size", _width);
            int h = EditorGUILayout.IntField(_height, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
            w = Mathf.Clamp(w, 2, LevelValidator.MaxSize);
            h = Mathf.Clamp(h, 2, LevelValidator.MaxSize);
            if (w != _width || h != _height) Resize(w, h);

            EditorGUILayout.Space(4);
            _tool = (Tool)GUILayout.Toolbar((int)_tool, new[] { "Wall", "Floor", "Plop", "Nest", "Erase" });
            if (_tool == Tool.Plop || _tool == Tool.Nest)
                _color = (PlopColor)EditorGUILayout.EnumPopup("Color", _color);
            EditorGUILayout.LabelField("Click / drag on the board to paint. Plop & Nest place one per click.", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear")) { Resize(_width, _height, clear: true); SetStatus("Board cleared."); }
            if (GUILayout.Button("Validate")) ValidateCurrent();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import JSON…")) ImportLevel();
            if (GUILayout.Button("Export JSON…")) ExportLevel();
            EditorGUILayout.EndHorizontal();
        }

        private void Resize(int w, int h, bool clear = false)
        {
            var newWalls = new bool[w * h];
            if (!clear)
                for (int y = 0; y < Math.Min(h, _height); y++)
                    for (int x = 0; x < Math.Min(w, _width); x++)
                        newWalls[y * w + x] = _walls[y * _width + x];
            _width = w;
            _height = h;
            _walls = newWalls;
            if (clear) { _plops.Clear(); _nests.Clear(); }
            else
            {
                _plops.RemoveAll(p => p.pos.X >= w || p.pos.Y >= h);
                _nests.RemoveAll(p => p.pos.X >= w || p.pos.Y >= h);
            }
            InvalidateResults();
        }

        private void InvalidateResults()
        {
            _solution = null;
            _analysis = null;
            _difficulty = null;
            _replay = null;
            _replayStep = 0;
            _metadata = null;
        }

        private LevelDefinition BuildLevel()
        {
            var walls = new List<GridPosition>();
            for (int i = 0; i < _walls.Length; i++) if (_walls[i]) walls.Add(new GridPosition(i % _width, i / _width));
            var plops = new List<PlopDefinition>();
            for (int i = 0; i < _plops.Count; i++) plops.Add(new PlopDefinition(i, _plops[i].color, _plops[i].pos));
            var nests = new List<NestDefinition>();
            foreach (var n in _nests) nests.Add(new NestDefinition(n.color, n.pos));
            return new LevelDefinition(_levelId, _levelName, _width, _height, walls, plops, nests, _metadata);
        }

        private void LoadIntoEditor(LevelDefinition level)
        {
            _levelId = level.Id;
            _levelName = level.Name;
            _width = level.Width;
            _height = level.Height;
            _walls = new bool[_width * _height];
            foreach (var w in level.Walls) if (level.InBounds(w)) _walls[level.CellIndex(w)] = true;
            _plops.Clear();
            foreach (var p in level.Plops) _plops.Add((p.Color, p.Start));
            _nests.Clear();
            foreach (var n in level.Nests) _nests.Add((n.Color, n.Position));
            InvalidateResults();
            _metadata = level.Metadata;
            if (_metadata?.Solution != null)
            {
                try { _solution = DirectionUtil.ParseMoveString(_metadata.Solution); } catch (FormatException) { _solution = null; }
            }
            _difficulty = _metadata?.Difficulty;
            Repaint();
        }

        private void ApplyTool(GridPosition pos)
        {
            int cell = pos.Y * _width + pos.X;
            int plopIdx = _plops.FindIndex(p => p.pos == pos);
            int nestIdx = _nests.FindIndex(n => n.pos == pos);
            switch (_tool)
            {
                case Tool.Wall:
                    if (plopIdx >= 0 || nestIdx >= 0) return;
                    _walls[cell] = true;
                    break;
                case Tool.Floor:
                    _walls[cell] = false;
                    break;
                case Tool.Erase:
                    _walls[cell] = false;
                    if (plopIdx >= 0) _plops.RemoveAt(plopIdx);
                    else if (nestIdx >= 0) _nests.RemoveAt(nestIdx);
                    break;
                case Tool.Plop:
                    _walls[cell] = false;
                    if (plopIdx >= 0) _plops.RemoveAt(plopIdx);
                    _plops.Add((_color, pos));
                    break;
                case Tool.Nest:
                    _walls[cell] = false;
                    if (nestIdx >= 0) _nests.RemoveAt(nestIdx);
                    _nests.Add((_color, pos));
                    break;
            }
            InvalidateResults();
        }

        private void ValidateCurrent()
        {
            var v = LevelValidator.Validate(BuildLevel());
            if (v.IsValid) SetStatus("Structure valid. Use Solve/Analyze to verify solvability.");
            else SetStatus("Invalid: " + v, MessageType.Error);
        }

        // ================= board drawing =================

        private void DrawBoard()
        {
            EditorGUILayout.LabelField(_replay != null ? "Board (replay step " + _replayStep + "/" + (_solution?.Length ?? 0) + ")" : "Board", EditorStyles.boldLabel);
            float cell = Mathf.Clamp(Mathf.Min(420f / _width, 420f / _height), 18f, 48f);
            Rect area = GUILayoutUtility.GetRect(cell * _width, cell * _height, GUILayout.ExpandWidth(false));
            var state = _replay?.CurrentState;
            LevelDefinition replayLevel = _replay?.Level;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    // y = 0 is the bottom row
                    var r = new Rect(area.x + x * cell, area.y + (_height - 1 - y) * cell, cell - 1, cell - 1);
                    var pos = new GridPosition(x, y);
                    bool wall = _walls[y * _width + x];
                    EditorGUI.DrawRect(r, wall ? new Color(0.35f, 0.35f, 0.38f) : new Color(0.86f, 0.84f, 0.80f));

                    int nestIdx = _nests.FindIndex(n => n.pos == pos);
                    if (nestIdx >= 0) DrawNest(r, PlopColors[(int)_nests[nestIdx].color]);

                    if (state != null && replayLevel != null)
                    {
                        int plop = state.PlopAt(replayLevel.CellIndex(pos));
                        if (plop >= 0) DrawPlop(r, PlopColors[(int)replayLevel.PlopColorOf(plop)], state.IsAsleep(plop));
                    }
                    else
                    {
                        int plopIdx = _plops.FindIndex(p => p.pos == pos);
                        if (plopIdx >= 0) DrawPlop(r, PlopColors[(int)_plops[plopIdx].color], false, plopIdx.ToString());
                    }
                }
            }

            var e = Event.current;
            if (_replay == null && (e.type == EventType.MouseDown || (e.type == EventType.MouseDrag && (_tool == Tool.Wall || _tool == Tool.Floor || _tool == Tool.Erase)))
                && e.button == 0 && area.Contains(e.mousePosition))
            {
                int x = Mathf.FloorToInt((e.mousePosition.x - area.x) / cell);
                int yFromTop = Mathf.FloorToInt((e.mousePosition.y - area.y) / cell);
                int y = _height - 1 - yFromTop;
                if (x >= 0 && x < _width && y >= 0 && y < _height)
                {
                    ApplyTool(new GridPosition(x, y));
                    e.Use();
                    Repaint();
                }
            }
        }

        private static void DrawNest(Rect r, Color c)
        {
            float t = Mathf.Max(2f, r.width * 0.1f);
            var inner = new Rect(r.x + r.width * 0.12f, r.y + r.height * 0.12f, r.width * 0.76f, r.height * 0.76f);
            EditorGUI.DrawRect(new Rect(inner.x, inner.y, inner.width, t), c);
            EditorGUI.DrawRect(new Rect(inner.x, inner.yMax - t, inner.width, t), c);
            EditorGUI.DrawRect(new Rect(inner.x, inner.y, t, inner.height), c);
            EditorGUI.DrawRect(new Rect(inner.xMax - t, inner.y, t, inner.height), c);
        }

        private static void DrawPlop(Rect r, Color c, bool asleep, string label = null)
        {
            var inner = new Rect(r.x + r.width * 0.25f, r.y + r.height * 0.25f, r.width * 0.5f, r.height * 0.5f);
            EditorGUI.DrawRect(inner, asleep ? Color.Lerp(c, Color.black, 0.45f) : c);
            if (asleep) GUI.Label(inner, "z", EditorStyles.centeredGreyMiniLabel);
            else if (label != null) GUI.Label(inner, label, EditorStyles.centeredGreyMiniLabel);
        }

        // ================= generation =================

        private void DrawGeneratorSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Generator (v" + GeneratorInfo.Version + ")", EditorStyles.boldLabel);
            _seedText = EditorGUILayout.TextField("Seed (dec or 0x hex)", _seedText);
            _gen.Width = EditorGUILayout.IntSlider("Width", _gen.Width, 4, 7);
            _gen.Height = EditorGUILayout.IntSlider("Height", _gen.Height, 4, 7);
            _gen.PlopCount = EditorGUILayout.IntSlider("Plops", _gen.PlopCount, 1, 3);
            _gen.WallDensity = EditorGUILayout.Slider("Wall density", (float)_gen.WallDensity, 0f, 0.5f);
            var targets = new[] { "Any", "Tutorial", "Easy", "Normal", "Hard", "Expert" };
            _targetIndex = EditorGUILayout.Popup("Target difficulty", _targetIndex, targets);
            _gen.TargetDifficulty = _targetIndex == 0 ? (DifficultyCategory?)null : (DifficultyCategory)(_targetIndex - 1);
            _gen.MaxAttempts = EditorGUILayout.IntField("Max attempts", _gen.MaxAttempts);
            _gen.SolverMaxStates = EditorGUILayout.IntField("Solver state budget", _gen.SolverMaxStates);
            _gen.MinSolutionLength = EditorGUILayout.IntField("Min solution length", _gen.MinSolutionLength);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate")) GenerateOne(false);
            if (GUILayout.Button("Next seed + Generate")) GenerateOne(true);
            EditorGUILayout.EndHorizontal();
        }

        private bool TryParseSeed(out ulong seed)
        {
            string s = _seedText.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return ulong.TryParse(s.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out seed);
            return ulong.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out seed);
        }

        private void GenerateOne(bool nextSeed)
        {
            if (!TryParseSeed(out ulong seed)) { SetStatus("Invalid seed.", MessageType.Error); return; }
            if (nextSeed) { seed++; _seedText = seed.ToString(); }
            try
            {
                var result = LevelGenerator.Generate(seed, _gen, RegistryFromPack());
                if (!result.Success) { SetStatus("Generation failed: " + result.Diagnostics(), MessageType.Warning); return; }
                LoadIntoEditor(result.Level);
                _analysis = result.Analysis;
                SetStatus("Generated " + result.Level.Id + ": " + result.Diagnostics());
            }
            catch (ArgumentException ex) { SetStatus(ex.Message, MessageType.Error); }
        }

        private DuplicateRegistry RegistryFromPack()
        {
            var registry = new DuplicateRegistry();
            foreach (var l in _pack) registry.TryAdd(l);
            return registry;
        }

        // ================= solving / replay / metrics =================

        private void DrawSolveSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Solver (v" + SolverInfo.Version + ")", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Solve")) Solve();
            if (GUILayout.Button("Analyze + Difficulty")) Analyze();
            EditorGUILayout.EndHorizontal();
            if (_solution != null)
                EditorGUILayout.SelectableLabel("Solution (" + _solution.Length + "): " + DirectionUtil.ToMoveString(_solution), GUILayout.Height(18));
        }

        private void Solve()
        {
            var level = BuildLevel();
            var r = BfsSolver.Solve(level);
            _solution = r.Moves;
            _replay = null;
            switch (r.Status)
            {
                case SolverStatus.Solved: SetStatus("Solved in " + r.SolutionLength + " moves (" + r.StatesVisited + " states, " + r.ElapsedMs.ToString("0.0") + " ms)."); break;
                case SolverStatus.ProvenUnsolvable: SetStatus("Proven unsolvable (" + r.StatesVisited + " reachable states explored).", MessageType.Warning); break;
                case SolverStatus.SearchLimitReached: SetStatus("Search limit reached — solvability unknown. " + r.Message, MessageType.Warning); break;
                default: SetStatus("Invalid level: " + r.Message, MessageType.Error); break;
            }
        }

        private void Analyze()
        {
            var level = BuildLevel();
            var a = StateSpaceAnalyzer.Analyze(level);
            _analysis = a;
            if (!a.Valid) { SetStatus("Invalid level: " + a.Message, MessageType.Error); return; }
            if (!a.Complete) { SetStatus("Analysis incomplete: " + a.Message, MessageType.Warning); return; }
            if (!a.Solvable) { _difficulty = null; SetStatus("Proven unsolvable. Dead-end states: " + a.DeadEndStates, MessageType.Warning); return; }
            _solution = a.Solution;
            _difficulty = DifficultyEstimator.Estimate(level, a, _gen.Weights);
            _metadata = _metadata ?? new LevelMetadata();
            _metadata.SolverVersion = SolverInfo.Version;
            _metadata.Solution = DirectionUtil.ToMoveString(a.Solution);
            _metadata.Difficulty = _difficulty;
            SetStatus("Analyzed: " + _difficulty.Category + " (score " + _difficulty.Score + "), " + a.ReachableStates + " states.");
        }

        private void DrawReplayControls()
        {
            if (_solution == null) return;
            EditorGUILayout.BeginHorizontal();
            if (_replay == null)
            {
                if (GUILayout.Button("Start replay")) StartReplay();
            }
            else
            {
                if (GUILayout.Button("|<")) { StartReplay(); }
                if (GUILayout.Button("< Undo") && _replay.Undo()) _replayStep--;
                if (GUILayout.Button("Step >") && _replayStep < _solution.Length)
                {
                    if (_replay.TryMove(_solution[_replayStep]).Changed) _replayStep++;
                }
                if (GUILayout.Button(">|")) { while (_replayStep < _solution.Length && _replay.TryMove(_solution[_replayStep]).Changed) _replayStep++; }
                if (GUILayout.Button("Exit replay")) _replay = null;
                if (_replay != null && _replay.IsCompleted) GUILayout.Label("Completed ✓");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void StartReplay()
        {
            try
            {
                _replay = new GameEngine();
                _replay.LoadLevel(BuildLevel());
                _replayStep = 0;
            }
            catch (InvalidLevelException ex) { _replay = null; SetStatus(ex.Message, MessageType.Error); }
        }

        private void DrawMetrics()
        {
            if (_difficulty == null && _analysis == null) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Metrics", EditorStyles.boldLabel);
            if (_difficulty != null)
            {
                EditorGUILayout.LabelField("Difficulty", _difficulty.Category + "  (score " + _difficulty.Score.ToString("0.##") + ")");
                foreach (var kv in _difficulty.Metrics) EditorGUILayout.LabelField("  " + kv.Key, kv.Value.ToString("0.###"));
            }
            else if (_analysis != null && _analysis.Complete)
            {
                EditorGUILayout.LabelField("Reachable states", _analysis.ReachableStates.ToString());
                EditorGUILayout.LabelField("Dead-end states", _analysis.DeadEndStates.ToString());
            }
        }

        // ================= import / export / pack =================

        private void ImportLevel()
        {
            string path = EditorUtility.OpenFilePanel("Import PLOP level or pack", "Assets/Levels", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string json = File.ReadAllText(path);
                if (json.Contains("\"levels\""))
                {
                    _pack = LevelSerializer.PackFromJson(json);
                    _packPath = ToProjectPath(path);
                    _packSelection = -1;
                    SetStatus("Loaded pack with " + _pack.Count + " levels.");
                }
                else
                {
                    LoadIntoEditor(LevelSerializer.FromJson(json));
                    SetStatus("Imported level " + _levelId + ".");
                }
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException || ex is ArgumentException)
            {
                SetStatus("Import failed: " + ex.Message, MessageType.Error);
            }
        }

        private void ExportLevel()
        {
            var level = BuildLevel();
            var v = LevelValidator.Validate(level);
            if (!v.IsValid) { SetStatus("Cannot export invalid level: " + v, MessageType.Error); return; }
            string path = EditorUtility.SaveFilePanel("Export PLOP level", "Assets/Levels", level.Id + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, LevelSerializer.ToJson(level) + "\n");
            AssetDatabase.Refresh();
            SetStatus("Exported " + path);
        }

        private void DrawPackSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Level pack", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _packPath = EditorGUILayout.TextField(_packPath);
            if (GUILayout.Button("Load", GUILayout.Width(50))) LoadPack();
            if (GUILayout.Button("Save", GUILayout.Width(50))) SavePack();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add current level")) AddCurrentToPack();
            if (GUILayout.Button("Verify pack")) VerifyPack();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            _batchCount = EditorGUILayout.IntField("Batch count", _batchCount);
            if (GUILayout.Button("Generate batch into pack")) GenerateBatch();
            EditorGUILayout.EndHorizontal();

            _packScroll = EditorGUILayout.BeginScrollView(_packScroll, GUILayout.Height(160));
            for (int i = 0; i < _pack.Count; i++)
            {
                var l = _pack[i];
                string diff = l.Metadata?.Difficulty != null ? l.Metadata.Difficulty.Category + " " + l.Metadata.Difficulty.Score.ToString("0.#") : "?";
                string label = (i + 1) + ". " + l.Name + "  [" + l.Width + "x" + l.Height + ", " + l.Plops.Count + "p, " + diff + "]";
                if (GUILayout.Toggle(_packSelection == i, label, "Button") && _packSelection != i)
                {
                    _packSelection = i;
                    LoadIntoEditor(l);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void LoadPack()
        {
            try
            {
                _pack = LevelSerializer.PackFromJson(File.ReadAllText(_packPath));
                _packSelection = -1;
                SetStatus("Loaded " + _pack.Count + " levels from " + _packPath);
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException)
            {
                SetStatus("Load failed: " + ex.Message, MessageType.Error);
            }
        }

        private void SavePack()
        {
            var pack = LevelValidator.ValidatePack(_pack);
            if (!pack.IsValid) { SetStatus("Pack invalid, not saved: " + pack, MessageType.Error); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(_packPath));
            File.WriteAllText(_packPath, LevelSerializer.PackToJson(Path.GetFileNameWithoutExtension(_packPath), _pack) + "\n");
            AssetDatabase.Refresh();
            SetStatus("Saved " + _pack.Count + " levels to " + _packPath);
        }

        private void AddCurrentToPack()
        {
            var level = BuildLevel();
            var report = LevelVerifier.Verify(level);
            if (!report.IsValid) { SetStatus("Not added (unverified): " + report, MessageType.Error); return; }
            if (RegistryFromPack().Contains(level)) { SetStatus("Not added: duplicate of a level already in the pack (incl. symmetry/recolor).", MessageType.Warning); return; }
            foreach (var l in _pack) if (l.Id == level.Id) { SetStatus("Not added: id '" + level.Id + "' already used.", MessageType.Warning); return; }
            if (level.Metadata?.Solution == null)
            {
                var a = report.Analysis;
                level = level.WithMetadata(new LevelMetadata
                {
                    SolverVersion = SolverInfo.Version,
                    Solution = DirectionUtil.ToMoveString(a.Solution),
                    Difficulty = DifficultyEstimator.Estimate(level, a),
                    Author = "Level Lab"
                });
            }
            _pack.Add(level);
            _packSelection = _pack.Count - 1;
            SetStatus("Added " + level.Id + " (" + report + "). Remember to Save.");
        }

        private void VerifyPack()
        {
            var report = LevelVerifier.VerifyPack(_pack);
            if (report.IsValid) SetStatus("Pack verified: " + _pack.Count + " levels. " + (report.Warnings.Count > 0 ? report.Warnings.Count + " warning(s): " + string.Join("; ", report.Warnings) : ""));
            else SetStatus("Pack verification failed: " + report, MessageType.Error);
        }

        private void GenerateBatch()
        {
            if (!TryParseSeed(out ulong seed)) { SetStatus("Invalid seed.", MessageType.Error); return; }
            var cts = new CancellationTokenSource();
            var settings = new BatchSettings
            {
                TargetCount = Math.Max(1, _batchCount),
                BaseSeed = seed,
                Profiles = GeneratorProfiles.Default(),
                MaxCalls = Math.Max(1, _batchCount) * 20,
                Cancellation = cts.Token
            };
            try
            {
                var result = BatchGenerator.Generate(settings, RegistryFromPack(), (i, level) =>
                {
                    if (EditorUtility.DisplayCancelableProgressBar("PLOP Level Lab", "Generated " + (i + 1) + "/" + settings.TargetCount, (i + 1f) / settings.TargetCount))
                        cts.Cancel();
                });
                int start = _pack.Count;
                foreach (var l in result.Levels) _pack.Add(l.WithMetadata(l.Metadata, l.Id, "Generated " + (start + 1 + _pack.Count - start)));
                SetStatus("Batch: accepted " + result.Stats.Accepted + ", candidates " + result.Stats.Candidates + ", duplicates rejected " +
                          result.Stats.Rejections[(int)RejectReason.Duplicate] + ", " + (result.Stats.ElapsedMs / 1000.0).ToString("0.0") + " s. Remember to Save.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private static string ToProjectPath(string absolute)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string full = Path.GetFullPath(absolute);
            return full.StartsWith(root, StringComparison.Ordinal) ? full.Substring(root.Length + 1).Replace('\\', '/') : full;
        }

        private void SetStatus(string message, MessageType type = MessageType.Info)
        {
            _status = message;
            _statusType = type;
            Repaint();
        }
    }
}
