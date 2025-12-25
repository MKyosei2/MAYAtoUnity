// Assets/MayaImporter/MayaGenerated_NormalizeNode.cs
// Duplicate-mapping fix:
// - NormalizeNode.cs already maps [MayaNodeType("normalize")]
// - This generated class MUST NOT map the same nodeType.

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    public sealed class MayaGenerated_NormalizeNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (normalize) legacy")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private string lastIncomingToInput;
        [SerializeField] private string lastIncomingToTime;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            lastIncomingToInput = FindLastIncomingTo("input", "in", "i");
            lastIncomingToTime = FindLastIncomingTo("time", "t");

            string inInput = string.IsNullOrEmpty(lastIncomingToInput) ? "none" : lastIncomingToInput;
            string inTime = string.IsNullOrEmpty(lastIncomingToTime) ? "none" : lastIncomingToTime;

            SetNotes($"{NodeType} '{NodeName}' decoded (legacy generated normalize): enabled={enabled}, attrs={AttributeCount}, conns={ConnectionCount}, incomingInput={inInput}, incomingTime={inTime}");
        }
    }
}
