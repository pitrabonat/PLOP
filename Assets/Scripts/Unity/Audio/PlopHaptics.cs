using Plop.Core;
using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>One optional native cue per swipe, with a shared pulse limit.</summary>
    internal sealed class PlopHaptics
    {
        internal enum Cue : byte { None, Blocked, Move, Settle, Complete }

        private double _nextPulse;
        public bool Enabled { get; set; }

        internal static Cue SelectCue(GameEvent[] events)
        {
            if (events == null) return Cue.None;
            var cue = Cue.None;
            foreach (var gameEvent in events)
            {
                switch (gameEvent.Type)
                {
                    case GameEventType.LevelCompleted: return Cue.Complete;
                    case GameEventType.PlopSettled: cue = Cue.Settle; break;
                    case GameEventType.PlopMoved: if (cue < Cue.Move) cue = Cue.Move; break;
                    case GameEventType.PlopBlocked: if (cue == Cue.None) cue = Cue.Blocked; break;
                }
            }
            return cue;
        }

        public void PlayMove(GameEvent[] events)
        {
            if (!Enabled) return;
            var cue = SelectCue(events);
            if (cue == Cue.None) return;

            double now = Time.unscaledTimeAsDouble;
            if (now < _nextPulse) return;
            _nextPulse = now + 0.14;

#if UNITY_IOS && !UNITY_EDITOR
            PlopPlayHaptic((int)cue);
#elif UNITY_ANDROID && !UNITY_EDITOR
            QueueAndroidFeedback(cue);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void PlopPlayHaptic(int cue);
#elif UNITY_ANDROID && !UNITY_EDITOR
        private static void QueueAndroidFeedback(Cue cue)
        {
            // These feedback constants are available on the minimum supported API.
            const int keyboardTap = 3;
            const int clockTick = 4;
            const int contextClick = 6;
            int feedback = cue == Cue.Complete ? contextClick : cue == Cue.Blocked ? keyboardTap : clockTick;
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
