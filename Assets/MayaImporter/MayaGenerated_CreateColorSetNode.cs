// NodeType: createColorSet (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("createColorSet")]
    public sealed class MayaGenerated_CreateColorSetNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (createColorSet)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private string colorSetName;
        [SerializeField] private bool clamped = true;
        [SerializeField] private int representation;

        [SerializeField] private string incomingMesh;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            colorSetName = ReadString("", ".colorSetName", "colorSetName", ".name", "name", ".setName", "setName");
            clamped = ReadBool(true, ".clamped", "clamped", ".clamp", "clamp");
            representation = ReadInt(0, ".representation", "representation", ".rep", "rep", ".type", "type");

            incomingMesh = FindLastIncomingTo("inputMesh", "inMesh", "input", "in", "worldMesh");
            string im = string.IsNullOrEmpty(incomingMesh) ? "none" : incomingMesh;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, setName='{colorSetName}', clamped={clamped}, rep={representation}, incomingMesh={im} (not executed; intent preserved)");
        }
    }
}
