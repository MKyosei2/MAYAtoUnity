// NodeType: cameraManip (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("cameraManip")]
    public sealed class MayaGenerated_CameraManipNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (cameraManip)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float size;
        [SerializeField] private string targetCamera;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            size = ReadFloat(0f, ".size", "size", ".s", "s");
            targetCamera = ReadString("", ".camera", "camera", ".target", "target", ".cam", "cam");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, size={size}, targetCamera='{targetCamera}'");
        }
    }
}
