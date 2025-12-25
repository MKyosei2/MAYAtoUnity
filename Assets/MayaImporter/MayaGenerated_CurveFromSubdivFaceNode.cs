// NodeType: curveFromSubdivFace (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("curveFromSubdivFace")]
    public sealed class MayaGenerated_CurveFromSubdivFaceNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveFromSubdivFace)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int faceIndex = -1;
        [SerializeField] private float tolerance;

        [SerializeField] private string incomingSubdiv;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            faceIndex = ReadInt(-1, ".faceIndex", "faceIndex", ".face", "face", ".fi", "fi");
            tolerance = ReadFloat(0f, ".tolerance", "tolerance", ".tol", "tol");

            incomingSubdiv = FindLastIncomingTo("inSubdiv", "inputSubdiv", "subdiv", "input", "in");
            string isd = string.IsNullOrEmpty(incomingSubdiv) ? "none" : incomingSubdiv;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, faceIndex={faceIndex}, tol={tolerance}, incomingSubdiv={isd} (curve not generated; connections preserved)");
        }
    }
}
