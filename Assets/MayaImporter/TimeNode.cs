using UnityEngine;

namespace MayaImporter.DAG
{
    /// <summary>
    /// Maya time node (time1).
    /// Holds global time information for the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeNode : MonoBehaviour
    {
        [Header("Frame Range")]
        public double startFrame;
        public double endFrame;
        public double currentFrame;

        [Header("Time Unit")]
        public string timeUnit;   // e.g. film, ntsc, pal
        public double framesPerSecond;

        [Header("Playback")]
        public bool playbackLoop;
        public bool playbackRealtime;

        /// <summary>
        /// Called by importer after attributes are assigned.
        /// </summary>
        public void Initialize(
            double start,
            double end,
            double current,
            string unit,
            double fps)
        {
            startFrame = start;
            endFrame = end;
            currentFrame = current;
            timeUnit = unit;
            framesPerSecond = fps;
        }
    }
}
