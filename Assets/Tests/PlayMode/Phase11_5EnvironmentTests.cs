using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Plop.Core;
using Plop.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Plop.Tests.PlayMode
{
    /// <summary>
    /// Phase 11.5 environmental art: connected surfaces, fan wind, Loop identity, trace feedback cleanup,
    /// quality modes, World board materials and living backgrounds. None of this may change the rules.
    /// </summary>
    public class Phase11_5EnvironmentTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            PlopShaders.SetFrozen(false);
            WorldAmbient.Apply(null, false, false);
        }

        private BoardView Board(LevelDefinition level, WorldTheme world = null, float ppu = 96f)
        {
            var go = new GameObject("Phase11.5 board");
            _made.Add(go);
            var board = go.AddComponent<BoardView>();
            if (world != null) board.SetWorld(world);
            board.Build(level, level.CreateInitialState(), ShapeAtlas.ForDensity(ppu));
            return board;
        }

        private static IEnumerable<GridPosition> Cells(LevelDefinition l, TileKind kind)
        {
            for (int y = 0; y < l.Height; y++)
            for (int x = 0; x < l.Width; x++)
                if (l.TileAt(new GridPosition(x, y)) == kind) yield return new GridPosition(x, y);
        }

        private static Dictionary<SurfaceConnectivity.Shape, int> Shapes(LevelDefinition l, TileKind kind)
        {
            var count = new Dictionary<SurfaceConnectivity.Shape, int>();
            foreach (var p in Cells(l, kind))
            {
                var s = SurfaceConnectivity.Classify(SurfaceConnectivity.Mask(l, p.X, p.Y, kind));
                count[s] = count.TryGetValue(s, out int n) ? n + 1 : 1;
            }
            return count;
        }

        private static int Count(Dictionary<SurfaceConnectivity.Shape, int> d, SurfaceConnectivity.Shape s) => d.TryGetValue(s, out int n) ? n : 0;

        // ------------------------------------------------------------------ adjacency classification

        [Test]
        public void Frost_Adjacency_ClassifiesIsolatedStraightCornerTeeAndCross()
        {
            var cross = LevelText.Parse("cross", "R...r", "..~..", ".~~~.", "..~..");
            var c = Shapes(cross, TileKind.Frost);
            Assert.AreEqual(1, Count(c, SurfaceConnectivity.Shape.Cross));
            Assert.AreEqual(4, Count(c, SurfaceConnectivity.Shape.End));

            var line = LevelText.Parse("line", "R...r", ".~~~.");
            var l = Shapes(line, TileKind.Frost);
            Assert.AreEqual(1, Count(l, SurfaceConnectivity.Shape.Straight));
            Assert.AreEqual(2, Count(l, SurfaceConnectivity.Shape.End));

            var corner = LevelText.Parse("corner", "R...r", "~~...", "~....");
            Assert.AreEqual(1, Count(Shapes(corner, TileKind.Frost), SurfaceConnectivity.Shape.Corner));

            var tee = LevelText.Parse("tee", "R...r", "~~~..", ".~...");
            Assert.AreEqual(1, Count(Shapes(tee, TileKind.Frost), SurfaceConnectivity.Shape.Tee));

            var single = LevelText.Parse("single", "R...r", "..~..");
            Assert.AreEqual(1, Count(Shapes(single, TileKind.Frost), SurfaceConnectivity.Shape.Isolated));
        }

        [Test]
        public void Lava_Diagonals_OnlyJoinWhenBothSidesAreLava()
        {
            // A 2×2 pool: every cell has exactly one inner diagonal (filled corner, no notch).
            var pool = LevelText.Parse("pool", "R...r", "!!...", "!!...");
            foreach (var p in Cells(pool, TileKind.Lava))
            {
                byte m = SurfaceConnectivity.Mask(pool, p.X, p.Y, TileKind.Lava);
                int diagonals = new[] { SurfaceConnectivity.NE, SurfaceConnectivity.SE, SurfaceConnectivity.SW, SurfaceConnectivity.NW }.Count(b => (m & b) != 0);
                Assert.AreEqual(1, diagonals, "cell " + p);
                Assert.AreEqual(SurfaceConnectivity.Shape.Corner, SurfaceConnectivity.Classify(m));
            }
            // Diagonal neighbours alone never connect: two separate rounded cells.
            var touching = LevelText.Parse("diag", "R...r", "!....", ".!...");
            foreach (var p in Cells(touching, TileKind.Lava))
                Assert.AreEqual(0, SurfaceConnectivity.Mask(touching, p.X, p.Y, TileKind.Lava), "cell " + p);
        }

        [Test]
        public void SurfaceData_SurvivesRendererColourConversion_ForEveryMask()
        {
            for (int m = 0; m < 256; m++)
            {
                var stored = PlopShaders.Pack(SurfaceConnectivity.EncodeSurface((byte)m, 77, 0.5f));
                Assert.AreEqual(m, SurfaceConnectivity.DecodeMask(PlopShaders.Unpack(stored)), "mask " + m);
            }
        }

        [Test]
        public void EdgeSelection_OnlyOuterSidesGetARim()
        {
            byte m = SurfaceConnectivity.N | SurfaceConnectivity.E;
            Assert.IsFalse(SurfaceConnectivity.IsOuterEdge(m, Direction.Up));
            Assert.IsFalse(SurfaceConnectivity.IsOuterEdge(m, Direction.Right));
            Assert.IsTrue(SurfaceConnectivity.IsOuterEdge(m, Direction.Down));
            Assert.IsTrue(SurfaceConnectivity.IsOuterEdge(m, Direction.Left));
        }

        [Test]
        public void Board_PassesNeighbourMasksToTheSurfaceShader()
        {
            var level = LevelText.Parse("masks", "R...r", "..~..", ".~~~.", "..~..");
            var board = Board(level);
            Assume.That(PlopShaders.Available, "procedural shaders unsupported on this platform");
            foreach (var p in Cells(level, TileKind.Frost))
            {
                var r = board.Environment.SurfaceRendererAt(p);
                Assert.IsNotNull(r, "renderer at " + p);
                Assert.AreSame(PlopShaders.Frost, r.sharedMaterial, "one shared material");
                Assert.AreEqual(SurfaceConnectivity.Mask(level, p.X, p.Y, TileKind.Frost),
                    SurfaceConnectivity.DecodeMask(PlopShaders.Unpack(r.color)), "mask at " + p);
            }
        }

        // ------------------------------------------------------------------ fans, wind and Loops

        [Test]
        public void Wind_StopsAtWallsAndEdges_AndNeverExceedsPower()
        {
            var walled = LevelText.Parse(3, "walled", "R...r", ">..#.");
            var fan = walled.Tiles.Single(t => t.Kind == TileKind.Fan);
            Assert.AreEqual(3, SurfaceConnectivity.WindRun(walled, fan.Position, fan.Direction, fan.Power), "fan + 2 free cells, then a pebble");

            var open = LevelText.Parse(2, "open", "R...r", ">....");
            var f2 = open.Tiles.Single(t => t.Kind == TileKind.Fan);
            Assert.AreEqual(3, SurfaceConnectivity.WindRun(open, f2.Position, f2.Direction, f2.Power), "power 2 → fan + 2 cells");

            var edge = LevelText.Parse(3, "edge", "R...r", "....>");
            var f3 = edge.Tiles.Single(t => t.Kind == TileKind.Fan);
            Assert.AreEqual(1, SurfaceConnectivity.WindRun(edge, f3.Position, f3.Direction, f3.Power), "facing the edge: only the fan cell");
        }

        [Test]
        public void Wind_IsAlignedWithTheFan_AndShowsStrengthWithoutAnimation()
        {
            var level = LevelText.Parse(3, "wind", "R...r", ">..#.");
            var board = Board(level);
            var fan = level.Tiles.Single(t => t.Kind == TileKind.Fan);
            var env = board.Environment;
            Assert.AreEqual(3, env.WindRunAt(fan.Position));
            var pips = board.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Breeze power"));
            Assert.AreEqual(3, pips, "static chevrons/pips show strength with animations off");
            Assume.That(PlopShaders.Available);
            var wind = env.WindAt(fan.Position);
            Assert.AreEqual(3, wind.Length, "one airflow quad per cell, none past the pebble");
            foreach (var w in wind)
            {
                Assert.Less(Vector3.Distance(w.transform.right, Vector3.right), 1e-3f, "quad +x points downwind");
                Assert.Less(w.sortingOrder, -970, "wind stays under Nests and Plops");
            }
        }

        [Test]
        public void FanBlades_TurnOnlyWhileVisibleAndMotionIsAllowed()
        {
            var level = LevelText.Parse(2, "blades", "R...r", ">....");
            var board = Board(level);
            var fan = level.Tiles.Single(t => t.Kind == TileKind.Fan);
            var blades = board.Environment.BladesAt(fan.Position);
            var a0 = blades.localRotation;
            board.Environment.Tick(1f, 0.1f);
            Assert.Greater(Quaternion.Angle(a0, blades.localRotation), 1f, "idle rotation");

            board.SetVisible(false);
            var a1 = blades.localRotation;
            board.Environment.Tick(1.1f, 0.1f);
            Assert.AreEqual(0f, Quaternion.Angle(a1, blades.localRotation), 1e-3f, "paused when not visible");

            board.SetVisible(true);
            board.SetReducedMotion(true);
            var a2 = blades.localRotation;
            board.Environment.Tick(1.2f, 0.1f);
            Assert.AreEqual(0f, Quaternion.Angle(a2, blades.localRotation), 1e-3f, "Reduced Motion: no decorative spin");
            Assert.IsTrue(PlopShaders.Frozen);
            Assert.AreEqual(PlopShaders.FrozenTime, PlopShaders.Time, 1e-4f);
        }

        [Test]
        public void LoopPairs_AreIdentifiedByBeadsNotOnlyColour()
        {
            var beads = Enumerable.Range(0, 6).Select(SurfaceConnectivity.PortalBeads).ToList();
            Assert.AreEqual(6, beads.Distinct().Count(), "each of six pairs has its own bead count");
            for (int pair = 0; pair < 6; pair++)
            {
                var stored = PlopShaders.Pack(SurfaceConnectivity.EncodePortal(pair, 0f, 0f));
                Assert.AreEqual(pair, SurfaceConnectivity.DecodePortalPair(PlopShaders.Unpack(stored)));
            }
            var lab = VisualLab.Get(4).Level;
            var board = Board(lab);
            Assume.That(PlopShaders.Available);
            foreach (var t in lab.Tiles.Where(t => t.Kind == TileKind.Portal))
                Assert.AreEqual(SurfaceConnectivity.PortalSlot(t.Pair),
                    SurfaceConnectivity.DecodePortalPair(PlopShaders.Unpack(board.Environment.PortalRendererAt(t.Position).color)));
        }

        // ------------------------------------------------------------------ trace visualisation and cleanup

        private static GameEvent[] Events(LevelDefinition level, string moves, out GameEngine engine)
        {
            engine = new GameEngine();
            engine.LoadLevel(level);
            var events = new List<GameEvent>();
            engine.EventRaised += events.Add;
            foreach (char c in moves)
                if (DirectionUtil.TryParse(c, out var d)) engine.TryMove(d);
            return events.ToArray();
        }

        [Test]
        public void VisualLab_Demos_ProduceTheirMechanicEvents()
        {
            Assert.IsTrue(Events(VisualLab.Get(1).Level, VisualLab.Get(1).Demo, out _).Any(e => e.Type == GameEventType.PlopBurned), "Lava");
            Assert.IsTrue(Events(VisualLab.Get(2).Level, VisualLab.Get(2).Demo, out _).Any(e => e.Type == GameEventType.PlopSlid), "Frost");
            Assert.IsTrue(Events(VisualLab.Get(3).Level, VisualLab.Get(3).Demo, out _).Any(e => e.Type == GameEventType.PlopPushed), "Breeze");
            Assert.IsTrue(Events(VisualLab.Get(4).Level, VisualLab.Get(4).Demo, out _).Any(e => e.Type == GameEventType.PlopTeleported), "Loop");
            // A sliding Plop that reaches a fan turns to the fan's direction (rules: it keeps sliding, not a push).
            var combo = Events(VisualLab.Get(5).Level, VisualLab.Get(5).Demo, out _).ToList();
            int right = combo.FindIndex(e => e.Type == GameEventType.PlopSlid && e.Direction == Direction.Right);
            int up = combo.FindIndex(e => e.Type == GameEventType.PlopSlid && e.Direction == Direction.Up);
            int loop = combo.FindIndex(e => e.Type == GameEventType.PlopTeleported);
            Assert.That(right >= 0 && up > right && loop > up, "combined chain keeps Core order: slide → fan turn → loop: " + string.Join(", ", combo));
            Assert.IsTrue(Events(VisualLab.Get(6).Level, VisualLab.Get(6).Demo, out _)
                .Any(e => e.Type == GameEventType.PlopBlocked && e.BlockReason == BlockReason.PortalExit), "blocked Loop");
        }

        [UnityTest]
        public IEnumerator TraceFeedback_IsClearedBySync_UndoOrRestartMidChain()
        {
            var lab = VisualLab.Get(5);
            var board = Board(lab.Level);
            var events = Events(lab.Level, lab.Demo, out var engine);
            board.Play(events);
            yield return null;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsTrue(board.HasQueuedSteps || board.IsTraveling, "chain is being shown");
            // Undo mid-chain: authoritative state wins at once, nothing lingers.
            board.Sync(lab.Level.CreateInitialState());
            Assert.IsFalse(board.HasQueuedSteps);
            Assert.IsFalse(board.Environment.HasActiveFeedback, "no pending exits, emitters or particles");
            Assert.AreEqual(0, board.Environment.Fx?.LiveCount ?? 0);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsFalse(board.Environment.HasActiveFeedback, "no stale delayed callbacks fire later");
        }

        [UnityTest]
        public IEnumerator Teleport_KeepsTheChainGoing_AndLandsOnTheAuthoritativeCell()
        {
            var lab = VisualLab.Get(4);
            var board = Board(lab.Level);
            var events = Events(lab.Level, lab.Demo, out var engine);
            board.Play(events);
            float until = Time.unscaledTime + 3f;
            while ((board.IsTraveling || board.Environment.HasActiveFeedback) && Time.unscaledTime < until) yield return null;
            var expected = board.CellToWorld(lab.Level.PositionOf(engine.CurrentState.CellOf(0)));
            Assert.Less(Vector3.Distance(expected, board.PlopViewPosition(0)), 0.02f);
            Assert.IsFalse(board.Environment.HasActiveFeedback);
        }

        [UnityTest]
        public IEnumerator ReducedMotion_ShowsTheResultWithoutEffects()
        {
            var lab = VisualLab.Get(1);
            var board = Board(lab.Level);
            board.SetReducedMotion(true);
            board.Play(Events(lab.Level, lab.Demo, out _));
            yield return null;
            Assert.AreEqual(0, board.Environment.Fx?.LiveCount ?? 0, "no particles under Reduced Motion");
        }

        [Test]
        public void TraceTiming_CompressesLongChains_WithinBounds()
        {
            Assert.AreEqual(1f, TraceTiming.Factor(0), 1e-5f);
            Assert.AreEqual(1f, TraceTiming.Factor(TraceTiming.CompressAfter - 1), 1e-5f, "short chains play at full length");
            float f = TraceTiming.Factor(TraceTiming.CompressAfter + 3);
            Assert.Less(f, 1f);
            Assert.GreaterOrEqual(f, TraceTiming.MinFactor);
            Assert.GreaterOrEqual(TraceTiming.Factor(50), TraceTiming.MinFactor);
        }

        // ------------------------------------------------------------------ World board materials and backgrounds

        [Test]
        public void EveryWorld_HasBoardMaterials_AndALivingBackground()
        {
            var catalog = WorldCatalog.Load();
            Assert.AreEqual(9, catalog.Worlds.Count);
            var modes = new HashSet<AmbientMode>();
            foreach (var w in catalog.Worlds)
            {
                Assert.IsNotNull(BoardMaterials.For(w), "board materials for " + w.Id);
                var ambient = WorldAmbient.For(w.Id);
                Assert.IsNotNull(ambient, "ambient for " + w.Id);
                modes.Add(ambient.Mode);
            }
            Assert.AreEqual(9, modes.Count, "every World animates differently");
            var finishes = catalog.Worlds.Select(w => BoardMaterials.For(w)).Select(p => p.TrayFinish + "/" + p.PebbleStyle + "/" + p.PebbleCap).Distinct().Count();
            Assert.GreaterOrEqual(finishes, 8, "materials differ in finish, not only tint");
        }

        [Test]
        public void WorldMaterials_SwapSprites_WithoutMovingTheGrid()
        {
            var level = VisualLab.Get(7).Level;
            var plain = Board(level);
            var frosted = WorldCatalog.Load().Worlds.Single(w => w.Id == "frosted-pond");
            var dressed = Board(level, frosted);
            Assert.IsFalse(plain.ShowsWorldMaterials);
            Assert.IsTrue(dressed.ShowsWorldMaterials, "first build bakes synchronously");
            var a = plain.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Tile ") || r.name.StartsWith("Pebble ")).ToDictionary(r => r.name);
            var b = dressed.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Tile ") || r.name.StartsWith("Pebble ")).ToDictionary(r => r.name);
            Assert.AreEqual(a.Count, b.Count);
            foreach (var kv in a)
            {
                Assert.AreEqual(kv.Value.transform.localPosition, b[kv.Key].transform.localPosition, kv.Key);
                Assert.AreEqual(kv.Value.sprite.bounds.size.x, b[kv.Key].sprite.bounds.size.x, 1e-4f, "same footprint " + kv.Key);
            }
            var tray = dressed.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "BoardPanel");
            Assert.AreNotSame(dressed.Atlas.Texture, tray.sprite.texture, "tray uses the World atlas");
            Assert.AreEqual(SpriteDrawMode.Sliced, tray.drawMode);
        }

        [UnityTest]
        public IEnumerator WorldChange_OnALiveBoard_BakesInTheBackground()
        {
            var level = VisualLab.Get(7).Level;
            var worlds = WorldCatalog.Load().Worlds;
            var board = Board(level, worlds.Single(w => w.Id == "ember-hollow"), 112f);
            board.SetWorld(worlds.Single(w => w.Id == "prism-observatory"));
            float until = Time.unscaledTime + 5f;
            while (!board.ShowsWorldMaterials && Time.unscaledTime < until) yield return null;
            Assert.IsTrue(board.ShowsWorldMaterials);
            Assert.AreEqual("prism-observatory", board.Materials.Id);
            Assert.IsFalse(board.SurfacesPending);
        }

        [Test]
        public void AmbientClock_PausesWhenHidden_AndFreezesUnderReducedMotion()
        {
            var world = WorldCatalog.Load().Worlds.Single(w => w.Id == "breezy-meadow");
            var cam = new GameObject("cam").AddComponent<Camera>();
            _made.Add(cam.gameObject);
            cam.orthographic = true;
            var go = new GameObject("backdrop");
            _made.Add(go);
            var backdrop = go.AddComponent<WorldBackdrop>();
            backdrop.Show(world, cam, new BoardLayout.Result { OrthographicSize = 8f });
            Assume.That(PlopShaders.Ambient != null, "ambient shader unsupported");
            Assert.IsTrue(backdrop.AmbientActive);
            Assert.AreEqual(AmbientMode.Meadow, backdrop.CurrentAmbient);
            foreach (var w in WorldCatalog.Load().Worlds)
                Assert.LessOrEqual(WorldAmbient.For(w.Id).ParticleCount, 64, w.Id + ": low particle count");
            Assert.AreEqual(1, go.GetComponentsInChildren<Transform>().Count(t => t.name == "World Ambient"), "one ambient layer, for the current World only");
            Assert.AreEqual(4, backdrop.AmbientRendererCount, "three small prop regions (two sail circles, grass) and one particle mesh; nothing full screen");
            var meadow = WorldAmbient.For("breezy-meadow");
            Assert.LessOrEqual(meadow.ParticleCount, 64, "low particle count");
            var other = WorldCatalog.Load().Worlds.Single(w => w.Id == "frosted-pond");
            backdrop.Show(other, cam, new BoardLayout.Result { OrthographicSize = 8f });
            Assert.AreEqual(1, go.GetComponentsInChildren<Transform>().Count(t => t.name == "World Ambient"), "switching World replaces the layer");

            backdrop.SetAmbientVisible(false);
            Assert.IsFalse(backdrop.AmbientActive, "hidden behind menus / studio / background");
            backdrop.SetAmbientVisible(true);

            backdrop.SetBatterySaver(true);
            Assert.Less(WorldAmbient.Density, 1f, "Battery Saver thins particles");
            backdrop.SetReducedMotion(true);
            Assert.IsFalse(WorldAmbient.Moving);
            float t = WorldAmbient.Time;
            WorldAmbient.Advance(0.5f, 1f);
            Assert.AreEqual(t, WorldAmbient.Time, "Reduced Motion: no decorative motion");
            backdrop.SetReducedMotion(false);
            WorldAmbient.Advance(0.05f, 1f);
            Assert.Greater(WorldAmbient.Time, t);
        }
    }
}
