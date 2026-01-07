// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: createCVManip (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("createCVManip")]
    public sealed class MayaGenerated_CreateCVManipNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (createCVManip)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float size = 1f;
        [SerializeField] private int degree = 3;
        [SerializeField] private bool periodic;

        [SerializeField] private string incomingCurve;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            size = ReadFloat(1f, ".size", "size", ".s", "s");
            degree = ReadInt(3, ".degree", "degree", ".deg", "deg");
            periodic = ReadBool(false, ".periodic", "periodic", ".closed", "closed", ".form", "form");

            incomingCurve = FindLastIncomingTo("curve", "inputCurve", "ic", "input", "in");
            string ic = string.IsNullOrEmpty(incomingCurve) ? "none" : incomingCurve;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, size={size}, degree={degree}, periodic={periodic}, incomingCurve={ic}");
        }
    }
}
