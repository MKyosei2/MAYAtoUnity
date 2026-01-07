// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: deltaMush
// Production implementation: meaningful DecodePhaseC (not generic stub)

using System;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("deltaMush")]
    public sealed class MayaGenerated_DeltaMushNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (deltaMush)")]
        [SerializeField] private bool enabled = true;

        [Header("Core Params (best-effort)")]
        [SerializeField] private float envelope = 1f;
        [SerializeField] private int smoothingIterations = 10;
        [SerializeField] private float smoothingStep = 0.5f;
        [SerializeField] private bool preserveVolume = false;
        [SerializeField] private bool pinBorderVertices = false;
        [SerializeField] private bool pinAllVertices = false;

        [Header("Geometry (best-effort)")]
        [SerializeField] private string inputGeometryNode;
        [SerializeField] private string outputGeometryNode;

        [Header("Connection Hints")]
        [SerializeField] private string incomingInputPlug;
        [SerializeField] private string incomingEnvelopePlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // enable heuristics
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            envelope = Mathf.Clamp01(ReadFloat(envelope, ".envelope", "envelope", ".env", "env"));

            smoothingIterations = Mathf.Max(0, ReadInt(
                smoothingIterations,
                ".smoothingIterations", "smoothingIterations",
                ".iterations", "iterations",
                ".smoothIterations", "smoothIterations",
                ".si", "si"));

            smoothingStep = Mathf.Max(0f, ReadFloat(
                smoothingStep,
                ".smoothingStep", "smoothingStep",
                ".step", "step",
                ".smoothStep", "smoothStep",
                ".ss", "ss"));

            preserveVolume = ReadBool(preserveVolume,
                ".preserveVolume", "preserveVolume",
                ".keepVolume", "keepVolume",
                ".pv", "pv");

            pinBorderVertices = ReadBool(pinBorderVertices,
                ".pinBorderVertices", "pinBorderVertices",
                ".pinBorder", "pinBorder",
                ".pbv", "pbv");

            pinAllVertices = ReadBool(pinAllVertices,
                ".pinAllVertices", "pinAllVertices",
                ".pinAll", "pinAll",
                ".pav", "pav");

            // Connections: input/output geometry best-effort (contains-scan)
            incomingInputPlug = FindIncomingPlugContains("inputGeometry", "inputMesh", "inMesh", "input", "geom", "geometry");
            incomingEnvelopePlug = FindIncomingPlugContains("envelope", "env");

            inputGeometryNode = PlugToNode(incomingInputPlug) ?? inputGeometryNode;
            outputGeometryNode = FindIncomingNodeContains("outputGeometry", "outputMesh", "outMesh", "output") ?? outputGeometryNode;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, env={envelope:0.###}, iter={smoothingIterations}, step={smoothingStep:0.###}, " +
                     $"preserveVol={preserveVolume}, pinBorder={pinBorderVertices}, pinAll={pinAllVertices}, " +
                     $"in={inputGeometryNode ?? "null"}, out={outputGeometryNode ?? "null"}");
        }

        private string FindIncomingPlugContains(params string[] patterns)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
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

        private string FindIncomingNodeContains(params string[] patterns)
        {
            var plug = FindIncomingPlugContains(patterns);
            return PlugToNode(plug);
        }

        private static string PlugToNode(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return null;
            return MayaPlugUtil.ExtractNodePart(plug);
        }
    }
}
