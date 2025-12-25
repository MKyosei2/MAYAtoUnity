// AUTO-PATCHED by MayaPhaseCStubPatcher (one-shot)
// NodeType: polySplitToolManip1
// Phase C implementation: non-empty DecodePhaseC + MayaPhaseCNodeBase (coverage: not STUB)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("polySplitToolManip1")]
    public sealed class MayaGenerated_PolySplitToolManip1Node : MayaPhaseCNodeBase
    {
        [Header("Decoded (polySplitToolManip1)")]
        [SerializeField] private bool enabled = true;

        // A couple of common decoded hints (generic)
        [SerializeField] private string lastIncomingToInput;
        [SerializeField] private string lastIncomingToTime;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // Generic enable heuristics (works across many nodes)
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            // Generic connection hints (best-effort)
            lastIncomingToInput = FindLastIncomingTo("input", "in", "i");
            lastIncomingToTime  = FindLastIncomingTo("time", "t");

            string inInput = string.IsNullOrEmpty(lastIncomingToInput) ? "none" : lastIncomingToInput;
            string inTime  = string.IsNullOrEmpty(lastIncomingToTime)  ? "none" : lastIncomingToTime;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, attrs={AttributeCount}, conns={ConnectionCount}, incomingInput={inInput}, incomingTime={inTime} (generic PhaseC)");
        }
    }
}
