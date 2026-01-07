using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Bend Deformer
    /// Production implementation:
    /// - Keeps raw attrs/conns (already stored by MayaNodeComponentBase)
    /// - Decodes common + bend-specific attrs into inspector fields
    /// - Overrides ApplyToUnity => coverage: not STUB
    ///
    /// Unity reconstruction:
    /// - Unityに概念がないため、コンポーネントとしてパラメータを保持（将来のメッシュ変形評価に接続可能）
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("bend")]
    public sealed class BendDeformer : NonLinearDeformerBase
    {
        // ===== Bend specific attributes =====
        [Header("Bend Specific")]
        [Tooltip("Bend roll angle (degrees)")]
        public float roll = 0f;

        [Tooltip("Bend scale")]
        public float scale = 1f;

        [Tooltip("Bend orientation axis (0:X, 1:Y, 2:Z)")]
        public int bendAxis = 1;

        // ===== Space / Matrix =====
        [Header("Matrices")]
        [Tooltip("Handle matrix (local space)")]
        public Matrix4x4 handleMatrix = Matrix4x4.identity;

        [Tooltip("Deformer space matrix")]
        public Matrix4x4 deformerSpaceMatrix = Matrix4x4.identity;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            // Identity
            deformerName = NodeName;
            mayaNodeType = NodeType;
            mayaNodeUUID = Uuid;

            // Common deformer envelope
            envelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, envelope, ".envelope", "envelope", ".env", "env"));

            // NonLinear common (best-effort key set)
            lowBound = DeformerDecodeUtil.ReadFloat(this, lowBound, ".lowBound", "lowBound", ".lb", "lb");
            highBound = DeformerDecodeUtil.ReadFloat(this, highBound, ".highBound", "highBound", ".hb", "hb");
            curvature = DeformerDecodeUtil.ReadFloat(this, curvature, ".curvature", "curvature", ".curv", "curv");
            startAngle = DeformerDecodeUtil.ReadFloat(this, startAngle, ".startAngle", "startAngle", ".sa", "sa");
            endAngle = DeformerDecodeUtil.ReadFloat(this, endAngle, ".endAngle", "endAngle", ".ea", "ea");
            amplitude = DeformerDecodeUtil.ReadFloat(this, amplitude, ".amplitude", "amplitude", ".amp", "amp");
            offset = DeformerDecodeUtil.ReadFloat(this, offset, ".offset", "offset", ".off", "off");
            dropoff = DeformerDecodeUtil.ReadFloat(this, dropoff, ".dropoff", "dropoff", ".do", "do");

            // Bend specific
            roll = DeformerDecodeUtil.ReadFloat(this, roll, ".roll", "roll");
            scale = DeformerDecodeUtil.ReadFloat(this, scale, ".scale", "scale", ".sc", "sc");
            bendAxis = Mathf.Clamp(DeformerDecodeUtil.ReadInt(this, bendAxis, ".bendAxis", "bendAxis", ".axis", "axis"), 0, 2);

            // Axis / direction (optional)
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

            // Matrices: connections preferred, else local tokens
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

            log?.Info($"[bend] '{NodeName}' env={envelope:0.###} roll={roll:0.###} scale={scale:0.###} axis={bendAxis} lb={lowBound:0.###} hb={highBound:0.###}");
        }

        private void OnValidate()
        {
            if (bendAxis < 0) bendAxis = 0;
            if (bendAxis > 2) bendAxis = 2;
        }
    }
}
