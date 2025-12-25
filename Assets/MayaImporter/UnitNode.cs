using UnityEngine;

namespace MayaImporter.DAG
{
    /// <summary>
    /// Maya currentUnit settings holder.
    /// Scene-wide unit definition.
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitNode : MonoBehaviour
    {
        [Header("Linear Unit")]
        public string linearUnit;   // cm, m, mm

        [Header("Angular Unit")]
        public string angleUnit;    // deg, rad

        [Header("Time Unit")]
        public string timeUnit;     // film, ntsc, pal
        public double framesPerSecond;

        /// <summary>
        /// Initialize from Maya currentUnit command.
        /// </summary>
        public void Initialize(
            string linear,
            string angle,
            string time,
            double fps)
        {
            linearUnit = linear;
            angleUnit = angle;
            timeUnit = time;
            framesPerSecond = fps;
        }
    }
}
