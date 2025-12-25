using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Wrap Deformer
    /// Phase-1:
    /// - ApplyToUnity 実装（STUB脱却）
    /// - wrap固有パラメータ + driver/driven/influences を best-effort で保持
    /// - Unityに概念がないので、再構築用コンポーネントとして保存（将来の実評価に接続可能）
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("wrap")]
    public sealed class WrapDeformer : DeformerBase
    {
        [Header("Wrap Specific")]
        public float weightThreshold = 0.0f;
        public float maxDistance = 1.0f;
        public bool exclusiveBind = false;
        public bool autoWeightThreshold = true;
        public int bindMethod = 0;

        [Header("Geometry Binding")]
        public string driverGeometry;
        public string drivenGeometry;
        public List<string> influenceNodes = new List<string>();

        [Header("Matrices")]
        public Matrix4x4 wrapMatrix = Matrix4x4.identity;
        public Matrix4x4 bindPreMatrix = Matrix4x4.identity;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            deformerName = NodeName;
            mayaNodeType = NodeType;
            mayaNodeUUID = Uuid;

            envelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, envelope, ".envelope", "envelope", ".env", "env"));

            weightThreshold = DeformerDecodeUtil.ReadFloat(this, weightThreshold, ".weightThreshold", "weightThreshold", ".wth", "wth");
            maxDistance = Mathf.Max(0f, DeformerDecodeUtil.ReadFloat(this, maxDistance, ".maxDistance", "maxDistance", ".md", "md"));
            exclusiveBind = DeformerDecodeUtil.ReadBool(this, exclusiveBind, ".exclusiveBind", "exclusiveBind", ".excl", "excl");
            autoWeightThreshold = DeformerDecodeUtil.ReadBool(this, autoWeightThreshold, ".autoWeightThreshold", "autoWeightThreshold", ".awt", "awt");
            bindMethod = DeformerDecodeUtil.ReadInt(this, bindMethod, ".bindMethod", "bindMethod", ".method", "method");

            // Matrices（接続優先）
            var wmPlug = FindIncomingPlugByDstContains("wrapMatrix", "matrix", "wrap");
            if (!string.IsNullOrEmpty(wmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(wmPlug, out var wm))
                wrapMatrix = wm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".wrapMatrix", out wm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "wrapMatrix", out wm) ||
                     DeformerDecodeUtil.TryReadMatrix4x4(this, ".matrix", out wm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "matrix", out wm))
                wrapMatrix = wm;

            var bpmPlug = FindIncomingPlugByDstContains("bindPreMatrix", "preMatrix", "bindPre");
            if (!string.IsNullOrEmpty(bpmPlug) && DeformerDecodeUtil.TryResolveConnectedMatrix(bpmPlug, out var bpm))
                bindPreMatrix = bpm;
            else if (DeformerDecodeUtil.TryReadMatrix4x4(this, ".bindPreMatrix", out bpm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "bindPreMatrix", out bpm) ||
                     DeformerDecodeUtil.TryReadMatrix4x4(this, ".preMatrix", out bpm) || DeformerDecodeUtil.TryReadMatrix4x4(this, "preMatrix", out bpm))
                bindPreMatrix = bpm;

            // Driver / Driven / Influences best-effort（dst属性名の部分一致で拾う）
            driverGeometry = FindConnectedNodeByDstContains("driver", "drivers", "driverPoints", "driverGeometry", "driverMesh") ?? driverGeometry;
            drivenGeometry = FindConnectedNodeByDstContains("driven", "drivens", "drivenPoints", "drivenGeometry", "drivenMesh", "input", "inputGeometry", "inMesh") ?? drivenGeometry;

            influenceNodes.Clear();
            CollectConnectedNodesByDstContains(influenceNodes, "influence", "influences", "infl", "driverTransform", "influenceTransform");

            // DeformerBase geometry fields
            inputGeometry = drivenGeometry;
            outputGeometry = FindConnectedNodeByDstContains("output", "outputGeometry", "outMesh", "outputMesh");

            log?.Info($"[wrap] '{NodeName}' env={envelope:0.###} wth={weightThreshold:0.###} maxD={maxDistance:0.###} excl={exclusiveBind} autoWth={autoWeightThreshold} method={bindMethod} " +
                      $"driver={driverGeometry ?? "null"} driven={drivenGeometry ?? "null"} infl={influenceNodes.Count}");
        }

        private void CollectConnectedNodesByDstContains(List<string> outList, params string[] patterns)
        {
            if (outList == null) return;
            if (Connections == null || patterns == null || patterns.Length == 0) return;

            var set = new HashSet<string>();
            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Destination &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                bool hit = false;
                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, System.StringComparison.Ordinal))
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit) continue;

                var node = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                if (string.IsNullOrEmpty(node)) continue;

                if (set.Add(node))
                    outList.Add(node);
            }
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
