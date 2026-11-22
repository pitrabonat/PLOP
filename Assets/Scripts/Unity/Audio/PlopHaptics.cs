using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>At most one short native cue for a settled or completed move.</summary>
    internal sealed class PlopHaptics
    {
        private double _nextPulse;

        public bool Enabled { get; set; }

        public void PlayMove(GameEvent[] events)
        {
            if (!Enabled || events == null) return;
            double now = Time.unscaledTimeAsDouble;
            if (now < _nextPulse) return;

            bool settled = false;
            bool completed = false;
            foreach (var gameEvent in events)
            {
                settled |= gameEvent.Type == GameEventType.PlopSettled;
                completed |= gameEvent.Type == GameEventType.LevelCompleted;
            }
            if (!settled && !completed) return;

            _nextPulse = now + 0.12;
#if UNITY_IOS && !UNITY_EDITOR
            PlopPlayHaptic(completed ? 1 : 0);
#elif UNITY_ANDROID && !UNITY_EDITOR
            QueueAndroidFeedback(completed);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void PlopPlayHaptic(int completed);
#elif UNITY_ANDROID && !UNITY_EDITOR
        private static void QueueAndroidFeedback(bool completed)
        {
            // CONTEXT_CLICK and CLOCK_TICK are supported by the minimum Android API.
            // performHapticFeedback without override flags respects device preferences.
            const int contextClick = 6;
            const int clockTick = 4;
            int feedback = completed ? contextClick : clockTick;
            try
            {
                using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                activity?.Call("runOnUiThread", new AndroidJavaRunnable(() => PlayAndroidFeedback(feedback)));
            }
            catch (AndroidJavaException) { }
        }

        private static void PlayAndroidFeedback(int feedback)
        {
            try
            {
                using var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                using var window = activity?.Call<AndroidJavaObject>("getWindow");
                using var view = window?.Call<AndroidJavaObject>("getDecorView");
                if (view != null) view.Call<bool>("performHapticFeedback", feedback);
            }
            catch (AndroidJavaException) { }
        }
#endif
    }
}
