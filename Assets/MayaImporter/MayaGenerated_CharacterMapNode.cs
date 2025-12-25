// NodeType: characterMap (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("characterMap")]
    public sealed class MayaGenerated_CharacterMapNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (characterMap)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private string mapName;
        [SerializeField] private int entryCountHint;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            mapName = ReadString("", ".name", "name", ".mapName", "mapName");
            entryCountHint = ReadInt(0, ".entryCount", "entryCount", ".count", "count");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, mapName='{mapName}', entryCountHint={entryCountHint} (mapping via connections preserved)");
        }
    }
}
