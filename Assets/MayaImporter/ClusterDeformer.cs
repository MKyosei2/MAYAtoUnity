using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Cluster Deformer
    /// Phase-1:
    /// - ApplyToUnity 実装（STUB脱却）
    /// - パラメータ/接続をデコードし、Unity上で「再構築できるコンポーネント」として保持
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("cluster")]
    public sealed class ClusterDeformer : DeformerBase
    {
        [Header("Cluster Specific")]
        public bool relative = true;
        public string weightedNode;
        public Vector3 origin = Vector3.zero;

        [Header("Matrices")]
        public Matrix4x4 clusterMatrix = Matrix4x4.identity;
        public Matrix4x4 bindPreMatrix = Matrix4x4.identity;

        [Header("Geometry Binding")]
        public string geometryNode;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            deformerName = NodeName;
            mayaNodeType = NodeType;
            mayaNodeUUID = Uuid;

            envelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, envelope, ".envelope", "envelope", ".env", "env"));

            // Cluster attrs
            relative = DeformerDecodeUtil.ReadBool(this, relative, ".relative", "relative", ".rel", "rel");

            origin = DeformerDecodeUtil.ReadVec3(this, origin,
                packedKeys: new[] { ".origin", "origin" },
                xKeys: new[] { ".originX", "originX", ".ox", "ox" },
                yKeys: new[] { ".originY", "originY", ".oy", "oy" },
                zKeys: new[] { ".originZ", "originZ", ".oz", "oz" });

            // Weighted node (best-effort from connections)
            weightedNode = FindConnectedNodeByDstContains("weighted", "weightedNode", "weightedMatrix", "weightNode") ?? weightedNode;

            // Matrices (connections preferred)
            var cmPlug = FindIncomingPlugByDstContains("clusterMatrix", "matrix", "cluster");
            if (!string.IsNullOrEmpty(cmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(cmPlug, out var cm))
                clusterMatrix = cm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".clusterMatrix", out cm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "clusterMatrix", out cm) ||
                     DeformerDecodeUtil.TryReadMatrix4x4(this, ".matrix", out cm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "matrix", out cm))
                clusterMatrix = cm;

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

            log?.Info($"[cluster] '{NodeName}' env={envelope:0.###} rel={relative} origin=({origin.x:0.###},{origin.y:0.###},{origin.z:0.###}) weighted={weightedNode ?? "null"}");
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
