// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: chooser (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("chooser")]
    public sealed class MayaGenerated_ChooserNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (chooser)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int index;
        [SerializeField] private string incomingIndex;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            index = ReadInt(0, ".index", "index", ".i", "i", ".choice", "choice");
            incomingIndex = FindLastIncomingTo("index", "i", "choice");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, index={index}, incomingIndex={(string.IsNullOrEmpty(incomingIndex) ? "none" : incomingIndex)}");
        }
    }
}
