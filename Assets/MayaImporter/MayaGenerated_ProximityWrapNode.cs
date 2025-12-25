using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    /// <summary>
    /// NodeType: proximityWrap
    /// Phase-1/Phase-C implementation:
    /// - Decode common proximityWrap parameters (maxDistance/falloff/etc) best-effort
    /// - Store driver/driven/influences as node names from connections
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("proximityWrap")]
    public sealed class MayaGenerated_ProximityWrapNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (proximityWrap)")]
        [SerializeField] private bool enabled = true;

        [Header("Core Params (best-effort)")]
        [SerializeField] private float envelope = 1f;
        [SerializeField] private float maxDistance = 1f;
        [SerializeField] private float falloff = 0f;
        [SerializeField] private int bindMethod = 0;
        [SerializeField] private bool useGeodesicDistance = false;

        [Header("Geometry / Drivers (best-effort)")]
        [SerializeField] private string drivenGeometryNode;
        [SerializeField] private string driverGeometryNode;
        [SerializeField] private List<string> influenceNodes = new List<string>();

        [Header("Connection Hints")]
        [SerializeField] private string incomingDrivenPlug;
        [SerializeField] private string incomingDriverPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            envelope = Mathf.Clamp01(ReadFloat(envelope, ".envelope", "envelope", ".env", "env"));
            maxDistance = Mathf.Max(0f, ReadFloat(maxDistance, ".maxDistance", "maxDistance", ".md", "md", ".radius", "radius"));
            falloff = Mathf.Max(0f, ReadFloat(falloff, ".falloff", "falloff", ".fo", "fo", ".falloffDistance", "falloffDistance"));
            bindMethod = ReadInt(bindMethod, ".bindMethod", "bindMethod", ".method", "method");
            useGeodesicDistance = ReadBool(useGeodesicDistance, ".useGeodesicDistance", "useGeodesicDistance", ".geodesic", "geodesic");

            // Driven / Driver (substring scan)
            incomingDrivenPlug = FindIncomingPlugContains("driven", "drivenGeometry", "drivenMesh", "inputGeometry", "inputMesh", "inMesh", "input");
            incomingDriverPlug = FindIncomingPlugContains("driver", "driverGeometry", "driverMesh", "drivers", "driverPoints");

            drivenGeometryNode = PlugToNode(incomingDrivenPlug) ?? drivenGeometryNode;
            driverGeometryNode = PlugToNode(incomingDriverPlug) ?? driverGeometryNode;

            // Influences (collect many)
            influenceNodes.Clear();
            CollectIncomingNodesContains(influenceNodes, "influence", "influences", "infl", "driverTransform", "influenceTransform");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, env={envelope:0.###}, maxD={maxDistance:0.###}, falloff={falloff:0.###}, " +
                     $"bindMethod={bindMethod}, geodesic={useGeodesicDistance}, driven={drivenGeometryNode ?? "null"}, driver={driverGeometryNode ?? "null"}, infl={influenceNodes.Count}");
        }

        private string FindIncomingPlugContains(params string[] patterns)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }
            return null;
        }

        private void CollectIncomingNodesContains(List<string> outList, params string[] patterns)
        {
            if (outList == null) return;
            if (Connections == null || Connections.Count == 0) return;
            if (patterns == null || patterns.Length == 0) return;

            var set = new HashSet<string>(StringComparer.Ordinal);

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                bool hit = false;
                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, StringComparison.Ordinal)) { hit = true; break; }
                }
                if (!hit) continue;

                var node = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                if (string.IsNullOrEmpty(node)) continue;

                if (set.Add(node)) outList.Add(node);
            }
        }

        private static string PlugToNode(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return null;
            return MayaPlugUtil.ExtractNodePart(plug);
        }
    }
}
