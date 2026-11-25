using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Plop.Meta
{
    public enum PlaytestEvent { Start, Undo, Restart, Hint, Failure, Solved, Left }

    /// <summary>
    /// Phase 11 local playtest metrics (development builds only; nothing leaves the device, no analytics SDK).
    /// One row per level visit: time, moves against the optimum, undos, restarts, hints, failures and how the
    /// visit ended. The game writes rows to a CSV file; <see cref="Summarize"/> turns a file into a per-level table.
    /// </summary>
    public sealed class PlaytestLog
    {
        public const string Header = "session,level,id,optimal,seconds,moves,undos,restarts,hints,failures,outcome";

        public sealed class Visit
        {
            public string Session = "";
            public int Level;
            public string Id = "";
            public int Optimal, Moves, Undos, Restarts, Hints, Failures;
            public double Seconds;
            public string Outcome = "left";
        }

        private Visit _current;
        private DateTime _started;
        public string Session { get; }

        public PlaytestLog(string session) { Session = session ?? ""; }

        public Visit Current => _current;

        /// <summary>Records an event; returns a finished CSV row when a visit ends, else null.</summary>
        public string Record(PlaytestEvent e, DateTime utcNow, int level = 0, string id = null, int optimal = 0, int moves = 0)
        {
            string finished = null;
            switch (e)
            {
                case PlaytestEvent.Start:
                    if (_current != null) finished = Close("left", utcNow, _current.Moves);
                    _current = new Visit { Session = Session, Level = level, Id = id ?? "", Optimal = optimal };
                    _started = utcNow;
                    break;
                case PlaytestEvent.Undo: if (_current != null) _current.Undos++; break;
                case PlaytestEvent.Restart: if (_current != null) _current.Restarts++; break;
                case PlaytestEvent.Hint: if (_current != null) _current.Hints++; break;
                case PlaytestEvent.Failure: if (_current != null) _current.Failures++; break;
                case PlaytestEvent.Solved:
                    if (_current != null) finished = Close(moves == _current.Optimal && _current.Hints == 0 ? "perfect" : "solved", utcNow, moves);
                    break;
                case PlaytestEvent.Left:
                    if (_current != null) finished = Close("left", utcNow, moves);
                    break;
            }
            return finished;
        }

        private string Close(string outcome, DateTime utcNow, int moves)
        {
            var v = _current;
            _current = null;
            v.Outcome = outcome;
            v.Moves = moves;
            v.Seconds = Math.Max(0, (utcNow - _started).TotalSeconds);
            return ToCsv(v);
        }

        public static string ToCsv(Visit v) => string.Join(",", Clean(v.Session), v.Level.ToString(CultureInfo.InvariantCulture), Clean(v.Id),
            v.Optimal.ToString(CultureInfo.InvariantCulture), v.Seconds.ToString("0.0", CultureInfo.InvariantCulture),
            v.Moves.ToString(CultureInfo.InvariantCulture), v.Undos.ToString(CultureInfo.InvariantCulture),
            v.Restarts.ToString(CultureInfo.InvariantCulture), v.Hints.ToString(CultureInfo.InvariantCulture),
            v.Failures.ToString(CultureInfo.InvariantCulture), v.Outcome);

        private static string Clean(string s) => (s ?? "").Replace(",", " ").Replace("\n", " ").Replace("\r", " ");

        public static List<Visit> Parse(string csv)
        {
            var visits = new List<Visit>();
            foreach (var raw in (csv ?? "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("session,", StringComparison.Ordinal)) continue;
                var f = line.Split(',');
                if (f.Length != 11) continue;
                try
                {
                    visits.Add(new Visit
                    {
                        Session = f[0], Level = int.Parse(f[1], CultureInfo.InvariantCulture), Id = f[2],
                        Optimal = int.Parse(f[3], CultureInfo.InvariantCulture), Seconds = double.Parse(f[4], CultureInfo.InvariantCulture),
                        Moves = int.Parse(f[5], CultureInfo.InvariantCulture), Undos = int.Parse(f[6], CultureInfo.InvariantCulture),
                        Restarts = int.Parse(f[7], CultureInfo.InvariantCulture), Hints = int.Parse(f[8], CultureInfo.InvariantCulture),
                        Failures = int.Parse(f[9], CultureInfo.InvariantCulture), Outcome = f[10]
                    });
                }
                catch (FormatException) { }
                catch (OverflowException) { }
            }
            return visits;
        }

        /// <summary>Markdown table per level: visits, solve rate, median time, median moves over optimum, help used.</summary>
        public static string Summarize(IEnumerable<Visit> visits)
        {
            var sb = new StringBuilder("| Level | Visits | Solved | Perfect | Median s | Median +moves | Undos | Restarts | Hints | Failures |\n|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (var g in visits.GroupBy(v => v.Level).OrderBy(g => g.Key))
            {
                var list = g.ToList();
                var solved = list.Where(v => v.Outcome != "left").ToList();
                double Med(IEnumerable<double> xs) { var a = xs.OrderBy(x => x).ToList(); return a.Count == 0 ? 0 : a[a.Count / 2]; }
                sb.Append("| ").Append(g.Key).Append(" | ").Append(list.Count).Append(" | ").Append(solved.Count)
                  .Append(" | ").Append(list.Count(v => v.Outcome == "perfect"))
                  .Append(" | ").Append(Med(solved.Select(v => v.Seconds)).ToString("0", CultureInfo.InvariantCulture))
                  .Append(" | ").Append(Med(solved.Select(v => (double)(v.Moves - v.Optimal))).ToString("0", CultureInfo.InvariantCulture))
                  .Append(" | ").Append(list.Sum(v => v.Undos)).Append(" | ").Append(list.Sum(v => v.Restarts))
                  .Append(" | ").Append(list.Sum(v => v.Hints)).Append(" | ").Append(list.Sum(v => v.Failures)).Append(" |\n");
            }
            return sb.ToString();
        }
    }
}
