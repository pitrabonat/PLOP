using System.Collections;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>One opaque authored image behind the board, with an optional short entry dissolve.</summary>
    public sealed class WorldBackdrop : MonoBehaviour
    {
        // Behind the tray and its contact shadow. Screen-space HUD stays above the camera output.
        private const int BackgroundSortingOrder = -10000;
        private const float Coverage = 1.01f;

        private SpriteRenderer _renderer;
        private WorldTheme _theme;
        private Coroutine _reveal;
        private bool _reducedMotion;

        public void Show(WorldTheme theme, Camera camera, BoardLayout.Result layout)
        {
            if (_renderer == null)
            {
                var art = new GameObject("World Art");
                art.transform.SetParent(transform, false);
                _renderer = art.AddComponent<SpriteRenderer>();
                _renderer.sortingOrder = BackgroundSortingOrder;
            }

            if (_theme != theme)
            {
                EndReveal();
                _theme = theme;
                _renderer.sprite = theme == null ? null : theme.LoadBackdrop();
            }

            bool visible = camera != null && _renderer.sprite != null;
            _renderer.enabled = visible;
            if (!visible) return;

            // Uniform cover keeps the authored props in proportion as the portrait aspect changes.
            float height = 2f * layout.OrthographicSize;
            float width = height * camera.aspect;
            Vector3 artSize = _renderer.sprite.bounds.size;
            float scale = Mathf.Max(width / artSize.x, height / artSize.y) * Coverage;
            Vector3 center = camera.transform.position;
            Transform artTransform = _renderer.transform;
            artTransform.position = new Vector3(center.x, center.y, 0f);
            artTransform.localScale = new Vector3(scale, scale, 1f);
        }

        public void SetReducedMotion(bool value)
        {
            _reducedMotion = value;
            if (value) EndReveal();
        }

        public void PlayReveal()
        {
            if (_reducedMotion || _theme == null || _theme.RevealSeconds <= 0f ||
                _renderer == null || !_renderer.enabled || !isActiveAndEnabled)
                return;

            EndReveal();
            _reveal = StartCoroutine(Reveal(_theme.RevealSeconds));
        }

        private IEnumerator Reveal(float duration)
        {
            float started = Time.unscaledTime;
            while (Time.unscaledTime < started + duration)
            {
                float elapsed = (Time.unscaledTime - started) / duration;
                float opacity = Mathf.SmoothStep(0.28f, 1f, elapsed);
                _renderer.color = new Color(1f, 1f, 1f, opacity);
                yield return null;
            }

            _renderer.color = Color.white;
            _reveal = null;
        }

        private void OnDisable() => EndReveal();

        private void EndReveal()
        {
            if (_reveal != null)
            {
                StopCoroutine(_reveal);
                _reveal = null;
            }

            if (_renderer != null) _renderer.color = Color.white;
        }
    }
}
