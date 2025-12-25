// AUTO-PATCHED by MayaPhaseCPatchAllNodeTypeStubs (one-shot)
// NodeType: water
// Phase C implementation (non-empty DecodePhaseC; coverage: not STUB)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Shaders
{
    [DisallowMultipleComponent]
    [MayaNodeType("water")]
    public sealed class WaterNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (water)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private string incomingInput;
        [SerializeField] private string incomingTime;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // Generic enable heuristics (works across many nodes)
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            // Generic connection hints (best-effort)
            incomingInput = FindLastIncomingTo("input", "in", "i");
            incomingTime  = FindLastIncomingTo("time", "t");

            string inInput = string.IsNullOrEmpty(incomingInput) ? "none" : incomingInput;
            string inTime  = string.IsNullOrEmpty(incomingTime)  ? "none" : incomingTime;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, attrs={AttributeCount}, conns={ConnectionCount}, incomingInput={inInput}, incomingTime={inTime} (generic PhaseC)");
        }
    }
}
