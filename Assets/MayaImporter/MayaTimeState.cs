using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Global time state for Maya-style evaluation (Unity only).
    /// Time is expressed in "frames" (float).
    /// </summary>
    public static class MayaTimeState
    {
        public static float Fps = 24f;

        /// <summary>Current time in frames.</summary>
        public static float CurrentFrame = 0f;

        /// <summary>Playback on/off.</summary>
        public static bool Playing = true;

        /// <summary>Playback speed multiplier.</summary>
        public static float Speed = 1f;

        /// <summary>If true, time loops in [StartFrame, EndFrame].</summary>
        public static bool Loop = true;

        public static float StartFrame = 0f;
        public static float EndFrame = 120f;

        public static void SetRange(float start, float end)
        {
            StartFrame = start;
            EndFrame = Mathf.Max(end, start + 1f);
            CurrentFrame = Mathf.Clamp(CurrentFrame, StartFrame, EndFrame);
        }
    }
}
