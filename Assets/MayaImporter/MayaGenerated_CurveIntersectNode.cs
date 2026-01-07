// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: curveIntersect (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("curveIntersect")]
    public sealed class MayaGenerated_CurveIntersectNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveIntersect)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private float tolerance;
        [SerializeField] private int mode;
        [SerializeField] private bool selfIntersect;

        [SerializeField] private string incomingCurveA;
        [SerializeField] private string incomingCurveB;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            tolerance = ReadFloat(0f, ".tolerance", "tolerance", ".tol", "tol");
            mode = ReadInt(0, ".mode", "mode", ".operation", "operation", ".op", "op");
            selfIntersect = ReadBool(false, ".selfIntersect", "selfIntersect", ".self", "self");

            incomingCurveA = FindLastIncomingTo("inputCurveA", "curveA", "inputCurve1", "curve1", "inputCurve", "ic");
            incomingCurveB = FindLastIncomingTo("inputCurveB", "curveB", "inputCurve2", "curve2");

            string ia = string.IsNullOrEmpty(incomingCurveA) ? "none" : incomingCurveA;
            string ib = string.IsNullOrEmpty(incomingCurveB) ? "none" : incomingCurveB;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, tol={tolerance}, mode={mode}, selfIntersect={selfIntersect}, incomingA={ia}, incomingB={ib} (intersection not solved; connections preserved)");
        }
    }
}
