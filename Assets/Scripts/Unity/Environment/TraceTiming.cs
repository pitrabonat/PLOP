using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// Presentation timing for forced movement (Frost slides, fan pushes, Loop teleports). Logic has already
    /// happened when these play; the values only shape how long the board takes to show the authoritative trace.
    /// Long chains are compressed so input never waits on a slow replay.
    /// </summary>
    public static class TraceTiming
    {
        public static float StepLead = 0.05f;
        public static float SlideSecondsPerCell = 0.065f;
        public static float PushSecondsPerCell = 0.085f;
        public static float TeleportSeconds = 0.30f;
        /// <summary>Steps still queued behind the current one before compression starts.</summary>
        public static int CompressAfter = 2;
        public static float CompressFactor = 0.8f;
        public static float MinFactor = 0.6f;

        /// <summary>Speed factor (≤ 1) for a step given how many more steps are queued behind it.</summary>
        public static float Factor(int queuedBehind)
        {
            if (queuedBehind < CompressAfter) return 1f;
            float f = Mathf.Pow(CompressFactor, queuedBehind - CompressAfter + 1);
            return Mathf.Max(MinFactor, f);
        }

        public static float Slide(int cells, int queuedBehind) => (StepLead + SlideSecondsPerCell * cells) * Factor(queuedBehind);
        public static float Push(int cells, int queuedBehind) => (StepLead + PushSecondsPerCell * cells) * Factor(queuedBehind);
        public static float Teleport(int queuedBehind) => TeleportSeconds * Factor(queuedBehind);
    }
}
