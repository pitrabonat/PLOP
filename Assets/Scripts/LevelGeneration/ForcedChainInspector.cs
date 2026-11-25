using System.Collections.Generic;
using Plop.Core;

namespace Plop.Generation
{
    /// <summary>Result of <see cref="ForcedChainInspector.Inspect"/>.</summary>
    public sealed class ForcedChainReport
    {
        public int StatesExplored;
        public int CommandsTried;
        /// <summary>True when every reachable state was visited (false: the cap was hit).</summary>
        public bool Complete;
        /// <summary>Longest forced chain (slides + pushes + teleports) caused by one command.</summary>
        public int LongestChain;
        public string LongestChainState;
        public Direction LongestChainDirection;
        /// <summary>Commands that exceeded the rules' safety budget (a forced-movement loop). Must be 0 to ship.</summary>
        public int UnresolvedCommands;
        public string FirstUnresolvedState;
        public Direction FirstUnresolvedDirection;
        public int FailingCommands;
        public bool LoopRisk => UnresolvedCommands > 0;
    }

    /// <summary>
    /// Level Lab / tooling helper: walks every reachable state (capped), applies each swipe through the real
    /// <see cref="MoveRules"/>, and records the longest forced-movement chain and any command that would not
    /// resolve (infinite-loop risk). Editor/offline only — never call it from a frame update.
    /// </summary>
    public static class ForcedChainInspector
    {
        public static ForcedChainReport Inspect(LevelDefinition level, int maxStates = 100_000)
        {
            var report = new ForcedChainReport { Complete = true };
            var start = level.CreateInitialState();
            var seen = new HashSet<GameState> { start };
            var queue = new Queue<GameState>();
            queue.Enqueue(start);
            var events = new List<GameEvent>();
            var order = new int[level.Plops.Count];
            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                report.StatesExplored++;
                if (state.AllAsleep) continue;
                for (int d = 0; d < 4; d++)
                {
                    var dir = (Direction)d;
                    var next = state.Clone();
                    events.Clear();
                    report.CommandsTried++;
                    bool changed;
                    try { changed = MoveRules.Apply(level, next, dir, events, order); }
                    catch (UnresolvedMovementException)
                    {
                        if (report.UnresolvedCommands++ == 0)
                        {
                            report.FirstUnresolvedState = state.ToString(level);
                            report.FirstUnresolvedDirection = dir;
                        }
                        continue;
                    }
                    if (!changed) continue;
                    int chain = 0;
                    foreach (var e in events)
                        if (e.Type == GameEventType.PlopSlid || e.Type == GameEventType.PlopPushed || e.Type == GameEventType.PlopTeleported) chain++;
                    if (chain > report.LongestChain)
                    {
                        report.LongestChain = chain;
                        report.LongestChainState = state.ToString(level);
                        report.LongestChainDirection = dir;
                    }
                    if (next.Failed) { report.FailingCommands++; continue; }
                    if (seen.Contains(next)) continue;
                    if (seen.Count >= maxStates) { report.Complete = false; continue; }
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }
            return report;
        }

        /// <summary>Human-readable lines for one command's events (the Level Lab effect debugger).</summary>
        public static List<string> Describe(LevelDefinition level, IEnumerable<GameEvent> events)
        {
            var lines = new List<string>();
            foreach (var e in events)
            {
                string who = e.PlopId >= 0 && e.PlopId < level.Plops.Count ? level.Plops[e.PlopId].Color + "#" + e.PlopId : "";
                switch (e.Type)
                {
                    case GameEventType.PlopMoved: lines.Add(who + " steps " + e.From + " → " + e.To); break;
                    case GameEventType.PlopBlocked: lines.Add(who + " blocked (" + e.BlockReason + ")"); break;
                    case GameEventType.PlopSlid: lines.Add(who + " slides on Frost " + e.From + " → " + e.To); break;
                    case GameEventType.PlopPushed: lines.Add(who + " pushed by Breeze " + e.Direction + " " + e.From + " → " + e.To); break;
                    case GameEventType.PlopTeleported: lines.Add(who + " takes a Loop " + e.From + " → " + e.To); break;
                    case GameEventType.PlopSettled: lines.Add(who + " settles in its Nest at " + e.To); break;
                    case GameEventType.PlopBurned: lines.Add(who + " touches Lava at " + e.To); break;
                    case GameEventType.LevelFailed: lines.Add("attempt failed (Lava) — Undo or Restart"); break;
                    case GameEventType.OutOfMoves: lines.Add("out of moves"); break;
                    case GameEventType.LevelCompleted: lines.Add("level completed"); break;
                }
            }
            return lines;
        }
    }
}
