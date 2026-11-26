using System;
using System.Collections.Generic;
using Plop.Core.Json;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// The branding pack's <c>Animation_Timeline.json</c>: reference-canvas layout (pixels, Y down) and keyframes for the
    /// mascot drop (y offset, squash/stretch, alpha) and logo pop-in (alpha, scale). Values between keyframes are
    /// interpolated with smoothstep, as the pack specifies.
    /// </summary>
    public sealed class SplashTimeline
    {
        public struct Pose
        {
            public float YOffset, ScaleX, ScaleY, MascotAlpha, LogoAlpha, LogoScale;
        }

        private struct Key
        {
            public float T;
            public Pose Pose;
        }

        public Vector2 Canvas = new Vector2(1080f, 1920f);
        public float Duration = 1.35f;
        public float MascotWidth = 540f, MascotRestBaseY = 1220f, MascotCenterX = 540f;
        public Vector2 ShadowCenter = new Vector2(540f, 1230f);
        public float ShadowWidth = 560f;
        public Vector2 LogoCenter = new Vector2(540f, 680f);
        public Vector2 LogoMax = new Vector2(760f, 340f);
        private readonly List<Key> _keys = new List<Key>();

        public int KeyCount => _keys.Count;
        public Pose Rest => _keys.Count > 0 ? _keys[_keys.Count - 1].Pose : new Pose { ScaleX = 1f, ScaleY = 1f, MascotAlpha = 1f, LogoAlpha = 1f, LogoScale = 1f };

        public static SplashTimeline Parse(string json)
        {
            var t = new SplashTimeline();
            var o = MiniJson.AsObject(MiniJson.Parse(json), "timeline");
            if (MiniJson.Get(o, "canvas", false) is List<object> canvas && canvas.Count == 2) t.Canvas = new Vector2(Num(canvas[0]), Num(canvas[1]));
            if (o.TryGetValue("duration_seconds", out var d)) t.Duration = Num(d);
            if (MiniJson.Get(o, "mascot", false) is Dictionary<string, object> m)
            {
                t.MascotWidth = Field(m, "width", t.MascotWidth);
                t.MascotRestBaseY = Field(m, "rest_base_y", t.MascotRestBaseY);
                t.MascotCenterX = Field(m, "center_x", t.MascotCenterX);
            }
            if (MiniJson.Get(o, "shadow", false) is Dictionary<string, object> s)
            {
                t.ShadowCenter = Pair(s, "center", t.ShadowCenter);
                t.ShadowWidth = Field(s, "width", t.ShadowWidth);
            }
            if (MiniJson.Get(o, "logo", false) is Dictionary<string, object> l)
            {
                t.LogoCenter = Pair(l, "center", t.LogoCenter);
                t.LogoMax = new Vector2(Field(l, "max_width", t.LogoMax.x), Field(l, "max_height", t.LogoMax.y));
            }
            foreach (var item in MiniJson.AsArray(MiniJson.Get(o, "keyframes"), "keyframes"))
            {
                var k = MiniJson.AsObject(item, "keyframe");
                t._keys.Add(new Key
                {
                    T = Field(k, "t", 0f),
                    Pose = new Pose
                    {
                        YOffset = Field(k, "y_offset", 0f),
                        ScaleX = Field(k, "sx", 1f),
                        ScaleY = Field(k, "sy", 1f),
                        MascotAlpha = Field(k, "mascot_alpha", 1f),
                        LogoAlpha = Field(k, "logo_alpha", 1f),
                        LogoScale = Field(k, "logo_scale", 1f)
                    }
                });
            }
            if (t._keys.Count == 0) throw new FormatException("Splash timeline has no keyframes.");
            t._keys.Sort((a, b) => a.T.CompareTo(b.T));
            return t;
        }

        /// <summary>Pose at <paramref name="time"/> seconds (clamped to the timeline), smoothstep between keyframes.</summary>
        public Pose Sample(float time)
        {
            if (time <= _keys[0].T) return _keys[0].Pose;
            for (int i = 1; i < _keys.Count; i++)
            {
                if (time > _keys[i].T) continue;
                var a = _keys[i - 1];
                var b = _keys[i];
                float u = b.T > a.T ? (time - a.T) / (b.T - a.T) : 1f;
                u = u * u * (3f - 2f * u);
                return new Pose
                {
                    YOffset = Mathf.LerpUnclamped(a.Pose.YOffset, b.Pose.YOffset, u),
                    ScaleX = Mathf.LerpUnclamped(a.Pose.ScaleX, b.Pose.ScaleX, u),
                    ScaleY = Mathf.LerpUnclamped(a.Pose.ScaleY, b.Pose.ScaleY, u),
                    MascotAlpha = Mathf.LerpUnclamped(a.Pose.MascotAlpha, b.Pose.MascotAlpha, u),
                    LogoAlpha = Mathf.LerpUnclamped(a.Pose.LogoAlpha, b.Pose.LogoAlpha, u),
                    LogoScale = Mathf.LerpUnclamped(a.Pose.LogoScale, b.Pose.LogoScale, u)
                };
            }
            return Rest;
        }

        private static float Num(object v) => v is long l ? l : v is double d ? (float)d : throw new FormatException("number expected");
        private static float Field(Dictionary<string, object> o, string key, float fallback) => o.TryGetValue(key, out var v) ? Num(v) : fallback;
        private static Vector2 Pair(Dictionary<string, object> o, string key, Vector2 fallback)
            => o.TryGetValue(key, out var v) && v is List<object> p && p.Count == 2 ? new Vector2(Num(p[0]), Num(p[1])) : fallback;
    }
}
