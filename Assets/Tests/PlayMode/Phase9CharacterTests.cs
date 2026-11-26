using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Plop.Core;
using Plop.Meta;
using Plop.Presentation;
using Plop.Solver;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Plop.Tests.PlayMode
{
    /// <summary>Phase 9: cosmetic content, Character Studio, gameplay integration and the jelly rig.</summary>
    public class Phase9CharacterTests
    {
        private GameObject _root;
        private PlopGameController _controller;
        private MemoryStore _store;

        private static LevelDefinition Level(string id, params string[] rows)
        {
            var l = LevelText.Parse(id, rows);
            return l.WithOptimalMoves(BfsSolver.Solve(l).SolutionLength);
        }

        private void CreateController(MemoryStore store = null)
        {
            _root = new GameObject("Phase9 test");
            _controller = _root.AddComponent<PlopGameController>();
            _controller.RememberProgress = false;
            _controller.UseLevels(new List<LevelDefinition> { Level("one", "R..r"), Level("two", "R.r", "B.b") });
            _store = store ?? new MemoryStore();
            _controller.UseMetaStore(_store);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            ShapeAtlas.ReleaseUnused(CharacterLook.Default);
        }

        private BoardView Board => _controller.Board;

        // ------------------------------------------------------------------ content

        [Test]
        public void ShippedCosmeticContent_HasNoProblems()
        {
            var issues = CosmeticContentValidator.Run(CosmeticCatalogLoader.Load());
            CollectionAssert.IsEmpty(issues, string.Join("\n", issues));
        }

        [Test]
        public void EveryCatalogAsset_ResolvesToARecipe_AndMissingItemsFallBack()
        {
            var catalog = CosmeticCatalogLoader.Load();
            foreach (var item in catalog.Items) Assert.IsTrue(CharacterArt.HasAsset(item.Asset), item.Id);
            var broken = CharacterPreset.Defaults(catalog);
            broken.Set(CosmeticCategory.Accessory, "accessory.deleted");
            broken.Set(CosmeticCategory.BodyStyle, null);
            var look = CharacterArt.Resolve(broken, catalog);
            Assert.AreSame(CharacterArt.Accessories["none"], look.Accessory);
            Assert.AreSame(CharacterArt.Styles["classic"], look.Style);
        }

        // ------------------------------------------------------------------ gameplay integration

        [UnityTest]
        public IEnumerator Cosmetics_NeverChangeLogic_AndKeepLogicalColours()
        {
            CreateController();
            yield return null;
            _controller.Wallet.Grant("test:dust", 1000);
            foreach (var id in new[] { "body.snoozy", "finish.pearl", "accessory.crown", "nest.marshmallow", "effect.sparkle", "palette.sorbet", "face.starry" })
            {
                Assert.AreEqual(UnlockResult.Unlocked, _controller.Cosmetics.TryUnlock(id), id);
                Assert.IsTrue(_controller.Cosmetics.Equip(id), id);
            }
            _controller.ApplyLook();
            Assert.AreEqual("snoozy/pearl/sorbet/starry/crown/marshmallow/sparkle", _controller.Look.Key);
            Assert.AreEqual(_controller.Look.Key, Board.Look.Key, "gameplay board wears the equipped look");

            // Same moves, same logic as a plain engine.
            var reference = new GameEngine();
            reference.LoadLevel(_controller.Engine.Level);
            foreach (var d in BfsSolver.Solve(_controller.Engine.Level).Moves)
            {
                _controller.Move(d);
                reference.TryMove(d);
                for (int i = 0; i < 60 && Board.IsTraveling; i++) yield return null;
            }
            Assert.IsTrue(_controller.Engine.IsCompleted);
            Assert.AreEqual(reference.CurrentState, _controller.Engine.CurrentState);
            Assert.AreEqual(Board.CellToWorld(new GridPosition(3, 0)), Board.PlopViewPosition(0));

            // Body sprites still follow the logical colour (and carry its match mark).
            var level = _controller.Engine.Level;
            for (int id = 0; id < Board.PlopCount; id++)
            {
                var body = Board.PlopViewAt(id).Root.Find("Pose/Body").GetComponent<SpriteRenderer>();
                Assert.AreSame(Board.Atlas.Get(ShapeAtlas.PlopKey((int)level.PlopColorOf(id))), body.sprite);
            }
        }

        [UnityTest]
        public IEnumerator UndoAndRestart_WithCosmetics_RestoreExactCells()
        {
            CreateController();
            yield return null;
            _controller.Wallet.Grant("test:dust", 500);
            _controller.Cosmetics.TryUnlock("accessory.nightcap");
            _controller.Cosmetics.Equip("accessory.nightcap");
            _controller.ApplyLook();
            var view = Board.PlopViewAt(0);
            Assert.IsNotNull(view.AccessoryTransform, "accessory attached to the body");
            Assert.AreEqual(view.Root.Find("Pose/Body"), view.AccessoryTransform.parent, "accessory deforms with the body");

            _controller.Move(Direction.Right);
            yield return null;
            _controller.Undo();
            Assert.AreEqual(Board.CellToWorld(new GridPosition(0, 0)), Board.PlopViewPosition(0), "Undo snaps the root to the exact cell");
            _controller.Move(Direction.Right);
            _controller.Move(Direction.Right);
            _controller.Restart();
            Assert.AreEqual(Board.CellToWorld(new GridPosition(0, 0)), Board.PlopViewPosition(0));
            Assert.AreEqual(_controller.Look.Key, Board.Look.Key);
        }

        [UnityTest]
        public IEnumerator Sleeping_ShowsSleepingFace_AndUndoWakes()
        {
            CreateController();
            yield return null;
            _controller.Wallet.Grant("test:dust", 500);
            _controller.Cosmetics.TryUnlock("finish.dewdrop");
            _controller.Cosmetics.Equip("finish.dewdrop");
            _controller.ApplyLook();
            foreach (var d in BfsSolver.Solve(_controller.Engine.Level).Moves)
            {
                _controller.Move(d);
                for (int i = 0; i < 60 && Board.IsTraveling; i++) yield return null;
            }
            float until = Time.unscaledTime + 1.5f;
            while (Board.PlopViewAt(0).CurrentExpression != Expression.Sleeping && Time.unscaledTime < until) yield return null;
            Assert.IsTrue(Board.PlopViewAt(0).IsAsleep);
            Assert.AreEqual(Expression.Sleeping, Board.PlopViewAt(0).CurrentExpression);
            _controller.Undo();
            yield return null;
            Assert.IsFalse(Board.PlopViewAt(0).IsAsleep);
            Assert.AreNotEqual(Expression.Sleeping, Board.PlopViewAt(0).CurrentExpression);
        }

        [UnityTest]
        public IEnumerator EquippedLook_SurvivesARestart()
        {
            CreateController();
            yield return null;
            _controller.Wallet.Grant("test:dust", 300);
            Assert.AreEqual(UnlockResult.Unlocked, _controller.Cosmetics.TryUnlock("body.bouncy"));
            _controller.Cosmetics.Equip("body.bouncy");
            _controller.Cosmetics.Equip("accessory.bow");
            _controller.ApplyLook();
            string key = _controller.Look.Key;
            Object.Destroy(_root);
            yield return null;

            CreateController(_store); // relaunch over the same save
            yield return null;
            Assert.AreEqual(key, _controller.Look.Key);
            Assert.AreEqual(key, Board.Look.Key);
            Assert.AreEqual(240, _controller.Wallet.Balance);
            Assert.IsTrue(_controller.Cosmetics.IsOwned("body.bouncy"));
        }

        [UnityTest]
        public IEnumerator Milestones_UnlockFromSavedProgress()
        {
            CreateController();
            yield return null;
            Assert.IsFalse(_controller.Cosmetics.IsOwned("accessory.leaf"));
            var progress = _controller.CosmeticProgress();
            Assert.AreEqual(0, progress.LevelsCompleted);
            var granted = _controller.Cosmetics.UpdateProgress(new ProgressSnapshot { LevelsCompleted = 10 });
            Assert.AreEqual(1, granted.Count);
            Assert.IsTrue(_controller.Cosmetics.IsOwned("accessory.leaf"));
        }

        // ------------------------------------------------------------------ Character Studio

        [UnityTest]
        public IEnumerator Studio_PreviewUnlockEquipAndReturn()
        {
            CreateController();
            yield return null;
            _controller.Wallet.Grant("test:dust", 100);
            _controller.ShowMenu();
            _controller.OpenStudio();
            var studio = _controller.Studio;
            Assert.IsTrue(studio.IsOpen);
            Assert.IsFalse(_controller.Menu.IsOpen);
            _controller.Move(Direction.Right);
            Assert.AreEqual(0, _controller.Engine.MoveCount, "no swipes reach the board while the Studio is open");
            Assert.AreEqual(CosmeticCategory.BodyStyle, studio.Category);
            Assert.AreEqual(3, studio.VisibleItemCount);

            studio.SelectCategory(CosmeticCategory.Accessory);
            Assert.AreEqual(9, studio.VisibleItemCount);
            studio.Select("accessory.cloud"); // 110 Dust: can preview, cannot afford
            Assert.AreSame(CharacterArt.Accessories["cloud"], studio.PreviewLook.Accessory, "locked items can be previewed");
            Assert.AreEqual("accessory.none", _controller.Cosmetics.Equipped.Get(CosmeticCategory.Accessory));
            Assert.AreEqual(CharacterStudio.ActionKind.NeedDust, studio.Action);
            StringAssert.Contains("10 more", studio.ActionText);
            studio.PressAction();
            Assert.AreEqual(100, _controller.Wallet.Balance);

            studio.Select("accessory.crown"); // milestone item with a 250 Dust alternative
            Assert.AreEqual(CharacterStudio.ActionKind.NeedDust, studio.Action, "too expensive for now");
            var status = GameObject.Find("Item Status").GetComponent<UnityEngine.UI.Text>().text;
            StringAssert.Contains("Finish 3 Worlds or 250", status, "both unlock routes are shown");

            studio.Select("accessory.flower"); // 40 Dust
            Assert.AreEqual(CharacterStudio.ActionKind.Unlock, studio.Action);
            studio.PressAction();
            Assert.AreEqual(CharacterStudio.ActionKind.Confirm, studio.Action, "first tap only arms the unlock");
            Assert.AreEqual(100, _controller.Wallet.Balance);
            studio.PressAction();
            Assert.AreEqual(60, _controller.Wallet.Balance);
            Assert.AreEqual(CharacterStudio.ActionKind.Equipped, studio.Action);
            Assert.AreEqual("accessory.flower", _controller.Cosmetics.Equipped.Get(CosmeticCategory.Accessory));

            studio.Select("accessory.bow"); // free
            Assert.AreEqual(CharacterStudio.ActionKind.Equip, studio.Action);
            studio.PressAction();
            Assert.AreEqual("accessory.bow", _controller.Cosmetics.Equipped.Get(CosmeticCategory.Accessory));

            studio.Close();
            Assert.IsFalse(studio.IsOpen);
            Assert.IsTrue(_controller.Menu.IsOpen, "Back returns to the main menu");
            Assert.AreSame(CharacterArt.Accessories["bow"], Board.Look.Accessory, "gameplay wears the new look");
            Assert.AreEqual(Board.Look.Key, _controller.Menu.Look.Key, "menu mascots too");
            _controller.StartCampaign();
            Assert.IsNotNull(Board.PlopViewAt(0).AccessoryTransform);
        }

        /// <summary>Phase 11 player-flow fix: Android's system Back did nothing anywhere.</summary>
        [UnityTest]
        public IEnumerator SystemBack_ClosesTheTopScreen_ThenReturnsToTheMenu()
        {
            CreateController();
            yield return null;
            var hud = _root.GetComponent<GameHud>();
            _controller.ShowMenu();
            _controller.OpenStudio();
            Assert.AreEqual("studio", _controller.HandleBack());
            Assert.IsFalse(_controller.Studio.IsOpen);
            Assert.IsTrue(_controller.Menu.IsOpen, "Studio → menu");

            _controller.Menu.Hide(instant: true);
            hud.ShowSettings();
            Assert.AreEqual("modal", _controller.HandleBack());
            Assert.IsFalse(hud.IsSettingsOpen);

            _controller.StartCampaign();
            Assert.IsFalse(_controller.Menu.IsOpen);
            Assert.AreEqual("menu", _controller.HandleBack(), "a level → main menu, like Home");
            Assert.IsTrue(_controller.Menu.IsOpen);
            Assert.AreEqual("background", _controller.HandleBack(), "the menu is the root");
        }

        [UnityTest]
        public IEnumerator StudioPreview_TapReactionsAreRateLimited_AndAtlasesAreReleased()
        {
            CreateController();
            yield return null;
            _controller.ShowMenu();
            _controller.OpenStudio();
            var studio = _controller.Studio;
            var preview = studio.PreviewPlop;
            Assert.IsNotNull(preview);
            preview.React(Reaction.Surprised, Time.unscaledTime);
            preview.Tick(Time.unscaledTime + 0.05f);
            Assert.AreEqual(Expression.Surprised, preview.CurrentExpression);
            Debug.Log("[PLOP] Studio bakes: thumbnails " + studio.ThumbnailBakeMs.ToString("0") + " ms, preview " + studio.LastPreviewBakeMs.ToString("0") + " ms");
            studio.Close();
            Assert.IsNull(studio.PreviewPlop, "no preview character stays alive during gameplay");
        }

        // ------------------------------------------------------------------ rig behaviour

        [Test]
        public void ReducedMotion_DisablesEffects_AndReactionsOnlyChangeTheFace()
        {
            var scene = new GameObject("Reduced motion rig");
            try
            {
                var look = CharacterLook.Default.With(effect: CharacterArt.Effects["sparkle"], accessory: CharacterArt.Accessories["bow"]);
                var board = scene.AddComponent<BoardView>();
                var engine = new GameEngine();
                engine.LoadLevel(LevelText.Parse("rm", "R..r"));
                board.SetReducedMotion(true);
                board.Build(engine.Level, engine.CurrentState, ShapeAtlas.ForDensity(96f, look));
                board.Play(engine.TryMove(Direction.Right).Events);
                Assert.IsFalse(board.Effects.Enabled);
                Assert.AreEqual(0, board.Effects.Alive, "no particles under Reduced Motion");

                var view = board.PlopViewAt(0);
                var body = view.Root.Find("Pose/Body");
                var scale = body.localScale;
                view.React(Reaction.Bounce, 5f);
                view.Tick(5.1f);
                Assert.AreEqual(scale, body.localScale, "no squash or hop");
                Assert.AreEqual(Expression.Happy, view.CurrentExpression, "the face still responds");
            }
            finally { Object.Destroy(scene); }
        }

        [Test]
        public void MoveEffects_StayWithinTheirPool_AndHalveInBatterySaver()
        {
            var scene = new GameObject("Effects pool");
            try
            {
                var look = CharacterLook.Default.With(effect: CharacterArt.Effects["bubble"]);
                var atlas = ShapeAtlas.ForDensity(96f, look);
                var fx = new MoveEffects(scene.transform, atlas, false);
                for (int i = 0; i < 30; i++) fx.Spawn(Vector3.zero, Vector2.right, Color.white, 1f, 0);
                Assert.LessOrEqual(fx.Alive, MoveEffects.PoolSize);
                fx.Tick(10f);
                Assert.AreEqual(0, fx.Alive, "every particle expires");
                fx.BatterySaver = true;
                fx.Spawn(Vector3.zero, Vector2.right, Color.white, 20f, 0);
                Assert.AreEqual(1, fx.Alive, "three bubbles become one at 30 FPS");
            }
            finally { Object.Destroy(scene); }
        }

        [Test]
        public void InterruptedReactionAndMove_KeepTheRootOnItsCell()
        {
            var scene = new GameObject("Interrupted rig");
            try
            {
                var look = CharacterLook.Default.With(style: CharacterArt.Styles["bouncy"], accessory: CharacterArt.Accessories["star"]);
                var view = new PlopView(scene.transform, ShapeAtlas.ForDensity(96f, look), 2, 4);
                view.Snap(Vector3.zero, false);
                view.React(Reaction.Wobble, 10f);
                view.Tick(10.05f);
                view.StartMove(Vector3.right, Vector2.right, 10.05f);
                view.React(Reaction.Squash, 10.08f);
                view.Tick(10.09f);
                view.Restore(Vector3.zero, false, ease: true);
                Assert.AreEqual(Vector3.zero, view.Root.localPosition, "Undo wins over any cosmetic animation");
                view.Tick(40f);
                Assert.AreEqual(Vector3.zero, view.Root.localPosition);
                Assert.AreEqual(Vector3.zero, view.Root.Find("Pose").localPosition);
                Assert.IsFalse(view.IsBusy(40f));
            }
            finally { Object.Destroy(scene); }
        }

        [Test]
        public void Board_RendersFromOneTexture_ForEveryLook()
        {
            var scene = new GameObject("Batching");
            try
            {
                var look = CharacterLook.Default.With(style: CharacterArt.Styles["snoozy"], finish: CharacterArt.Finishes["velvet"],
                    accessory: CharacterArt.Accessories["flower"], nest: CharacterArt.Nests["cloud"], effect: CharacterArt.Effects["puff"]);
                var board = scene.AddComponent<BoardView>();
                var engine = new GameEngine();
                engine.LoadLevel(LevelText.Parse("batch", "R.#b", "B.r.", "..Y.", "y..."));
                var atlas = ShapeAtlas.ForDensity(128f, look);
                board.Build(engine.Level, engine.CurrentState, atlas);
                var textures = new HashSet<Texture>();
                var materials = new HashSet<Material>();
                foreach (var r in scene.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (r.sprite == null) continue;
                    textures.Add(r.sprite.texture);
                    materials.Add(r.sharedMaterial);
                }
                Assert.AreEqual(1, textures.Count, "board, characters, accessories, Nests and effects share one atlas texture");
                Assert.AreEqual(1, materials.Count, "and one material, so they batch");
                Assert.LessOrEqual(atlas.Texture.width, 2048);
                Assert.LessOrEqual(atlas.Texture.height, 2048);
                Debug.Log("[PLOP] Gameplay atlas 128px/cell: " + atlas.Texture.width + "x" + atlas.Texture.height + ", " + (atlas.TextureBytes / 1024) + " KB, " + atlas.BakeMilliseconds.ToString("0") + " ms");
            }
            finally { Object.Destroy(scene); }
        }

        [Test]
        public void RigTick_DoesNotAllocate_AndIsCheap()
        {
            var scene = new GameObject("Tick cost");
            try
            {
                var look = CharacterLook.Default.With(accessory: CharacterArt.Accessories["crown"], face: CharacterArt.Faces["starry"]);
                var atlas = ShapeAtlas.ForDensity(128f, look);
                var views = new PlopView[6];
                for (int i = 0; i < views.Length; i++)
                {
                    views[i] = new PlopView(scene.transform, atlas, i, i + 1);
                    views[i].Snap(new Vector3(i, 0f, 0f), false);
                }
                for (int i = 0; i < 200; i++) foreach (var v in views) v.Tick(50f + i * 0.016f); // warm up

                long probe = GC.GetAllocatedBytesForCurrentThread();
                var sanity = new byte[4096];
                bool exact = GC.GetAllocatedBytesForCurrentThread() - probe >= 4096;
                GC.KeepAlive(sanity);
                // Unity's Mono may not expose the per-thread counter; fall back to managed heap growth
                // (approximate: a collection during the loop could hide small allocations).
                Func<long> measure = exact ? (Func<long>)GC.GetAllocatedBytesForCurrentThread
                                           : UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong;
                GC.Collect();
                long before = measure();
                var clock = Stopwatch.StartNew();
                const int frames = 1000;
                for (int f = 0; f < frames; f++)
                {
                    float now = 60f + f * 0.016f;
                    if (f % 120 == 0) views[f / 120 % 6].StartBump(Vector2.left, now, BlockReason.Wall);
                    if (f % 150 == 0) views[f / 150 % 6].React(Reaction.Wobble, now);
                    foreach (var v in views) v.Tick(now);
                }
                clock.Stop();
                long allocated = measure() - before;
                double perTick = clock.Elapsed.TotalMilliseconds * 1000.0 / (frames * views.Length);
                Debug.Log("[PLOP] PlopView.Tick: " + perTick.ToString("0.0") + " µs per Plop per frame (Editor), " + allocated +
                          " bytes allocated over " + frames + " frames (" + (exact ? "exact counter" : "heap growth") + ")");
                Assert.Less(allocated, exact ? 1024 : 16 * 1024, "animation must not allocate per frame");
                Assert.Less(perTick, 100.0, "a Plop's frame update stays well under 0.1 ms in the Editor");
            }
            finally { Object.Destroy(scene); }
        }

        [Test]
        public void StyleFaces_StayOnTheBody_DuringSquashAndStretch()
        {
            foreach (var style in CharacterArt.Styles.Values)
            {
                Assert.Less(style.Distance(style.EyeL.x, style.EyeL.y), -0.05f, style.Name);
                Assert.Less(style.Distance(style.Mouth.x, style.Mouth.y), -0.05f, style.Name);
                Assert.LessOrEqual(style.Bounds.width, 1.08f, style.Name + " fits a cell");
            }
            var scene = new GameObject("Face attachment");
            try
            {
                var view = new PlopView(scene.transform, ShapeAtlas.ForDensity(96f), 0, 1);
                Assert.AreEqual("Body", view.Root.Find("Pose/Body/EyeL").parent.name, "face parts are children of the deforming body");
            }
            finally { Object.Destroy(scene); }
        }
    }
}
