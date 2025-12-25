// Assets/MayaImporter/BulgeDeformer.cs
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Bulge Deformer
    /// Phase-2 implementation:
    /// - Overrides ApplyToUnity (no longer STUB)
    /// - Decodes bulge-specific attrs + best-effort matrices/geometry binding
    ///
    /// Unity reconstruction:
    /// - Unity has no native Bulge deformer, so we preserve parameters on this component.
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("bulge")]
    public sealed class BulgeDeformer : DeformerBase
    {
        // ===== Bulge specific attributes =====
        [Header("Bulge Specific")]
        [Tooltip("Bulge amount")]
        public float bulge = 0f;

        [Tooltip("Bulge center")]
        public float bulgeCenter = 0.5f;

        [Tooltip("Bulge width")]
        public float bulgeWidth = 1f;

        [Tooltip("Falloff")]
        public float falloff = 0f;

        [Tooltip("Bulge axis (0:X, 1:Y, 2:Z)")]
        public int bulgeAxis = 1;

        // ===== Matrices =====
        [Header("Matrices")]
        [Tooltip("Bulge handle matrix")]
        public Matrix4x4 handleMatrix = Matrix4x4.identity;

        [Tooltip("Deformer space matrix")]
        public Matrix4x4 deformerSpaceMatrix = Matrix4x4.identity;

        // ===== Geometry =====
        [Header("Geometry Binding")]
        [Tooltip("Affected geometry node name")]
        public string geometryNode;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            // Common deformer decode (identity/envelope/geometry best-effort)
            base.ApplyToUnity(options, log);

            // Bulge-specific decode (best-effort key set)
            bulge = DeformerDecodeUtil.ReadFloat(this, bulge, ".bulge", "bulge", ".bg", "bg");
            bulgeCenter = DeformerDecodeUtil.ReadFloat(this, bulgeCenter, ".bulgeCenter", "bulgeCenter", ".bc", "bc", ".center", "center");
            bulgeWidth = DeformerDecodeUtil.ReadFloat(this, bulgeWidth, ".bulgeWidth", "bulgeWidth", ".bw", "bw", ".width", "width");
            falloff = DeformerDecodeUtil.ReadFloat(this, falloff, ".falloff", "falloff", ".fo", "fo");

            bulgeAxis = Mathf.Clamp(
                DeformerDecodeUtil.ReadInt(this, bulgeAxis, ".bulgeAxis", "bulgeAxis", ".axis", "axis"),
                0, 2);

            // Matrices: prefer connected matrices if present
            var hmPlug = DeformerDecodeUtil.FindLastIncomingTo(this, "handleMatrix", "hm", "handle");
            if (!string.IsNullOrEmpty(hmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(hmPlug, out var hm))
                handleMatrix = hm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".handleMatrix", out hm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "handleMatrix", out hm))
                handleMatrix = hm;

            var dsmPlug = DeformerDecodeUtil.FindLastIncomingTo(this, "deformerSpaceMatrix", "dsm", "deformerSpace");
            if (!string.IsNullOrEmpty(dsmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(dsmPlug, out var dsm))
                deformerSpaceMatrix = dsm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".deformerSpaceMatrix", out dsm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "deformerSpaceMatrix", out dsm))
                deformerSpaceMatrix = dsm;

            // Geometry binding: keep both fields consistent
            // Prefer base.inputGeometry if we found it; otherwise try again with typical patterns.
            if (string.IsNullOrEmpty(geometryNode))
                geometryNode = inputGeometry;

            if (string.IsNullOrEmpty(geometryNode))
                geometryNode = FindConnectedNodeByDstContains("input", "inputGeometry", "inMesh", "inputMesh", "geom", "geometry");

            inputGeometry = geometryNode;

            log?.Info($"[bulge] '{NodeName}' env={envelope:0.###} bulge={bulge:0.###} center={bulgeCenter:0.###} width={bulgeWidth:0.###} falloff={falloff:0.###} axis={bulgeAxis} geo={geometryNode ?? "null"}");
        }

        private void OnValidate()
        {
            if (bulgeAxis < 0) bulgeAxis = 0;
            if (bulgeAxis > 2) bulgeAxis = 2;
        }
    }
}
