using System;
using System.IO;
using Plop.Core;
using Plop.Presentation;
using UnityEditor;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>
    /// Phase 11.5: renders the runtime board (BoardView, procedural Lava/Frost/Loop/wind shaders, World board
    /// materials) for the Visual Lab boards to PNG. These are renders of the shipped code, not concept art.
    /// Menu: PLOP → Visual Lab → Capture Close-ups. Batch:
    /// -executeMethod Plop.CharacterTools.EnvironmentCapture.CaptureBatch (PLOP_CAPTURE_DIR optional).
    /// </summary>
    public static class EnvironmentCapture
    {
        private const float T = 12.4f;

        [MenuItem("PLOP/Visual Lab/Capture Close-ups", priority = 41)]
        public static void CaptureFromMenu()
        {
            string dir = EditorUtility.SaveFolderPanel("Save environment close-ups", "Documentation/Previews", "Phase11_5");
            if (!string.IsNullOrEmpty(dir)) CaptureAll(dir);
        }

        public static void CaptureBatch()
        {
            string dir = Environment.GetEnvironmentVariable("PLOP_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath("Documentation/Previews/Phase11_5/editor");
            try
            {
                CaptureAll(dir);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static void CaptureAll(string dir)
        {
            Directory.CreateDirectory(dir);
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var catalog = WorldCatalog.Load();
                for (int i = 1; i <= VisualLab.Boards.Count; i++)
                {
                    var lab = VisualLab.Get(i);
                    var world = Find(catalog, lab.World);
                    Render(lab.Level, world, dir, "lab" + i + "-" + Slug(lab.Title) + ".png", null, 1400, T);
                }
                // Close-ups and two moments of the same pool (proves the surface animates).
                var lava = VisualLab.Get(1);
                Render(lava.Level, Find(catalog, lava.World), dir, "closeup-lava-a.png", new Rect(3.5f, 1.5f, 3f, 3f), 1200, T);
                Render(lava.Level, Find(catalog, lava.World), dir, "closeup-lava-b.png", new Rect(3.5f, 1.5f, 3f, 3f), 1200, T + 1.7f);
                var frost = VisualLab.Get(2);
                Render(frost.Level, Find(catalog, frost.World), dir, "closeup-frost.png", new Rect(1.5f, 1.5f, 4f, 4f), 1200, T);
                var loops = VisualLab.Get(4);
                Render(loops.Level, Find(catalog, loops.World), dir, "closeup-loops.png", new Rect(-0.5f, 3.5f, 3f, 3f), 1200, T);
                var breeze = VisualLab.Get(3);
                Render(breeze.Level, Find(catalog, breeze.World), dir, "closeup-breeze.png", new Rect(-0.5f, 1.5f, 4f, 4f), 1200, T);
                // Every World profile on the all-mechanics board.
                var all = VisualLab.Get(7);
                foreach (var w in catalog.Worlds) Render(all.Level, w, dir, "world-" + w.Id + ".png", null, 1000, T);
                // Living backgrounds: the real WorldBackdrop (far image + ambient layer) at phone aspect, board in front.
                foreach (var w in catalog.Worlds) RenderScene(all.Level, w, dir, "scene-" + w.Id + ".png", T);
                var meadow = Find(catalog, "breezy-meadow");
                RenderScene(all.Level, meadow, dir, "scene-breezy-meadow-later.png", T + 2.5f);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                PlopShaders.SetTime(PlopShaders.FrozenTime);
                WorldAmbient.SetTime(WorldAmbient.FrozenTime);
            }
            Debug.Log("[PLOP] Environment close-ups written to " + dir);
        }

        private static WorldTheme Find(WorldCatalog catalog, string id)
        {
            foreach (var w in catalog.Worlds) if (w.Id == id) return w;
            return catalog.Worlds.Count > 0 ? catalog.Worlds[0] : null;
        }

        /// <param name="cells">Board region to frame, in cell coordinates (x, y = lower-left cell corner); null frames the whole board.</param>
        private static void Render(LevelDefinition level, WorldTheme world, string dir, string file, Rect? cells, int width, float time)
        {
            var bg = world != null ? world.BackgroundColor : PlopPalette.PlayBackground;
            using (var stage = new CharacterStage(bg))
            {
                var board = stage.Add("Board").AddComponent<BoardView>();
                BoardStage.Apply(board, world);
                float ppu = cells.HasValue ? width / cells.Value.width : width / (level.Width + 1.2f);
                board.Build(level, level.CreateInitialState(), ShapeAtlas.ForDensity(ppu));
                PlopShaders.SetFrozen(false);
                PlopShaders.SetTime(time);
                board.Environment?.Tick(Time.unscaledTime, 0f);
                Vector2 center, size;
                if (cells.HasValue)
                {
                    var r = cells.Value;
                    center = new Vector2(r.center.x - (level.Width - 1) * 0.5f, r.center.y - (level.Height - 1) * 0.5f);
                    size = r.size;
                }
                else
                {
                    center = Vector2.zero;
                    size = new Vector2(level.Width + 1.2f, level.Height + 1.2f);
                }
                int height = Mathf.RoundToInt(width * size.y / size.x);
                var tex = stage.Capture(center, size, width, height);
                File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>Portrait phone framing (1080×2400 aspect): backdrop, ambient layer and board together.</summary>
        private static void RenderScene(LevelDefinition level, WorldTheme world, string dir, string file, float time)
        {
            if (world == null) return;
            const float aspect = 1080f / 2400f;
            const float orthoSize = 10f;
            using (var stage = new CharacterStage(world.BackgroundColor))
            {
                stage.Camera.aspect = aspect;
                stage.Camera.transform.position = new Vector3(0f, 0f, stage.Camera.transform.position.z);
                var board = stage.Add("Board").AddComponent<BoardView>();
                BoardStage.Apply(board, world);
                board.Build(level, level.CreateInitialState(), ShapeAtlas.ForDensity(1200f / (2f * orthoSize * aspect)));
                var backdrop = stage.Add("Backdrop").AddComponent<WorldBackdrop>();
                backdrop.Show(world, stage.Camera, new BoardLayout.Result { OrthographicSize = orthoSize });
                PlopShaders.SetFrozen(false);
                PlopShaders.SetTime(time);
                WorldAmbient.SetTime(time);
                board.Environment?.Tick(Time.unscaledTime, 0f);
                var tex = stage.Capture(Vector2.zero, new Vector2(2f * orthoSize * aspect, 2f * orthoSize), 540, 1200);
                File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static string Slug(string title)
        {
            int colon = title.IndexOf(':');
            string s = (colon >= 0 ? title.Substring(colon + 1) : title).Trim().ToLowerInvariant();
            return s.Replace(' ', '-');
        }
    }

    /// <summary>World surface setup shared by the capture tool and the Visual Lab window.</summary>
    internal static class BoardStage
    {
        public static void Apply(BoardView board, WorldTheme world)
        {
            if (world == null) return;
            Color key = Color.Lerp(Color.white, world.KeyLight, world.LightStrength * 0.18f);
            board.SetWorld(world);
            bool painted = BoardMaterials.For(world) != null;
            board.SetPalette((painted ? Color.white : world.TrayTint) * key, world.TileTint * key, painted ? key : world.PebbleTint);
        }
    }
}
