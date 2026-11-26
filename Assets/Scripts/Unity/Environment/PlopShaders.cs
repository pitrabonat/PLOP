using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Board shaders: Lava and Frost (Phase 11.6: built from the supplied art tiles, see <see cref="ArtPack"/>),
    /// Breeze wind and the living-World layers. One shared material per shader, one unit-quad sprite for every
    /// instance, and a single global clock. Per-renderer data is packed into the SpriteRenderer colour
    /// (<see cref="SurfaceConnectivity"/>), so instances never need their own material. If a shader or the art
    /// pack is missing or unsupported, <see cref="Available"/> is false and the board falls back to the baked
    /// atlas sprites.
    /// </summary>
    public static class PlopShaders
    {
        private static readonly int TimeId = Shader.PropertyToID("_PlopTime");
        private static readonly int BoardId = Shader.PropertyToID("_PlopBoard");
        private static readonly int WindTintId = Shader.PropertyToID("_PlopWindTint");
        private static readonly int ClearId = Shader.PropertyToID("_PlopClear");

        /// <summary>Reduced Motion shows this frozen moment: no bubbles mid-pop, glint off-surface.</summary>
        public const float FrozenTime = 7.35f;
        private const float Wrap = 600f;

        private static Material _lava, _frost, _wind, _ambient, _ambientParticles;
        private static Sprite _quad;
        private static bool _loaded, _available;
        private static float _time = FrozenTime;

        public static bool Frozen { get; private set; }
        /// <summary>Battery Saver slows ambient shader motion (fewer visible changes per second).</summary>
        public static float Speed { get; set; } = 1f;
        public static float Time => _time;

        public static bool Available { get { Load(); return _available; } }
        public static Material Lava { get { Load(); return _lava; } }
        public static Material Frost { get { Load(); return _frost; } }
        public static Material Wind { get { Load(); return _wind; } }
        /// <summary>Living-World background layer (independent of <see cref="Available"/>; null if unsupported).</summary>
        public static Material Ambient { get { Load(); return _ambient; } }
        public static Material AmbientParticles { get { Load(); return _ambientParticles; } }

        /// <summary>A 1×1 world-unit quad with UVs 0..1, pivot at the centre.</summary>
        public static Sprite Quad
        {
            get
            {
                if (_quad != null) return _quad;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "PLOP unit quad", wrapMode = TextureWrapMode.Clamp };
                var white = new Color32[16];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(white);
                tex.Apply(false, true);
                _quad = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
                _quad.name = "PLOP unit quad";
                return _quad;
            }
        }

        private static void Load()
        {
            if (_loaded && (_lava != null || !_available)) return;
            _loaded = true;
            var art = ArtPack.Asset;
            _lava = FromArt(art != null ? art.lavaMaterial : null, ArtPack.Lava);
            _frost = FromArt(art != null ? art.frostMaterial : null, ArtPack.Frost);
            _wind = Make("Shaders/PlopWind");
            _ambient = Make("Shaders/PlopAmbient");
            _ambientParticles = Make("Shaders/PlopAmbientParticles");
            _available = _lava != null && _frost != null && _wind != null && ArtPack.Ready;
            SetWindTint(DefaultWindTint);
            Shader.SetGlobalFloat(TimeId, _time);
        }

        private static Material Make(string resource)
        {
            var shader = Resources.Load<Shader>(resource);
            if (shader == null || !shader.isSupported) return null;
            return new Material(shader) { name = shader.name, hideFlags = HideFlags.DontSave };
        }

        /// <summary>Runtime copy of an art material, with the art's opaque box (content rect) filled in.</summary>
        private static Material FromArt(Material source, ArtImage art)
        {
            if (source == null || art == null || source.shader == null || !source.shader.isSupported) return null;
            var m = new Material(source) { name = source.name + " (runtime)", hideFlags = HideFlags.DontSave };
            var c = art.Content;
            m.SetVector(ArtRectId, new Vector4((c.x + c.width * 0.5f) / art.Width, (c.y + c.height * 0.5f) / art.Height,
                c.width / (float)art.Width, c.height / (float)art.Height));
            return m;
        }

        private static readonly int ArtRectId = Shader.PropertyToID("_ArtRect");

        public static readonly Color DefaultWindTint = new Color(0.47f, 0.78f, 0.72f, 0.95f);

        /// <summary>Loop pair colours; pair identity also uses the pip count on each frame.</summary>
        public static readonly Color[] DefaultPortalTints =
        {
            PlopPalette.Hex(0x9C8FE0), PlopPalette.Hex(0x6FC9B0), PlopPalette.Hex(0xF2A585),
            PlopPalette.Hex(0x86B8EA), PlopPalette.Hex(0xE6B064), PlopPalette.Hex(0xE592B4)
        };

        // Vectors are passed as-is (sRGB, like the palette); the shaders convert to linear once.
        public static void SetWindTint(Color tint) => Shader.SetGlobalVector(WindTintId, tint);

        /// <summary>
        /// Renderer colours are converted sRGB → linear by Unity in a Linear project before they reach the shader.
        /// Packed data bytes are pre-compensated here so the shader reads back the exact byte.
        /// </summary>
        public static Color Pack(Color data) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? new Color(Mathf.LinearToGammaSpace(data.r), Mathf.LinearToGammaSpace(data.g), Mathf.LinearToGammaSpace(data.b), data.a) : data;

        /// <summary>Inverse of <see cref="Pack"/> (tests and tools read renderer colours back).</summary>
        public static Color Unpack(Color stored) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? new Color(Mathf.GammaToLinearSpace(stored.r), Mathf.GammaToLinearSpace(stored.g), Mathf.GammaToLinearSpace(stored.b), stored.a) : stored;

        /// <summary>Board space for world-continuous noise: cell (0,0) centre and cells per world unit.</summary>
        public static void SetBoard(Vector3 cellZeroWorld, float cellsPerUnit) =>
            Shader.SetGlobalVector(BoardId, new Vector4(cellZeroWorld.x, cellZeroWorld.y, cellsPerUnit, 0f));

        /// <summary>Board rectangle in board cells (incl. tray) that ambient background motion keeps clear.</summary>
        public static void SetClear(Vector4 cellsMinMax) => Shader.SetGlobalVector(ClearId, cellsMinMax);

        public static void SetFrozen(bool frozen)
        {
            Frozen = frozen;
            if (frozen) _time = FrozenTime;
            Shader.SetGlobalFloat(TimeId, _time);
        }

        /// <summary>Advance the shared clock (once per frame, by whichever view is visible).</summary>
        public static void Advance(float deltaTime)
        {
            if (Frozen) return;
            _time += Mathf.Clamp(deltaTime, 0f, 0.1f) * Speed;
            if (_time > Wrap) _time -= Wrap;
            Shader.SetGlobalFloat(TimeId, _time);
        }

        /// <summary>Tools and tests: show a specific moment.</summary>
        public static void SetTime(float time)
        {
            _time = time;
            Shader.SetGlobalFloat(TimeId, _time);
        }
    }
}
