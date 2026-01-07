// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: curveWarp (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("curveWarp")]
    public sealed class MayaGenerated_CurveWarpNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (curveWarp)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private int warpType;
        [SerializeField] private float magnitude = 1f;
        [SerializeField] private float falloff;

        [SerializeField] private string incomingCurve;
        [SerializeField] private string incomingGeometry;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            warpType = ReadInt(0, ".warpType", "warpType", ".type", "type", ".mode", "mode");
            magnitude = ReadFloat(1f, ".magnitude", "magnitude", ".mag", "mag", ".strength", "strength");
            falloff = ReadFloat(0f, ".falloff", "falloff", ".f", "f");

            incomingCurve = FindLastIncomingTo("inputCurve", "curve", "ic", "input", "in");
            incomingGeometry = FindLastIncomingTo("inputGeometry", "inputGeom", "inputMesh", "inMesh", "worldMesh");

            string ic = string.IsNullOrEmpty(incomingCurve) ? "none" : incomingCurve;
            string ig = string.IsNullOrEmpty(incomingGeometry) ? "none" : incomingGeometry;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, warpType={warpType}, mag={magnitude}, falloff={falloff}, incomingCurve={ic}, incomingGeom={ig} (warp not executed; connections preserved)");
        }
    }
}
