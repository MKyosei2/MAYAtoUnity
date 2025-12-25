// NodeType: createBPManip (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("createBPManip")]
    public sealed class MayaGenerated_CreateBPManipNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (createBPManip)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float size = 1f;
        [SerializeField] private int mode;

        [SerializeField] private string incomingTarget;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            size = ReadFloat(1f, ".size", "size", ".s", "s", ".radius", "radius");
            mode = ReadInt(0, ".mode", "mode", ".m", "m");

            incomingTarget = FindLastIncomingTo("target", "t", "input", "in");
            string it = string.IsNullOrEmpty(incomingTarget) ? "none" : incomingTarget;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, size={size}, mode={mode}, incomingTarget={it}");
        }
    }
}
