// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: curveFromSurfaceBnd (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("curveFromSurfaceBnd")]
    public sealed class MayaGenerated_CurveFromSurfaceBndNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveFromSurfaceBnd)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int boundaryType;
        [SerializeField] private float tolerance;

        [SerializeField] private string incomingSurface;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            boundaryType = ReadInt(0, ".boundaryType", "boundaryType", ".type", "type", ".bnd", "bnd");
            tolerance = ReadFloat(0f, ".tolerance", "tolerance", ".tol", "tol");

            incomingSurface = FindLastIncomingTo("inputSurface", "inSurface", "surface", "is", "input", "in");
            string isf = string.IsNullOrEmpty(incomingSurface) ? "none" : incomingSurface;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, boundaryType={boundaryType}, tol={tolerance}, incomingSurface={isf} (curve not generated; connections preserved)");
        }
    }
}
