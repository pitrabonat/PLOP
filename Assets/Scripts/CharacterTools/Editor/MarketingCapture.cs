using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Plop.Presentation;
using UnityEditor;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>
    /// Marketing (Sprint 03.1): renders scripted performances of the runtime Plop rig as transparent PNG frame
    /// sequences for the trailer compositor. Each frame is rendered on black and on white; the difference gives exact
    /// straight alpha. Uses the shipped PlopView code (expressions, blinks, hops, slides, warps, sleep), no new art.
    /// Batch: -executeMethod Plop.CharacterTools.MarketingCapture.RunBatch with PLOP_MARKETING_DIR set.
    /// Editor-only; nothing here ships in a player or touches a scene.
    /// </summary>
    public static class MarketingCapture
    {
        private const int Fps = 30;
        private const float R0 = 1000f; // rig clock origin

        private sealed class Actor
        {
            public PlopView View;
            public CharacterLook Look;
            public int Color;
            public Vector2 Home;
            public bool Nest;
            public GameObject NestGo;
        }

        private sealed class Seq
        {
            public string Name;
            public float Duration;
            public int Ppu;
            public Vector2 Size;           // capture size in cells
            public Vector2 Center;         // capture centre (world)
            public int FollowActor = -1;   // camera follows this actor's root (x only)
            public Func<float, float> Clock = t => R0 + t;
            public List<Actor> Actors = new List<Actor>();
            public List<(float t, Action<float> act)> Events = new List<(float, Action<float>)>();
            public Action<float, Seq> PerFrame;
        }

        [MenuItem("PLOP/Marketing/Render Rig Performances", priority = 40)]
        public static void RunFromMenu() => RunAll(Path.GetFullPath("../../Marketing/FinalSprint03_1/01_CinematicSources/RigRenders"), null);

        public static void RunBatch()
        {
            string dir = Environment.GetEnvironmentVariable("PLOP_MARKETING_DIR");
            string only = Environment.GetEnvironmentVariable("PLOP_MARKETING_ONLY");
            try
            {
                RunAll(dir, string.IsNullOrEmpty(only) ? null : new HashSet<string>(only.Split(',')));
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ reflection helpers (timing control only)

        private static readonly FieldInfo NextBlink = typeof(PlopView).GetField("_nextBlink", BindingFlags.NonPublic | BindingFlags.Instance);

        private static void BlinkAt(PlopView v, float rigTime) => NextBlink.SetValue(v, rigTime);

        // ------------------------------------------------------------------ performances

        private static CharacterLook D => CharacterLook.Default;

        private static IEnumerable<Seq> Sequences()
        {
            // Shot 02: hero wakes up (eyes open slowly, blink, smile, hop). Reverse-sampled sleep transition = waking.
            {
                var s = new Seq { Name = "hero_wake", Duration = 3.6f, Ppu = 720, Size = new Vector2(1.5f, 1.5f), Center = new Vector2(0f, 0.18f) };
                var a = new Actor { Color = 0, Look = D };
                s.Actors.Add(a);
                const float wake0 = 0.45f, wake1 = 1.35f;
                s.Clock = t =>
                {
                    if (t < wake0) return R0 - 5f + t;                                           // frozen sleep pose
                    if (t < wake1) return R0 + 0.37f * (1f - Smooth((t - wake0) / (wake1 - wake0))); // reversed sleep
                    return R0 + (t - wake1);
                };
                s.Events.Add((0f, now => a.View.Snap(Vector3.zero, true)));
                s.Events.Add((wake0, now => { a.View.Snap(Vector3.zero, false); a.View.QueueSleep(R0); }));
                s.Events.Add((wake1, now => { a.View.Snap(Vector3.zero, false); BlinkAt(a.View, now + 0.42f); }));
                s.Events.Add((2.25f, now => { a.View.SetExpression(Expression.Happy); BlinkAt(a.View, now + 9f); }));
                s.Events.Add((2.45f, now => { a.View.ClearExpression(); a.View.React(Reaction.Bounce, now); }));
                s.Events.Add((3.1f, now => a.View.SetExpression(Expression.Happy)));
                yield return s;
            }
            // Shot 02 background: a mint friend notices and answers with a hop.
            {
                var s = new Seq { Name = "mint_react", Duration = 3.6f, Ppu = 400, Size = new Vector2(1.5f, 1.5f), Center = new Vector2(0f, 0.18f) };
                var a = new Actor { Color = 1, Look = D };
                s.Actors.Add(a);
                s.Events.Add((0f, now => { a.View.Snap(Vector3.zero, false); BlinkAt(a.View, now + 0.9f); }));
                s.Events.Add((1.55f, now => a.View.React(Reaction.Wobble, now)));
                s.Events.Add((2.75f, now => a.View.React(Reaction.Bounce, now)));
                yield return s;
            }
            // Shot 03-04: wind surprise, slide across the ice, warp into the portal. Camera follows (x).
            {
                var s = new Seq { Name = "hero_adventure", Duration = 3.4f, Ppu = 420, Size = new Vector2(1.6f, 1.5f), Center = new Vector2(0f, 0.18f), FollowActor = 0 };
                var a = new Actor { Color = 0, Look = D };
                s.Actors.Add(a);
                s.Events.Add((0f, now => { a.View.Snap(Vector3.zero, false); BlinkAt(a.View, now + 0.3f); }));
                s.Events.Add((0.5f, now => a.View.React(Reaction.Surprised, now)));
                s.Events.Add((0.95f, now => { a.View.SetExpression(Expression.Excited); a.View.StartMove(new Vector3(7f, 0f, 0f), Vector2.right, now, 1.5f, false); }));
                s.Events.Add((2.5f, now => { a.View.SetExpression(Expression.Surprised); a.View.StartWarp(new Vector3(7.4f, 0f, 0f), now, 1.6f); }));
                yield return s;
            }
            // Shot 09: four Plops in pillow Nests grow sleepy and fall asleep in turn (slowed 0.5x through the settle).
            foreach (var (name, color, sleepyAt, hopIn) in new[] { ("sleep_pink", 0, 1.55f, true), ("sleep_mint", 1, 0.55f, false), ("sleep_honey", 2, 0.85f, false), ("sleep_lilac", 3, 1.15f, false) })
            {
                var s = new Seq { Name = name, Duration = 4.4f, Ppu = 420, Size = new Vector2(hopIn ? 2.3f : 1.3f, 1.45f), Center = new Vector2(hopIn ? -0.5f : 0f, 0.12f) };
                var a = new Actor { Color = color, Look = D, Nest = true };
                s.Actors.Add(a);
                float sleepAt = sleepyAt + 0.75f;
                // slow-motion through the sleep settle so the eyes close gently (0.38 s in game -> ~0.95 s here)
                s.Clock = t => t < sleepAt ? R0 + t : R0 + sleepAt + Mathf.Min(t - sleepAt, 0.95f) * 0.4f + Mathf.Max(0f, t - sleepAt - 0.95f);
                s.Events.Add((0f, now =>
                {
                    a.View.Snap(hopIn ? new Vector3(-1f, 0f, 0f) : Vector3.zero, false);
                    BlinkAt(a.View, now + 0.35f + color * 0.2f);
                }));
                if (hopIn) s.Events.Add((0.2f, now => a.View.StartMove(Vector3.zero, Vector2.right, now, 0.42f, true)));
                s.Events.Add((sleepyAt, now => { a.View.SetExpression(Expression.Sleepy); BlinkAt(a.View, now + 99f); }));
                s.Events.Add((sleepAt, now => { a.View.ClearExpression(); a.View.QueueSleep(now); }));
                yield return s;
            }
            // Shot 08: the same Plop trying looks, one per beat (90 BPM). Every look renders the full clip; the editor cuts.
            foreach (var acc in new[] { "none", "bow", "flower", "crown", "nightcap", "star" })
            {
                var look = D.With(accessory: CharacterArt.Accessories[acc]);
                var s = new Seq { Name = "look_" + acc, Duration = 3.4f, Ppu = 720, Size = new Vector2(1.5f, 1.5f), Center = new Vector2(0f, 0.2f) };
                var a = new Actor { Color = 0, Look = look };
                s.Actors.Add(a);
                s.Events.Add((0f, now => { a.View.Snap(Vector3.zero, false); BlinkAt(a.View, now + 1.05f); }));
                // a small happy squash on every beat; the editor shows each look from its own beat onwards
                for (int k = 0; k < 5; k++) { float tk = k * 0.6667f; s.Events.Add((tk, now => a.View.React(Reaction.Squash, now))); }
                s.Events.Add((2.9f, now => BlinkAt(a.View, now)));
                yield return s;
            }
            // Short C: hero hops into its Nest, gets sleepy, sleeps.
            {
                var s = new Seq { Name = "hero_nest", Duration = 4.2f, Ppu = 600, Size = new Vector2(2.3f, 1.5f), Center = new Vector2(-0.5f, 0.15f) };
                var a = new Actor { Color = 0, Look = D, Nest = true };
                s.Actors.Add(a);
                s.Clock = t => t < 2.4f ? R0 + t : R0 + 2.4f + Mathf.Min(t - 2.4f, 0.95f) * 0.4f + Mathf.Max(0f, t - 3.35f);
                s.Events.Add((0f, now => { a.View.Snap(new Vector3(-1f, 0f, 0f), false); BlinkAt(a.View, now + 0.2f); }));
                s.Events.Add((0.45f, now => a.View.StartMove(Vector3.zero, Vector2.right, now, 0.42f, true)));
                s.Events.Add((1.0f, now => a.View.React(Reaction.HappyBlink, now)));
                s.Events.Add((1.6f, now => { a.View.SetExpression(Expression.Sleepy); BlinkAt(a.View, now + 99f); }));
                s.Events.Add((2.4f, now => { a.View.ClearExpression(); a.View.QueueSleep(now); }));
                yield return s;
            }
            // Stills for thumbnails and covers (single frames, large).
            foreach (var (name, color, expr) in new[] { ("still_pink_excited", 0, Expression.Excited), ("still_pink_happy", 0, Expression.Happy), ("still_mint_curious", 1, Expression.Curious), ("still_honey_happy", 2, Expression.Happy), ("still_lilac_surprised", 3, Expression.Surprised) })
            {
                var s = new Seq { Name = name, Duration = 1f / Fps, Ppu = 1200, Size = new Vector2(1.3f, 1.2f), Center = new Vector2(0f, 0.1f) };
                var a = new Actor { Color = color, Look = D };
                s.Actors.Add(a);
                s.Events.Add((0f, now => { a.View.Snap(Vector3.zero, false); BlinkAt(a.View, now + 99f); a.View.SetExpression(expr); }));
                yield return s;
            }
            {
                var s = new Seq { Name = "still_pink_asleep_nest", Duration = 1f / Fps, Ppu = 1200, Size = new Vector2(1.3f, 1.2f), Center = new Vector2(0f, 0.08f) };
                var a = new Actor { Color = 0, Look = D, Nest = true };
                s.Actors.Add(a);
                s.Events.Add((0f, now => a.View.Snap(Vector3.zero, true)));
                yield return s;
            }
        }

        // ------------------------------------------------------------------ renderer

        public static void RunAll(string dir, HashSet<string> only)
        {
            if (string.IsNullOrEmpty(dir)) throw new ArgumentException("PLOP_MARKETING_DIR not set");
            Directory.CreateDirectory(dir);
            var atlases = new Dictionary<string, ShapeAtlas>();
            foreach (var seq in Sequences())
            {
                if (only != null && !only.Contains(seq.Name)) continue;
                Render(seq, dir, atlases);
            }
            foreach (var a in atlases.Values) a.Dispose();
            Debug.Log("[PLOP] Marketing rig performances written to " + dir);
        }

        /// <summary>
        /// The game bakes face parts at most 192 px per cell (plenty on a phone board). For close-ups this repaints the
        /// same face painters (the game's own SDF code, via reflection) at the capture density and swaps the sprites in
        /// this tool's private atlas. Shapes and colours are identical; only the raster resolution changes.
        /// </summary>
        private static void SharpenFace(ShapeAtlas atlas, CharacterLook look, int ppu)
        {
            var t = typeof(ShapeAtlas);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var specType = t.GetNestedType("Spec", BindingFlags.NonPublic);
            var host = t.GetConstructor(flags, null, new[] { typeof(int) }, null).Invoke(new object[] { ppu });
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(specType));
            t.GetMethod("AddFace", flags).Invoke(host, new object[] { list, look.Face, "" });
            var sprites = (Dictionary<string, Sprite>)t.GetField("_sprites", flags).GetValue(atlas);
            foreach (var spec in list)
            {
                string key = (string)specType.GetField("Key").GetValue(spec);
                int w0 = (int)specType.GetField("W").GetValue(spec), h0 = (int)specType.GetField("H").GetValue(spec);
                int d0 = (int)specType.GetField("Density").GetValue(spec);
                float x0 = (float)specType.GetField("X0").GetValue(spec), y0 = (float)specType.GetField("Y0").GetValue(spec);
                var paint = (Delegate)specType.GetField("Paint").GetValue(spec);
                if (d0 >= ppu) continue;
                int w = Mathf.CeilToInt(w0 * (float)ppu / d0), h = Mathf.CeilToInt(h0 * (float)ppu / d0);
                float pixel = 1f / ppu;
                var px = new Color32[w * h];
                var args = new object[3];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    args[0] = x0 + (x + 0.5f) * pixel; args[1] = y0 + (y + 0.5f) * pixel; args[2] = pixel;
                    px[y * w + x] = (Color)paint.DynamicInvoke(args);
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(px);
                tex.Apply();
                var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(-x0 * ppu / w, -y0 * ppu / h), ppu, 0, SpriteMeshType.FullRect);
                sprite.name = key + "@" + ppu;
                sprites[key] = sprite;
            }
        }

        private static void Render(Seq seq, string root, Dictionary<string, ShapeAtlas> atlases)
        {
            string dir = Path.Combine(root, seq.Name);
            Directory.CreateDirectory(dir);
            int w = Mathf.RoundToInt(seq.Size.x * seq.Ppu), h = Mathf.RoundToInt(seq.Size.y * seq.Ppu);
            var meta = new StringBuilder();
            meta.Append("{\"name\":\"").Append(seq.Name).Append("\",\"fps\":").Append(Fps).Append(",\"ppu\":").Append(seq.Ppu)
                .Append(",\"width\":").Append(w).Append(",\"height\":").Append(h).Append(",\"groundY\":-0.345,\"frames\":[");
            using (var stage = new CharacterStage(Color.black))
            {
                foreach (var a in seq.Actors)
                {
                    string key = seq.Ppu + "|" + a.Look.Key;
                    if (!atlases.TryGetValue(key, out var atlas))
                    {
                        atlases[key] = atlas = ShapeAtlas.Build(seq.Ppu, a.Look, ShapeAtlas.Content.Characters | ShapeAtlas.Content.Nests, 0, 1, 2, 3, 4, 5);
                        SharpenFace(atlas, a.Look, seq.Ppu);
                    }
                    if (a.Nest)
                    {
                        a.NestGo = stage.Add("Nest");
                        var r = a.NestGo.AddComponent<SpriteRenderer>();
                        r.sprite = atlas.Get(ShapeAtlas.NestKey(a.Color));
                        r.sortingOrder = -20;
                    }
                    var holder = stage.Add("Plop");
                    a.View = new PlopView(holder.transform, atlas, a.Color, 7 + a.Color, false);
                    a.View.SetSortingBase(0);
                }
                int frames = Mathf.Max(1, Mathf.RoundToInt(seq.Duration * Fps));
                int next = 0;
                seq.Events.Sort((x, y) => x.t.CompareTo(y.t));
                for (int f = 0; f < frames; f++)
                {
                    float t = f / (float)Fps;
                    float now = seq.Clock(t);
                    while (next < seq.Events.Count && seq.Events[next].t <= t + 1e-4f) { seq.Events[next].act(now); next++; }
                    foreach (var a in seq.Actors) a.View.Tick(now);
                    Vector2 center = seq.Center;
                    Vector3 rootPos = seq.Actors[0].View.Root.localPosition;
                    if (seq.FollowActor >= 0) center.x += seq.Actors[seq.FollowActor].View.Root.localPosition.x;
                    var tex = CaptureAlpha(stage, center, seq.Size, w, h);
                    File.WriteAllBytes(Path.Combine(dir, $"f_{f:0000}.png"), tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);
                    if (f > 0) meta.Append(',');
                    meta.Append("{\"t\":").Append(t.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture))
                        .Append(",\"root\":[").Append(F(rootPos.x)).Append(',').Append(F(rootPos.y)).Append("],\"center\":[")
                        .Append(F(center.x)).Append(',').Append(F(center.y)).Append("],\"expr\":\"").Append(seq.Actors[0].View.CurrentExpression)
                        .Append("\",\"asleep\":").Append(seq.Actors[0].View.IsAsleep ? "true" : "false").Append('}');
                }
            }
            meta.Append("]}");
            File.WriteAllText(Path.Combine(dir, "meta.json"), meta.ToString());
            Debug.Log($"[PLOP] {seq.Name}: {Mathf.RoundToInt(seq.Duration * Fps)} frames {w}x{h}");
        }

        private static string F(float v) => v.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Renders on black and on white and solves for straight colour and alpha.</summary>
        private static Texture2D CaptureAlpha(CharacterStage stage, Vector2 center, Vector2 size, int w, int h)
        {
            stage.Camera.backgroundColor = Color.black;
            var black = stage.Capture(center, size, w, h);
            stage.Camera.backgroundColor = Color.white;
            var white = stage.Capture(center, size, w, h);
            var b = black.GetPixels32();
            var wh = white.GetPixels32();
            var o = new Color32[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                int d = ((wh[i].r - b[i].r) + (wh[i].g - b[i].g) + (wh[i].b - b[i].b)) / 3;
                int a = Mathf.Clamp(255 - d, 0, 255);
                if (a == 0) { o[i] = new Color32(0, 0, 0, 0); continue; }
                o[i] = new Color32((byte)Mathf.Min(255, b[i].r * 255 / a), (byte)Mathf.Min(255, b[i].g * 255 / a), (byte)Mathf.Min(255, b[i].b * 255 / a), (byte)a);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(o);
            tex.Apply();
            UnityEngine.Object.DestroyImmediate(black);
            UnityEngine.Object.DestroyImmediate(white);
            return tex;
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
