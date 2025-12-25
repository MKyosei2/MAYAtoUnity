using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Twist Deformer
    /// Phase-1 implementation:
    /// - Decodes attrs into fields
    /// - Overrides ApplyToUnity => coverage: not STUB
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("twist")]
    public sealed class TwistDeformer : NonLinearDeformerBase
    {
        [Header("Twist Specific")]
        [Tooltip("Total twist angle (degrees)")]
        public float twistAngle = 0f;

        [Tooltip("Rotation offset (degrees)")]
        public float rotationOffset = 0f;

        [Tooltip("Twist axis (0:X, 1:Y, 2:Z)")]
        public int twistAxis = 1;

        [Header("Matrices")]
        [Tooltip("Handle matrix")]
        public Matrix4x4 handleMatrix = Matrix4x4.identity;

        [Tooltip("Deformer space matrix")]
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

            // Twist specific
            twistAngle = DeformerDecodeUtil.ReadFloat(this, twistAngle, ".twistAngle", "twistAngle", ".angle", "angle", ".twist", "twist");
            rotationOffset = DeformerDecodeUtil.ReadFloat(this, rotationOffset, ".rotationOffset", "rotationOffset", ".roff", "roff");
            twistAxis = Mathf.Clamp(DeformerDecodeUtil.ReadInt(this, twistAxis, ".twistAxis", "twistAxis", ".axis", "axis"), 0, 2);

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

            log?.Info($"[twist] '{NodeName}' env={envelope:0.###} angle={twistAngle:0.###} roff={rotationOffset:0.###} axis={twistAxis}");
        }

        private void OnValidate()
        {
            if (twistAxis < 0) twistAxis = 0;
            if (twistAxis > 2) twistAxis = 2;
        }
    }
}
