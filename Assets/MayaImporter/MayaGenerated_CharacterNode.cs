// NodeType: character (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("character")]
    public sealed class MayaGenerated_CharacterNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (character)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private string characterName;
        [SerializeField] private int memberCountHint;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            characterName = ReadString("", ".name", "name", ".characterName", "characterName");
            memberCountHint = ReadInt(0, ".memberCount", "memberCount", ".count", "count");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, characterName='{characterName}', memberCountHint={memberCountHint} (membership via connections preserved)");
        }
    }
}
