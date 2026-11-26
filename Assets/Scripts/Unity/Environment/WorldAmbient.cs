using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>Which animated layer set a World's background uses (see PlopAmbient.shader).</summary>
    public enum AmbientMode : byte { Motes, Snow, Meadow, Embers, Prism, Garden, Rain, Attic, Moonlit }

    public enum AmbientShape : byte { Dot, Star, Streak, Bokeh, Seed, Flake, Drop }

    /// <summary>A set of identical GPU-animated particles (see PlopAmbientParticles.shader).</summary>
    [Serializable]
    public sealed class AmbientParticleSet
    {
        public string shape = "dot";
        public int count = 20;
        public float size = 6f, vx, vy, sway, alpha = 0.5f, twinkle;
        public int tint;
        public float[] band = { 0f, 1f };
        [NonSerialized] public AmbientShape Shape;
    }

    /// <summary>A rectangle of the backdrop (pixels, 576×1024, y down) where region props are drawn.</summary>
    [Serializable]
    public sealed class AmbientRegion { public float x, y, w, h; }

    /// <summary>One World's living-background settings, from Resources/Worlds/Ambient.json.</summary>
    [Serializable]
    public sealed class WorldAmbientProfile
    {
        public string world = "";
        public string mode = "motes";
        public string tint = "#FFF6E6", tint2 = "#FFFFFF";
        public float speed = 1f, density = 1f;
        public AmbientRegion[] regions = Array.Empty<AmbientRegion>();
        public AmbientParticleSet[] particles = Array.Empty<AmbientParticleSet>();

        public int ParticleCount { get { int n = 0; foreach (var p in particles) n += Mathf.Max(0, p.count); return n; } }

        [NonSerialized] public AmbientMode Mode;
        [NonSerialized] public Color Tint, Tint2;

        internal void Resolve()
        {
            Mode = Enum.TryParse(mode, true, out AmbientMode m) ? m : AmbientMode.Motes;
            Tint = ColorUtility.TryParseHtmlString(tint, out var a) ? a : Color.white;
            Tint2 = ColorUtility.TryParseHtmlString(tint2, out var b) ? b : Color.white;
            speed = Mathf.Clamp(speed, 0f, 3f);
            density = Mathf.Clamp01(density);
            regions ??= Array.Empty<AmbientRegion>();
            particles ??= Array.Empty<AmbientParticleSet>();
            foreach (var p in particles)
            {
                p.Shape = Enum.TryParse(p.shape, true, out AmbientShape sh) ? sh : AmbientShape.Dot;
                p.count = Mathf.Clamp(p.count, 0, WorldAmbient.MaxParticlesPerSet);
                if (p.band == null || p.band.Length < 2) p.band = new[] { 0f, 1f };
            }
        }
    }

    /// <summary>
    /// Living-World ambient clock and settings. One clock for the single active background (no per-object
    /// Update); it only advances while the background is visible, never under Reduced Motion, and more slowly
    /// with fewer particles in Battery Saver.
    /// </summary>
    public static class WorldAmbient
    {
        public const string Resource = "Worlds/Ambient";
        public const float FrozenTime = 7.35f;
        /// <summary>Wrap period: 3600 s keeps the windmill sails (20°/s, 26°/s) seamless at the wrap.</summary>
        public const float Wrap = 3600f;
        public const float SaverDensity = 0.55f, SaverSpeed = 0.7f;
        public const int MaxParticlesPerSet = 64;
        public static readonly Vector2 ArtPixels = new Vector2(576f, 1024f);

        private static readonly int AmbientId = Shader.PropertyToID("_PlopAmbient");
        private static readonly int TintId = Shader.PropertyToID("_PlopAmbientTint");
        private static readonly int Tint2Id = Shader.PropertyToID("_PlopAmbientTint2");

        [Serializable] private sealed class Set { public WorldAmbientProfile[] profiles = Array.Empty<WorldAmbientProfile>(); }
        private static Dictionary<string, WorldAmbientProfile> _byWorld;

        public static float Time { get; private set; } = FrozenTime;
        /// <summary>Layer opacity (the backdrop's short entry dissolve).</summary>
        public static float Opacity { get; private set; } = 1f;
        public static void SetOpacity(float opacity) { Opacity = Mathf.Clamp01(opacity); Publish(); }
        public static float Density { get; private set; } = 1f;
        public static bool Moving { get; private set; }

        public static IReadOnlyDictionary<string, WorldAmbientProfile> All { get { Load(); return _byWorld; } }

        public static WorldAmbientProfile For(string worldId)
        {
            Load();
            return worldId != null && _byWorld.TryGetValue(worldId, out var p) ? p : null;
        }

        /// <summary>Publishes a profile's colours and the quality settings to the shader.</summary>
        public static void Apply(WorldAmbientProfile profile, bool batterySaver, bool reducedMotion)
        {
            if (profile != null)
            {
                Shader.SetGlobalVector(TintId, profile.Tint);
                Shader.SetGlobalVector(Tint2Id, profile.Tint2);
            }
            Density = (profile?.density ?? 1f) * (batterySaver ? SaverDensity : 1f);
            Moving = !reducedMotion;
            if (reducedMotion) Time = FrozenTime;
            Publish();
        }

        /// <summary>Advance the ambient clock; called once per frame by the visible background only.</summary>
        public static void Advance(float deltaTime, float speed)
        {
            if (!Moving) return;
            Time += Mathf.Clamp(deltaTime, 0f, 0.1f) * speed;
            if (Time >= Wrap) Time -= Wrap;
            Publish();
        }

        /// <summary>Tools and tests: show a specific moment.</summary>
        public static void SetTime(float time)
        {
            Time = time;
            Publish();
        }

        private static void Publish() => Shader.SetGlobalVector(AmbientId, new Vector4(Time, Density, Moving ? 1f : 0f, Opacity));

        /// <summary>
        /// Builds the static particle mesh for a profile: one quad per particle with its seed data in UV channels.
        /// Rank (uv3.w) is spread evenly so Battery Saver hides a uniform fraction of every set.
        /// </summary>
        public static Mesh BuildParticleMesh(WorldAmbientProfile profile)
        {
            int n = profile?.ParticleCount ?? 0;
            if (n == 0) return null;
            var verts = new Vector3[n * 4];
            var uv0 = new List<Vector4>(n * 4); var uv1 = new List<Vector4>(n * 4);
            var uv2 = new List<Vector4>(n * 4); var uv3 = new List<Vector4>(n * 4);
            var tris = new int[n * 6];
            var rng = new System.Random(profile.world.GetHashCode() & 0x7FFFFFFF);
            var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
            int q = 0;
            foreach (var set in profile.particles)
            {
                for (int k = 0; k < set.count; k++, q++)
                {
                    // Stratified spawn: even coverage without clumps.
                    float sx = ((k + (float)rng.NextDouble()) / set.count + (float)rng.NextDouble() * 0.37f) % 1f;
                    float sy = (float)rng.NextDouble();
                    float size = set.size * (0.8f + 0.4f * (float)rng.NextDouble());
                    float phase = (float)rng.NextDouble();
                    float rank = (k + 0.5f) / set.count;
                    for (int c = 0; c < 4; c++)
                    {
                        verts[q * 4 + c] = Vector3.zero;
                        uv0.Add(new Vector4(corners[c].x, corners[c].y, sx, sy));
                        uv1.Add(new Vector4(size, set.alpha, set.vx, set.vy));
                        uv2.Add(new Vector4(set.sway, phase, (float)set.Shape, set.tint));
                        uv3.Add(new Vector4(set.band[0], set.band[1], set.twinkle, rank));
                    }
                    int v = q * 4, t = q * 6;
                    tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                    tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
                }
            }
            var mesh = new Mesh { name = "PLOP ambient " + profile.world, hideFlags = HideFlags.DontSave };
            mesh.vertices = verts;
            mesh.SetUVs(0, uv0); mesh.SetUVs(1, uv1); mesh.SetUVs(2, uv2); mesh.SetUVs(3, uv3);
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e4f, 1e4f, 10f));   // positions come from the shader
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static void Load()
        {
            if (_byWorld != null) return;
            _byWorld = new Dictionary<string, WorldAmbientProfile>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null) return;
            var set = JsonUtility.FromJson<Set>(asset.text);
            foreach (var p in set?.profiles ?? Array.Empty<WorldAmbientProfile>())
            {
                if (string.IsNullOrEmpty(p.world)) continue;
                p.Resolve();
                _byWorld[p.world] = p;
            }
        }
    }
}
