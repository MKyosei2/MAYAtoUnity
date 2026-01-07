// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: channels (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("channels")]
    public sealed class MayaGenerated_ChannelsNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (channels)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private string channelSetName;
        [SerializeField] private int channelCountHint;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            channelSetName = ReadString("", ".name", "name", ".channelSetName", "channelSetName");
            channelCountHint = ReadInt(0, ".channelCount", "channelCount", ".count", "count", ".n", "n");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, name='{channelSetName}', channelCountHint={channelCountHint} (channel membership via connections preserved)");
        }
    }
}
