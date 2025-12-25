// NodeType: createUVSet (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("createUVSet")]
    public sealed class MayaGenerated_CreateUVSetNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (createUVSet)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private string uvSetName;
        [SerializeField] private bool replaceExisting;
        [SerializeField] private int projectionType;

        [SerializeField] private string incomingMesh;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            uvSetName = ReadString("", ".uvSetName", "uvSetName", ".name", "name", ".setName", "setName");
            replaceExisting = ReadBool(false, ".replaceExisting", "replaceExisting", ".replace", "replace");
            projectionType = ReadInt(0, ".projectionType", "projectionType", ".projType", "projType", ".type", "type");

            incomingMesh = FindLastIncomingTo("inputMesh", "inMesh", "input", "in", "worldMesh");
            string im = string.IsNullOrEmpty(incomingMesh) ? "none" : incomingMesh;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, uvSet='{uvSetName}', replaceExisting={replaceExisting}, projType={projectionType}, incomingMesh={im} (not executed; intent preserved)");
        }
    }
}
