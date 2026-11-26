using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// The World background: the authored far image (opaque) plus, since Phase 11.5, one animated ambient layer
    /// (PlopAmbient.shader: mid props such as windmill sails, clouds or a light beam, and near particles such as
    /// snow, embers or seeds) that stays clear of the board. Only the current World exists; the ambient clock
    /// advances in this one Update and only while the layer is visible and motion is allowed.
    /// </summary>
    public sealed class WorldBackdrop : MonoBehaviour
    {
        // Behind the tray and its contact shadow. Screen-space HUD stays above the camera output.
        private const int BackgroundSortingOrder = -10000;
        private const int AmbientSortingOrder = -9990;
        private const float Coverage = 1.01f;

        private SpriteRenderer _renderer;
        private GameObject _ambientRoot;
        private readonly List<Renderer> _ambientRenderers = new List<Renderer>();
        private Mesh _particleMesh;
        private WorldAmbientProfile _ambientProfile;
        private bool _ambientOn;
        private static readonly int ArtSizeId = Shader.PropertyToID("_PlopArtSize");
        private static readonly int ArtRectId = Shader.PropertyToID("_PlopArtRect");
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
                _ambientProfile = theme == null ? null : WorldAmbient.For(theme.Id);
                ApplyAmbientSettings();
                BuildAmbient();
            }

            bool visible = camera != null && _renderer.sprite != null;
            _renderer.enabled = visible;
            EnsureAmbient(visible);
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
            // Backdrop pixel space for the ambient shaders (local size for the particle mesh, world rect for regions).
            Shader.SetGlobalVector(ArtSizeId, new Vector4(artSize.x, artSize.y, 0f, 0f));
            Shader.SetGlobalVector(ArtRectId, new Vector4(center.x - artSize.x * scale * 0.5f, center.y + artSize.y * scale * 0.5f,
                WorldAmbient.ArtPixels.x / (artSize.x * scale), WorldAmbient.ArtPixels.y / (artSize.y * scale)));
        }

        private bool _ambientVisible = true, _batterySaver;

        /// <summary>True when the animated layers are drawn (World has a profile, shaders supported, not hidden).</summary>
        public bool AmbientActive => _ambientOn;
        public AmbientMode? CurrentAmbient => _ambientProfile?.Mode;
        /// <summary>Region quads plus the particle mesh renderer for the current World (diagnostics, tests).</summary>
        public int AmbientRendererCount => _ambientRenderers.Count;

        public void SetAmbientVisible(bool visible)
        {
            if (_ambientVisible == visible) return;
            _ambientVisible = visible;
            EnsureAmbient(_renderer != null && _renderer.enabled);
        }

        public void SetBatterySaver(bool saver)
        {
            if (_batterySaver == saver) return;
            _batterySaver = saver;
            ApplyAmbientSettings();
        }

        public void SetReducedMotion(bool value)
        {
            _reducedMotion = value;
            if (value) EndReveal();
            ApplyAmbientSettings();
        }

        private void ApplyAmbientSettings() => WorldAmbient.Apply(_ambientProfile, _batterySaver, _reducedMotion);

        /// <summary>Only the current World's layers exist: the previous World's quads and mesh are destroyed.</summary>
        private void BuildAmbient()
        {
            ReleaseAmbient();
            if (_ambientProfile == null || _renderer == null || _renderer.sprite == null) return;
            _ambientRoot = new GameObject("World Ambient");
            _ambientRoot.transform.SetParent(_renderer.transform, false);
            Vector2 size = _renderer.sprite.bounds.size;
            Vector2 art = WorldAmbient.ArtPixels;
            if (PlopShaders.Ambient != null)
            {
                var data = PlopShaders.Pack(new Color((byte)_ambientProfile.Mode / 255f, 0f, 0f, 1f));
                foreach (var r in _ambientProfile.regions)
                {
                    var go = new GameObject("Ambient region");
                    go.transform.SetParent(_ambientRoot.transform, false);
                    go.transform.localPosition = new Vector3(((r.x + r.w * 0.5f) / art.x - 0.5f) * size.x, (0.5f - (r.y + r.h * 0.5f) / art.y) * size.y, 0f);
                    go.transform.localScale = new Vector3(r.w / art.x * size.x, r.h / art.y * size.y, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = PlopShaders.Quad;
                    sr.sharedMaterial = PlopShaders.Ambient;
                    sr.sortingOrder = AmbientSortingOrder;
                    sr.color = data;
                    _ambientRenderers.Add(sr);
                }
            }
            if (PlopShaders.AmbientParticles != null)
            {
                _particleMesh = WorldAmbient.BuildParticleMesh(_ambientProfile);
                if (_particleMesh != null)
                {
                    var go = new GameObject("Ambient particles");
                    go.transform.SetParent(_ambientRoot.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = _particleMesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = PlopShaders.AmbientParticles;
                    mr.sortingOrder = AmbientSortingOrder + 1;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    _ambientRenderers.Add(mr);
                }
            }
            EnsureAmbient(_renderer.enabled);
        }

        private void ReleaseAmbient()
        {
            _ambientRenderers.Clear();
            _ambientOn = false;
            if (_ambientRoot != null)
            {
                _ambientRoot.transform.SetParent(null, false);   // gone from the hierarchy now, destroyed at frame end
                _ambientRoot.SetActive(false);
                DestroyObject(_ambientRoot);
            }
            if (_particleMesh != null) DestroyObject(_particleMesh);
            _ambientRoot = null;
            _particleMesh = null;
        }

        private static void DestroyObject(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        private void OnDestroy() => ReleaseAmbient();

        private void EnsureAmbient(bool backdropVisible)
        {
            _ambientOn = backdropVisible && _ambientVisible && _ambientRenderers.Count > 0;
            foreach (var r in _ambientRenderers) if (r != null) r.enabled = _ambientOn;
        }

        private void Update()
        {
            if (!AmbientActive) return;
            float speed = (_ambientProfile?.speed ?? 1f) * (_batterySaver ? WorldAmbient.SaverSpeed : 1f);
            WorldAmbient.Advance(Time.unscaledDeltaTime, speed);
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
                WorldAmbient.SetOpacity(opacity);
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
            WorldAmbient.SetOpacity(1f);
        }
    }
}
