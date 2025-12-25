using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Base class for all Maya animation curves.
    /// Stores keyframe data independent of Unity.
    /// Phase-1 강화:
    /// - WrapMode(pre/post) を保持し、ToUnityCurve に反映
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class AnimCurveBase : MonoBehaviour
    {
        [Header("Keyframes")]
        public float[] times;
        public float[] values;

        [Header("Tangents")]
        public float[] inTangents;
        public float[] outTangents;

        [Header("Wrap (Unity, best-effort)")]
        public WrapMode preWrapMode = WrapMode.ClampForever;
        public WrapMode postWrapMode = WrapMode.ClampForever;

        public virtual void Initialize(
            float[] keyTimes,
            float[] keyValues,
            float[] inTan,
            float[] outTan)
        {
            times = keyTimes;
            values = keyValues;
            inTangents = inTan;
            outTangents = outTan;
        }

        /// <summary>
        /// Builds a Unity AnimationCurve (best-effort).
        /// </summary>
        public virtual AnimationCurve ToUnityCurve()
        {
            if (times == null || values == null)
                return null;

            int count = Mathf.Min(times.Length, values.Length);
            Keyframe[] keys = new Keyframe[count];

            for (int i = 0; i < count; i++)
            {
                Keyframe kf = new Keyframe(times[i], values[i]);

                if (inTangents != null && i < inTangents.Length)
                    kf.inTangent = inTangents[i];
                if (outTangents != null && i < outTangents.Length)
                    kf.outTangent = outTangents[i];

                keys[i] = kf;
            }

            var curve = new AnimationCurve(keys)
            {
                preWrapMode = preWrapMode,
                postWrapMode = postWrapMode
            };

            return curve;
        }
    }
}
