// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: createEPManip (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("createEPManip")]
    public sealed class MayaGenerated_CreateEPManipNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (createEPManip)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float size = 1f;
        [SerializeField] private int degree = 3;
        [SerializeField] private bool snapToCurve;

        [SerializeField] private string incomingCurve;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            size = ReadFloat(1f, ".size", "size", ".s", "s");
            degree = ReadInt(3, ".degree", "degree", ".deg", "deg");
            snapToCurve = ReadBool(false, ".snap", "snap", ".snapToCurve", "snapToCurve");

            incomingCurve = FindLastIncomingTo("curve", "inputCurve", "ic", "input", "in");
            string ic = string.IsNullOrEmpty(incomingCurve) ? "none" : incomingCurve;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, size={size}, degree={degree}, snapToCurve={snapToCurve}, incomingCurve={ic}");
        }
    }
}
