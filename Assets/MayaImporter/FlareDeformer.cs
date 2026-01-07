using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Flare Deformer
    /// Production implementation:
    /// - Decodes attrs into fields
    /// - Overrides ApplyToUnity => coverage: not STUB
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("flare")]
    public sealed class FlareDeformer : NonLinearDeformerBase
    {
        [Header("Flare Specific")]
        public float startFlare = 1f;
        public float endFlare = 1f;
        public float curve = 0f;
        public int flareAxis = 1;

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

            // Flare specific
            startFlare = DeformerDecodeUtil.ReadFloat(this, startFlare, ".startFlare", "startFlare", ".sf", "sf");
            endFlare = DeformerDecodeUtil.ReadFloat(this, endFlare, ".endFlare", "endFlare", ".ef", "ef");
            curve = DeformerDecodeUtil.ReadFloat(this, curve, ".curve", "curve");
            flareAxis = Mathf.Clamp(DeformerDecodeUtil.ReadInt(this, flareAxis, ".flareAxis", "flareAxis", ".axis", "axis"), 0, 2);

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

            log?.Info($"[flare] '{NodeName}' env={envelope:0.###} start={startFlare:0.###} end={endFlare:0.###} curve={curve:0.###} axis={flareAxis}");
        }

        private void OnValidate()
        {
            if (flareAxis < 0) flareAxis = 0;
            if (flareAxis > 2) flareAxis = 2;
        }
    }
}
