// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: curveFromSurfaceIso (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("curveFromSurfaceIso")]
    public sealed class MayaGenerated_CurveFromSurfaceIsoNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveFromSurfaceIso)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int isoDirection; // 0=U, 1=V (best-effort)
        [SerializeField] private float isoParam;
        [SerializeField] private float tolerance;

        [SerializeField] private string incomingSurface;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            isoDirection = ReadInt(0, ".isoDirection", "isoDirection", ".dir", "dir", ".direction", "direction");
            isoParam = ReadFloat(0f, ".isoParam", "isoParam", ".parameter", "parameter", ".u", "u", ".v", "v");
            tolerance = ReadFloat(0f, ".tolerance", "tolerance", ".tol", "tol");

            incomingSurface = FindLastIncomingTo("inputSurface", "inSurface", "surface", "is", "input", "in");
            string isf = string.IsNullOrEmpty(incomingSurface) ? "none" : incomingSurface;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, isoDir={isoDirection}, isoParam={isoParam}, tol={tolerance}, incomingSurface={isf} (curve not generated; connections preserved)");
        }
    }
}
