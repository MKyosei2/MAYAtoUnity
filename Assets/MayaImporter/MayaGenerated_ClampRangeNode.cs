// NodeType: clampRange (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("clampRange")]
    public sealed class MayaGenerated_ClampRangeNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (clampRange)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float min;
        [SerializeField] private float max;
        [SerializeField] private float input;

        [SerializeField] private string incomingInput;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            min = ReadFloat(0f, ".min", "min", ".mn", "mn");
            max = ReadFloat(1f, ".max", "max", ".mx", "mx");
            input = ReadFloat(0f, ".input", "input", ".in", "in", ".value", "value");

            incomingInput = FindLastIncomingTo("input", "in", "value");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, min={min}, max={max}, input={input}, incomingInput={(string.IsNullOrEmpty(incomingInput) ? "none" : incomingInput)}");
        }
    }
}
