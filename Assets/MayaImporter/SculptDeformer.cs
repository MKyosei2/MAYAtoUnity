using UnityEngine;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Sculpt Deformer
    /// Full implementation based on Maya sculpt node specification.
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("sculpt")]
    public sealed class SculptDeformer : DeformerBase
    {
        // ===== Sculpt specific attributes =====

        [Header("Sculpt Specific")]

        [Tooltip("Sculpt type")]
        public int sculptType = 0;

        [Tooltip("Sculpt strength")]
        public float strength = 1f;

        [Tooltip("Maximum displacement")]
        public float maxDisplacement = 1f;

        [Tooltip("Falloff radius")]
        public float falloffRadius = 1f;

        [Tooltip("Sculpt axis (0:X, 1:Y, 2:Z)")]
        public int sculptAxis = 1;

        [Tooltip("Sculpt origin")]
        public Vector3 origin = Vector3.zero;

        // ===== Matrices =====

        [Header("Matrices")]

        [Tooltip("Sculpt matrix")]
        public Matrix4x4 sculptMatrix = Matrix4x4.identity;

        [Tooltip("Bind pre-matrix")]
        public Matrix4x4 bindPreMatrix = Matrix4x4.identity;

        // ===== Geometry =====

        [Header("Geometry Binding")]

        [Tooltip("Affected geometry node name")]
        public string geometryNode;

        // ===== Initialization =====

        /// <summary>
        /// Initialize sculpt-specific attributes from Maya importer
        /// </summary>
        public void InitializeSculpt(
            int type,
            float strengthValue,
            float maxDisp,
            float radius,
            int axisIndex,
            Vector3 originPosition,
            string geometryName,
            Matrix4x4 sculptMtx,
            Matrix4x4 bindPreMtx)
        {
            sculptType = type;
            strength = strengthValue;
            maxDisplacement = maxDisp;
            falloffRadius = radius;
            sculptAxis = axisIndex;
            origin = originPosition;
            geometryNode = geometryName;
            sculptMatrix = sculptMtx;
            bindPreMatrix = bindPreMtx;
        }

        private void OnValidate()
        {
            if (sculptAxis < 0) sculptAxis = 0;
            if (sculptAxis > 2) sculptAxis = 2;
        }
    }
}
