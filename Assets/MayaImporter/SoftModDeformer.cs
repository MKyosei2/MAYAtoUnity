using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya SoftMod Deformer
    /// Phase-1:
    /// - ApplyToUnity 実装（STUB脱却）
    /// - Unityに概念がないため、再構築用コンポーネントとしてパラメータ/接続を保持
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("softMod")]
    public sealed class SoftModDeformer : DeformerBase
    {
        [Header("SoftMod Specific")]
        public float falloffRadius = 1f;
        public int falloffMode = 0;
        public bool useFalloffCurve = false;
        public string falloffCurveNode;
        public Vector3 origin = Vector3.zero;

        [Header("Matrices")]
        public Matrix4x4 softModMatrix = Matrix4x4.identity;
        public Matrix4x4 bindPreMatrix = Matrix4x4.identity;

        [Header("Geometry Binding")]
        public string geometryNode;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            deformerName = NodeName;
            mayaNodeType = NodeType;
            mayaNodeUUID = Uuid;

            envelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, envelope, ".envelope", "envelope", ".env", "env"));

            falloffRadius = Mathf.Max(0f, DeformerDecodeUtil.ReadFloat(this, falloffRadius, ".falloffRadius", "falloffRadius", ".radius", "radius"));
            falloffMode = DeformerDecodeUtil.ReadInt(this, falloffMode, ".falloffMode", "falloffMode", ".mode", "mode");
            useFalloffCurve = DeformerDecodeUtil.ReadBool(this, useFalloffCurve, ".useFalloffCurve", "useFalloffCurve", ".useCurve", "useCurve");

            origin = DeformerDecodeUtil.ReadVec3(this, origin,
                packedKeys: new[] { ".origin", "origin" },
                xKeys: new[] { ".originX", "originX", ".ox", "ox" },
                yKeys: new[] { ".originY", "originY", ".oy", "oy" },
                zKeys: new[] { ".originZ", "originZ", ".oz", "oz" });

            // falloffCurve node（接続best-effort）
            falloffCurveNode = FindConnectedNodeByDstContains("falloffCurve", "curve", "ramp") ?? falloffCurveNode;

            // Matrices（接続優先）
            var smPlug = FindIncomingPlugByDstContains("softModMatrix", "matrix", "softMod");
            if (!string.IsNullOrEmpty(smPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(smPlug, out var sm))
                softModMatrix = sm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".softModMatrix", out sm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "softModMatrix", out sm) ||
                     DeformerDecodeUtil.TryReadMatrix4x4(this, ".matrix", out sm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "matrix", out sm))
                softModMatrix = sm;

            var bpmPlug = FindIncomingPlugByDstContains("bindPreMatrix", "preMatrix", "bindPre");
            if (!string.IsNullOrEmpty(bpmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(bpmPlug, out var bpm))
                bindPreMatrix = bpm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".bindPreMatrix", out bpm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "bindPreMatrix", out bpm) ||
                     DeformerDecodeUtil.TryReadMatrix4x4(this, ".preMatrix", out bpm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "preMatrix", out bpm))
                bindPreMatrix = bpm;

            // Geometry best-effort
            geometryNode = FindConnectedNodeByDstContains("input", "inputGeometry", "inMesh", "inputMesh", "geom", "geometry") ?? geometryNode;
            inputGeometry = geometryNode;
            outputGeometry = FindConnectedNodeByDstContains("output", "outputGeometry", "outMesh", "outputMesh");

            log?.Info($"[softMod] '{NodeName}' env={envelope:0.###} radius={falloffRadius:0.###} mode={falloffMode} useCurve={useFalloffCurve} curve={falloffCurveNode ?? "null"}");
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
