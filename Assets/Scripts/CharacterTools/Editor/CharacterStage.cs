using System;
using UnityEditor;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>
    /// Isolated preview scene with an orthographic camera for rendering runtime Plop rigs in the Editor
    /// (Animation Lab, reference sheets). Nothing here touches the open scene or ships in a player.
    /// </summary>
    internal sealed class CharacterStage : IDisposable
    {
        private readonly PreviewRenderUtility _utility;
        private readonly GameObject _root;

        public Transform Root => _root.transform;
        public Camera Camera => _utility.camera;

        public CharacterStage(Color background)
        {
            _utility = new PreviewRenderUtility();
            var cam = _utility.camera;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            _root = new GameObject("Character Stage");
            _utility.AddSingleGO(_root);
        }

        public GameObject Add(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            return go;
        }

        public void Clear()
        {
            for (int i = _root.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(_root.transform.GetChild(i).gameObject);
        }

        /// <summary>Renders the world rectangle (centre, size in cells) into a readable texture.</summary>
        public Texture2D Capture(Vector2 center, Vector2 size, int width, int height)
        {
            var cam = _utility.camera;
            cam.aspect = width / (float)height;
            cam.orthographicSize = Mathf.Max(size.y * 0.5f, size.x * 0.5f / cam.aspect);
            cam.transform.position = new Vector3(center.x, center.y, -10f);
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 1;
            var previous = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        /// <summary>Draws the current camera view into an Editor rect (Animation Lab).</summary>
        public void Draw(Rect rect, Vector2 center, float height)
        {
            var cam = _utility.camera;
            cam.orthographicSize = height * 0.5f;
            cam.transform.position = new Vector3(center.x, center.y, -10f);
            _utility.BeginPreview(rect, GUIStyle.none);
            cam.Render();
            _utility.EndAndDrawPreview(rect);
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            _utility.Cleanup();
        }
    }
}
