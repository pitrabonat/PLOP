// PLOP! Sprint 03 marketing probe (read-only; uses Plop.Lib = the game's own Core/Solver/Generation code).
//   probe <pack.json> [level ...]                 quality signals per level (settlement matters, traps, decisions, coordination)
//   probe <pack.json> --cues <level> <moves>      replays the move string ('.' = 0.45 s pause, as the dev replay) through
//        MoveRules.Apply and prints one sound cue per authoritative engine event, timed from the first command (seconds).
using System; using System.Collections.Generic; using System.IO; using System.Linq;
using Plop.Core; using Plop.Solver; using Plop.Generation;
var levels = LevelSerializer.PackFromJson(File.ReadAllText(args[0]));
if (args.Length > 3 && args[1] == "--cues")
{
    var L = levels[int.Parse(args[2]) - 1]; var s = L.CreateInitialState(); string moves = args[3];
    int variant = 0, cmd = 0; bool done = false; var ev = new List<GameEvent>();
    for (int k = 0; k < moves.Length && !done; k++)
    {
        if (!DirectionUtil.TryParse(moves[k], out var d)) continue;
        double t = k * 0.45; ev.Clear(); cmd++;
        bool moved = MoveRules.Apply(L, s, d, ev);
        var types = ev.Select(e => e.Type).ToHashSet(); if (s.AllAsleep) types.Add(GameEventType.LevelCompleted);
        void C(string id, double dt) => Console.WriteLine($"{t + dt:0.000}\t{id}\t{cmd}");
        if (moved) C("move_" + (variant++ % 4 + 1), 0); else C("blocked_" + (variant++ % 2 + 1), 0);
        if (types.Contains(GameEventType.PlopSlid)) { C("ice_slide", 0.05); C("ice_stop", 0.28); }
        if (types.Contains(GameEventType.PlopPushed)) C("fan", 0.08);
        if (types.Contains(GameEventType.PlopTeleported)) { C("portal_in", 0.10); C("portal_out", 0.30); }
        if (ev.Any(e => e.Type == GameEventType.PlopBlocked && e.BlockReason == BlockReason.Plop) && moved) C("collision_" + (variant % 2 + 1), 0.16);
        if (types.Contains(GameEventType.PlopBurned)) { C("lava_sizzle", 0.22); C("deadlock", 0.9); done = true; }
        if (types.Contains(GameEventType.PlopSettled)) { C("settle", 0.16); if (!types.Contains(GameEventType.LevelCompleted)) C("sleep", 0.32); }
        if (types.Contains(GameEventType.LevelCompleted))
        {
            bool perfect = cmd <= L.OptimalMoves;
            C("sleep", 0.30); C(perfect ? "perfect" : "complete", 0.40); C("dust", perfect ? 1.53 : 1.23); done = true;
        }
    }
    return;
}
var pick = args.Skip(1).Select(int.Parse).ToHashSet();
for (int i = 0; i < levels.Count; i++)
{
    if (pick.Count > 0 && !pick.Contains(i + 1)) continue;
    var L = levels[i]; var a = StateSpaceAnalyzer.AnalyzeWithGraph(L);
    var ins = PuzzleInsight.Analyze(L, a); var q = PuzzleQuality.Evaluate(L, a, ins);
    string lavaFirst = "";
    foreach (Direction d in Enum.GetValues(typeof(Direction))) { var s = L.CreateInitialState(); MoveRules.Apply(L, s, d); if (s.Failed) lavaFirst += DirectionUtil.ToMoveString(new[] { d }); }
    Console.WriteLine($"{i + 1}\topt {a.StartDistance} {DirectionUtil.ToMoveString(a.Solution)}\tsettle {q.SettlementMatters}\ttraps {ins.TemptingTraps + ins.EarlySettleTraps}\tdecisions {q.MeaningfulDecisions}\tcoord {q.Coordination:0.00}\tquality {q.Quality}\tmech {q.MechanicSummary()}\tlavaFirst {lavaFirst}");
}
