// NodeType: createPtexUV (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("createPtexUV")]
    public sealed class MayaGenerated_CreatePtexUVNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (createPtexUV)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private int resolution = 256;
        [SerializeField] private bool perFace;
        [SerializeField] private string uvSetName;

        [SerializeField] private string incomingMesh;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            resolution = ReadInt(256, ".resolution", "resolution", ".res", "res", ".textureResolution", "textureResolution");
            perFace = ReadBool(false, ".perFace", "perFace", ".pf", "pf");
            uvSetName = ReadString("", ".uvSetName", "uvSetName", ".uvSet", "uvSet", ".name", "name");

            incomingMesh = FindLastIncomingTo("inputMesh", "inMesh", "input", "in", "worldMesh");
            string im = string.IsNullOrEmpty(incomingMesh) ? "none" : incomingMesh;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, res={resolution}, perFace={perFace}, uvSet='{uvSetName}', incomingMesh={im} (not executed; intent preserved)");
        }
    }
}
