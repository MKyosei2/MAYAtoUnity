using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Lattice Deformer
    /// Phase-1:
    /// - ApplyToUnity 実装（STUB脱却）
    /// - divisions/local/outside をデコード
    /// - lattice/baseLattice の接続先ノード名を best-effort で保持
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("lattice")]
    public sealed class LatticeDeformer : DeformerBase
    {
        [Header("Lattice Specific")]
        public int divisionU = 2;
        public int divisionV = 2;
        public int divisionW = 2;
        public bool local = true;
        public bool outsideLattice = false;

        [Header("Lattice Nodes")]
        public string latticeNode;
        public string baseLatticeNode;

        [Header("Geometry Binding")]
        public string geometryNode;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            deformerName = NodeName;
            mayaNodeType = NodeType;
            mayaNodeUUID = Uuid;

            envelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, envelope, ".envelope", "envelope", ".env", "env"));

            // divisions（Mayaでは s/t/uDivisions のことが多い）
            divisionU = Mathf.Max(1, DeformerDecodeUtil.ReadInt(this, divisionU, ".divisionU", "divisionU", ".sDivisions", "sDivisions", ".sDiv", "sDiv"));
            divisionV = Mathf.Max(1, DeformerDecodeUtil.ReadInt(this, divisionV, ".divisionV", "divisionV", ".tDivisions", "tDivisions", ".tDiv", "tDiv"));
            divisionW = Mathf.Max(1, DeformerDecodeUtil.ReadInt(this, divisionW, ".divisionW", "divisionW", ".uDivisions", "uDivisions", ".uDiv", "uDiv"));

            local = DeformerDecodeUtil.ReadBool(this, local, ".local", "local", ".localSpace", "localSpace");
            outsideLattice = DeformerDecodeUtil.ReadBool(this, outsideLattice, ".outsideLattice", "outsideLattice", ".outside", "outside");

            // lattice/baseLattice (connections best-effort)
            //  - latticeDeformer は lattice / baseLattice / ffd などへ接続されることが多いので、dst側の属性名で推測
            latticeNode = FindConnectedNodeByDstContains("lattice", "ffd", "latticeInput", "latticeMatrix") ?? latticeNode;
            baseLatticeNode = FindConnectedNodeByDstContains("baseLattice", "base", "baseMatrix") ?? baseLatticeNode;

            // Geometry best-effort
            geometryNode = FindConnectedNodeByDstContains("input", "inputGeometry", "inMesh", "inputMesh", "geom", "geometry") ?? geometryNode;
            inputGeometry = geometryNode;
            outputGeometry = FindConnectedNodeByDstContains("output", "outputGeometry", "outMesh", "outputMesh");

            log?.Info($"[lattice] '{NodeName}' env={envelope:0.###} div=({divisionU},{divisionV},{divisionW}) local={local} outside={outsideLattice} lat={latticeNode ?? "null"} base={baseLatticeNode ?? "null"}");
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
