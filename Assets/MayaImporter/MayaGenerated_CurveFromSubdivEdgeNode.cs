// NodeType: curveFromSubdivEdge (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("curveFromSubdivEdge")]
    public sealed class MayaGenerated_CurveFromSubdivEdgeNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveFromSubdivEdge)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int edgeIndex = -1;
        [SerializeField] private float tolerance;

        [SerializeField] private string incomingSubdiv;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            edgeIndex = ReadInt(-1, ".edgeIndex", "edgeIndex", ".edge", "edge", ".ei", "ei");
            tolerance = ReadFloat(0f, ".tolerance", "tolerance", ".tol", "tol");

            incomingSubdiv = FindLastIncomingTo("inSubdiv", "inputSubdiv", "subdiv", "input", "in");
            string isd = string.IsNullOrEmpty(incomingSubdiv) ? "none" : incomingSubdiv;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, edgeIndex={edgeIndex}, tol={tolerance}, incomingSubdiv={isd} (curve not generated; connections preserved)");
        }
    }
}
