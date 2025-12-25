// NodeType: closeSurface (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("closeSurface")]
    public sealed class MayaGenerated_CloseSurfaceNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (closeSurface)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private int direction;
        [SerializeField] private float tolerance;

        [SerializeField] private string incomingSurface;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            direction = ReadInt(0, ".direction", "direction", ".dir", "dir");
            tolerance = ReadFloat(0f, ".tolerance", "tolerance", ".tol", "tol");

            incomingSurface = FindLastIncomingTo("inputSurface", "is", "input", "in");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, direction={direction}, tolerance={tolerance}, incomingSurface={(string.IsNullOrEmpty(incomingSurface) ? "none" : incomingSurface)}");
        }
    }
}
