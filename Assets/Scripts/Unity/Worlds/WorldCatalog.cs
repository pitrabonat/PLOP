using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Level ordinals address cosmetic worlds independently of the puzzle catalog and save format.</summary>
    public sealed class WorldCatalog
    {
        private const string ResourceFolder = "Worlds";
        private WorldCatalog(WorldTheme[] worlds) => Worlds = Array.AsReadOnly(worlds);

        public static WorldCatalog Empty { get; } = new WorldCatalog(Array.Empty<WorldTheme>());
        public IReadOnlyList<WorldTheme> Worlds { get; }

        public static WorldCatalog Load()
        {
            var worlds = Resources.LoadAll<WorldTheme>(ResourceFolder);
            Array.Sort(worlds, (left, right) =>
            {
                int order = left.FirstLevel.CompareTo(right.FirstLevel);
                return order != 0 ? order : string.CompareOrdinal(left.Id, right.Id);
            });

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var ordered = new List<WorldTheme>(worlds.Length);
            int end = 0;
            foreach (WorldTheme world in worlds)
            {
                if (string.IsNullOrWhiteSpace(world.Id) || string.IsNullOrWhiteSpace(world.DisplayName) ||
                    !world.HasValidRange || world.FirstLevel <= end || !ids.Add(world.Id))
                {
                    Debug.LogWarning($"World '{world.name}' has an invalid or overlapping mapping.", world);
                    continue;
                }

                ordered.Add(world);
                end = world.LastLevel;
            }

            return ordered.Count == 0 ? Empty : new WorldCatalog(ordered.ToArray());
        }

        public WorldTheme ForLevelIndex(int zeroBasedIndex)
        {
            foreach (WorldTheme world in Worlds)
                if (world.ContainsLevelIndex(zeroBasedIndex)) return world;
            return null;
        }
    }
}
