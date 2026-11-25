using System.Collections.Generic;
using System.Linq;
using Plop.Core;
using Plop.Presentation;
using UnityEditor;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>
    /// PLOP Animation Lab (Editor only): previews the runtime Plop rig with any cosmetic look, expression and
    /// animation state at adjustable speed, with Reduced Motion. Uses the shipped PlopView, so what you see is
    /// exactly what gameplay plays. Menu: PLOP → Characters → Animation Lab.
    /// </summary>
    public sealed class AnimationLabWindow : EditorWindow
    {
        private CharacterStage _stage;
        private PlopView _view;
        private ShapeAtlas _atlas;
        private string _style = "classic", _finish = "classic", _palette = "classic", _face = "classic", _accessory = "none", _nest = "classic", _effect = "none";
        private int _color;
        private Expression _expression = Expression.Neutral;
        private bool _holdExpression, _reducedMotion, _showNest = true, _darkBackdrop;
        private float _speed = 1f, _clock = 100f, _zoom = 1.6f;
        private double _lastTime;
        private Vector3 _position;
        private Transform _nestObject;

        [MenuItem("PLOP/Characters/Animation Lab", priority = 20)]
        public static void Open()
        {
            var w = GetWindow<AnimationLabWindow>("PLOP Animation Lab");
            w.minSize = new Vector2(640, 560);
        }

        private void OnEnable()
        {
            _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Step;
            Rebuild();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Step;
            _stage?.Dispose();
            _stage = null;
        }

        private CharacterLook Look() => CharacterArt.Compose(CharacterArt.Styles[_style], CharacterArt.Finishes[_finish],
            CharacterArt.Palettes[_palette], CharacterArt.Faces[_face], CharacterArt.Accessories[_accessory],
            CharacterArt.Nests[_nest], CharacterArt.Effects[_effect]);

        private void Rebuild()
        {
            _stage?.Dispose();
            _stage = new CharacterStage(_darkBackdrop ? PlopPalette.Hex(0x3E3550) : PlopPalette.PlayBackground);
            _atlas = ShapeAtlas.ForDensity(256, Look());
            if (_showNest)
            {
                var nest = _stage.Add("Nest").AddComponent<SpriteRenderer>();
                nest.sprite = _atlas.Get(ShapeAtlas.NestKey(_color));
                nest.sortingOrder = -10;
                nest.transform.localPosition = new Vector3(1.1f, 0f, 0f);
                _nestObject = nest.transform;
            }
            var tile = _stage.Add("Tile").AddComponent<SpriteRenderer>();
            tile.sprite = _atlas.Get("tile");
            tile.sortingOrder = -20;
            _view = new PlopView(_stage.Root, _atlas, _color, 3, _reducedMotion);
            _view.SetSortingBase(0);
            _position = Vector3.zero;
            _view.Snap(_position, false);
            if (_holdExpression) _view.SetExpression(_expression);
        }

        private void Step()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastTime);
            _lastTime = now;
            _clock += dt * _speed;
            _view?.Tick(_clock);
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                _style = Popup("Body", _style, CharacterArt.Styles.Keys);
                _finish = Popup("Finish", _finish, CharacterArt.Finishes.Keys);
                _palette = Popup("Palette", _palette, CharacterArt.Palettes.Keys);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _face = Popup("Face", _face, CharacterArt.Faces.Keys);
                _accessory = Popup("Accessory", _accessory, CharacterArt.Accessories.Keys);
                _nest = Popup("Nest", _nest, CharacterArt.Nests.Keys);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _effect = Popup("Effect", _effect, CharacterArt.Effects.Keys);
                _color = EditorGUILayout.Popup("Colour slot", _color, PlopPalette.SlotNames);
                _showNest = EditorGUILayout.ToggleLeft("Show Nest", _showNest, GUILayout.Width(90));
                _darkBackdrop = EditorGUILayout.ToggleLeft("Dark", _darkBackdrop, GUILayout.Width(60));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _reducedMotion = EditorGUILayout.ToggleLeft("Reduced Motion", _reducedMotion, GUILayout.Width(130));
                _speed = EditorGUILayout.Slider("Speed", _speed, 0.05f, 2f);
            }
            if (EditorGUI.EndChangeCheck()) Rebuild();

            using (new EditorGUILayout.HorizontalScope())
            {
                _expression = (Expression)EditorGUILayout.EnumPopup("Expression", _expression);
                if (GUILayout.Button("Hold", GUILayout.Width(60))) { _holdExpression = true; _view.SetExpression(_expression); }
                if (GUILayout.Button("Release", GUILayout.Width(70))) { _holdExpression = false; _view.ClearExpression(); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Idle")) { _view.ClearExpression(); _view.Snap(_position, false); }
                if (GUILayout.Button("Move ←")) Move(Vector2.left);
                if (GUILayout.Button("Move →")) Move(Vector2.right);
                if (GUILayout.Button("Move ↑")) Move(Vector2.up);
                if (GUILayout.Button("Move ↓")) Move(Vector2.down);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bump wall")) _view.StartBump(Vector2.right, _clock, BlockReason.Wall);
                if (GUILayout.Button("Bump Plop")) _view.StartBump(Vector2.right, _clock, BlockReason.Plop);
                if (GUILayout.Button("Settle")) _view.QueueSleep(_clock);
                if (GUILayout.Button("Wake")) _view.Snap(_position, false);
                if (GUILayout.Button("Celebrate")) _view.StartHop(_clock);
                if (GUILayout.Button("Undo ease")) { _view.Snap(_position + Vector3.right * 0.6f, false); _view.Restore(_position, false, true); }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (Reaction r in System.Enum.GetValues(typeof(Reaction)))
                    if (GUILayout.Button(r.ToString())) _view.React(r, _clock);
            }
            _zoom = EditorGUILayout.Slider("Zoom (cells)", _zoom, 1.2f, 4f);

            var rect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint && _stage != null)
                _stage.Draw(rect, new Vector2(_showNest ? 0.55f : 0f, 0.05f), _zoom);

            var acc = _view?.AccessoryTransform;
            EditorGUILayout.LabelField("Expression shown", _view != null ? _view.CurrentExpression.ToString() : "-");
            EditorGUILayout.LabelField("Accessory attachment", acc == null ? "none" :
                CharacterArt.Accessories[_accessory].Anchor + " at " + ((Vector2)acc.localPosition).ToString("0.000") + ", " + acc.localEulerAngles.z.ToString("0") + "°");
            EditorGUILayout.LabelField("Atlas", _atlas == null ? "-" : _atlas.PixelsPerUnit + " px/cell, " + (_atlas.TextureBytes / 1024) + " KB, baked in " + _atlas.BakeMilliseconds.ToString("0") + " ms");
        }

        private void Move(Vector2 dir)
        {
            _position += (Vector3)dir;
            if (Mathf.Abs(_position.x) > 1.5f || Mathf.Abs(_position.y) > 1.5f) _position = Vector3.zero;
            _view.StartMove(_position, dir, _clock);
        }

        private static string Popup(string label, string value, IEnumerable<string> options)
        {
            var list = options.ToArray();
            int index = Mathf.Max(0, System.Array.IndexOf(list, value));
            return list[EditorGUILayout.Popup(label, index, list)];
        }
    }
}
