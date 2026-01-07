using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Wave Deformer
    /// Production implementation:
    /// - Decodes attrs into fields
    /// - Overrides ApplyToUnity => coverage: not STUB
    ///
    /// Note:
    /// - time/speed などは Maya の time 入力接続に依存する場合があるため、まずは固定値として保持。
    ///   （次のフェーズで time 接続の評価へ拡張可能）
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("wave")]
    public sealed class WaveDeformer : NonLinearDeformerBase
    {
        [Header("Wave Specific")]
        public float waveAmplitude = 1f;
        public float wavelength = 1f;
        public float waveOffset = 0f;
        public float waveDropoff = 0f;
        public float noise = 0f;
        public float waveTime = 0f;
        public float waveSpeed = 1f;
        public Vector3 waveDirection = Vector3.right;

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

            // Wave specific
            waveAmplitude = DeformerDecodeUtil.ReadFloat(this, waveAmplitude, ".waveAmplitude", "waveAmplitude", ".amplitude", "amplitude", ".wa", "wa");
            wavelength = DeformerDecodeUtil.ReadFloat(this, wavelength, ".wavelength", "wavelength", ".wl", "wl");
            waveOffset = DeformerDecodeUtil.ReadFloat(this, waveOffset, ".waveOffset", "waveOffset", ".woff", "woff");
            waveDropoff = DeformerDecodeUtil.ReadFloat(this, waveDropoff, ".waveDropoff", "waveDropoff", ".wdo", "wdo");
            noise = DeformerDecodeUtil.ReadFloat(this, noise, ".noise", "noise");
            waveTime = DeformerDecodeUtil.ReadFloat(this, waveTime, ".waveTime", "waveTime", ".time", "time");
            waveSpeed = DeformerDecodeUtil.ReadFloat(this, waveSpeed, ".waveSpeed", "waveSpeed", ".speed", "speed");

            waveDirection = DeformerDecodeUtil.ReadVec3(this, waveDirection,
                packedKeys: new[] { ".waveDirection", "waveDirection", ".direction", "direction" },
                xKeys: new[] { ".waveDirectionX", "waveDirectionX", ".directionX", "directionX" },
                yKeys: new[] { ".waveDirectionY", "waveDirectionY", ".directionY", "directionY" },
                zKeys: new[] { ".waveDirectionZ", "waveDirectionZ", ".directionZ", "directionZ" });

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

            log?.Info($"[wave] '{NodeName}' env={envelope:0.###} amp={waveAmplitude:0.###} wl={wavelength:0.###} t={waveTime:0.###} speed={waveSpeed:0.###}");
        }
    }
}
