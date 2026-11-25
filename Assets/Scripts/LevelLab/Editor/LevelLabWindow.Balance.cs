using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Plop.Core;
using Plop.Generation;
using UnityEditor;
using UnityEngine;

namespace Plop.LevelLab
{
    /// <summary>
    /// Phase 11 balancing view: the offline quality audit (PuzzleQuality + simulated players) for the loaded pack,
    /// with filters, trivial and similar-level detection, old-vs-replacement comparison and report export.
    /// </summary>
    public sealed partial class LevelLabWindow
    {
        private List<QualityAuditRow> _quality;
        private List<LevelDefinition> _comparePack;
        private string _comparePackPath = "Assets/Levels/Archive/PLOP_Pack_002.json";
        private Vector2 _qualityScroll;
        private int _qWorld, _qBand, _qMechanic, _qFlag, _qSort;
        private int _qSelected = -1;
        private static readonly string[] BandOptions = { "Any band", "Tutorial", "Easy", "Normal", "Hard", "Expert" };
        private static readonly string[] MechanicOptions = { "Any mechanic", "Classic", "Frost", "Breeze", "Lava", "Loops" };
        private static readonly string[] FlagOptions =
        {
            "Any", "Flagged only", "random-solvable", "greedy-solvable", "shallow", "few-decisions", "repetitive",
            "decorative-mechanic", "idle-tiles", "long-for-its-decisions", "easy-for-position", "similar"
        };
        private static readonly string[] SortOptions = { "Pack order", "Quality ↑", "Challenge ↓", "Random success ↓" };

