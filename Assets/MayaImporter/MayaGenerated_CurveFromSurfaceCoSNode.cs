// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: curveFromSurfaceCoS (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("curveFromSurfaceCoS")]
    public sealed class MayaGenerated_CurveFromSurfaceCoSNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveFromSurfaceCoS)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int direction;
        [SerializeField] private float tolerance;

        [SerializeField] private string incomingSurface;
        [SerializeField] private string incomingCurveOnSurface;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            direction = ReadInt(0, ".direction", "direction", ".dir", "dir");
            tolerance = ReadFloat(0f, ".tolerance", "tolerance", ".tol", "tol");

            incomingSurface = FindLastIncomingTo("inputSurface", "inSurface", "surface", "is", "input", "in");
            incomingCurveOnSurface = FindLastIncomingTo("curveOnSurface", "cos", "inputCurve", "ic");

            string isf = string.IsNullOrEmpty(incomingSurface) ? "none" : incomingSurface;
            string icos = string.IsNullOrEmpty(incomingCurveOnSurface) ? "none" : incomingCurveOnSurface;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, dir={direction}, tol={tolerance}, incomingSurface={isf}, incomingCoS={icos} (curve not generated; connections preserved)");
        }
    }
}
