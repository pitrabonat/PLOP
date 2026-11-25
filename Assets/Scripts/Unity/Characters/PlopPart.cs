using UnityEngine;
using UnityEngine.UI;

namespace Plop.Presentation
{
    /// <summary>
    /// One layer of a Plop rig, backed by a SpriteRenderer on the board or a UI Image in menus and the Studio.
    /// Positions are in cell units in both cases (UI rigs scale their root), so a single animation drives both.
    /// </summary>
    internal sealed class PlopPart
    {
        public readonly Transform Transform;
        private readonly SpriteRenderer _renderer;
        private readonly Image _image;
        private Sprite _sprite;
        private Color _color = Color.white;
        private bool _visible = true;

        public PlopPart(string name, Transform parent, Sprite sprite, bool ui)
        {
            if (ui)
            {
                var go = new GameObject(name, typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                var parentRect = parent as RectTransform;
                rt.anchorMin = rt.anchorMax = parentRect != null ? parentRect.pivot : new Vector2(0.5f, 0.5f);
                _image = go.AddComponent<Image>();
                _image.raycastTarget = false;
                Transform = rt;
            }
            else
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                _renderer = go.AddComponent<SpriteRenderer>();
                Transform = go.transform;
            }
            Sprite = sprite;
        }

        public SpriteRenderer Renderer => _renderer;
        public Image Image => _image;

        public Sprite Sprite
        {
            get => _sprite;
            set
            {
                if (ReferenceEquals(_sprite, value)) return;
                _sprite = value;
                if (_renderer != null)
                {
                    _renderer.sprite = value;
                    return;
                }
                _image.sprite = value;
                _image.enabled = value != null && _visible;
                if (value == null) return;
                var rt = (RectTransform)Transform;
                rt.sizeDelta = value.rect.size / value.pixelsPerUnit;
                rt.pivot = new Vector2(value.pivot.x / value.rect.width, value.pivot.y / value.rect.height);
            }
        }

        public Color Color
        {
            get => _color;
            set
            {
                if (_color == value) return;
                _color = value;
                if (_renderer != null) _renderer.color = value;
                else _image.color = value;
            }
        }

        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible == value) return;
                _visible = value;
                if (_renderer != null) _renderer.enabled = value;
                else _image.enabled = value && _sprite != null;
            }
        }

        public int Order
        {
            set { if (_renderer != null) _renderer.sortingOrder = value; }
        }
    }
}
