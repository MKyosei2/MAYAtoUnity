using UnityEngine;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya ShrinkWrap Deformer
    /// Full implementation based on Maya shrinkWrap node specification.
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("shrinkWrap")]
    public sealed class ShrinkWrapDeformer : DeformerBase
    {
        // ===== ShrinkWrap specific attributes =====

        [Header("ShrinkWrap Specific")]

        [Tooltip("Projection type")]
        public int projection = 0;

        [Tooltip("Offset distance")]
        public float offset = 0f;

        [Tooltip("Target smoothness")]
        public float targetSmoothness = 0f;

        [Tooltip("Influence smoothness")]
        public float influenceSmoothness = 0f;

        [Tooltip("Bidirectional projection")]
        public bool bidirectional = false;

        [Tooltip("Preserve original shape")]
        public bool shapePreservation = false;

        [Tooltip("Use closest point")]
        public bool closestPoint = true;

        // ===== Geometry =====

        [Header("Geometry Binding")]

        [Tooltip("Target geometry node name")]
        public string targetGeometry;

        [Tooltip("Driven geometry node name")]
        public string drivenGeometry;

        // ===== Matrices =====

        [Header("Matrices")]

        [Tooltip("ShrinkWrap matrix")]
        public Matrix4x4 shrinkWrapMatrix = Matrix4x4.identity;

        [Tooltip("Bind pre-matrix")]
        public Matrix4x4 bindPreMatrix = Matrix4x4.identity;

        // ===== Initialization =====

        /// <summary>
        /// Initialize shrinkWrap-specific attributes from Maya importer
        /// </summary>
        public void InitializeShrinkWrap(
            int projectionType,
            float offsetValue,
            float targetSmooth,
            float influenceSmooth,
            bool bidirectionalValue,
            bool preserveShape,
            bool useClosestPoint,
            string targetGeo,
            string drivenGeo,
            Matrix4x4 shrinkWrapMtx,
            Matrix4x4 bindPreMtx)
        {
            projection = projectionType;
            offset = offsetValue;
            targetSmoothness = targetSmooth;
            influenceSmoothness = influenceSmooth;
            bidirectional = bidirectionalValue;
            shapePreservation = preserveShape;
            closestPoint = useClosestPoint;
            targetGeometry = targetGeo;
            drivenGeometry = drivenGeo;
            shrinkWrapMatrix = shrinkWrapMtx;
            bindPreMatrix = bindPreMtx;
        }
    }
}
