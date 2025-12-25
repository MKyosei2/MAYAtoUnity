using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Represents a single Maya animation keyframe.
    /// </summary>
    [System.Serializable]
    public class AnimKeyframe
    {
        public float time;
        public float value;

        public float inTangent;
        public float outTangent;

        public AnimKeyframe(float t, float v, float inTan, float outTan)
        {
            time = t;
            value = v;
            inTangent = inTan;
            outTangent = outTan;
        }
    }
}
