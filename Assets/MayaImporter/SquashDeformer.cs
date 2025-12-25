using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Squash Deformer
    /// Phase-1 implementation:
    /// - Decodes attrs into fields
    /// - Overrides ApplyToUnity => coverage: not STUB
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("squash")]
    public sealed class SquashDeformer : NonLinearDeformerBase
    {
        [Header("Squash Specific")]
        [Tooltip("Squash factor")]
        public float factor = 1f;

        [Tooltip("Expand amount")]
        public float expand = 0f;

        [Tooltip("Maximum expansion")]
        public float maxExpand = 1f;

        [Tooltip("Start smoothness")]
        public float startSmoothness = 0f;

        [Tooltip("End smoothness")]
        public float endSmoothness = 0f;

        [Tooltip("Squash axis (0:X, 1:Y, 2:Z)")]
        public int squashAxis = 1;

        [Header("Matrices")]
        public Matrix4x4 handleMatrix = Matrix4x4.identity;
        public Matrix4x4 deformerSpaceMatrix = Matrix4x4.identity;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            deformerName = NodeName;
            mayaNodeType = NodeType;
            mayaNodeUUID = Uuid;

            envelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, envelope, ".envelope", "envelope", ".env", "env"));

            // NonLinear common
            lowBound = DeformerDecodeUtil.ReadFloat(this, lowBound, ".lowBound", "lowBound", ".lb", "lb");
            highBound = DeformerDecodeUtil.ReadFloat(this, highBound, ".highBound", "highBound", ".hb", "hb");
            curvature = DeformerDecodeUtil.ReadFloat(this, curvature, ".curvature", "curvature", ".curv", "curv");
            startAngle = DeformerDecodeUtil.ReadFloat(this, startAngle, ".startAngle", "startAngle", ".sa", "sa");
            endAngle = DeformerDecodeUtil.ReadFloat(this, endAngle, ".endAngle", "endAngle", ".ea", "ea");
            amplitude = DeformerDecodeUtil.ReadFloat(this, amplitude, ".amplitude", "amplitude", ".amp", "amp");
            offset = DeformerDecodeUtil.ReadFloat(this, offset, ".offset", "offset", ".off", "off");
            dropoff = DeformerDecodeUtil.ReadFloat(this, dropoff, ".dropoff", "dropoff", ".do", "do");

            // Squash specific
            factor = DeformerDecodeUtil.ReadFloat(this, factor, ".factor", "factor");
            expand = DeformerDecodeUtil.ReadFloat(this, expand, ".expand", "expand");
            maxExpand = DeformerDecodeUtil.ReadFloat(this, maxExpand, ".maxExpand", "maxExpand");
            startSmoothness = DeformerDecodeUtil.ReadFloat(this, startSmoothness, ".startSmoothness", "startSmoothness", ".startSmooth", "startSmooth");
            endSmoothness = DeformerDecodeUtil.ReadFloat(this, endSmoothness, ".endSmoothness", "endSmoothness", ".endSmooth", "endSmooth");
            squashAxis = Mathf.Clamp(DeformerDecodeUtil.ReadInt(this, squashAxis, ".squashAxis", "squashAxis", ".axis", "axis"), 0, 2);

            axis = DeformerDecodeUtil.ReadVec3(this, axis,
                packedKeys: new[] { ".axis", "axis" },
                xKeys: new[] { ".axisX", "axisX" },
                yKeys: new[] { ".axisY", "axisY" },
                zKeys: new[] { ".axisZ", "axisZ" });

            direction = DeformerDecodeUtil.ReadVec3(this, direction,
                packedKeys: new[] { ".direction", "direction" },
                xKeys: new[] { ".directionX", "directionX" },
                yKeys: new[] { ".directionY", "directionY" },
                zKeys: new[] { ".directionZ", "directionZ" });

            // Matrices
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

            log?.Info($"[squash] '{NodeName}' env={envelope:0.###} factor={factor:0.###} expand={expand:0.###} maxExpand={maxExpand:0.###} axis={squashAxis}");
        }

        private void OnValidate()
        {
            if (squashAxis < 0) squashAxis = 0;
            if (squashAxis > 2) squashAxis = 2;
        }
    }
}
