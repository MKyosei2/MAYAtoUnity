// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: centerManip (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("centerManip")]
    public sealed class MayaGenerated_CenterManipNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (centerManip)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private Vector3 center;
        [SerializeField] private string incomingCenter;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            float x = ReadFloat(0f, ".centerX", "centerX", ".cx", "cx", ".x", "x");
            float y = ReadFloat(0f, ".centerY", "centerY", ".cy", "cy", ".y", "y");
            float z = ReadFloat(0f, ".centerZ", "centerZ", ".cz", "cz", ".z", "z");
            center = new Vector3(x, y, z);

            incomingCenter = FindLastIncomingTo("centerX", "centerY", "centerZ", "center");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, center={center}, incoming={(string.IsNullOrEmpty(incomingCenter) ? "none" : incomingCenter)}");
        }
    }
}
