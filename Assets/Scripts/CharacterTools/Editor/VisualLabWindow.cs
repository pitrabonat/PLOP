using System.Linq;
using Plop.Presentation;
using UnityEditor;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>
    /// Editor-only environmental preview (Phase 11.5): any Visual Lab board in any World, with a time scrubber,
    /// Reduced Motion and Battery Saver toggles, the living background, and one-click Play in Editor (opens the
    /// board in the running game via the development prefs). Lives in the editor-only PLOP.CharacterTools
    /// assembly, so none of it reaches a player build.
    /// </summary>
    public sealed class VisualLabWindow : EditorWindow
    {
        private int _board = 7;
        private int _world;
        private float _time = 12.4f;
        private bool _animate = true, _reduced, _saver, _scene = true;
        private CharacterStage _stage;
        private BoardView _view;
        private WorldBackdrop _backdrop;
        private string[] _worldIds;
        private bool _dirty = true;
        private double _last;

        [MenuItem("PLOP/Visual Lab/Preview Window", priority = 40)]
        public static void Open() => GetWindow<VisualLabWindow>("PLOP Visual Lab");

        private void OnEnable()
        {
            _worldIds = WorldCatalog.Load().Worlds.Select(w => w.Id).ToArray();
            _last = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            _stage?.Dispose();
            _stage = null;
            PlopShaders.SetFrozen(false);
            WorldAmbient.Apply(null, false, false);
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _last);
            _last = now;
            if (!_animate || _reduced) return;
            _time += dt * (_saver ? WorldAmbient.SaverSpeed : 1f);
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            var titles = VisualLab.Boards.Select(b => b.Title).ToArray();
            _board = EditorGUILayout.Popup("Board", _board - 1, titles) + 1;
            _world = EditorGUILayout.Popup("World", _world, _worldIds);
            using (new EditorGUILayout.HorizontalScope())
            {
                _scene = GUILayout.Toggle(_scene, "Phone scene (background)", EditorStyles.miniButtonLeft);
                _reduced = GUILayout.Toggle(_reduced, "Reduced Motion", EditorStyles.miniButtonMid);
                _saver = GUILayout.Toggle(_saver, "Battery Saver (30)", EditorStyles.miniButtonRight);
            }
            if (EditorGUI.EndChangeCheck()) _dirty = true;
            using (new EditorGUILayout.HorizontalScope())
            {
                _animate = GUILayout.Toggle(_animate, "Animate", GUILayout.Width(70));
                _time = EditorGUILayout.Slider(_time, 0f, 60f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use board's World")) { _world = System.Array.IndexOf(_worldIds, VisualLab.Get(_board).World); _dirty = true; }
                if (GUILayout.Button("Play in Editor")) PlayInEditor();
                if (GUILayout.Button("Capture all close-ups…")) EnvironmentCapture.CaptureFromMenu();
            }
            EditorGUILayout.HelpBox("Demo moves: " + (string.IsNullOrEmpty(VisualLab.Get(_board).Demo) ? "none" : VisualLab.Get(_board).Demo) +
                ". Development boards only: never saved, compiled out of release players.", MessageType.None);

            var rect = GUILayoutUtility.GetRect(position.width, position.height - 130f);
            if (Event.current.type != EventType.Repaint || rect.height < 50f) return;
            if (_dirty) Rebuild();
            PlopShaders.SetFrozen(_reduced);
            if (!_reduced) PlopShaders.SetTime(_time);
            WorldAmbient.Apply(WorldAmbient.For(_worldIds[Mathf.Clamp(_world, 0, _worldIds.Length - 1)]), _saver, _reduced);
            if (!_reduced) WorldAmbient.SetTime(_time);
            _view.Environment?.Tick((float)EditorApplication.timeSinceStartup, 0f);
            var level = VisualLab.Get(_board).Level;
            float height = _scene ? 20f : level.Height + 1.2f;
            float aspect = rect.width / rect.height;
            height = Mathf.Max(height, (_scene ? 9f : level.Width + 1.2f) / aspect);
            _stage.Draw(rect, Vector2.zero, height);
        }

        private void Rebuild()
        {
            _dirty = false;
            _stage?.Dispose();
            var world = WorldCatalog.Load().Worlds[Mathf.Clamp(_world, 0, _worldIds.Length - 1)];
            _stage = new CharacterStage(world.BackgroundColor);
            var lab = VisualLab.Get(_board);
            _view = _stage.Add("Board").AddComponent<BoardView>();
            BoardStage.Apply(_view, world);
            _view.SetReducedMotion(_reduced);
            _view.SetBatterySaver(_saver);
            _view.Build(lab.Level, lab.Level.CreateInitialState(), ShapeAtlas.ForDensity(96f));
            if (_scene)
            {
                _stage.Camera.aspect = 1080f / 2400f;
                _backdrop = _stage.Add("Backdrop").AddComponent<WorldBackdrop>();
                _backdrop.SetReducedMotion(_reduced);
                _backdrop.SetBatterySaver(_saver);
                _backdrop.Show(world, _stage.Camera, new BoardLayout.Result { OrthographicSize = 10f });
            }
        }

        private void PlayInEditor()
        {
            PlayerPrefs.SetInt("plop.debug.lab", _board);
            PlayerPrefs.SetString("plop.debug.world", _worldIds[_world]);
            PlayerPrefs.Save();
            Debug.Log("[PLOP] Visual Lab " + _board + " in " + _worldIds[_world] + " on next Play (clear with PLOP/Visual Lab/Clear Play Override).");
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("PLOP/Visual Lab/Clear Play Override", priority = 42)]
        public static void ClearOverride()
        {
            PlayerPrefs.DeleteKey("plop.debug.lab");
            PlayerPrefs.DeleteKey("plop.debug.world");
            PlayerPrefs.Save();
        }
    }
}
