using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Plop.Core;
using Plop.Core.Json;
using Plop.Meta;

namespace Plop.Bench
{
    /// <summary>Phase 11: <c>--economy [out.md] [out.json]</c> runs <see cref="EconomySimulator"/> on the shipped campaign.</summary>
    internal static class EconomyCommand
    {
        private static double Difficulty(string category)
        {
            switch (category)
            {
                case "Tutorial": return 0.0;
                case "Easy": return 0.2;
                case "Normal": return 0.45;
                case "Hard": return 0.7;
                case "Expert": return 1.0;
                default: return 0.5;
            }
        }

        public static int Run(string root, string mdPath, string jsonPath)
        {
            var levels = LevelSerializer.PackFromJson(File.ReadAllText(Path.Combine(root, "Assets", "Levels", "Resources", "Levels", "PLOP_Pack_003.json")));
            var difficulty = levels.Select(l => Difficulty(l.Metadata?.Difficulty?.Category.ToString())).ToList();
            // Mirrors PlopGameController.HintIsFree: the first six levels and every level that introduces something.
            var free = levels.Select((l, i) => i < 6 || l.Instruction != null).ToList();
            var catalog = MiniJson.AsObject(MiniJson.Parse(File.ReadAllText(Path.Combine(root, "Assets", "Data", "Cosmetics", "Resources", "Cosmetics", "PLOP_Cosmetics_001.json"))), "catalog");
            var prices = new List<int>();
            int milestoneItems = 0;
            foreach (var o in MiniJson.AsArray(catalog["items"], "items"))
            {
                var item = MiniJson.AsObject(o, "item");
                string unlock = MiniJson.GetString(item, "unlock");
                if (unlock == "Dust") prices.Add(MiniJson.GetInt(item, "cost", 0));
                else if (unlock == "Milestone") milestoneItems++;
            }

            var sb = new StringBuilder();
            sb.Append("# Economy simulation — PLOP_Pack_003\n\n");
            sb.Append("Simulated with the real DreamDustWallet and RewardRules (completion ").Append(RewardRules.LevelComplete)
              .Append(", Perfect +").Append(RewardRules.LevelPerfect).Append(", gift ").Append(RewardRules.DailyGift)
              .Append(", Daily ").Append(RewardRules.DailyPuzzle).Append(" +").Append(RewardRules.DailyPuzzlePerfect)
              .Append(" Perfect, hint ").Append(RewardRules.HintCost).Append("). ").Append(prices.Count).Append(" Dust cosmetics costing ")
              .Append(prices.Sum()).Append(" in total; ").Append(milestoneItems).Append(" milestone cosmetics not modelled as purchases. ")
              .Append("Behaviour rates are assumptions (no human data); 200 seeded runs per profile, medians shown.\n\n");
            var json = new StringBuilder("{\n  \"pack\": \"PLOP_Pack_003\",\n  \"profiles\": [\n");
            bool firstProfile = true;
            foreach (var profile in EconomySimulator.Profiles)
            {
                var r = EconomySimulator.Run(profile, difficulty, free, prices);
                sb.Append("## ").Append(profile.Name).Append("\n\n");
                sb.Append($"{profile.LevelsPerDay} levels/day · Perfect {profile.PerfectRate:P0} · wants a hint {profile.HintRate:P0} ×{profile.HintsWhenStuck} · Daily {profile.DailyPuzzleRate:P0} · gift {profile.GiftRate:P0} · reserve {profile.Reserve}\n\n");
                sb.Append("| Levels | Days | Earned | Levels | Perfect | Daily | Gift | Hints bought | Free hints | Grace hints | Denied | Runs with a denial | Cosmetics | Balance |\n");
                sb.Append("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|\n");
                if (!firstProfile) json.Append(",\n");
                firstProfile = false;
                json.Append("    {\"name\": \"").Append(profile.Name).Append("\", \"milestones\": [");
                for (int m = 0; m < r.Median.Count; m++)
                {
                    var s = r.Median[m];
                    sb.Append($"| {s.Levels} | {s.Days} | {s.Earned} | {s.FromLevels} | {s.FromPerfect} | {s.FromDaily} | {s.FromGift} | {s.HintsBought} | {s.HintsFree} | {s.HintsGrace} | {s.HintsDenied} | {r.DeniedRuns[m]:P0} | {s.Cosmetics}/{prices.Count} | {s.Balance} |\n");
                    json.Append(m == 0 ? "" : ", ").Append($"{{\"levels\": {s.Levels}, \"days\": {s.Days}, \"earned\": {s.Earned}, \"hintsBought\": {s.HintsBought}, \"hintsGrace\": {s.HintsGrace}, \"hintsDenied\": {s.HintsDenied}, \"deniedRunShare\": {r.DeniedRuns[m]:0.000}, \"cosmetics\": {s.Cosmetics}, \"balance\": {s.Balance}}}");
                }
                json.Append("]}");
                sb.Append('\n');
            }
            json.Append("\n  ]\n}\n");
            Console.WriteLine(sb);
            if (mdPath != null) File.WriteAllText(mdPath, sb.ToString());
            if (jsonPath != null) File.WriteAllText(jsonPath, json.ToString());
            return 0;
        }
    }
}
