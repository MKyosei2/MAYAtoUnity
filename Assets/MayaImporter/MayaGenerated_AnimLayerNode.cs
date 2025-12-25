// NodeType: animLayer
// Phase-1: meaningful decode + "what does this layer affect" capture.
//
// - Decodes: mute/solo/lock/weight + (best-effort) rotation accumulation flags etc.
// - Captures incoming/outgoing plugs for later clip/layer reconstruction.
// - Publishes a metadata component only (layer itself doesn't output a single scalar).

using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("animLayer")]
    public sealed class MayaGenerated_AnimLayerNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (animLayer)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool mute = false;
        [SerializeField] private bool solo = false;
        [SerializeField] private bool lockLayer = false;
        [SerializeField] private float weight = 1f;

        [Header("Connections (best-effort)")]
        [SerializeField] private int incomingCount;
        [SerializeField] private int outgoingCount;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            bool disabled = ReadBool(false, ".disabled", "disabled");
            enabled = !disabled;

            mute = ReadBool(false, ".mute", "mute");
            solo = ReadBool(false, ".solo", "solo");
            lockLayer = ReadBool(false, ".lock", "lock", ".locked", "locked");
            weight = ReadFloat(1f, ".weight", "weight", ".w", "w", ".envelope", "envelope", ".env", "env");

            var meta = GetComponent<MayaAnimLayerMetadata>() ?? gameObject.AddComponent<MayaAnimLayerMetadata>();
            meta.nodeName = NodeName;
            meta.nodeType = NodeType;

            meta.enabled = enabled;
            meta.mute = mute;
            meta.solo = solo;
            meta.lockLayer = lockLayer;
            meta.weight = weight;

            meta.incomingPlugs.Clear();
            meta.outgoingPlugs.Clear();
            meta.affectedDstNodeAttrs.Clear();

            if (Connections != null)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;

                    if (c.RoleForThisNode == ConnectionRole.Destination || c.RoleForThisNode == ConnectionRole.Both)
                    {
                        // incoming: something -> animLayer.*
                        meta.incomingPlugs.Add(c.SrcPlug);
                    }

                    if (c.RoleForThisNode == ConnectionRole.Source || c.RoleForThisNode == ConnectionRole.Both)
                    {
                        // outgoing: animLayer.* -> something
                        meta.outgoingPlugs.Add(c.DstPlug);

                        var dstNode = MayaPlugUtil.ExtractNodePart(c.DstPlug);
                        var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                        if (!string.IsNullOrEmpty(dstNode) && !string.IsNullOrEmpty(dstAttr))
                            meta.affectedDstNodeAttrs.Add(dstNode + "." + dstAttr);
                    }
                }
            }

            incomingCount = meta.incomingPlugs.Count;
            outgoingCount = meta.outgoingPlugs.Count;

            meta.lastBuildFrame = Time.frameCount;

            SetNotes($"animLayer '{NodeName}' decoded: enabled={enabled}, mute={mute}, solo={solo}, lock={lockLayer}, weight={weight:0.###}, in={incomingCount}, out={outgoingCount}");
            log.Info($"[animLayer] '{NodeName}' enabled={enabled} mute={mute} solo={solo} lock={lockLayer} weight={weight:0.###} in={incomingCount} out={outgoingCount}");
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaAnimLayerMetadata : MonoBehaviour
    {
        public string nodeName;
        public string nodeType;

        public bool enabled;
        public bool mute;
        public bool solo;
        public bool lockLayer;
        public float weight;

        public List<string> incomingPlugs = new List<string>();
        public List<string> outgoingPlugs = new List<string>();
        public List<string> affectedDstNodeAttrs = new List<string>();

        public int lastBuildFrame;
    }
}
