using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;

namespace Plop.Tests.PlayMode
{
    /// <summary>
    /// Phase 11.6 custom art pack: the supplied sprites are imported correctly, become the board's actual
    /// art (tiles, pebbles, Nests, Lava, Frost, fans, portals), stay aligned to the logical grid, and animate
    /// only through presentation (never the rules).
    /// </summary>
    public class Phase11_6ArtPackTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            PlopShaders.SetFrozen(false);
        }

        private BoardView Board(LevelDefinition level, float ppu = 96f)
        {
            var go = new GameObject("Phase11.6 board");
            _made.Add(go);
            var board = go.AddComponent<BoardView>();
            board.Build(level, level.CreateInitialState(), ShapeAtlas.ForDensity(ppu));
            return board;
        }

        [Test]
        public void ArtPack_LoadsEverySuppliedBoardSprite()
        {
            Assert.IsTrue(ArtPack.Ready, "Resources/ArtPack/Phase11_6 with readable sprites");
            var a = ArtPack.Asset;
            foreach (var s in new[] { a.floor, a.pebble, a.lava, a.frost, a.fanHousing, a.fanBlades, a.portalFrame, a.portalVortex })
            {
                Assert.IsNotNull(s);
                Assert.AreEqual(320, s.texture.width);
                Assert.IsTrue(s.texture.isReadable, s.name);
                Assert.AreEqual(1, s.texture.mipmapCount, s.name + ": screen-space sprites have no mipmaps");
            }
            Assert.AreEqual(4, a.nests.Length, "Berry, Mint, Honey, Lilac");
            Assert.AreSame(a.lava.texture, a.lavaMaterial.GetTexture("_ArtTex"));
            Assert.AreSame(a.frost.texture, a.frostMaterial.GetTexture("_ArtTex"));
        }

        [Test]
        public void ArtPack_SpritesAreCentredOnTheirRealAnchors()
        {
            Assert.IsTrue(ArtPack.Ready);
            foreach (var art in new[] { ArtPack.Floor, ArtPack.Pebble, ArtPack.Lava, ArtPack.Frost, ArtPack.PortalVortex })
            {
                float cx = art.Content.x + art.Content.width * 0.5f, cy = art.Content.y + art.Content.height * 0.5f;
                Assert.AreEqual(art.CenterX, cx, 1.01f, art.Name + " box centred (x)");
                Assert.AreEqual(art.CenterY, cy, 1.01f, art.Name + " box centred (y)");
            }
            // Rings are centred on their hole, so the blades / vortex turn on the right axis.
            Assert.Greater(ArtPack.FanHousing.HoleRadius, 40f);
            Assert.Greater(ArtPack.PortalFrame.HoleRadius, 40f);
            Assert.AreEqual(0f, ArtPack.FanBlades.HoleRadius, 0.5f, "the hub sits on the pivot");
            Assert.Less(ArtPack.BladeRadiusCells, 0.5f);
            Assert.Less(ArtPack.VortexRadiusCells, 0.5f);
        }

        [Test]
        public void Atlas_DrawsTheArt_AndFallsBackWithoutIt()
        {
            var atlas = ShapeAtlas.Build(64, CharacterLook.Default, ShapeAtlas.Content.All);
            Assert.IsTrue(atlas.Has(ShapeAtlas.PortalVortexKey(0)), "separate vortex sprite");
            Assert.IsTrue(atlas.Has(ShapeAtlas.FanChevronKey));
            atlas.Dispose();

            ArtPack.ForceProcedural = true;
            try
            {
                var fallback = ShapeAtlas.Build(64, CharacterLook.Default, ShapeAtlas.Content.All);
                Assert.IsFalse(fallback.Has(ShapeAtlas.PortalVortexKey(0)), "procedural Phase 11.5 portal");
                Assert.IsTrue(fallback.Has("tile") && fallback.Has(ShapeAtlas.NestKey(0)));
                fallback.Dispose();
            }
            finally { ArtPack.ForceProcedural = false; }
        }

        [Test]
        public void Nest_IsReDyedToEveryPaletteSlot()
        {
            Assert.IsTrue(ArtPack.Ready);
            for (int c = 0; c < PlopPalette.SlotCount; c++)
            {
                var src = ArtPack.NestFor(PlopPalette.NestOuter(c));
                Assert.IsNotNull(src);
            }
            // The four supplied cushions are picked for their own colours.
            Assert.AreSame(ArtPack.Nests[0], ArtPack.NestFor(PlopPalette.NestOuter(0)), "Berry");
            Assert.AreSame(ArtPack.Nests[1], ArtPack.NestFor(PlopPalette.NestOuter(1)), "Mint");
            Assert.AreSame(ArtPack.Nests[2], ArtPack.NestFor(PlopPalette.NestOuter(2)), "Honey");
            Assert.AreSame(ArtPack.Nests[3], ArtPack.NestFor(PlopPalette.NestOuter(3)), "Lilac");
            // Re-dyeing keeps neutral details (the ivory crescent) and moves the dyed fabric.
            var ivory = new Color(0.96f, 0.94f, 0.90f, 1f);
            Assert.AreEqual(ivory, ArtPack.Redye(ivory, ArtPack.Nests[0].DyeHsv, new Vector3(0.55f, 0.5f, 0.8f)));
        }

        [Test]
        public void Surfaces_UseTheArtMaterials()
        {
            Assume.That(PlopShaders.Available);
            var level = VisualLab.Get(1).Level;
            var board = Board(level);
            var lava = level.Tiles.First(t => t.Kind == TileKind.Lava).Position;
            var r = board.Environment.SurfaceRendererAt(lava);
            Assert.AreSame(PlopShaders.Lava, r.sharedMaterial);
            Assert.AreSame(ArtPack.Asset.lava.texture, r.sharedMaterial.GetTexture("_ArtTex"));
            Assert.AreNotEqual(Vector4.zero, r.sharedMaterial.GetVector("_ArtRect"));
        }

        [Test]
        public void Fan_BladesTurnOnTheHousingHub_AndChevronsShowStrengthAndDirection()
        {
            var level = LevelText.Parse(3, "fan", "R...r", ">..#.");
            var board = Board(level);
            var fan = level.Tiles.Single(t => t.Kind == TileKind.Fan);
            var blades = board.Environment.BladesAt(fan.Position);
            Assert.Less(Vector3.Distance(blades.localPosition, board.CellToLocal(fan.Position)), 1e-4f, "blades centred on the cell (housing hole)");
            var chevrons = board.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name.StartsWith("Breeze power")).ToList();
            Assert.AreEqual(3, chevrons.Count);
            foreach (var c in chevrons)
            {
                Assert.Less(Vector3.Distance(c.transform.up, Vector3.right), 1e-3f, "chevron points downwind");
                Assert.Greater(c.transform.localPosition.x, board.CellToLocal(fan.Position).x, "on the downwind rim");
            }
        }

        [Test]
        public void Portal_VortexRotatesUnderAStaticFrame_AndStopsForReducedMotion()
        {
            Assume.That(ArtPack.Ready);
            var level = VisualLab.Get(4).Level;
            var board = Board(level);
            var p = level.Tiles.First(t => t.Kind == TileKind.Portal).Position;
            var vortex = board.Environment.VortexAt(p);
            var frame = board.Environment.PortalRendererAt(p);
            Assert.Less(vortex.sortingOrder, frame.sortingOrder, "vortex under the frame (pips stay on top)");
            var a0 = vortex.transform.localRotation;
            var f0 = frame.transform.localRotation;
            board.Environment.Tick(1f, 0.1f);
            Assert.Greater(Quaternion.Angle(a0, vortex.transform.localRotation), 1f, "vortex turns");
            Assert.AreEqual(0f, Quaternion.Angle(f0, frame.transform.localRotation), 1e-3f, "frame stays still");

            board.SetReducedMotion(true);
            var a1 = vortex.transform.localRotation;
            board.Environment.Tick(1.1f, 0.1f);
            Assert.AreEqual(0f, Quaternion.Angle(a1, vortex.transform.localRotation), 1e-3f, "Reduced Motion: no decorative spin");
        }

        [Test]
        public void Portal_AmbientMotes_NeverCountAsGameplayFeedback()
        {
            Assume.That(ArtPack.Ready);
            var level = VisualLab.Get(4).Level;
            var board = Board(level);
            float now = Time.unscaledTime;
            for (int k = 0; k < 40; k++) board.Environment.Tick(now + 0.1f * k, 0.1f);
            Assert.Greater(board.Environment.Fx.AmbientCount, 0, "idle portals emit small motes");
            Assert.AreEqual(0, board.Environment.Fx.LiveCount);
            Assert.IsFalse(board.Environment.HasActiveFeedback, "idle motes never hold input or the idle timers");
        }

        [Test]
        public void ArtPack_ChangesNoRules()
        {
            // The same lab level and moves produce the same Core events with or without the art pack.
            var lab = VisualLab.Get(5);
            string Run()
            {
                var engine = new GameEngine();
                engine.LoadLevel(lab.Level);
                var log = new List<string>();
                engine.EventRaised += e => log.Add(e.Type + ":" + e.PlopId + ":" + e.From + ">" + e.To);
                foreach (char c in lab.Demo) if (DirectionUtil.TryParse(c, out var d)) engine.TryMove(d);
                return string.Join("|", log);
            }
            string withArt = Run();
            ArtPack.ForceProcedural = true;
            try { Assert.AreEqual(withArt, Run()); }
            finally { ArtPack.ForceProcedural = false; }
        }

#if UNITY_EDITOR
        [Test]
        public void ImportSettings_KeepTransparencyAndQuality()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Phase11_6" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
                Assert.AreEqual(UnityEditor.TextureImporterType.Sprite, imp.textureType, path);
                Assert.IsTrue(imp.alphaIsTransparency, path);
                Assert.IsFalse(imp.mipmapEnabled, path);
                Assert.AreEqual(320f, imp.spritePixelsPerUnit, path);
                var android = imp.GetPlatformTextureSettings("Android");
                Assert.IsTrue(android.overridden && android.format == UnityEditor.TextureImporterFormat.RGBA32, path + ": no lossy compression");
            }
        }
#endif
    }
}
