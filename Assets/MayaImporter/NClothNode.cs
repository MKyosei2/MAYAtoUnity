using System;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nCloth")]
    [DisallowMultipleComponent]
    public sealed class NClothNode : MayaNodeComponentBase
    {
        [Header("Resolved (best-effort)")]
        public string connectedMeshNode;     // Maya mesh/shape name
        public Transform connectedMesh;      // Unity transform
        public string connectedNucleusNode;  // Maya nucleus name
        public MayaNucleusRuntimeWorld nucleusWorld;

        [Header("nCloth 주요パラメータ (best-effort)")]
        public float stretchResistance = 50f;
        public float bendResistance = 50f;
        public float damping = 0.1f;
        public float friction = 0.2f;
        public float thickness = 0.01f;
        public bool selfCollision = false;

        [Header("Unity Cloth (auto if possible)")]
        public Cloth unityCloth;
        public string notes;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            var scene = MayaBuildContext.CurrentScene;

            // Read some common-ish attrs (names differ by Maya versions / rigs, so broad keys)
            stretchResistance = ReadFloat(".stretchResistance", ".sr", stretchResistance);
            bendResistance = ReadFloat(".bendResistance", ".br", bendResistance);
            damping = ReadFloat(".damp", ".damping", damping);
            friction = ReadFloat(".friction", ".fr", friction);
            thickness = ReadFloat(".thickness", ".th", thickness);
            selfCollision = ReadBool(".selfCollide", ".sc", selfCollision);

            // Resolve connected mesh + nucleus (best-effort)
            connectedMeshNode = FindConnectedMeshNode(scene);
            connectedMesh = MayaNodeLookup.FindTransform(connectedMeshNode);

            connectedNucleusNode = FindConnectedNucleusNode(scene);
            var nucTr = MayaNodeLookup.FindTransform(connectedNucleusNode);
            nucleusWorld = nucTr != null ? nucTr.GetComponent<MayaNucleusRuntimeWorld>() : null;

            // Keep a binding component on THIS node (so "Maya1ノード=Unity1コンポーネント" の情報が残る)
            var bind = GetComponent<MayaNClothBinding>();
            if (bind == null) bind = gameObject.AddComponent<MayaNClothBinding>();

            bind.SourceNodeName = NodeName;
            bind.MeshNodeName = connectedMeshNode;
            bind.MeshTransform = connectedMesh;
            bind.NucleusNodeName = connectedNucleusNode;
            bind.NucleusWorld = nucleusWorld;
            bind.StretchResistance = stretchResistance;
            bind.BendResistance = bendResistance;
            bind.Damping = damping;
            bind.Friction = friction;
            bind.Thickness = thickness;
            bind.SelfCollision = selfCollision;

            // Try attach Unity Cloth to the connected mesh (if possible)
            unityCloth = null;
            if (connectedMesh != null)
            {
                var smr = connectedMesh.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    unityCloth = connectedMesh.GetComponent<Cloth>();
                    if (unityCloth == null) unityCloth = connectedMesh.gameObject.AddComponent<Cloth>();

                    ApplyToUnityCloth(unityCloth);
                }
                else
                {
                    log.Warn($"[nCloth] '{NodeName}': connected mesh '{connectedMesh.name}' has no SkinnedMeshRenderer -> Unity Cloth cannot be auto-attached. (data is still preserved)");
                }
            }
            else
            {
                log.Warn($"[nCloth] '{NodeName}': connected mesh not found. meshNode='{connectedMeshNode}' (data is still preserved)");
            }

            notes =
                $"nCloth '{NodeName}': mesh='{MayaPlugUtil.LeafName(connectedMeshNode)}' nucleus='{MayaPlugUtil.LeafName(connectedNucleusNode)}' " +
                $"sr={stretchResistance} br={bendResistance} damp={damping} fr={friction} th={thickness} selfCol={selfCollision} " +
                $"unityCloth={(unityCloth != null ? "Y" : "N")}";

            log.Info("[nCloth] " + notes);
        }

        private void ApplyToUnityCloth(Cloth cloth)
        {
            if (cloth == null) return;

            cloth.useGravity = true;

            // Maya値はレンジが広い事が多いので、0..1に寄せる（best-effort）
            cloth.stretchingStiffness = Normalize01(stretchResistance);
            cloth.bendingStiffness = Normalize01(bendResistance);

            cloth.damping = Mathf.Clamp01(damping);
            cloth.friction = Mathf.Clamp01(friction);

            // thickness/selfCollision は Unity Cloth の直接対応が薄いので binder で保持しつつ、ここでは触らない
        }

        private static float Normalize01(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return 0.5f;
            if (v <= 0f) return 0f;

            // よくある: 0..100
            if (v > 1f && v <= 100f) return Mathf.Clamp01(v / 100f);

            // よくある: 0..10
            if (v > 1f && v <= 10f) return Mathf.Clamp01(v / 10f);

            return Mathf.Clamp01(v);
        }

        private string FindConnectedMeshNode(MayaSceneData scene)
        {
            // Prefer this.Connections (already related to this node)
            // Look for inputMesh / inMesh / inputGeometry-ish
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";
                if (dstAttr.IndexOf("inputMesh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dstAttr.IndexOf("inMesh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dstAttr.IndexOf("inputGeo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dstAttr.IndexOf("inputGeometry", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return c.SrcNodePart ?? MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            // Fallback: outputMesh -> mesh.inMesh
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var srcAttr = MayaPlugUtil.ExtractAttrPart(c.SrcPlug) ?? "";
                if (srcAttr.IndexOf("outputMesh", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    srcAttr.IndexOf("outMesh", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return c.DstNodePart ?? MayaPlugUtil.ExtractNodePart(c.DstPlug);
                }
            }

            return null;
        }

        private string FindConnectedNucleusNode(MayaSceneData scene)
        {
            if (scene == null) return null;

            // Try find any connected node whose type is nucleus
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                var other = (c.RoleForThisNode == ConnectionRole.Source) ? c.DstNodePart : c.SrcNodePart;
                if (string.IsNullOrEmpty(other)) other = (c.RoleForThisNode == ConnectionRole.Source)
                    ? MayaPlugUtil.ExtractNodePart(c.DstPlug)
                    : MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                if (string.IsNullOrEmpty(other)) continue;

                var rec = FindNodeByAnyName(scene, other);
                var t = rec != null ? (rec.NodeType ?? "") : "";
                if (t.IndexOf("nucleus", StringComparison.OrdinalIgnoreCase) >= 0)
                    return other;
            }

            return null;
        }

        private float ReadFloat(string k1, string k2, float def)
        {
            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count > 0 && TryF(a.Tokens[0], out var f1)) return f1;
            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count > 0 && TryF(a.Tokens[0], out var f2)) return f2;
            return def;
        }

        private bool ReadBool(string k1, string k2, bool def)
        {
            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count > 0)
            {
                var s = (a.Tokens[0] ?? "").Trim().ToLowerInvariant();
                if (s == "1" || s == "true") return true;
                if (s == "0" || s == "false") return false;
            }
            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count > 0)
            {
                var s = (a.Tokens[0] ?? "").Trim().ToLowerInvariant();
                if (s == "1" || s == "true") return true;
                if (s == "0" || s == "false") return false;
            }
            return def;
        }

        private static bool TryF(string s, out float f)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
        }

        private static NodeRecord FindNodeByAnyName(MayaSceneData scene, string nameOrDag)
        {
            if (scene?.Nodes == null || string.IsNullOrEmpty(nameOrDag)) return null;

            if (scene.Nodes.TryGetValue(nameOrDag, out var exact) && exact != null)
                return exact;

            var leaf = MayaPlugUtil.LeafName(nameOrDag);

            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;
                if (string.Equals(MayaPlugUtil.LeafName(n.Name), leaf, StringComparison.Ordinal))
                    return n;
            }

            return null;
        }
    }
}
