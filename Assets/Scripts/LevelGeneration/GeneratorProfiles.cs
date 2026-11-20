using System.Collections.Generic;

namespace Plop.Generation
{
    /// <summary>Shared generator profiles (used by the benchmark, Level Lab batches and the shipped pack).</summary>
    public static class GeneratorProfiles
    {
        /// <summary>Default round-robin mix: 4x4..7x7 boards, 1–3 Plops. Changing this changes pack output.</summary>
        public static List<GeneratorSettings> Default() => new List<GeneratorSettings>
        {
            new GeneratorSettings { Width = 4, Height = 4, PlopCount = 1, WallDensity = 0.20 },
            new GeneratorSettings { Width = 5, Height = 5, PlopCount = 1, WallDensity = 0.30 },
            new GeneratorSettings { Width = 4, Height = 5, PlopCount = 2, WallDensity = 0.15 },
            new GeneratorSettings { Width = 5, Height = 5, PlopCount = 2, WallDensity = 0.20 },
            new GeneratorSettings { Width = 6, Height = 6, PlopCount = 2, WallDensity = 0.20 },
            new GeneratorSettings { Width = 5, Height = 6, PlopCount = 3, WallDensity = 0.15 },
            new GeneratorSettings { Width = 6, Height = 6, PlopCount = 3, WallDensity = 0.20 },
            new GeneratorSettings { Width = 7, Height = 7, PlopCount = 3, WallDensity = 0.20 },
        };
    }
}
