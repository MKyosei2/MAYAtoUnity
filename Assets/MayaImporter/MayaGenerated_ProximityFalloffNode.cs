using System;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    /// <summary>
    /// NodeType: proximityFalloff
    /// Phase-1/Phase-C implementation:
    /// - Decode basic falloff shaping params best-effort
    /// - Keep connection hints (input/output)
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("proximityFalloff")]
    public sealed class MayaGenerated_ProximityFalloffNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (proximityFalloff)")]
        [SerializeField] private bool enabled = true;

        [Header("Falloff Params (best-effort)")]
        [SerializeField] private float falloffDistance = 1f;
        [SerializeField] private float exponent = 1f;
        [SerializeField] private float smoothness = 0f;
        [SerializeField] private int falloffMode = 0;

        [Header("Connection Hints")]
        [SerializeField] private string incomingInputPlug;
        [SerializeField] private string outgoingOutputPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            falloffDistance = Mathf.Max(0f, ReadFloat(falloffDistance,
                ".falloffDistance", "falloffDistance",
                ".distance", "distance",
                ".radius", "radius",
                ".fd", "fd"));

            exponent = Mathf.Max(0f, ReadFloat(exponent,
                ".exponent", "exponent",
                ".power", "power",
                ".exp", "exp"));

            smoothness = Mathf.Max(0f, ReadFloat(smoothness,
                ".smoothness", "smoothness",
                ".smooth", "smooth",
                ".sm", "sm"));

            falloffMode = ReadInt(falloffMode,
                ".falloffMode", "falloffMode",
                ".mode", "mode");

            incomingInputPlug = FindIncomingPlugContains("input", "in", "distance", "value");
            outgoingOutputPlug = FindOutgoingPlugContains("output", "out", "falloff", "value");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, dist={falloffDistance:0.###}, exp={exponent:0.###}, smooth={smoothness:0.###}, mode={falloffMode}, " +
                     $"in={(string.IsNullOrEmpty(incomingInputPlug) ? "none" : incomingInputPlug)}, out={(string.IsNullOrEmpty(outgoingOutputPlug) ? "none" : outgoingOutputPlug)}");
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

        private string FindOutgoingPlugContains(params string[] patterns)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var srcAttr = MayaPlugUtil.ExtractAttrPart(c.SrcPlug);
                if (string.IsNullOrEmpty(srcAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (srcAttr.Contains(pat, StringComparison.Ordinal))
                        return c.DstPlug;
                }
            }
            return null;
        }
    }
}
