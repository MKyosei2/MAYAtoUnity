using UnityEngine;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya NonLinear Deformer ‹¤’ÊŠî’êƒNƒ‰ƒX
    /// Bend / Twist / Squash / Wave / Flare / Sine
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class NonLinearDeformerBase : DeformerBase
    {
        [Header("NonLinear Common")]

        [Tooltip("Low bound of deformation")]
        public float lowBound = -1f;

        [Tooltip("High bound of deformation")]
        public float highBound = 1f;

        [Tooltip("Deformer curvature")]
        public float curvature = 0f;

        [Tooltip("Start angle (degrees)")]
        public float startAngle = 0f;

        [Tooltip("End angle (degrees)")]
        public float endAngle = 0f;

        [Tooltip("Amplitude")]
        public float amplitude = 1f;

        [Tooltip("Offset")]
        public float offset = 0f;

        [Tooltip("Dropoff")]
        public float dropoff = 0f;

        [Header("Axis / Direction")]

        [Tooltip("Deformation axis")]
        public Vector3 axis = Vector3.up;

        [Tooltip("Deformation direction")]
        public Vector3 direction = Vector3.up;

        [Header("Space")]

        [Tooltip("Use local space")]
        public bool useLocalSpace = true;

        [Header("Handle / Transform")]

        [Tooltip("Handle transform node name")]
        public string handleNode;

        [Tooltip("Handle parent transform node name")]
        public string handleParentNode;

        /// <summary>
        /// Initialize common NonLinear attributes
        /// </summary>
        public virtual void InitializeNonLinear(
            float low,
            float high,
            float curv,
            float startAng,
            float endAng,
            float amp,
            float off,
            float drop,
            Vector3 deformAxis,
            Vector3 deformDirection,
            bool localSpace)
        {
            lowBound = low;
            highBound = high;
            curvature = curv;
            startAngle = startAng;
            endAngle = endAng;
            amplitude = amp;
            offset = off;
            dropoff = drop;
            axis = deformAxis;
            direction = deformDirection;
            useLocalSpace = localSpace;
        }

        /// <summary>
        /// Assign handle transform information
        /// </summary>
        public virtual void SetHandle(string handle, string parent)
        {
            handleNode = handle;
            handleParentNode = parent;
        }
    }
}
