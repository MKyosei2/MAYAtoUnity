// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: closeCurve (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("closeCurve")]
    public sealed class MayaGenerated_CloseCurveNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (closeCurve)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool keepOriginal;
        [SerializeField] private float tolerance;

        [SerializeField] private string incomingCurve;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            keepOriginal = ReadBool(false, ".keepOriginal", "keepOriginal", ".ko", "ko");
            tolerance = ReadFloat(0f, ".tolerance", "tolerance", ".tol", "tol");

            incomingCurve = FindLastIncomingTo("inputCurve", "ic", "input", "in");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, keepOriginal={keepOriginal}, tolerance={tolerance}, incomingCurve={(string.IsNullOrEmpty(incomingCurve) ? "none" : incomingCurve)}");
        }
    }
}
