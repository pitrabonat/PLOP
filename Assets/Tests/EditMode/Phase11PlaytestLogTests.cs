using System;
using NUnit.Framework;
using Plop.Meta;

namespace Plop.Tests
{
    public class Phase11PlaytestLogTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Visit_CountsHelpAndEndsOnce()
        {
            var log = new PlaytestLog("s1");
            Assert.IsNull(log.Record(PlaytestEvent.Undo, T0), "events before a visit are ignored");
            Assert.IsNull(log.Record(PlaytestEvent.Start, T0, 7, "PLOP-1", 9));
            log.Record(PlaytestEvent.Undo, T0);
            log.Record(PlaytestEvent.Restart, T0);
            log.Record(PlaytestEvent.Failure, T0);
            log.Record(PlaytestEvent.Hint, T0);
            string row = log.Record(PlaytestEvent.Solved, T0.AddSeconds(42.5), moves: 9);
            Assert.AreEqual("s1,7,PLOP-1,9,42.5,9,1,1,1,1,solved", row, "a hinted optimum is not Perfect");
            Assert.IsNull(log.Record(PlaytestEvent.Left, T0.AddSeconds(50)), "a finished visit is not closed twice");
        }

        [Test]
        public void StartingAnotherLevel_ClosesTheOpenVisitAsLeft_AndPerfectIsDetected()
        {
            var log = new PlaytestLog("s");
            log.Record(PlaytestEvent.Start, T0, 1, "A", 3);
            string left = log.Record(PlaytestEvent.Start, T0.AddSeconds(5), 2, "B", 4);
            StringAssert.EndsWith(",left", left);
            string perfect = log.Record(PlaytestEvent.Solved, T0.AddSeconds(9), moves: 4);
            StringAssert.EndsWith(",perfect", perfect);
        }

        [Test]
        public void Summary_ParsesRowsAndSkipsDamagedLines()
        {
            string csv = PlaytestLog.Header + "\n" +
                         "s,1,A,3,10.0,3,0,0,0,0,perfect\n" +
                         "s,1,A,3,30.0,5,2,1,0,0,solved\n" +
                         "garbage line\n" +
                         "s,2,B,6,99.0,4,0,0,1,0,left\n";
            var visits = PlaytestLog.Parse(csv);
            Assert.AreEqual(3, visits.Count);
            string table = PlaytestLog.Summarize(visits);
            StringAssert.Contains("| 1 | 2 | 2 | 1 |", table);
            StringAssert.Contains("| 2 | 1 | 0 | 0 |", table);
        }
    }
}
