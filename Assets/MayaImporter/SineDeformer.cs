using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Sine Deformer
    /// Phase-1:
    /// - ApplyToUnity 実装（STUB脱却）
    /// - raw attrs/conns を保持したまま、代表的パラメータをデコードしてUnity上で再構築可能にする
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("sine")]
    public sealed class SineDeformer : NonLinearDeformerBase
    {
        [Header("Sine Specific")]
        public float sineAmplitude = 1f;
        public float wavelength = 1f;
        public float sineOffset = 0f;
        public float sineDropoff = 0f;
        public float phase = 0f;
        public float time = 0f;
        public Vector3 sineDirection = Vector3.up;

        [Header("Matrices")]
        public Matrix4x4 handleMatrix = Matrix4x4.identity;
        public Matrix4x4 deformerSpaceMatrix = Matrix4x4.identity;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            // Identity
            deformerName = NodeName;
            mayaNodeType = NodeType;
            mayaNodeUUID = Uuid;

            // Common envelope
            envelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, envelope, ".envelope", "envelope", ".env", "env"));

            // NonLinear common（代表的キーを広めに拾う）
            lowBound = DeformerDecodeUtil.ReadFloat(this, lowBound, ".lowBound", "lowBound", ".lb", "lb");
            highBound = DeformerDecodeUtil.ReadFloat(this, highBound, ".highBound", "highBound", ".hb", "hb");
            curvature = DeformerDecodeUtil.ReadFloat(this, curvature, ".curvature", "curvature", ".curv", "curv");
            startAngle = DeformerDecodeUtil.ReadFloat(this, startAngle, ".startAngle", "startAngle", ".sa", "sa");
            endAngle = DeformerDecodeUtil.ReadFloat(this, endAngle, ".endAngle", "endAngle", ".ea", "ea");
            amplitude = DeformerDecodeUtil.ReadFloat(this, amplitude, ".amplitude", "amplitude", ".amp", "amp");
            offset = DeformerDecodeUtil.ReadFloat(this, offset, ".offset", "offset", ".off", "off");
            dropoff = DeformerDecodeUtil.ReadFloat(this, dropoff, ".dropoff", "dropoff", ".do", "do");

            // Sine specific（ノード固有キー優先 + 互換キーも拾う）
            sineAmplitude = DeformerDecodeUtil.ReadFloat(this, sineAmplitude,
                ".sineAmplitude", "sineAmplitude", ".amplitude", "amplitude", ".sa", "sa");
            wavelength = DeformerDecodeUtil.ReadFloat(this, wavelength,
                ".wavelength", "wavelength", ".wl", "wl");
            sineOffset = DeformerDecodeUtil.ReadFloat(this, sineOffset,
                ".sineOffset", "sineOffset", ".offset", "offset", ".so", "so");
            sineDropoff = DeformerDecodeUtil.ReadFloat(this, sineDropoff,
                ".sineDropoff", "sineDropoff", ".dropoff", "dropoff", ".sd", "sd");
            phase = DeformerDecodeUtil.ReadFloat(this, phase,
                ".phase", "phase", ".ph", "ph");
            time = DeformerDecodeUtil.ReadFloat(this, time,
                ".time", "time", ".t", "t");

            sineDirection = DeformerDecodeUtil.ReadVec3(this, sineDirection,
                packedKeys: new[] { ".sineDirection", "sineDirection", ".direction", "direction" },
                xKeys: new[] { ".sineDirectionX", "sineDirectionX", ".directionX", "directionX" },
                yKeys: new[] { ".sineDirectionY", "sineDirectionY", ".directionY", "directionY" },
                zKeys: new[] { ".sineDirectionZ", "sineDirectionZ", ".directionZ", "directionZ" });

            // Matrices（接続優先）
            var hmPlug = FindIncomingPlugByDstContains("handleMatrix", "hm", "handle");
            if (!string.IsNullOrEmpty(hmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(hmPlug, out var hm))
                handleMatrix = hm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".handleMatrix", out hm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "handleMatrix", out hm))
                handleMatrix = hm;

            var dsmPlug = FindIncomingPlugByDstContains("deformerSpaceMatrix", "dsm", "deformerSpace");
            if (!string.IsNullOrEmpty(dsmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(dsmPlug, out var dsm))
                deformerSpaceMatrix = dsm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".deformerSpaceMatrix", out dsm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "deformerSpaceMatrix", out dsm))
                deformerSpaceMatrix = dsm;

            // Geometry best-effort
            inputGeometry = FindConnectedNodeByDstContains("input", "inputGeometry", "inMesh", "inputMesh", "geom", "geometry");
            outputGeometry = FindConnectedNodeByDstContains("output", "outputGeometry", "outMesh", "outputMesh");

            log?.Info($"[sine] '{NodeName}' env={envelope:0.###} amp={sineAmplitude:0.###} wl={wavelength:0.###} phase={phase:0.###} time={time:0.###}");
        }

        private string FindIncomingPlugByDstContains(params string[] patterns)
        {
            if (Connections == null || patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Destination &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;

                    if (dstAttr.Contains(pat, System.StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }

            return null;
        }

        private string FindConnectedNodeByDstContains(params string[] patterns)
        {
            var plug = FindIncomingPlugByDstContains(patterns);
            if (string.IsNullOrEmpty(plug)) return null;
            return MayaPlugUtil.ExtractNodePart(plug);
        }
    }
}
