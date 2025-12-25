using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Nodes
{
    [MayaNodeType("curveInfo")]
    [DisallowMultipleComponent]
    public sealed class CurveInfoNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveInfo) - local preview")]
        [SerializeField] private float arcLength;
        [SerializeField] private float parameter;
        [SerializeField] private float curvature;
        [SerializeField] private string inputCurveIncomingPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // Typical attrs (aliases included)
            arcLength = ReadFloat(0f, ".arcLength", "arcLength", ".al", "al");
            parameter = ReadFloat(0f, ".parameter", "parameter", ".pr", "pr", ".param", "param");
            curvature = ReadFloat(0f, ".curvature", "curvature", ".cv", "cv");

            // Often driven by connection (inputCurve)
            inputCurveIncomingPlug = FindLastIncomingTo("inputCurve", "ic", "input");

            SetNotes(
                $"curveInfo decoded: arcLength={arcLength}, parameter={parameter}, curvature={curvature}, " +
                $"inputCurveIncoming={(string.IsNullOrEmpty(inputCurveIncomingPlug) ? "none" : inputCurveIncomingPlug)} " +
                $"(local-only preview; connections preserved)"
            );
        }
    }
}
