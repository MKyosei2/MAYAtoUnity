// NodeType: ceil (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("ceil")]
    public sealed class MayaGenerated_CeilNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (ceil)")]
        [SerializeField] private float input;
        [SerializeField] private string incomingInput;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            input = ReadFloat(0f, ".input", "input", ".in", "in", ".value", "value");
            incomingInput = FindLastIncomingTo("input", "in", "value");

            SetNotes($"{NodeType} '{NodeName}' decoded: input={input}, incoming={(string.IsNullOrEmpty(incomingInput) ? "none" : incomingInput)}");
        }
    }
}
