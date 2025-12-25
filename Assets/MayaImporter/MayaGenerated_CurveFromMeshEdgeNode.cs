// NodeType: curveFromMeshEdge (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("curveFromMeshEdge")]
    public sealed class MayaGenerated_CurveFromMeshEdgeNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveFromMeshEdge)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int edgeIndex = -1;
        [SerializeField] private float tolerance;

        [SerializeField] private string incomingMesh;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            edgeIndex = ReadInt(-1, ".edgeIndex", "edgeIndex", ".edge", "edge", ".ei", "ei");
            tolerance = ReadFloat(0f, ".tolerance", "tolerance", ".tol", "tol");

            incomingMesh = FindLastIncomingTo("inMesh", "inputMesh", "mesh", "worldMesh", "input", "in");
            string im = string.IsNullOrEmpty(incomingMesh) ? "none" : incomingMesh;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, edgeIndex={edgeIndex}, tol={tolerance}, incomingMesh={im} (curve not generated; connections preserved)");
        }
    }
}
