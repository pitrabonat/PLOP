#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace Plop.Presentation
{
    /// <summary>Local inspection enabled explicitly on a development player.</summary>
    internal sealed class PlopDiagnostics : MonoBehaviour
    {
        internal const string Preference = "plop.debug.diagnostics";
        private const int WindowSize = 120;
        private readonly float[] _frames = new float[WindowSize];
        private readonly float[] _sorted = new float[WindowSize];
        private PlopGameController _controller;
        private ProfilerRecorder _main, _render, _gc, _draws;
        private int _count, _cursor;
        private float _nextRefresh;
        private string _view = "";
        private string _recentIssue = "";
        private GUIStyle _style;
        // Phase 12: frame-time distribution over 10 s windows, written to the log (logcat / Xcode) for reports.
        private const float ReportSeconds = 10f;
        private readonly float[] _window = new float[2400];
        private int _windowCount, _gcAtWindow;
        private float _windowStart = -1f;

        internal void Inspect(PlopGameController controller)
        {
            _controller = controller;
            _main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            _render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread");
            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            Application.logMessageReceived += RememberGameplayIssue;
        }

        private void RememberGameplayIssue(string message, string stack, LogType type)
        {
            if (type != LogType.Log && message.StartsWith("[PLOP] ", StringComparison.Ordinal))
                _recentIssue = message.Length > 120 ? message.Substring(0, 120) : message;
        }

        private void Update()
        {
            if (_controller == null || _controller.IsSuspended) { _windowStart = -1f; return; }
            _frames[_cursor] = 1000f * Time.unscaledDeltaTime;
            _cursor = (_cursor + 1) % WindowSize;
            _count = Mathf.Min(_count + 1, WindowSize);
            RecordWindow(1000f * Time.unscaledDeltaTime);
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;

            Array.Copy(_frames, _sorted, _count);
            Array.Sort(_sorted, 0, _count);
            float median = Percentile(0.5f);
            var engine = _controller.Engine;
            var level = engine.HasLevel ? engine.Level.Id : "none";
            var phase = _controller.DiagnosticState;
            _view = $"Level {level} · World {_controller.CurrentWorld?.Id ?? "none"}\n" +
                $"{phase} · Moves {engine.MoveCount} · Pending {_controller.PendingMove?.ToString() ?? "none"}\n" +
                $"Loop {1000f / Mathf.Max(0.01f, median):F1} FPS · p50 {median:F1} / p90 {Percentile(0.9f):F1} ms\n" +
                $"Main {FrameTime(_main)} · Render {FrameTime(_render)} · GC {Counter(_gc)} B · Draws {Counter(_draws)}\n" +
                $"Unity allocated {Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F1} MiB";
            if (_recentIssue.Length > 0) _view += "\n" + _recentIssue;
        }

        private void RecordWindow(float ms)
        {
            float now = Time.unscaledTime;
            if (_windowStart < 0f) { _windowStart = now; _gcAtWindow = GC.CollectionCount(0); _windowCount = 0; return; }
            if (_windowCount < _window.Length) _window[_windowCount++] = ms;
            if (now - _windowStart < ReportSeconds) return;
            int n = _windowCount;
            if (n > 0)
            {
                Array.Sort(_window, 0, n);
                double sum = 0; for (int i = 0; i < n; i++) sum += _window[i];
                float budget = Application.targetFrameRate > 0 ? 1000f / Application.targetFrameRate : 16.7f;
                int spikes = 0; for (int i = 0; i < n; i++) if (_window[i] > 2f * budget) spikes++;
                float P(float f) => _window[Mathf.Clamp(Mathf.CeilToInt(n * f) - 1, 0, n - 1)];
                Debug.Log($"[PLOP] frames target {Application.targetFrameRate} n {n} avg {1000.0 * n / sum:F1} FPS " +
                    $"p50 {P(0.5f):F1} p95 {P(0.95f):F1} p99 {P(0.99f):F1} max {_window[n - 1]:F1} ms spikes>{2f * budget:F0}ms {spikes} " +
                    $"gc0 {GC.CollectionCount(0) - _gcAtWindow} heap {Profiler.GetMonoUsedSizeLong() / 1048576f:F1} MiB " +
                    $"unity {Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F1} MiB level {(_controller.Engine.HasLevel ? _controller.Engine.Level.Id : "none")}");
            }
            _windowStart = now;
            _windowCount = 0;
            _gcAtWindow = GC.CollectionCount(0);
        }

        private float Percentile(float fraction) => _sorted[Mathf.CeilToInt(_count * fraction) - 1];
        private static string FrameTime(ProfilerRecorder sample)
            => sample.Valid && sample.Count > 0 ? $"{sample.LastValue / 1_000_000f:F1} ms" : "n/a";
        private static string Counter(ProfilerRecorder sample)
            => sample.Valid && sample.Count > 0 ? sample.LastValue.ToString() : "n/a";

        private void OnGUI()
        {
            if (_view.Length == 0) return;
            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.8f, 2f);
            _style ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            _style.normal.textColor = Color.white;
            _style.fontSize = Mathf.RoundToInt(15f * scale);
            Rect card = new Rect(safe.xMin + 8f * scale, Screen.height - safe.yMax + 8f * scale,
                safe.width - 16f * scale, 142f * scale);
            GUI.Box(card, GUIContent.none);
            GUI.Label(new Rect(card.x + 10f * scale, card.y + 8f * scale,
                card.width - 20f * scale, card.height), _view, _style);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= RememberGameplayIssue;
            _main.Dispose();
            _render.Dispose();
            _gc.Dispose();
            _draws.Dispose();
        }
    }
}
#endif
