// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    /// <summary>
    /// NodeType: proximityPin
    /// Production/Phase-C implementation:
    /// - Decode common pin parameters best-effort
    /// - Store input geometry / driver references (node names)
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("proximityPin")]
    public sealed class MayaGenerated_ProximityPinNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (proximityPin)")]
        [SerializeField] private bool enabled = true;

        [Header("Core Params (best-effort)")]
        [SerializeField] private float envelope = 1f;
        [SerializeField] private float maxDistance = 1f;
        [SerializeField] private float strength = 1f;
        [SerializeField] private bool maintainOffset = true;

        [Header("Geometry (best-effort)")]
        [SerializeField] private string inputGeometryNode;
        [SerializeField] private string driverNode;

        [Header("Connection Hints")]
        [SerializeField] private string incomingInputPlug;
        [SerializeField] private string incomingDriverPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            envelope = Mathf.Clamp01(ReadFloat(envelope, ".envelope", "envelope", ".env", "env"));
            maxDistance = Mathf.Max(0f, ReadFloat(maxDistance, ".maxDistance", "maxDistance", ".md", "md", ".radius", "radius"));
            strength = Mathf.Max(0f, ReadFloat(strength, ".strength", "strength", ".pinStrength", "pinStrength", ".s", "s"));
            maintainOffset = ReadBool(maintainOffset, ".maintainOffset", "maintainOffset", ".mo", "mo", ".offset", "offset");

            incomingInputPlug = FindIncomingPlugContains("inputGeometry", "inputMesh", "inMesh", "input", "geom", "geometry");
            incomingDriverPlug = FindIncomingPlugContains("driver", "driverGeometry", "driverMesh", "driverTransform", "target", "worldMatrix");

            inputGeometryNode = PlugToNode(incomingInputPlug) ?? inputGeometryNode;
            driverNode = PlugToNode(incomingDriverPlug) ?? driverNode;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, env={envelope:0.###}, maxD={maxDistance:0.###}, strength={strength:0.###}, maintainOffset={maintainOffset}, " +
                     $"in={inputGeometryNode ?? "null"}, driver={driverNode ?? "null"}");
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

        private static string PlugToNode(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return null;
            return MayaPlugUtil.ExtractNodePart(plug);
        }
    }
}
