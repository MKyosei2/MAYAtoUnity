// Auto-generated placeholder -> Phase C implementation (decoded, non-empty).
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("AlembicNode")]
    public sealed class MayaGenerated_AlembicNodeNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (AlembicNode)")]
        [SerializeField] private string abcFilePath;
        [SerializeField] private string objectPathInCache;
        [SerializeField] private bool enabled = true;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            abcFilePath = ReadString("",
                ".abcFile", "abcFile",
                ".fileName", "fileName",
                ".cacheFileName", "cacheFileName",
                ".path", "path");

            objectPathInCache = ReadString("",
                ".abcObjectPath", "abcObjectPath",
                ".objectPath", "objectPath");

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            SetNotes($"AlembicNode decoded: enabled={enabled}, file='{abcFilePath}', objectPath='{objectPathInCache}'");
        }
    }
}