        private void DrawBalanceSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Balance & quality (Phase 11)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_pack.Count == 0))
                if (GUILayout.Button("Quality audit (" + _pack.Count + ")")) RunQualityAudit();
            using (new EditorGUI.DisabledScope(_quality == null))
            {
                if (GUILayout.Button("Export report…")) ExportQualityReport();
                if (GUILayout.Button("Clear")) { _quality = null; _qSelected = -1; }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            _comparePackPath = EditorGUILayout.TextField("Compare with", _comparePackPath);
            if (GUILayout.Button("Load", GUILayout.Width(50))) LoadComparePack();
            EditorGUILayout.EndHorizontal();
            if (_quality == null)
            {
                EditorGUILayout.LabelField("Audits every level: decisions, planning depth, mechanic roles, Random/Greedy/Look-ahead/Limited-information players.", EditorStyles.miniLabel);
                return;
            }

            var worlds = new List<string> { "Any World" };
            worlds.AddRange(_quality.Select(r => r.WorldId).Distinct());
            EditorGUILayout.BeginHorizontal();
            _qWorld = EditorGUILayout.Popup(Mathf.Min(_qWorld, worlds.Count - 1), worlds.ToArray(), GUILayout.Width(130));
            _qBand = EditorGUILayout.Popup(_qBand, BandOptions, GUILayout.Width(90));
            _qMechanic = EditorGUILayout.Popup(_qMechanic, MechanicOptions, GUILayout.Width(100));
            _qFlag = EditorGUILayout.Popup(_qFlag, FlagOptions, GUILayout.Width(150));
            _qSort = EditorGUILayout.Popup(_qSort, SortOptions, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            var rows = _quality.Where(r => Matches(r, worlds[Mathf.Min(_qWorld, worlds.Count - 1)])).ToList();
            if (_qSort == 1) rows = rows.OrderBy(r => r.Analysed ? r.Quality.Quality : -1).ToList();
            else if (_qSort == 2) rows = rows.OrderByDescending(r => r.Analysed ? r.Insight.Challenge : -1).ToList();
            else if (_qSort == 3) rows = rows.OrderByDescending(r => r.Analysed ? r.Quality.Player(PlayerProfile.Random).SuccessRate : -1).ToList();
            var analysed = rows.Where(r => r.Analysed).ToList();
            EditorGUILayout.LabelField(rows.Count + " shown · avg quality " + (analysed.Count > 0 ? analysed.Average(r => r.Quality.Quality).ToString("0") : "-") +
                " · greedy solves " + analysed.Count(r => r.Quality.Player(PlayerProfile.Greedy).Solved) +
                " · flagged " + rows.Count(r => r.Flags.Count > 0), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("  #  world             band     opt depth dens qual rand%  G  L2 L3 LI  flags", EditorStyles.miniBoldLabel);
            _qualityScroll = EditorGUILayout.BeginScrollView(_qualityScroll, GUILayout.Height(220));
            foreach (var r in rows)
            {
                string line = (r.Index + 1).ToString().PadLeft(3) + "  " + r.WorldId.PadRight(18);
                if (!r.Analysed) line += r.Problem;
                else
                {
                    var q = r.Quality;
                    string P(PlayerProfile p) => q.Player(p).Solved ? "✓" : "·";
                    line += r.Category.PadRight(8) + " " + r.Optimal.ToString().PadLeft(3) + "  " +
                            (q.PlanningDepth > SimulatedPlayers.MaxLookAhead ? ">4" : q.PlanningDepth.ToString()).PadLeft(3) + "  " +
                            q.DecisionDensity.ToString("0.00") + " " + q.Quality.ToString("0").PadLeft(3) + "  " +
                            (q.Player(PlayerProfile.Random).SuccessRate * 100).ToString("0").PadLeft(4) + "   " +
                            P(PlayerProfile.Greedy) + "  " + P(PlayerProfile.LookAhead2) + "  " + P(PlayerProfile.LookAhead3) + "  " +
                            P(PlayerProfile.LimitedInformation) + "  " + string.Join(", ", r.Flags);
                }
                var style = r.Flags.Count > 0
                    ? _warnRow ?? (_warnRow = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.85f, 0.45f, 0.1f) } })
                    : _plainRow ?? (_plainRow = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft });
                if (GUILayout.Button(new GUIContent(line, r.Level.Id), style))
                {
                    _qSelected = r.Index;
                    _packSelection = r.Index;
                    LoadIntoEditor(r.Level);
                }
            }
            EditorGUILayout.EndScrollView();
            DrawQualityDetail();
        }

        private bool Matches(QualityAuditRow r, string world)
        {
            if (world != "Any World" && r.WorldId != world) return false;
            if (_qBand > 0 && r.Category != BandOptions[_qBand]) return false;
            if (_qMechanic > 0)
            {
                var kinds = r.Level.Tiles.Select(t => t.Kind).Distinct().ToList();
                var want = _qMechanic == 1 ? (TileKind?)null : _qMechanic == 2 ? TileKind.Frost : _qMechanic == 3 ? TileKind.Fan : _qMechanic == 4 ? TileKind.Lava : TileKind.Portal;
                if (want == null ? kinds.Count > 0 : !kinds.Contains(want.Value)) return false;
            }
            if (_qFlag == 1 && r.Flags.Count == 0) return false;
            if (_qFlag >= 2)
            {
                string f = FlagOptions[_qFlag];
                if (f == "similar" ? !r.Flags.Any(x => x.StartsWith("similar")) : !r.Flags.Contains(f)) return false;
            }
            return true;
        }

        private void DrawQualityDetail()
        {
            if (_quality == null || _qSelected < 0 || _qSelected >= _quality.Count) return;
            var r = _quality[_qSelected];
            if (!r.Analysed) return;
            var q = r.Quality;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Level " + (r.Index + 1) + " — quality " + q.Quality.ToString("0") + " (" + r.Category + ")", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Decisions", q.MeaningfulDecisions + " meaningful · density " + q.DecisionDensity.ToString("0.00") + " · misleading " + q.MisleadingChoices);
            EditorGUILayout.LabelField("Planning depth", q.PlanningDepth > SimulatedPlayers.MaxLookAhead ? "deeper than " + SimulatedPlayers.MaxLookAhead : q.PlanningDepth.ToString());
            EditorGUILayout.LabelField("Coordination", q.Coordination.ToString("0.00") + " · solo-unsolvable Plops " + q.SoloUnsolvable);
            EditorGUILayout.LabelField("Mechanics", q.MechanicSummary());
            EditorGUILayout.LabelField("Solutions", q.ShortestSolutionCount + " shortest · " + q.NearOptimalSolutions + " within +2 · " + q.GoodFirstMoves + " good first swipes");
            foreach (var p in q.Players) EditorGUILayout.LabelField("  " + p.Profile, p.Describe(), EditorStyles.miniLabel);
            if (r.MostSimilarIndex >= 0)
                EditorGUILayout.LabelField("Most similar", "#" + (r.MostSimilarIndex + 1) + " (" + (r.MostSimilarScore * 100).ToString("0") + "%)");

            if (_comparePack == null || r.Index >= _comparePack.Count) return;
            var old = _comparePack[r.Index];
            EditorGUILayout.LabelField(old.Id == r.Level.Id ? "Same puzzle as the comparison pack's level " + (r.Index + 1)
                : "Comparison pack level " + (r.Index + 1) + " (" + old.Id + ") → " + r.Level.Id, EditorStyles.miniBoldLabel);
            if (old.Id == r.Level.Id) return;
            _monoBox = _monoBox ?? new GUIStyle(EditorStyles.helpBox) { font = Font.CreateDynamicFontFromOSFont("Menlo", 12), fontSize = 12 };
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Before\n" + LevelText.Render(old) + "\n" + old.OptimalMoves + " moves", _monoBox, GUILayout.MinHeight(110));
            EditorGUILayout.LabelField("Now\n" + LevelText.Render(r.Level) + "\n" + r.Optimal + " moves · quality " + q.Quality.ToString("0"), _monoBox, GUILayout.MinHeight(110));
            EditorGUILayout.EndHorizontal();
        }

        private void RunQualityAudit()
        {
            var levels = _pack.ToList();
            var rows = new QualityAuditRow[levels.Count];
            try
            {
                EditorUtility.DisplayProgressBar("PLOP Level Lab", "Auditing " + levels.Count + " levels on " + Environment.ProcessorCount + " threads…", 0.5f);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Parallel.For(0, levels.Count, i => rows[i] = QualityAudit.AnalyzeLevel(levels[i], i,
                    levels.Count == BalancedCampaign.LevelCount ? BalancedCampaign.WorldOf(i + 1) : "pack"));
                _quality = rows.ToList();
                QualityAudit.Finish(_quality);
                _qSelected = -1;
                SetStatus("Quality audit: " + _quality.Count(r => r.Analysed) + " analysed in " + watch.Elapsed.TotalSeconds.ToString("0.0") +
                          " s, " + _quality.Count(r => r.Flags.Count > 0) + " flagged. Heuristic metrics; see Documentation/Phase11.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void LoadComparePack()
        {
            try
            {
                _comparePack = LevelSerializer.PackFromJson(File.ReadAllText(_comparePackPath));
                SetStatus("Comparison pack: " + _comparePack.Count + " levels from " + _comparePackPath);
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                _comparePack = null;
                SetStatus("Could not load comparison pack: " + e.Message, MessageType.Warning);
            }
        }

        private void ExportQualityReport()
        {
            string path = EditorUtility.SaveFilePanel("Export quality report", "Documentation", "quality_report", "md");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, "# Level Lab quality report — " + Path.GetFileName(_packPath) + "\n\n" + QualityAudit.ToMarkdownTable(_quality));
            File.WriteAllText(Path.ChangeExtension(path, ".json"), QualityAudit.ToJson(_quality, Path.GetFileNameWithoutExtension(_packPath)));
            SetStatus("Exported " + path + " and its .json.");
        }
    }
}
